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

	// Context menu on Assets (Project window right-click)
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

		// Gather all assets under source (files + folders).
		var allGuids = AssetDatabase.FindAssets("", new[] { sourceFolder });
		var allPaths = allGuids
			.Select(AssetDatabase.GUIDToAssetPath)
			.Where(p => p.StartsWith(sourceFolder + "/", StringComparison.Ordinal))
			.ToArray();

		// 1) Ensure destination subfolder structure exists.
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

			// 3) Delete empty folders from deepest to shallowest, then root.
			// (After moving files, remaining folders should be empty.)
			var remaining = AssetDatabase.FindAssets("", new[] { sourceFolder })
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(p => p.StartsWith(sourceFolder + "/", StringComparison.Ordinal))
				.ToArray();

			var remainingFolders = remaining
				.Where(AssetDatabase.IsValidFolder)
				.OrderByDescending(p => p.Count(c => c == '/'))
				.ToArray();

			foreach (var f in remainingFolders)
				AssetDatabase.DeleteAsset(f);

			AssetDatabase.DeleteAsset(sourceFolder);

			Debug.Log($"[Merge Into] Moved: {moved}, Skipped: {skipped}, Renamed: {renamed}, Replaced: {replaced}\nSource: {sourceFolder}\nDest: {destFolder}");
		}
		finally
		{
			AssetDatabase.StopAssetEditing();
			AssetDatabase.Refresh();
		}
	}

	private static bool AssetExists(string assetPath)
	{
		if (AssetDatabase.IsValidFolder(assetPath)) return true;
		return File.Exists(ToAbsolutePath(assetPath));
	}

	private static string ToAbsolutePath(string assetPath)
	{
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

	private class MergeIntoWindow : EditorWindow
	{
		private string _sourcePath;
		private DefaultAsset _destFolderAsset;
		private CollisionPolicy _policy = CollisionPolicy.RenameIncoming;

		public static MergeIntoWindow Open(string sourcePath)
		{
			var w = CreateInstance<MergeIntoWindow>();
			w.titleContent = new GUIContent("Merge Into");
			w.minSize = new Vector2(420, 140);
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

					// Persist last destination for next time
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
				messageOrPath = "Destination must be a folder asset inside the Project (under Assets/).";
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
