using System;
using System.Collections;
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
		private static readonly Type EntityIdType;

		static FolderBrowserWindow()
		{
			EditorApplication.projectWindowItemOnGUI -= OnProjectItemGUI;
			EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;

			ProjectBrowserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
			InternalEditorUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.InternalEditorUtility");
			EntityIdType = typeof(UnityEngine.Object).Assembly.GetType("UnityEngine.EntityId");
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

		private static bool TryGetClickedFolder(out string folderPath)
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
			return TryGetClickedFolder(out _);
		}

		[MenuItem("Assets/Open in new View", false, 2100)]
		private static void OpenInNewView()
		{
			if (!TryGetClickedFolder(out var folder))
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
			return ProjectBrowserType != null && InternalEditorUtilityType != null && TryGetClickedFolder(out _);
		}

		[MenuItem("Assets/Expand All", false, 2110)]
		private static void ExpandAll()
		{
			if (!TryGetClickedFolder(out var folder))
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
			if (!TryGetClickedFolder(out var folder))
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

			int folderId = folderObj.GetInstanceID();

			var setFolderSelection = ProjectBrowserType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.FirstOrDefault(m =>
				{
					if (m.Name != "SetFolderSelection") return false;
					var p = m.GetParameters();
					return p.Length == 2 &&
						   p[0].ParameterType == typeof(int[]) &&
						   p[1].ParameterType == typeof(bool);
				});

			if (setFolderSelection != null)
			{
				setFolderSelection.Invoke(win, new object[] { new[] { folderId }, true });
				return;
			}

			var frameObject = ProjectBrowserType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.FirstOrDefault(m =>
				{
					if (m.Name != "FrameObject") return false;
					var p = m.GetParameters();
					return p.Length == 2 &&
						   p[0].ParameterType == typeof(int) &&
						   p[1].ParameterType == typeof(bool);
				});

			if (frameObject != null)
			{
				frameObject.Invoke(win, new object[] { folderId, false });
				return;
			}

			EditorGUIUtility.PingObject(folderObj);
		}

		private static bool TryApplyRecursiveFolderExpansion(string folderPath, bool expand)
		{
			if (InternalEditorUtilityType == null || ProjectBrowserType == null)
				return false;

			folderPath = NormalizeFolderPath(folderPath);
			if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
				return false;

			var expandedProp = InternalEditorUtilityType.GetProperty(
				"expandedProjectWindowItems",
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

			if (expandedProp == null || !expandedProp.PropertyType.IsArray)
				return false;

			var elementType = expandedProp.PropertyType.GetElementType();
			if (elementType == null)
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
			if (element == null)
				return false;

			UnityEngine.Object unityObject = null;

			if (elementType == typeof(int))
				unityObject = EditorUtility.InstanceIDToObject((int)element);
			else if (EntityIdType != null && elementType == EntityIdType)
			{
				foreach (var method in typeof(EditorUtility).GetMethods(BindingFlags.Public | BindingFlags.Static))
				{
					if (method.Name != "EntityIdToObject")
						continue;

					var parameters = method.GetParameters();
					if (parameters.Length != 1 || parameters[0].ParameterType != elementType)
						continue;

					unityObject = method.Invoke(null, new[] { element }) as UnityEngine.Object;
					if (unityObject != null)
						break;
				}
			}

			if (unityObject == null)
				return false;

			assetPath = AssetDatabase.GetAssetPath(unityObject);
			return !string.IsNullOrEmpty(assetPath);
		}

		private static bool TryCreateExpandedElementForFolder(string folderPath, Type elementType, out object element)
		{
			element = null;
			var folderObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(NormalizeFolderPath(folderPath));
			if (folderObject == null)
				return false;

			if (elementType == typeof(int))
			{
				element = folderObject.GetInstanceID();
				return true;
			}

			if (EntityIdType != null && elementType == EntityIdType)
			{
				var getEntityId = typeof(UnityEngine.Object).GetMethod(
					"GetEntityId",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
					null,
					Type.EmptyTypes,
					null);

				if (getEntityId != null && EntityIdType.IsAssignableFrom(getEntityId.ReturnType))
				{
					element = getEntityId.Invoke(folderObject, null);
					return true;
				}

				return TryConvertInstanceIdToEntityId(folderObject.GetInstanceID(), out element);
			}

			return false;
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
			var keyA = ExpandedSortKey(a, elementType);
			var keyB = ExpandedSortKey(b, elementType);
			return keyA.CompareTo(keyB);
		}

		private static long ExpandedSortKey(object element, Type elementType)
		{
			if (elementType == typeof(int))
				return (int)element;

			if (TryConvertEntityIdToInstanceId(element, out var instanceId))
				return instanceId;

			return 0;
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

		private static bool TryConvertEntityIdToInstanceId(object entityIdValue, out int instanceId)
		{
			instanceId = 0;
			if (entityIdValue == null)
				return false;

			var valueType = entityIdValue.GetType();

			foreach (var method in valueType.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if (method.Name != "op_Implicit" || method.ReturnType != typeof(int))
					continue;

				var parameters = method.GetParameters();
				if (parameters.Length == 1 && parameters[0].ParameterType == valueType)
				{
					instanceId = (int)method.Invoke(null, new[] { entityIdValue });
					return true;
				}
			}

			foreach (var method in typeof(EditorUtility).GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if (method.Name != "EntityIdToObject")
					continue;

				var parameters = method.GetParameters();
				if (parameters.Length != 1 || parameters[0].ParameterType != valueType)
					continue;

				var unityObject = method.Invoke(null, new[] { entityIdValue }) as UnityEngine.Object;
				if (unityObject != null)
				{
					instanceId = unityObject.GetInstanceID();
					return true;
				}
			}

			return false;
		}

		private static bool TryConvertInstanceIdToEntityId(int instanceId, out object entityIdBoxed)
		{
			entityIdBoxed = null;
			if (EntityIdType == null)
				return false;

			var fromInstanceId = EntityIdType.GetMethod(
				"FromInstanceID",
				BindingFlags.Public | BindingFlags.Static,
				null,
				new[] { typeof(int) },
				null);

			if (fromInstanceId != null && EntityIdType.IsAssignableFrom(fromInstanceId.ReturnType))
			{
				entityIdBoxed = fromInstanceId.Invoke(null, new object[] { instanceId });
				return true;
			}

			foreach (var method in EntityIdType.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if (method.Name != "op_Implicit" || method.ReturnType != EntityIdType)
					continue;

				var parameters = method.GetParameters();
				if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
				{
					entityIdBoxed = method.Invoke(null, new object[] { instanceId });
					return true;
				}
			}

			var ctor = EntityIdType.GetConstructor(new[] { typeof(int) });
			if (ctor != null)
			{
				entityIdBoxed = ctor.Invoke(new object[] { instanceId });
				return true;
			}

			var unityObject = EditorUtility.InstanceIDToObject(instanceId);
			if (unityObject == null)
				return false;

			var getEntityId = typeof(UnityEngine.Object).GetMethod(
				"GetEntityId",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				Type.EmptyTypes,
				null);

			if (getEntityId == null || !EntityIdType.IsAssignableFrom(getEntityId.ReturnType))
				return false;

			entityIdBoxed = getEntityId.Invoke(unityObject, null);
			return true;
		}

		private static object BuildExpandedListForTreeState(Type propertyType, Array filtered)
		{
			if (filtered == null)
				return null;

			var filteredElementType = filtered.GetType().GetElementType();

			if (propertyType == typeof(List<int>) && filteredElementType == typeof(int))
			{
				var list = new List<int>(filtered.Length);
				for (var index = 0; index < filtered.Length; index++)
					list.Add((int)filtered.GetValue(index));

				return list;
			}

			if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
			{
				var listElementType = propertyType.GetGenericArguments()[0];
				var list = (IList)Activator.CreateInstance(propertyType);

				if (listElementType == filteredElementType)
				{
					for (var index = 0; index < filtered.Length; index++)
						list.Add(filtered.GetValue(index));

					return list;
				}

				if (listElementType == typeof(int) && EntityIdType != null && filteredElementType == EntityIdType)
				{
					for (var index = 0; index < filtered.Length; index++)
					{
						if (TryConvertEntityIdToInstanceId(filtered.GetValue(index), out var id))
							list.Add(id);
					}

					return list;
				}

				if (EntityIdType != null && listElementType == EntityIdType && filteredElementType == typeof(int))
				{
					for (var index = 0; index < filtered.Length; index++)
					{
						var id = (int)filtered.GetValue(index);
						if (TryConvertInstanceIdToEntityId(id, out var boxed))
							list.Add(boxed);
					}

					return list;
				}
			}

			return null;
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
