using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Core.Framework.Editor
{
	/// <summary>
	/// Project window helpers: open a locked browser on a folder, and expand or collapse a folder subtree in the active tree.
	/// Uses reflection on Unity's internal ProjectBrowser / TreeView implementation; may need updates after major Unity upgrades.
	/// </summary>
	[InitializeOnLoad]
	public static class FolderBrowserWindow
	{
		private static string s_LastContextGuid;
		private static string s_LastContextFolderPath;
		private static double s_LastContextTime;
		private const double FreshSeconds = 2.5;

		private static readonly Type ProjectBrowserType;
		private static readonly Type InternalEditorUtilityType;

		static FolderBrowserWindow()
		{
			EditorApplication.projectWindowItemOnGUI -= OnProjectItemGUI;
			EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;

			ProjectBrowserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
			InternalEditorUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.InternalEditorUtility");
		}

		private static void OnProjectItemGUI(string guid, Rect rect)
		{
			var e = Event.current;
			if (e == null) return;

			if ((e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition)) ||
				(e.type == EventType.ContextClick && rect.Contains(e.mousePosition)))
			{
				s_LastContextGuid = guid;
				s_LastContextTime = EditorApplication.timeSinceStartup;

				var path = AssetDatabase.GUIDToAssetPath(guid)?.Replace("\\", "/");
				s_LastContextFolderPath = !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path)
					? path
					: null;
			}
		}

		/// <summary>
		/// Resolves a folder for Assets menu validation when <see cref="Selection"/> alone is wrong
		/// (e.g. two-column Project window: hierarchy vs list selection).
		/// Uses the last Project-window context click path when fresh, then <see cref="Selection.activeObject"/> if it is a folder.
		/// </summary>
		public static bool TryGetProjectWindowFolderForMenus(out string folderPath)
		{
			folderPath = null;

			if (!string.IsNullOrEmpty(s_LastContextFolderPath) &&
				EditorApplication.timeSinceStartup - s_LastContextTime <= FreshSeconds)
			{
				folderPath = s_LastContextFolderPath;
				return true;
			}

			if (!string.IsNullOrEmpty(s_LastContextGuid) &&
				EditorApplication.timeSinceStartup - s_LastContextTime <= FreshSeconds)
			{
				var path = AssetDatabase.GUIDToAssetPath(s_LastContextGuid)?.Replace("\\", "/");
				if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
				{
					folderPath = path;
					return true;
				}
			}

			var obj = Selection.activeObject;
			if (obj != null)
			{
				var path = AssetDatabase.GetAssetPath(obj)?.Replace("\\", "/");
				if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
				{
					folderPath = path;
					return true;
				}
			}

			return false;
		}

		[MenuItem("Assets/Open in new View", true, 2100)]
		private static bool ValidateOpenInNewView()
		{
			return TryGetProjectWindowFolderForMenus(out _);
		}

		[MenuItem("Assets/Open in new View", false, 2100)]
		private static void OpenInNewView()
		{
			if (!TryGetProjectWindowFolderForMenus(out var folder))
				return;

			EditorApplication.delayCall += () =>
			{
				try
				{
					OpenNewLockedProjectBrowserAtFolder(folder);
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			};
		}

		[MenuItem("Assets/Expand All", true, 2110)]
		[MenuItem("Assets/Collapse All", true, 2111)]
		private static bool ValidateRecursiveExpandMenu()
		{
			return ProjectBrowserType != null && InternalEditorUtilityType != null && TryGetProjectWindowFolderForMenus(out _);
		}

		[MenuItem("Assets/Expand All", false, 2110)]
		private static void ExpandAll()
		{
			if (!TryGetProjectWindowFolderForMenus(out var folder))
				return;

			EditorApplication.delayCall += () =>
			{
				if (!TryApplyRecursiveFolderExpansion(folder, expand: true))
					Debug.LogWarning("Expand All: Could not expand this folder in the Project tree. Try clicking the Project window first, or switch Project layout if this keeps happening.");
			};
		}

		[MenuItem("Assets/Collapse All", false, 2111)]
		private static void CollapseAll()
		{
			if (!TryGetProjectWindowFolderForMenus(out var folder))
				return;

			EditorApplication.delayCall += () =>
			{
				if (!TryApplyRecursiveFolderExpansion(folder, expand: false))
					Debug.LogWarning("Collapse All: Could not collapse this folder in the Project tree. Try clicking the Project window first, or switch Project layout if this keeps happening.");
			};
		}

		private static void OpenNewLockedProjectBrowserAtFolder(string folderPath)
		{
			if (ProjectBrowserType == null)
			{
				Debug.LogError("FolderBrowserWindow: Could not find UnityEditor.ProjectBrowser type.");
				return;
			}

			var win = CreateNewProjectBrowserWindow();
			if (win == null)
			{
				Debug.LogError("FolderBrowserWindow: Failed to create a new Project Browser window.");
				return;
			}

			win.titleContent = new GUIContent($"Folder: {Path.GetFileName(folderPath)}");
			win.Show();
			win.Focus();

			EditorApplication.delayCall += () =>
			{
				if (win == null) return;

				SetProjectBrowserLocked(win, true);
				FrameFolderInProjectBrowser(win, folderPath);

				win.Repaint();
				win.Focus();
			};
		}

		private static EditorWindow CreateNewProjectBrowserWindow()
		{
			var createWindow = typeof(EditorWindow)
				.GetMethods(BindingFlags.Static | BindingFlags.Public)
				.FirstOrDefault(m =>
				{
					if (m.Name != "CreateWindow") return false;
					var p = m.GetParameters();
					return p.Length == 1 && p[0].ParameterType == typeof(Type);
				});

			if (createWindow != null)
			{
				try
				{
					return createWindow.Invoke(null, new object[] { ProjectBrowserType }) as EditorWindow;
				}
				catch { /* fall back */ }
			}

			try
			{
				return ScriptableObject.CreateInstance(ProjectBrowserType) as EditorWindow;
			}
			catch
			{
				return null;
			}
		}

		private static void SetProjectBrowserLocked(EditorWindow win, bool locked)
		{
			var prop = ProjectBrowserType.GetProperty("isLocked",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
			{
				prop.SetValue(win, locked);
				return;
			}

			var field =
				ProjectBrowserType.GetField("m_LockTracker", BindingFlags.Instance | BindingFlags.NonPublic) ??
				ProjectBrowserType.GetField("m_IsLocked", BindingFlags.Instance | BindingFlags.NonPublic);

			if (field == null) return;

			if (field.FieldType == typeof(bool))
			{
				field.SetValue(win, locked);
				return;
			}

			var tracker = field.GetValue(win);
			if (tracker == null) return;

			var tProp = tracker.GetType().GetProperty("isLocked",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			if (tProp != null && tProp.PropertyType == typeof(bool) && tProp.CanWrite)
				tProp.SetValue(tracker, locked);
		}

		private static void FrameFolderInProjectBrowser(EditorWindow win, string folderPath)
		{
			var folderObj = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
			if (folderObj == null) return;

			EntityId folderId = folderObj.GetEntityId();

			SwitchToTwoColumnView(win);

			if (TryShowFolderContents(win, folderId))
				return;

			if (TrySetFolderSelection(win, folderId))
				return;

			Debug.LogError($"FolderBrowserWindow: Could not open folder contents for '{folderPath}'.");
		}

		private static void SwitchToTwoColumnView(EditorWindow win)
		{
			var setTwoColumns = ProjectBrowserType.GetMethod(
				"SetTwoColumns",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				Type.EmptyTypes,
				null);

			setTwoColumns?.Invoke(win, null);
		}

		private static bool TryShowFolderContents(EditorWindow win, EntityId folderId)
		{
			var method = ProjectBrowserType.GetMethod("ShowFolderContents",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null, new[] { typeof(EntityId), typeof(bool) }, null);
			if (method == null) return false;
			method.Invoke(win, new object[] { folderId, true });
			return true;
		}

		private static bool TrySetFolderSelection(EditorWindow win, EntityId folderId)
		{
			var method = ProjectBrowserType.GetMethod("SetFolderSelection",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null, new[] { typeof(EntityId[]), typeof(bool), typeof(bool) }, null);
			if (method == null) return false;
			method.Invoke(win, new object[] { new[] { folderId }, true, false });
			return true;
		}

		private static bool TryApplyRecursiveFolderExpansion(string folderPath, bool expand)
		{
			if (InternalEditorUtilityType == null || ProjectBrowserType == null)
				return false;

			folderPath = NormalizeFolderPath(folderPath);
			if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
				return false;

			var expandedProp = InternalEditorUtilityType.GetProperty(
				"expandedProjectWindowItemIds",
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

			if (expandedProp == null || !expandedProp.PropertyType.IsArray)
				return false;

			var elementType = expandedProp.PropertyType.GetElementType();
			if (elementType != typeof(EntityId))
				return false;

			if (!TryGetExpandedItemsArray(expandedProp, elementType, out var current))
				return false;

			Array result = expand
				? ExpandMergeSubtree(current, folderPath, elementType)
				: FilterExpandedEntriesNotUnderFolder(current, folderPath, elementType);

			result = SortExpandedArray(result, elementType);
			expandedProp.SetValue(null, result);
			PushExpandedStateToAllProjectBrowsers(result);
			return true;
		}

		private static string NormalizeFolderPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return string.Empty;

			return path.Replace("\\", "/").TrimEnd('/');
		}

		private static bool TryGetExpandedItemsArray(PropertyInfo expandedProp, Type elementType, out Array array)
		{
			var raw = expandedProp.GetValue(null);
			if (raw == null)
			{
				array = Array.CreateInstance(elementType, 0);
				return true;
			}

			if (raw is Array existing && existing.GetType().GetElementType() == elementType)
			{
				array = existing;
				return true;
			}

			array = null;
			return false;
		}

		private static Array FilterExpandedEntriesNotUnderFolder(Array current, string collapseRootPath, Type elementType)
		{
			collapseRootPath = NormalizeFolderPath(collapseRootPath);
			var keep = new List<object>();

			for (var index = 0; index < current.Length; index++)
			{
				var element = current.GetValue(index);
				if (!TryGetAssetPathForExpandedElement(element, elementType, out var assetPath))
				{
					keep.Add(element);
					continue;
				}

				assetPath = NormalizeFolderPath(assetPath);
				if (AssetDatabase.IsValidFolder(assetPath) && IsAssetPathSameOrUnderFolder(assetPath, collapseRootPath))
					continue;

				keep.Add(element);
			}

			var result = Array.CreateInstance(elementType, keep.Count);
			for (var index = 0; index < keep.Count; index++)
				result.SetValue(keep[index], index);

			return result;
		}

		private static bool IsAssetPathSameOrUnderFolder(string assetPath, string folderRootPath)
		{
			if (string.Equals(assetPath, folderRootPath, StringComparison.OrdinalIgnoreCase))
				return true;

			var prefix = folderRootPath.TrimEnd('/') + "/";
			return assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
		}

		private static Array ExpandMergeSubtree(Array current, string folderPath, Type elementType)
		{
			var expandRoot = NormalizeFolderPath(folderPath);

			var keptOutsideExpandRoot = new List<object>();
			var foldersUnderRootByPath = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			for (var index = 0; index < current.Length; index++)
			{
				var element = current.GetValue(index);

				if (!TryGetAssetPathForExpandedElement(element, elementType, out var assetPath))
				{
					keptOutsideExpandRoot.Add(element);
					continue;
				}

				assetPath = NormalizeFolderPath(assetPath);

				if (!AssetDatabase.IsValidFolder(assetPath))
				{
					keptOutsideExpandRoot.Add(element);
					continue;
				}

				if (!IsAssetPathSameOrUnderFolder(assetPath, expandRoot))
				{
					keptOutsideExpandRoot.Add(element);
					continue;
				}

				if (!foldersUnderRootByPath.ContainsKey(assetPath))
					foldersUnderRootByPath[assetPath] = element;
			}

			var requiredPaths = new List<string>();
			CollectFolderPathsWithExpandableChildren(expandRoot, requiredPaths);

			foreach (var path in requiredPaths)
			{
				if (foldersUnderRootByPath.ContainsKey(path))
					continue;

				if (!TryCreateExpandedElementForFolder(path, elementType, out var newElement))
					continue;

				foldersUnderRootByPath[path] = newElement;
			}

			var merged = new List<object>();
			merged.AddRange(keptOutsideExpandRoot);
			merged.AddRange(foldersUnderRootByPath.Values);

			merged.Sort((a, b) => CompareExpandedElements(a, b, elementType));

			var result = Array.CreateInstance(elementType, merged.Count);
			for (var index = 0; index < merged.Count; index++)
				result.SetValue(merged[index], index);

			return result;
		}

		private static void CollectFolderPathsWithExpandableChildren(string folderPath, List<string> results)
		{
			folderPath = NormalizeFolderPath(folderPath);
			if (!AssetDatabase.IsValidFolder(folderPath))
				return;

			if (!FolderHasExpandableContent(folderPath))
				return;

			results.Add(folderPath);

			foreach (var subfolder in AssetDatabase.GetSubFolders(folderPath))
				CollectFolderPathsWithExpandableChildren(NormalizeFolderPath(subfolder), results);
		}

		private static bool TryGetAssetPathForExpandedElement(object element, Type elementType, out string assetPath)
		{
			assetPath = null;
			if (!(element is EntityId id)) return false;
			var unityObject = EditorUtility.EntityIdToObject(id);
			if (unityObject == null) return false;
			assetPath = AssetDatabase.GetAssetPath(unityObject);
			return !string.IsNullOrEmpty(assetPath);
		}

		private static bool TryCreateExpandedElementForFolder(string folderPath, Type elementType, out object element)
		{
			element = null;
			if (elementType != typeof(EntityId)) return false;
			var folderObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(NormalizeFolderPath(folderPath));
			if (folderObject == null) return false;
			element = folderObject.GetEntityId();
			return true;
		}

		private static Array SortExpandedArray(Array source, Type elementType)
		{
			if (source == null || source.Length <= 1)
				return source;

			var ordered = new List<object>(source.Length);
			for (var index = 0; index < source.Length; index++)
				ordered.Add(source.GetValue(index));

			ordered.Sort((a, b) => CompareExpandedElements(a, b, elementType));

			var result = Array.CreateInstance(elementType, ordered.Count);
			for (var index = 0; index < ordered.Count; index++)
				result.SetValue(ordered[index], index);

			return result;
		}

		private static int CompareExpandedElements(object a, object b, Type elementType)
		{
			return ((EntityId)a).CompareTo((EntityId)b);
		}

		private static bool FolderHasExpandableContent(string folderPath)
		{
			if (AssetDatabase.GetSubFolders(folderPath).Length > 0)
				return true;

			foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { folderPath }))
			{
				var assetPath = AssetDatabase.GUIDToAssetPath(guid);
				var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/") ?? string.Empty;
				if (parent == folderPath)
					return true;
			}

			return false;
		}

		private static object BuildExpandedListForTreeState(Type propertyType, Array filtered)
		{
			if (propertyType != typeof(List<EntityId>) || !(filtered is EntityId[] ids))
				return null;
			return new List<EntityId>(ids);
		}

		private static void PushExpandedStateToAllProjectBrowsers(Array filteredExpanded)
		{
			UnityEngine.Object[] browsers;
			try
			{
				browsers = Resources.FindObjectsOfTypeAll(ProjectBrowserType);
			}
			catch
			{
				return;
			}

			if (browsers == null || browsers.Length == 0)
				return;

			foreach (var obj in browsers)
			{
				var pb = obj as EditorWindow;
				if (pb == null)
					continue;

				foreach (var stateFieldName in new[] { "m_FolderTreeState", "m_AssetTreeState" })
				{
					var stateField = ProjectBrowserType.GetField(stateFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
					var state = stateField?.GetValue(pb);
					if (state == null)
						continue;

					var expandedIdsProp = state.GetType().GetProperty(
						"expandedIDs",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

					if (expandedIdsProp == null)
						continue;

					var expandedValue = BuildExpandedListForTreeState(expandedIdsProp.PropertyType, filteredExpanded);
					if (expandedValue != null)
						expandedIdsProp.SetValue(state, expandedValue);
				}

				foreach (var treeFieldName in new[] { "m_FolderTree", "m_AssetTree" })
				{
					var treeField = ProjectBrowserType.GetField(treeFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
					var tree = treeField?.GetValue(pb);
					if (tree == null)
						continue;

					var reload = tree.GetType().GetMethod(
						"ReloadData",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

					reload?.Invoke(tree, null);
				}

				pb.Repaint();
			}
		}
	}
}
