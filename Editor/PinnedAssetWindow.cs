// Assets/Editor/FolderBrowser/PinnedAssetWindow.cs
using UnityEditor;
using UnityEngine;

public class PinnedAssetWindow : EditorWindow
{
	private const string DragKey = "PinnedAssetWindow_Drag";
	private static UnityEngine.Object s_DragObject;

	[SerializeField] private UnityEngine.Object _pinned;
	[SerializeField] private string _path;

	[MenuItem("Tools/Folder Browser/Pinned Asset Shelf")]
	public static void OpenEmpty()
	{
		var w = CreateInstance<PinnedAssetWindow>();
		w.titleContent = new GUIContent("Pinned Asset");
		w.minSize = new Vector2(280, 150);
		w.Show();
	}

	[MenuItem("Assets/Pin in Asset Shelf", false, 2001)]
	private static void PinSelected()
	{
		var obj = Selection.activeObject;
		if (!obj) return;

		var w = CreateInstance<PinnedAssetWindow>();
		w.titleContent = new GUIContent("Pinned Asset");
		w.minSize = new Vector2(280, 150);
		w.SetPinned(obj);
		w.Show();
	}

	private void OnEnable()
	{
		SceneView.duringSceneGui -= OnSceneGUI;
		SceneView.duringSceneGui += OnSceneGUI;

		if (_pinned == null && !string.IsNullOrEmpty(_path))
			_pinned = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_path);
	}

	private void OnDisable()
	{
		SceneView.duringSceneGui -= OnSceneGUI;
	}

	private void SetPinned(UnityEngine.Object obj)
	{
		_pinned = obj;
		_path = obj ? AssetDatabase.GetAssetPath(obj) : "";
		Repaint();
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("Pinned Asset", EditorStyles.boldLabel);

		EditorGUI.BeginChangeCheck();
		var newPinned = EditorGUILayout.ObjectField("Asset", _pinned, typeof(UnityEngine.Object), false);
		if (EditorGUI.EndChangeCheck())
			SetPinned(newPinned);

		GUILayout.Space(8);

		DrawDragTile();   // <-- DRAG OUT FROM HERE
		GUILayout.Space(8);

		using (new EditorGUILayout.HorizontalScope())
		{
			GUI.enabled = _pinned != null;

			if (GUILayout.Button("Ping"))
				EditorGUIUtility.PingObject(_pinned);

			if (GUILayout.Button("Open"))
				AssetDatabase.OpenAsset(_pinned);

			if (GUILayout.Button("Clear", GUILayout.Width(60)))
				SetPinned(null);

			GUI.enabled = true;
		}

		GUILayout.Space(8);
		DrawDropZone();   // <-- DROP IN TO PIN
	}

	private void DrawDragTile()
	{
		var r = GUILayoutUtility.GetRect(0, 54, GUILayout.ExpandWidth(true));
		GUI.Box(r, GUIContent.none, EditorStyles.helpBox);

		if (_pinned == null)
		{
			GUI.Label(r, "Drag: (nothing pinned)", EditorStyles.centeredGreyMiniLabel);
			return;
		}

		var content = EditorGUIUtility.ObjectContent(_pinned, _pinned.GetType());
		var thumbRect = new Rect(r.x + 8, r.y + 8, 38, 38);
		var textRect = new Rect(r.x + 54, r.y + 10, r.width - 62, 34);

		if (content.image != null)
			GUI.DrawTexture(thumbRect, content.image, ScaleMode.ScaleToFit);

		GUI.Label(textRect, $"DRAG →  {_pinned.name}\n(drop onto Inspector fields / Scene / Hierarchy)", EditorStyles.miniLabel);

		var e = Event.current;

		// Start the drag
		if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
		{
			s_DragObject = _pinned;

			DragAndDrop.PrepareStartDrag();
			DragAndDrop.objectReferences = new[] { _pinned };
			DragAndDrop.SetGenericData(DragKey, _pinned); // used for Scene placement
			DragAndDrop.StartDrag(_pinned.name);

			e.Use();
		}
	}

	private void DrawDropZone()
	{
		var dropRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
		GUI.Box(dropRect, "Drop an Asset Here to Pin", EditorStyles.helpBox);

		var e = Event.current;
		if (!dropRect.Contains(e.mousePosition))
			return;

		if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

			if (e.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();
				if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
					SetPinned(DragAndDrop.objectReferences[0]);
			}

			e.Use();
		}
	}

	// ---- Scene drop support (Prefab placement) ----
	private static void OnSceneGUI(SceneView sv)
	{
		var e = Event.current;
		if (e == null) return;

		// Only react if the drag originated from our tile
		var dragged = DragAndDrop.GetGenericData(DragKey) as UnityEngine.Object;
		if (dragged == null) return;

		// We only auto-place prefabs into the scene. Everything else is still draggable to Inspector fields.
		if (!(dragged is GameObject go) || PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.NotAPrefab)
			return;

		if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

			if (e.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();

				var placed = PlacePrefabInScene(go, sv);
				if (placed != null)
				{
					Selection.activeGameObject = placed;
					Undo.RegisterCreatedObjectUndo(placed, "Place Pinned Prefab");
				}

				DragAndDrop.SetGenericData(DragKey, null);
				s_DragObject = null;
			}

			e.Use();
		}
	}

	private static GameObject PlacePrefabInScene(GameObject prefabAsset, SceneView sv)
	{
		// Raycast to colliders first
		var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

		Vector3 pos;
		if (Physics.Raycast(ray, out var hit, 10000f))
			pos = hit.point;
		else
			pos = ray.origin + ray.direction * 10f; // fallback: 10m in front of camera

		// Instantiate prefab properly
		var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
		if (instance == null) return null;

		instance.transform.position = pos;
		instance.transform.rotation = Quaternion.identity;
		return instance;
	}
}
