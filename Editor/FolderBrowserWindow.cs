// FolderBrowserWindow.cs
// Put under Assets/.../Editor/

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FolderBrowserWindow
{
	private static string s_LastContextGuid;
	private static double s_LastContextTime;
	private const double FreshSeconds = 0.75;

	private static readonly Type ProjectBrowserType;

	static FolderBrowserWindow()
	{
		EditorApplication.projectWindowItemOnGUI -= OnProjectItemGUI;
		EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;

		ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
	}

	private static void OnProjectItemGUI(string guid, Rect rect)
	{
		var e = Event.current;
		if (e == null) return;

		// Capture right-click on an actual item row/thumbnail (more reliable than ContextClick alone).
		if ((e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition)) ||
			(e.type == EventType.ContextClick && rect.Contains(e.mousePosition)))
		{
			s_LastContextGuid = guid;
			s_LastContextTime = EditorApplication.timeSinceStartup;
		}
	}

	private static bool TryGetClickedFolder(out string folderPath)
	{
		folderPath = null;

		// 1) GUID captured from right-click
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

		// 2) Selection fallback (Unity often selects item on right-click)
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

	// ---------------- MENU ----------------

	[MenuItem("Assets/Open in new View", true, 2100)]
	private static bool ValidateMenu()
	{
		return TryGetClickedFolder(out _);
	}

	[MenuItem("Assets/Open in new View", false, 2100)]
	private static void OpenMenu()
	{
		if (!TryGetClickedFolder(out var folder))
			return;

		// Defer to avoid context menu event-loop weirdness.
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

	// ---------------- CORE ----------------

	private static void OpenNewLockedProjectBrowserAtFolder(string folderPath)
	{
		if (ProjectBrowserType == null)
		{
			Debug.LogError("FolderBrowserWindow: Could not find UnityEditor.ProjectBrowser type.");
			return;
		}

		// GUARANTEE: create a NEW Project Browser window instance.
		var win = CreateNewProjectBrowserWindow();
		if (win == null)
		{
			Debug.LogError("FolderBrowserWindow: Failed to create a new Project Browser window.");
			return;
		}

		win.titleContent = new GUIContent($"Folder: {Path.GetFileName(folderPath)}");
		win.Show();
		win.Focus();

		// Configure after it initializes.
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
		// Preferred (Unity 2020+ typically): EditorWindow.CreateWindow(Type)
		// This creates a NEW dockable window instance (does not reuse existing).
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

		// Fallback: CreateInstance + Show (older versions).
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
		// Most versions: property "isLocked"
		var prop = ProjectBrowserType.GetProperty("isLocked",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
		{
			prop.SetValue(win, locked);
			return;
		}

		// Fallback fields
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

		// Preferred: SetFolderSelection(int[] folderInstanceIDs, bool revealInTreeView)
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

		// Fallback: FrameObject(int instanceID, bool ping)
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

		// Last resort
		EditorGUIUtility.PingObject(folderObj);
	}
}
