// Assets/Editor/FolderMergeInto.cs
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FolderMergeInto
{
	private const string PrefKeyLastDestGuid = "FolderMergeInto.LastDestGuid";

	public enum CollisionPolicy
	{
		Skip,
		RenameIncoming,
		ReplaceDestination
	}

	// Project window right-click on a folder
	[MenuItem("Assets/Merge Into...", false, 2200)]
	private static void OpenMergeIntoWindow()
	{
		var sourcePath = GetSingleSelectedFolderPath();
		if (string.IsNullOrEmpty(sourcePath)) return;

		var w = MergeIntoWindow.Open(sourcePath);
		w.ShowUtility();
	}

	[MenuItem("Assets/Merge Into...", true)]
	private static bool ValidateOpenMergeIntoWindow()
	{
		return !string.IsNullOrEmpty(GetSingleSelectedFolderPath());
	}

	private static string GetSingleSelectedFolderPath()
	{
		var paths = Selection.assetGUIDs
			.Select(AssetDatabase.GUIDToAssetPath)
			.Where(p => AssetDatabase.IsValidFolder(p))
			.ToArray();

		return paths.Length == 1 ? paths[0] : null;
	}

	private static void MergeMoveFolderContents(string sourceFolder, string destFolder, CollisionPolicy policy)
	{
		if (!AssetDatabase.IsValidFolder(sourceFolder) || !AssetDatabase.IsValidFolder(destFolder))
			throw new InvalidOperationException("Source or destination is not a valid folder.");

		if (sourceFolder == destFolder)
			throw new InvalidOperationException("Source and destination are the same folder.");

		// Prevent merging into itself (or a subfolder) which would recurse forever.
		if (destFolder.StartsWith(sourceFolder + "/", StringComparison.Ordinal))
			throw new InvalidOperationException("Destination cannot be inside the source folder.");

		// Gather all assets under source (files + folders). Note: this will NOT include empty folders.
		var allGuids = AssetDatabase.FindAssets("", new[] { sourceFolder });
		var allPaths = allGuids
			.Select(AssetDatabase.GUIDToAssetPath)
			.Where(p => p.StartsWith(sourceFolder + "/", StringComparison.Ordinal))
			.ToArray();

		// 1) Ensure destination subfolder structure exists (for non-empty folders only).
		var folderPaths = allPaths
			.Where(AssetDatabase.IsValidFolder)
			.OrderBy(p => p.Count(c => c == '/')) // parents first
			.ToArray();

		foreach (var srcSubFolder in folderPaths)
		{
			string rel = srcSubFolder.Substring(sourceFolder.Length).TrimStart('/');
			string dstSubFolder = CombineAssetPath(destFolder, rel);
			EnsureFolderExists(dstSubFolder);
		}

		// 2) Move files (preserves GUID/meta).
		var filePaths = allPaths
			.Where(p => !AssetDatabase.IsValidFolder(p))
			.Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(p => p.Count(c => c == '/')) // deeper first fine for files
			.ToArray();

		int moved = 0, skipped = 0, renamed = 0, replaced = 0;

		AssetDatabase.StartAssetEditing();
		try
		{
			foreach (var src in filePaths)
			{
				string rel = src.Substring(sourceFolder.Length).TrimStart('/');
				string dst = CombineAssetPath(destFolder, rel);

				if (AssetExists(dst))
				{
					switch (policy)
					{
						case CollisionPolicy.Skip:
							skipped++;
							continue;

						case CollisionPolicy.RenameIncoming:
							dst = AssetDatabase.GenerateUniqueAssetPath(dst);
							renamed++;
							break;

						case CollisionPolicy.ReplaceDestination:
							if (!AssetDatabase.DeleteAsset(dst))
								throw new IOException($"Failed to delete destination asset: {dst}");
							replaced++;
							break;
					}
				}

				EnsureFolderExists(Path.GetDirectoryName(dst)?.Replace("\\", "/"));

				string err = AssetDatabase.MoveAsset(src, dst);
				if (!string.IsNullOrEmpty(err))
					throw new IOException($"Move failed:\n{src}\n -> {dst}\nError: {err}");

				moved++;
			}
		}
		finally
		{
			AssetDatabase.StopAssetEditing();
		}

		// 3) Cleanup: remove empty folders left behind (INCLUDING folders FindAssets() can't see).
		AssetDatabase.Refresh();
		CleanupEmptyFoldersUnder(sourceFolder);
		AssetDatabase.Refresh();

		Debug.Log($"[Merge Into] Moved: {moved}, Skipped: {skipped}, Renamed: {renamed}, Replaced: {replaced}\nSource: {sourceFolder}\nDest: {destFolder}");
	}

	// ---------- Empty folder cleanup (filesystem-backed) ----------

	private static void CleanupEmptyFoldersUnder(string sourceFolderAssetPath)
	{
		// Convert "Assets/..." -> absolute
		var absSource = ToAbsolutePath(sourceFolderAssetPath);
		if (!Directory.Exists(absSource))
			return;

		// Enumerate directories deepest-first so children delete before parents.
		var dirs = Directory.GetDirectories(absSource, "*", SearchOption.AllDirectories)
			.OrderByDescending(d => d.Length)
			.ToArray();

		foreach (var absDir in dirs)
		{
			if (!Directory.Exists(absDir))
				continue;

			// Only delete if the directory is effectively empty (ignoring .meta).
			if (!IsDirEffectivelyEmpty(absDir))
				continue;

			var assetDir = AbsDirToAssetPath(absDir);

			// Prefer Unity-side deletion (keeps DB happy).
			if (AssetDatabase.IsValidFolder(assetDir))
			{
				if (AssetDatabase.DeleteAsset(assetDir))
					continue;
			}

			// Fallback: delete from filesystem (folder + meta).
			TryDeleteDirectoryAndMeta(absDir);
		}

		// Finally delete the root source folder if it is now empty.
		if (Directory.Exists(absSource) && IsDirEffectivelyEmpty(absSource))
		{
			if (AssetDatabase.IsValidFolder(sourceFolderAssetPath))
			{
				if (!AssetDatabase.DeleteAsset(sourceFolderAssetPath))
					TryDeleteDirectoryAndMeta(absSource);
			}
			else
			{
				TryDeleteDirectoryAndMeta(absSource);
			}
		}
	}

	private static bool IsDirEffectivelyEmpty(string absDir)
	{
		// Any subdirectories means not empty (even if they contain only meta, they’ll be processed deepest-first)
		var subDirs = Directory.GetDirectories(absDir);
		if (subDirs.Length > 0) return false;

		// Any files other than ".meta" means not empty
		var files = Directory.GetFiles(absDir);
		foreach (var f in files)
		{
			if (!f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
				return false;
		}

		return true;
	}

	private static string AbsDirToAssetPath(string absDir)
	{
		absDir = absDir.Replace("\\", "/");
		var absAssets = Application.dataPath.Replace("\\", "/"); // .../<Project>/Assets
		if (!absDir.StartsWith(absAssets, StringComparison.Ordinal))
			throw new InvalidOperationException("Path not under Assets/: " + absDir);

		return "Assets" + absDir.Substring(absAssets.Length);
	}

	private static void TryDeleteDirectoryAndMeta(string absDir)
	{
		try
		{
			if (Directory.Exists(absDir))
				Directory.Delete(absDir, recursive: false);

			var meta = absDir.TrimEnd('/', '\\') + ".meta";
			if (File.Exists(meta))
				File.Delete(meta);
		}
		catch (Exception e)
		{
			// If recursive:false fails due to OS thinking it's not empty, that's fine; we only call when empty.
			Debug.LogWarning($"[Merge Into] Failed to delete folder '{absDir}': {e.Message}");
		}
	}

	// ---------- Path/asset helpers ----------

	private static bool AssetExists(string assetPath)
	{
		if (AssetDatabase.IsValidFolder(assetPath)) return true;
		return File.Exists(ToAbsolutePath(assetPath));
	}

	private static string ToAbsolutePath(string assetPath)
	{
		// assetPath like "Assets/..."
		string projectRoot = Directory.GetParent(Application.dataPath)!.FullName.Replace("\\", "/");
		return $"{projectRoot}/{assetPath}".Replace("\\", "/");
	}

	private static string CombineAssetPath(string a, string b)
	{
		if (string.IsNullOrEmpty(b)) return a.Replace("\\", "/");
		return (a.TrimEnd('/') + "/" + b.TrimStart('/')).Replace("\\", "/");
	}

	private static void EnsureFolderExists(string folderAssetPath)
	{
		if (string.IsNullOrEmpty(folderAssetPath)) return;
		folderAssetPath = folderAssetPath.Replace("\\", "/");
		if (AssetDatabase.IsValidFolder(folderAssetPath)) return;

		var parts = folderAssetPath.Split('/');
		if (parts.Length == 0 || parts[0] != "Assets")
			throw new InvalidOperationException("Folders must be under Assets/");

		string current = "Assets";
		for (int i = 1; i < parts.Length; i++)
		{
			string next = current + "/" + parts[i];
			if (!AssetDatabase.IsValidFolder(next))
				AssetDatabase.CreateFolder(current, parts[i]);
			current = next;
		}
	}

	// ---------- UI Window ----------

	private class MergeIntoWindow : EditorWindow
	{
		private string _sourcePath;
		private DefaultAsset _destFolderAsset;
		private CollisionPolicy _policy = CollisionPolicy.RenameIncoming;

		public static MergeIntoWindow Open(string sourcePath)
		{
			var w = CreateInstance<MergeIntoWindow>();
			w.titleContent = new GUIContent("Merge Into");
			w.minSize = new Vector2(440, 160);
			w._sourcePath = sourcePath;

			// Restore last destination if possible.
			var guid = EditorPrefs.GetString(PrefKeyLastDestGuid, "");
			if (!string.IsNullOrEmpty(guid))
			{
				var p = AssetDatabase.GUIDToAssetPath(guid);
				if (!string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p))
					w._destFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(p);
			}

			return w;
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Source (selected folder)", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(_sourcePath, MessageType.None);

			EditorGUILayout.Space(6);

			EditorGUILayout.LabelField("Destination folder", EditorStyles.boldLabel);
			_destFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
				_destFolderAsset,
				typeof(DefaultAsset),
				false
			);

			EditorGUILayout.Space(6);

			_policy = (CollisionPolicy)EditorGUILayout.EnumPopup("On name collision", _policy);

			EditorGUILayout.Space(10);

			using (new EditorGUI.DisabledScope(!IsValidDestination(out _)))
			{
				if (GUILayout.Button("Merge (Move)"))
				{
					if (!IsValidDestination(out var destPath))
						return;

					// Persist last destination
					var destGuid = AssetDatabase.AssetPathToGUID(destPath);
					if (!string.IsNullOrEmpty(destGuid))
						EditorPrefs.SetString(PrefKeyLastDestGuid, destGuid);

					try
					{
						MergeMoveFolderContents(_sourcePath, destPath, _policy);
						Close();
					}
					catch (Exception ex)
					{
						Debug.LogException(ex);
						EditorUtility.DisplayDialog("Merge Into Failed", ex.Message, "OK");
					}
				}
			}

			if (!IsValidDestination(out var why))
			{
				EditorGUILayout.Space(6);
				EditorGUILayout.HelpBox(why, MessageType.Warning);
			}
		}

		private bool IsValidDestination(out string messageOrPath)
		{
			messageOrPath = "Pick a destination folder.";

			if (_destFolderAsset == null)
				return false;

			var destPath = AssetDatabase.GetAssetPath(_destFolderAsset);
			if (string.IsNullOrEmpty(destPath) || !AssetDatabase.IsValidFolder(destPath))
			{
				messageOrPath = "Destination must be a folder inside the Project (under Assets/).";
				return false;
			}

			if (destPath == _sourcePath)
			{
				messageOrPath = "Destination cannot be the same as the source.";
				return false;
			}

			if (destPath.StartsWith(_sourcePath + "/", StringComparison.Ordinal))
			{
				messageOrPath = "Destination cannot be inside the source folder.";
				return false;
			}

			messageOrPath = destPath;
			return true;
		}
	}
}
