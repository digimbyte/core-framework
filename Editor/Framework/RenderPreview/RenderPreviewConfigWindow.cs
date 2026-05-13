using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Core.Framework.Editor.RenderPreview
{
public class RenderPreviewConfigWindow : EditorWindow
{
    private static RenderPreviewConfigWindow window;
    private List<RenderPreviewSettings> presets;
    private int selectedPresetIndex = 0;
    private RenderPreviewSettings currentSettings;
    private Vector2 scrollPosition;
    private bool showAdvancedSettings = false;
    
    // Preview system
    private Texture2D previewTexture;
    private string[] selectedFolders;
    private string currentPreviewAssetPath;
    private bool autoRefreshPreview = false;

    private string cachedBonePathsAssetKey;
    private string[] cachedBonePathsForUi;

    private static System.Action<RenderPreviewSettings> onRenderCallback;

    public static void ShowWindow(System.Action<RenderPreviewSettings> onRender, string[] folders)
    {
        window = GetWindow<RenderPreviewConfigWindow>("Render Preview Config");
        window.minSize = new Vector2(600, 700);
        onRenderCallback = onRender;
        window.selectedFolders = folders;
        window.Initialize();
        window.Show();
    }

    private void Initialize()
    {
        presets = RenderPreviewSettings.GetDefaultPresets();
        LoadCustomPresets();
        
        if (presets.Count > 0)
        {
            currentSettings = presets[0].Clone();
        }
        else
        {
            currentSettings = new RenderPreviewSettings();
        }
    }

    private void OnGUI()
    {
        if (currentSettings == null)
        {
            Initialize();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Render Preview Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        DrawPresetSelector();
        EditorGUILayout.Space(10);

        DrawBasicSettings();
        EditorGUILayout.Space(10);

        DrawCameraSettings();
        EditorGUILayout.Space(10);

        DrawFocusTargetSettings();
        EditorGUILayout.Space(10);

        DrawModelOffsetSettings();
        EditorGUILayout.Space(10);

        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Lighting Settings", true);
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            DrawLightingSettings();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(20);

        DrawPreview();
        EditorGUILayout.Space(10);

        DrawButtons();

        EditorGUILayout.EndScrollView();
    }
    
    private void OnDestroy()
    {
        CleanupPreview();
    }

    private void DrawPresetSelector()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preset:", GUILayout.Width(100));

        string[] presetNames = new string[presets.Count];
        for (int i = 0; i < presets.Count; i++)
        {
            
            if (autoRefreshPreview && previewTexture != null)
            {
                RefreshPreview();
            }
            presetNames[i] = presets[i].presetName;
        }

        int newPresetIndex = EditorGUILayout.Popup(selectedPresetIndex, presetNames);
        if (newPresetIndex != selectedPresetIndex)
        {
            selectedPresetIndex = newPresetIndex;
            currentSettings = presets[selectedPresetIndex].Clone();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Save as New Preset", GUILayout.Width(150)))
        {
            SaveAsNewPreset();
        }

        if (GUILayout.Button("Update Current Preset", GUILayout.Width(150)))
        {
            UpdateCurrentPreset();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawBasicSettings()
    {
        EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        currentSettings.previewSize = EditorGUILayout.IntSlider("Preview Size", currentSettings.previewSize, 64, 2048);

        currentSettings.includeSubfolders = EditorGUILayout.Toggle("Include subfolders", currentSettings.includeSubfolders);

        currentSettings.backgroundType = (RenderPreviewSettings.BackgroundType)EditorGUILayout.EnumPopup(
            "Background Type", currentSettings.backgroundType);

        if (currentSettings.backgroundType == RenderPreviewSettings.BackgroundType.Color)
        {
            currentSettings.backgroundColor = EditorGUILayout.ColorField("Background Color", currentSettings.backgroundColor);
        }

        if (EditorGUI.EndChangeCheck() && autoRefreshPreview && previewTexture != null)
        {
            RefreshPreview();
        }
    }

    private void DrawCameraSettings()
    {
        EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        currentSettings.cameraDirection = EditorGUILayout.Vector3Field("Camera Direction", currentSettings.cameraDirection);
        
        if (GUILayout.Button("Normalize Direction"))
        {
            currentSettings.cameraDirection = currentSettings.cameraDirection.normalized;
            if (autoRefreshPreview && previewTexture != null)
            {
                RefreshPreview();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Front View"))
        {
            currentSettings.cameraDirection = new Vector3(0f, 0.15f, -1f).normalized;
            if (autoRefreshPreview && previewTexture != null)
            {
                RefreshPreview();
            }
        }
        if (GUILayout.Button("Side View"))
        {
            currentSettings.cameraDirection = new Vector3(1f, 0.15f, 0f).normalized;
            if (autoRefreshPreview && previewTexture != null)
            {
                RefreshPreview();
            }
        }
        if (GUILayout.Button("Top View"))
        {
            currentSettings.cameraDirection = new Vector3(0f, 1f, -0.1f).normalized;
            if (autoRefreshPreview && previewTexture != null)
            {
                RefreshPreview();
            }
        }
        EditorGUILayout.EndHorizontal();

        currentSettings.cameraDistance = EditorGUILayout.Slider("Camera Distance", currentSettings.cameraDistance, 0.5f, 3f);
        currentSettings.fieldOfView = EditorGUILayout.Slider("Field of View", currentSettings.fieldOfView, 10f, 90f);
        currentSettings.autoFitFieldOfView = EditorGUILayout.Toggle("Auto-fit field of view", currentSettings.autoFitFieldOfView);
        currentSettings.cameraRoll = EditorGUILayout.Slider("Camera Roll", currentSettings.cameraRoll, 0f, 360f);
        
        if (EditorGUI.EndChangeCheck() && autoRefreshPreview && previewTexture != null)
        {
            RefreshPreview();
        }
    }

    private void DrawFocusTargetSettings()
    {
        EditorGUILayout.LabelField("Focus Target", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        int pivotMode = 0;
        if (currentSettings.focusPivot == RenderPreviewSettings.FocusPivot.Bone)
        {
            pivotMode = 1;
        }
        else if (currentSettings.focusPivot == RenderPreviewSettings.FocusPivot.CustomPivot)
        {
            pivotMode = 2;
        }

        string[] pivotLabels = { "Bounds center", "Bone", "Custom pivot" };
        int newPivotMode = EditorGUILayout.Popup("Pivot", pivotMode, pivotLabels);
        if (newPivotMode == 1)
        {
            currentSettings.focusPivot = RenderPreviewSettings.FocusPivot.Bone;
        }
        else if (newPivotMode == 2)
        {
            currentSettings.focusPivot = RenderPreviewSettings.FocusPivot.CustomPivot;
        }
        else
        {
            currentSettings.focusPivot = RenderPreviewSettings.FocusPivot.BoundsCenter;
        }

        if (currentSettings.focusPivot == RenderPreviewSettings.FocusPivot.Bone)
        {
            EnsureBonePathCacheForUi();
            if (cachedBonePathsForUi != null && cachedBonePathsForUi.Length > 0)
            {
                var labels = new string[cachedBonePathsForUi.Length];
                for (int i = 0; i < cachedBonePathsForUi.Length; i++)
                {
                    labels[i] = string.IsNullOrEmpty(cachedBonePathsForUi[i])
                        ? "Root"
                        : cachedBonePathsForUi[i];
                }

                int boneIndex = 0;
                for (int i = 0; i < cachedBonePathsForUi.Length; i++)
                {
                    if (cachedBonePathsForUi[i] == currentSettings.focusBonePath)
                    {
                        boneIndex = i;
                        break;
                    }
                }

                int picked = EditorGUILayout.Popup("Bone", boneIndex, labels);
                currentSettings.focusBonePath = cachedBonePathsForUi[picked];
            }
        }
        else if (currentSettings.focusPivot == RenderPreviewSettings.FocusPivot.CustomPivot)
        {
            currentSettings.customPivotPrimary = EditorGUILayout.TextField("Primary", currentSettings.customPivotPrimary);
            currentSettings.customPivotFallback = EditorGUILayout.TextField("Fallback", currentSettings.customPivotFallback);
        }

        if (EditorGUI.EndChangeCheck() && autoRefreshPreview && previewTexture != null)
        {
            RefreshPreview();
        }
    }

    private void EnsureBonePathCacheForUi()
    {
        if (!string.IsNullOrEmpty(cachedBonePathsAssetKey) &&
            cachedBonePathsAssetKey == currentPreviewAssetPath &&
            cachedBonePathsForUi != null)
        {
            return;
        }

        RebuildBonePathCacheFromCurrentPreviewAsset();
    }

    private void RebuildBonePathCacheFromCurrentPreviewAsset()
    {
        cachedBonePathsForUi = null;
        cachedBonePathsAssetKey = currentPreviewAssetPath;

        if (string.IsNullOrEmpty(currentPreviewAssetPath))
        {
            return;
        }

        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(currentPreviewAssetPath);
        if (!(asset is GameObject prefab))
        {
            return;
        }

        GameObject instance = RenderPrefabPreviews.TryInstantiatePrefabForPreview(prefab);
        if (instance == null)
        {
            return;
        }

        try
        {
            List<string> paths = RenderPrefabPreviews.CollectSortedBonePaths(instance);
            cachedBonePathsForUi = paths.ToArray();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private void DrawModelOffsetSettings()
    {
        EditorGUILayout.LabelField("Model Offset", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        currentSettings.modelOffset = EditorGUILayout.Vector3Field("Offset (world units)", currentSettings.modelOffset);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Offset"))
        {
            currentSettings.modelOffset = Vector3.zero;
            if (autoRefreshPreview && previewTexture != null)
            {
                RefreshPreview();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        currentSettings.autoFrame = EditorGUILayout.Toggle("Auto frame", currentSettings.autoFrame);

        if (EditorGUI.EndChangeCheck() && autoRefreshPreview && previewTexture != null)
        {
            RefreshPreview();
        }
    }

    private void DrawLightingSettings()
    {
        EditorGUI.BeginChangeCheck();
        
        EditorGUILayout.LabelField("Key Light", EditorStyles.miniLabel);
        currentSettings.keyLightIntensity = EditorGUILayout.Slider("Intensity", currentSettings.keyLightIntensity, 0f, 3f);
        currentSettings.keyLightRotation = EditorGUILayout.Vector3Field("Rotation", currentSettings.keyLightRotation);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Fill Light", EditorStyles.miniLabel);
        currentSettings.fillLightIntensity = EditorGUILayout.Slider("Intensity", currentSettings.fillLightIntensity, 0f, 3f);
        currentSettings.fillLightRotation = EditorGUILayout.Vector3Field("Rotation", currentSettings.fillLightRotation);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Ambient", EditorStyles.miniLabel);
        currentSettings.ambientColor = EditorGUILayout.ColorField("Ambient Color", currentSettings.ambientColor);
        
        if (EditorGUI.EndChangeCheck() && autoRefreshPreview && previewTexture != null)
        {
            RefreshPreview();
        }
    }
    
    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Generate Preview", GUILayout.Width(150)))
        {
            RefreshPreview();
        }
        
        autoRefreshPreview = EditorGUILayout.Toggle("Auto-Refresh", autoRefreshPreview);
        
        if (previewTexture != null && GUILayout.Button("Pick Different Item", GUILayout.Width(150)))
        {
            RefreshPreview(true);
        }
        
        EditorGUILayout.EndHorizontal();
        
        if (previewTexture != null)
        {
            // Match export: render is square; IMGUI would otherwise stretch GetRect to the scroll view width.
            const float livePreviewMaxSide = 300f;
            int textureWidth = previewTexture.width;
            int textureHeight = previewTexture.height;
            float displaySide = Mathf.Clamp(
                Mathf.Min(livePreviewMaxSide, textureWidth, textureHeight),
                1f,
                livePreviewMaxSide);

            Rect previewRect = GUILayoutUtility.GetRect(
                displaySide,
                displaySide,
                GUILayout.Width(displaySide),
                GUILayout.Height(displaySide),
                GUILayout.ExpandWidth(false));
            EditorGUI.DrawTextureTransparent(previewRect, previewTexture, ScaleMode.ScaleToFit);
            
            if (!string.IsNullOrEmpty(currentPreviewAssetPath))
            {
                EditorGUILayout.LabelField($"Previewing: {System.IO.Path.GetFileName(currentPreviewAssetPath)}", EditorStyles.miniLabel);
            }
        }
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Cancel", GUILayout.Height(30)))
        {
            Close();
        }

        if (GUILayout.Button("Render with These Settings", GUILayout.Height(30)))
        {
            onRenderCallback?.Invoke(currentSettings);
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SaveAsNewPreset()
    {
        string presetName = EditorUtility.SaveFilePanel(
            "Save Preset As",
            "Assets/Editor/RenderPresets",
            "CustomPreset.json",
            "json");

        if (!string.IsNullOrEmpty(presetName))
        {
            currentSettings.presetName = System.IO.Path.GetFileNameWithoutExtension(presetName);
            string json = JsonUtility.ToJson(currentSettings, true);
            System.IO.File.WriteAllText(presetName, json);
            
            AssetDatabase.Refresh();
            LoadCustomPresets();
            
            Debug.Log($"Preset saved: {presetName}");
        }
    }

    private void UpdateCurrentPreset()
    {
        if (selectedPresetIndex < presets.Count)
        {
            presets[selectedPresetIndex] = currentSettings.Clone();
            presets[selectedPresetIndex].presetName = currentSettings.presetName;
            
            // Only save if it's a custom preset (saved to disk)
            string presetsPath = "Assets/Editor/RenderPresets";
            if (System.IO.Directory.Exists(presetsPath))
            {
                string[] files = System.IO.Directory.GetFiles(presetsPath, "*.json");
                foreach (string file in files)
                {
                    string json = System.IO.File.ReadAllText(file);
                    RenderPreviewSettings saved = JsonUtility.FromJson<RenderPreviewSettings>(json);
                    if (saved.presetName == currentSettings.presetName)
                    {
                        System.IO.File.WriteAllText(file, JsonUtility.ToJson(currentSettings, true));
                        Debug.Log($"Preset updated: {file}");
                        return;
                    }
                }
            }
            
            Debug.Log("Updated preset in memory (default presets cannot be saved to disk).");
        }
    }

    private void LoadCustomPresets()
    {
        string presetsPath = "Assets/Editor/RenderPresets";
        if (!System.IO.Directory.Exists(presetsPath))
        {
            return;
        }

        string[] files = System.IO.Directory.GetFiles(presetsPath, "*.json");
        foreach (string file in files)
        {
            try
            {
                string json = System.IO.File.ReadAllText(file);
                RenderPreviewSettings preset = JsonUtility.FromJson<RenderPreviewSettings>(json);
                
                // Check if preset already exists
                bool exists = false;
                foreach (var p in presets)
                {
                    if (p.presetName == preset.presetName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    presets.Add(preset);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to load preset from {file}: {e.Message}");
            }
        }
    }
    
    private void RefreshPreview(bool forceNewAsset = false)
    {
        if (selectedFolders == null || selectedFolders.Length == 0)
        {
            Debug.LogWarning("No folders selected for preview.");
            return;
        }
        
        CleanupPreview();
        
        // Pick a random prefab or material
        UnityEngine.Object asset = forceNewAsset ? PickRandomAsset(selectedFolders) : (currentPreviewAssetPath != null ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(currentPreviewAssetPath) : PickRandomAsset(selectedFolders));
        
        if (asset == null)
        {
            asset = PickRandomAsset(selectedFolders);
        }
        
        if (asset == null)
        {
            Debug.LogWarning("No prefabs or materials found in selected folders.");
            return;
        }
        
        currentPreviewAssetPath = AssetDatabase.GetAssetPath(asset);
        
        // Render the preview
        if (asset is GameObject prefab)
        {
            previewTexture = RenderPrefabPreview(prefab);
        }
        else if (asset is Material material)
        {
            previewTexture = RenderMaterialPreview(material);
        }
        
        Repaint();
    }
    
    private UnityEngine.Object PickRandomAsset(string[] folders)
    {
        var allAssets = new System.Collections.Generic.List<string>();

        bool recursive = currentSettings != null && currentSettings.includeSubfolders;

        foreach (string folder in folders)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folder });

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (RenderPrefabPreviews.AssetMatchesFolderScope(path, folder, recursive))
                {
                    allAssets.Add(path);
                }
            }

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (RenderPrefabPreviews.AssetMatchesFolderScope(path, folder, recursive))
                {
                    allAssets.Add(path);
                }
            }
        }

        if (allAssets.Count == 0)
        {
            return null;
        }

        string randomPath = allAssets[UnityEngine.Random.Range(0, allAssets.Count)];
        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(randomPath);
    }
    
    private Texture2D RenderPrefabPreview(GameObject prefab)
    {
        PreviewRenderUtility previewUtility = new PreviewRenderUtility();
        GameObject instance = null;

        try
        {
            instance = RenderPrefabPreviews.TryInstantiatePrefabForPreview(prefab);
            if (instance == null)
            {
                return null;
            }

            previewUtility.AddSingleGO(instance);

            if (!TryGetRenderableBounds(instance, out Bounds bounds))
            {
                return null;
            }

            cachedBonePathsAssetKey = currentPreviewAssetPath;
            cachedBonePathsForUi = RenderPrefabPreviews.CollectSortedBonePaths(instance).ToArray();

            Vector3 lookTarget = RenderPrefabPreviews.GetCameraLookTargetWorld(instance, bounds, currentSettings);

            Vector3 cameraPanWorld = Vector3.zero;
            float? fieldOfViewOverride = null;
            bool wantFrame = currentSettings.autoFrame;
            bool wantFit = currentSettings.autoFitFieldOfView;
            if (wantFrame && wantFit)
            {
                cameraPanWorld = RenderPrefabPreviews.ComputeAutoFrameCameraPanWorld(
                    previewUtility,
                    instance,
                    bounds,
                    lookTarget,
                    currentSettings,
                    null);
                fieldOfViewOverride = RenderPrefabPreviews.ComputeTightestFieldOfViewForGreenBorder(
                    previewUtility,
                    bounds,
                    lookTarget,
                    cameraPanWorld,
                    currentSettings);
                cameraPanWorld = RenderPrefabPreviews.ComputeAutoFrameCameraPanWorld(
                    previewUtility,
                    instance,
                    bounds,
                    lookTarget,
                    currentSettings,
                    fieldOfViewOverride);
                fieldOfViewOverride = RenderPrefabPreviews.ComputeTightestFieldOfViewForGreenBorder(
                    previewUtility,
                    bounds,
                    lookTarget,
                    cameraPanWorld,
                    currentSettings);
            }
            else if (wantFrame)
            {
                cameraPanWorld = RenderPrefabPreviews.ComputeAutoFrameCameraPanWorld(
                    previewUtility,
                    instance,
                    bounds,
                    lookTarget,
                    currentSettings,
                    null);
            }
            else if (wantFit)
            {
                fieldOfViewOverride = RenderPrefabPreviews.ComputeTightestFieldOfViewForGreenBorder(
                    previewUtility,
                    bounds,
                    lookTarget,
                    Vector3.zero,
                    currentSettings);
            }

            RenderPrefabPreviews.SetupPrefabPreviewCamera(
                previewUtility,
                bounds,
                lookTarget,
                currentSettings.previewSize,
                currentSettings.previewSize,
                currentSettings,
                null,
                cameraPanWorld,
                fieldOfViewOverride);
            RenderPrefabPreviews.SetupPrefabPreviewLights(previewUtility, currentSettings);

            previewUtility.BeginStaticPreview(new Rect(0, 0, currentSettings.previewSize, currentSettings.previewSize));
            RenderPrefabPreviews.RenderPreviewCamera(previewUtility);
            return previewUtility.EndStaticPreview();
        }
        finally
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            previewUtility.Cleanup();
        }
    }
    
    private Texture2D RenderMaterialPreview(Material material)
    {
        PreviewRenderUtility previewUtility = new PreviewRenderUtility();
        GameObject sphere = null;

        try
        {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());

            MeshRenderer meshRenderer = sphere.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            previewUtility.AddSingleGO(sphere);

            Bounds bounds = meshRenderer.bounds;

            cachedBonePathsAssetKey = currentPreviewAssetPath;
            cachedBonePathsForUi = null;

            Vector3 lookTarget = RenderPrefabPreviews.GetCameraLookTargetWorld(sphere, bounds, currentSettings);

            RenderPrefabPreviews.SetupPrefabPreviewCamera(
                previewUtility,
                bounds,
                lookTarget,
                currentSettings.previewSize,
                currentSettings.previewSize,
                currentSettings,
                null);
            RenderPrefabPreviews.SetupPrefabPreviewLights(previewUtility, currentSettings);

            previewUtility.BeginStaticPreview(new Rect(0, 0, currentSettings.previewSize, currentSettings.previewSize));
            RenderPrefabPreviews.RenderPreviewCamera(previewUtility);
            return previewUtility.EndStaticPreview();
        }
        finally
        {
            if (sphere != null)
            {
                UnityEngine.Object.DestroyImmediate(sphere);
            }

            previewUtility.Cleanup();
        }
    }

    private bool TryGetRenderableBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
    
    private void CleanupPreview()
    {
        if (previewTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(previewTexture);
            previewTexture = null;
        }
    }
}
}
