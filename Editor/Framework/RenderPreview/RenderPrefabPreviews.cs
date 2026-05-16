using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Framework.Editor.RenderPreview
{
public static class RenderPrefabPreviews
{
    private const string OutputFolderName = "RenderedPreviews";
    /// <summary>Top-level Unity content folder; rendering from here would scan the whole project.</summary>
    private const string DisallowedPreviewRootFolder = "Assets";
    
    // Default settings (legacy, used when not specified)
    private const int PreviewSize = 256;
    private static readonly Vector3 CameraDirection = new Vector3(1f, 0.65f, -1f).normalized;
    /// <summary>Material sphere thumbnails: camera offset so the key light reads from upper-left.</summary>
    private static readonly Vector3 MaterialSphereCameraDirection = new Vector3(0.85f, 0.35f, -0.95f).normalized;
    private static readonly Color PreviewBackground = new Color(0.19f, 0.19f, 0.19f);

    private const int AutoFrameProbeResolution = 64;
    private const int AutoFrameMaxProbeRenders = 10;
    private const float AutoFrameMinAcceptBorderGreenRatio = 0.06f;
    /// <summary>
    /// Added after auto-fit picks the tightest passing FOV so final renders keep a margin (probes are 64²; hairline passes clip at full size).
    /// </summary>
    private const float AutoFitFieldOfViewSlackDegrees = 2.5f;
    private static readonly Color AutoFrameProbeClearColor = new Color(0f, 1f, 0f, 1f);

    private static RenderPreviewSettings currentSettings;

    /// <summary>
    /// When <paramref name="includeSubfolders"/> is false, only assets whose parent folder equals <paramref name="folderPath"/> match.
    /// When true, any asset under that folder path matches (recursive tree).
    /// </summary>
    public static bool AssetMatchesFolderScope(string assetPath, string folderPath, bool includeSubfolders)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folderPath))
        {
            return false;
        }

        string normalizedFolder = folderPath.TrimEnd('/').Replace("\\", "/");
        string normalizedAsset = assetPath.Replace("\\", "/");

        if (includeSubfolders)
        {
            return normalizedAsset.StartsWith(normalizedFolder + "/", System.StringComparison.OrdinalIgnoreCase);
        }

        string parentDir = Path.GetDirectoryName(normalizedAsset)?.Replace("\\", "/");
        return parentDir != null &&
               string.Equals(parentDir, normalizedFolder, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Uses <see cref="PreviewRenderUtility.Render(bool,bool)"/> so ambient color and lighting overrides apply.
    /// Calling <c>camera.Render()</c> directly skips that setup.
    /// </summary>
    public static void RenderPreviewCamera(PreviewRenderUtility previewUtility)
    {
        bool useScriptableRenderPipeline = GraphicsSettings.defaultRenderPipeline != null;
        previewUtility.Render(useScriptableRenderPipeline);
    }

    /// <summary>
    /// World position the camera looks at before roll. Includes <see cref="RenderPreviewSettings.modelOffset"/>.
    /// </summary>
    public static Vector3 GetCameraLookTargetWorld(GameObject instance, Bounds bounds, RenderPreviewSettings settings)
    {
        if (settings == null)
        {
            return bounds.center;
        }

        Vector3 basePoint = bounds.center;
        if (instance != null)
        {
            if (settings.focusPivot == RenderPreviewSettings.FocusPivot.Bone)
            {
                Transform bone = ResolveBoneAlongPath(instance.transform, settings.focusBonePath);
                if (bone != null)
                {
                    basePoint = bone.position;
                }
            }
            else if (settings.focusPivot == RenderPreviewSettings.FocusPivot.CustomPivot)
            {
                Transform match = ResolveTransformByNamePatterns(
                    instance.transform,
                    settings.customPivotPrimary,
                    settings.customPivotFallback);
                if (match != null)
                {
                    basePoint = match.position;
                }
            }
        }

        return basePoint + settings.modelOffset;
    }

    /// <summary>
    /// Bone paths under the instance root (empty string = root transform), sorted for UI. Uses skinned meshes and humanoid mapping when present.
    /// </summary>
    public static List<string> CollectSortedBonePaths(GameObject instance)
    {
        var set = new HashSet<string>(System.StringComparer.Ordinal);
        if (instance == null)
        {
            return new List<string> { string.Empty };
        }

        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || !animator.isHuman)
            {
                continue;
            }

            foreach (HumanBodyBones humanBone in (HumanBodyBones[])System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (humanBone == HumanBodyBones.LastBone)
                {
                    continue;
                }

                Transform boneTransform = animator.GetBoneTransform(humanBone);
                if (boneTransform == null)
                {
                    continue;
                }

                string path = BuildPathFromRoot(instance.transform, boneTransform);
                if (path != null)
                {
                    set.Add(path);
                }
            }
        }

        foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin == null || skin.bones == null)
            {
                continue;
            }

            foreach (Transform boneTransform in skin.bones)
            {
                if (boneTransform == null)
                {
                    continue;
                }

                string path = BuildPathFromRoot(instance.transform, boneTransform);
                if (path != null)
                {
                    set.Add(path);
                }
            }
        }

        var sortedNonRoot = new List<string>();
        foreach (string entry in set)
        {
            if (!string.IsNullOrEmpty(entry))
            {
                sortedNonRoot.Add(entry);
            }
        }

        sortedNonRoot.Sort(System.StringComparer.Ordinal);

        var result = new List<string> { string.Empty };
        result.AddRange(sortedNonRoot);
        return result;
    }

    private static string BuildPathFromRoot(Transform root, Transform node)
    {
        if (node == null || root == null)
        {
            return null;
        }

        if (node == root)
        {
            return string.Empty;
        }

        var segments = new List<string>();
        Transform walk = node;
        while (walk != null && walk != root)
        {
            segments.Add(walk.name);
            walk = walk.parent;
        }

        if (walk != root)
        {
            return null;
        }

        segments.Reverse();
        return string.Join("/", segments.ToArray());
    }

    private static Transform ResolveBoneAlongPath(Transform root, string path)
    {
        if (root == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(path))
        {
            return root;
        }

        Transform current = root;
        foreach (string segment in path.Split('/'))
        {
            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }

            Transform next = null;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child.name == segment)
                {
                    next = child;
                    break;
                }
            }

            if (next == null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// First transform under <paramref name="root"/> (including root) whose name contains the pattern (ordinal, case-insensitive).
    /// Tries <paramref name="primaryContains"/> first in hierarchy order; if none, tries <paramref name="fallbackContains"/>.
    /// Empty patterns are ignored for that pass.
    /// </summary>
    private static Transform ResolveTransformByNamePatterns(Transform root, string primaryContains, string fallbackContains)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
        Transform hit = FindFirstTransformWhoseNameContains(nodes, primaryContains);
        if (hit != null)
        {
            return hit;
        }

        return FindFirstTransformWhoseNameContains(nodes, fallbackContains);
    }

    private static Transform FindFirstTransformWhoseNameContains(Transform[] nodes, string containsFragment)
    {
        if (nodes == null || string.IsNullOrEmpty(containsFragment))
        {
            return null;
        }

        StringComparison ord = StringComparison.OrdinalIgnoreCase;
        foreach (Transform t in nodes)
        {
            if (t == null)
            {
                continue;
            }

            if (t.name.IndexOf(containsFragment, ord) >= 0)
            {
                return t;
            }
        }

        return null;
    }

    [MenuItem("Assets/Tools/Render Previews", false, 1200)]
    private static void ShowRenderConfigWindow()
    {
        string[] folders = GetSelectedFolders();
        if (folders.Length == 0)
        {
            Debug.LogWarning(
                "Select or right-click a folder in the Project window to render previews. The Assets root folder is not allowed.");
            return;
        }

        RenderPreviewConfigWindow.ShowWindow(RenderSelectedFoldersWithSettings, folders);
    }

    private static void RenderSelectedFoldersWithSettings(RenderPreviewSettings settings)
    {
        currentSettings = settings;
        
        string[] folders = GetSelectedFolders();
        if (folders.Length == 0)
        {
            Debug.LogWarning(
                "Select or right-click a folder in the Project window to render previews. The Assets root folder is not allowed.");
            return;
        }

        int totalPrefabs = 0;
        int totalMaterials = 0;
        foreach (string folder in folders)
        {
            totalPrefabs += RenderFolderPrefabs(folder);
            totalMaterials += RenderFolderMaterials(folder);
        }

        AssetDatabase.Refresh();
        Debug.Log(
            $"Rendered {totalPrefabs} prefab previews and {totalMaterials} material (sphere) previews at {settings.previewSize}x{settings.previewSize} using '{settings.presetName}' preset.");
        
        currentSettings = null; // Reset after rendering
    }

    /// <summary>
    /// Resolves folder targets from the Project window without scanning folder contents.
    /// Merges <see cref="Selection.assetGUIDs"/>, <see cref="Selection.objects"/>, and
    /// <see cref="FolderBrowserWindow.TryGetProjectWindowFolderForMenus"/> so hierarchy clicks / context menus
    /// match what Unity shows as the active folder (same basis as Assets/Open in new View).
    /// </summary>
    private static string[] GetSelectedFolders()
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ConsiderFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            path = path.Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            if (path.Equals(DisallowedPreviewRootFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            folders.Add(path);
        }

        foreach (string guid in Selection.assetGUIDs)
        {
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            ConsiderFolder(AssetDatabase.GUIDToAssetPath(guid));
        }

        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (selected == null)
            {
                continue;
            }

            ConsiderFolder(AssetDatabase.GetAssetPath(selected));
        }

        if (FolderBrowserWindow.TryGetProjectWindowFolderForMenus(out string contextFolder))
        {
            ConsiderFolder(contextFolder);
        }

        return folders.ToArray();
    }

    private static int RenderFolderPrefabs(string folderPath)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        int renderedCount = 0;
        
        int previewSize = currentSettings != null ? currentSettings.previewSize : PreviewSize;

        bool recursive = currentSettings != null && currentSettings.includeSubfolders;

        foreach (string guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetMatchesFolderScope(prefabPath, folderPath, recursive))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                continue;
            }

            Texture2D preview = RenderPrefabTexture(prefab, previewSize, previewSize);
            if (preview == null)
            {
                Debug.LogWarning($"Skipped preview for '{prefabPath}' (no renderable bounds, or prefab could not be instantiated).");
                continue;
            }

            string outputPath = GetOutputAssetPath(prefabPath);
            EnsureAssetFolderExists(Path.GetDirectoryName(outputPath)?.Replace("\\", "/"));

            WritePreviewPngAndImport(outputPath, preview.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(preview);

            renderedCount++;
        }

        Debug.Log($"Rendered {renderedCount} prefab previews for folder '{folderPath}'.");
        return renderedCount;
    }

    private static int RenderFolderMaterials(string folderPath)
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        int renderedCount = 0;
        
        int previewSize = currentSettings != null ? currentSettings.previewSize : PreviewSize;

        bool recursive = currentSettings != null && currentSettings.includeSubfolders;

        foreach (string guid in materialGuids)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetMatchesFolderScope(materialPath, folderPath, recursive))
            {
                continue;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || material.shader == null)
            {
                continue;
            }

            Texture2D preview = RenderMaterialOnSphereTexture(material, previewSize, previewSize);
            if (preview == null)
            {
                Debug.LogWarning($"Skipped material preview for '{materialPath}'.");
                continue;
            }

            string outputPath = GetOutputAssetPath(materialPath);
            EnsureAssetFolderExists(Path.GetDirectoryName(outputPath)?.Replace("\\", "/"));

            WritePreviewPngAndImport(outputPath, preview.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(preview);

            renderedCount++;
        }

        if (renderedCount > 0)
        {
            Debug.Log($"Rendered {renderedCount} material previews for folder '{folderPath}'.");
        }

        return renderedCount;
    }

    /// <summary>
    /// Writes next to the source asset: <c>{parent}/RenderedPreviews/{name}_preview.png</c>.
    /// Recursive runs keep each preview under the same folder as its prefab or material (matches item browser and avoids a single flat tree under the selection root).
    /// </summary>
    private static string GetOutputAssetPath(string sourceAssetPath)
    {
        string assetDirectory = Path.GetDirectoryName(sourceAssetPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(assetDirectory))
        {
            assetDirectory = "Assets";
        }

        string fileName = $"{Path.GetFileNameWithoutExtension(sourceAssetPath)}_preview.png";
        return $"{assetDirectory}/{OutputFolderName}/{fileName}";
    }

    /// <summary>Always overwrites and forces a reimport so re-running the tool refreshes existing previews.</summary>
    private static void WritePreviewPngAndImport(string assetPath, byte[] pngBytes)
    {
        string normalized = assetPath.Replace("\\", "/");
        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), normalized);
        File.WriteAllBytes(absolutePath, pngBytes);
        AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureAssetFolderExists(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureAssetFolderExists(parent);
        }

        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    /// <summary>
    /// Use for preview renders when <see cref="PrefabUtility.InstantiatePrefab"/> may throw on broken or non-standard prefab assets.
    /// </summary>
    public static GameObject TryInstantiatePrefabForPreview(GameObject prefabAsset)
    {
        if (prefabAsset == null)
        {
            return null;
        }

        try
        {
            return PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private static Texture2D RenderPrefabTexture(GameObject prefab, int width, int height)
    {
        PreviewRenderUtility previewUtility = new PreviewRenderUtility();
        GameObject instance = null;

        try
        {
            instance = TryInstantiatePrefabForPreview(prefab);
            if (instance == null)
            {
                return null;
            }

            previewUtility.AddSingleGO(instance);

            if (!TryGetRenderableBounds(instance, out Bounds bounds))
            {
                return null;
            }

            Vector3 lookTarget = currentSettings != null
                ? GetCameraLookTargetWorld(instance, bounds, currentSettings)
                : bounds.center;

            Vector3 cameraPanWorld = Vector3.zero;
            float? fieldOfViewOverride = null;
            if (currentSettings != null)
            {
                bool wantFrame = currentSettings.autoFrame;
                bool wantFit = currentSettings.autoFitFieldOfView;
                if (wantFrame && wantFit)
                {
                    // Pan is probed at max FOV, then FOV tightens — that zoom invalidates pan. Re-pan at the chosen FOV and refit once.
                    cameraPanWorld = ComputeAutoFrameCameraPanWorld(
                        previewUtility,
                        instance,
                        bounds,
                        lookTarget,
                        currentSettings,
                        null);
                    fieldOfViewOverride = ComputeTightestFieldOfViewForGreenBorder(
                        previewUtility,
                        bounds,
                        lookTarget,
                        cameraPanWorld,
                        currentSettings);
                    cameraPanWorld = ComputeAutoFrameCameraPanWorld(
                        previewUtility,
                        instance,
                        bounds,
                        lookTarget,
                        currentSettings,
                        fieldOfViewOverride);
                    fieldOfViewOverride = ComputeTightestFieldOfViewForGreenBorder(
                        previewUtility,
                        bounds,
                        lookTarget,
                        cameraPanWorld,
                        currentSettings);
                }
                else if (wantFrame)
                {
                    cameraPanWorld = ComputeAutoFrameCameraPanWorld(
                        previewUtility,
                        instance,
                        bounds,
                        lookTarget,
                        currentSettings,
                        null);
                }
                else if (wantFit)
                {
                    fieldOfViewOverride = ComputeTightestFieldOfViewForGreenBorder(
                        previewUtility,
                        bounds,
                        lookTarget,
                        Vector3.zero,
                        currentSettings);
                }
            }

            SetupPrefabPreviewCamera(previewUtility, bounds, lookTarget, width, height, currentSettings, null, cameraPanWorld, fieldOfViewOverride);
            SetupPrefabPreviewLights(previewUtility, currentSettings);

            previewUtility.BeginStaticPreview(new Rect(0, 0, width, height));
            RenderPreviewCamera(previewUtility);
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

    private static Texture2D RenderMaterialOnSphereTexture(Material material, int width, int height)
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

            SetupMaterialPreviewCamera(previewUtility, sphere, bounds, width, height);
            SetupMaterialPreviewLights(previewUtility);

            previewUtility.BeginStaticPreview(new Rect(0, 0, width, height));
            RenderPreviewCamera(previewUtility);
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

    private static void SetupMaterialPreviewCamera(PreviewRenderUtility previewUtility, GameObject sceneInstance, Bounds bounds, int width, int height)
    {
        Camera camera = previewUtility.camera;
        camera.clearFlags = CameraClearFlags.Color;
        
        // Handle custom settings if available
        if (currentSettings != null)
        {
            if (currentSettings.backgroundType == RenderPreviewSettings.BackgroundType.Transparent)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
            }
            else
            {
                camera.backgroundColor = currentSettings.backgroundColor;
            }
            
            float aspect = width / (float)height;
            camera.aspect = aspect;
            camera.fieldOfView = currentSettings.fieldOfView;

            // Sphere radius (primitive sphere: half-extents are equal). Avoid extents.magnitude (box diagonal) — it adds large padding.
            float radius = Mathf.Max(0.01f, Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));

            float standoffHalfVerticalRad = RenderPreviewSettings.CameraStandoffReferenceFieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalfVertical = Mathf.Tan(standoffHalfVerticalRad);
            // Unity uses vertical FOV; fit circle to the tighter of vertical vs horizontal frustum at the target plane.
            float tanHalfMin = tanHalfVertical * Mathf.Min(1f, aspect);
            float distance = radius / tanHalfMin;

            // Apply custom distance multiplier
            distance *= currentSettings.cameraDistance;

            Vector3 target = GetCameraLookTargetWorld(sceneInstance, bounds, currentSettings);
            Vector3 cameraDir = currentSettings.cameraDirection.normalized;
            camera.transform.position = target + cameraDir * distance;
            camera.transform.LookAt(target);
            currentSettings.ApplyCameraRoll(camera.transform);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2.5f);
            camera.farClipPlane = distance + radius * 4f;
        }
        else
        {
            // Use default material sphere settings
            camera.backgroundColor = PreviewBackground;
            float aspect = width / (float)height;
            camera.aspect = aspect;
            camera.fieldOfView = 30f;

            // Sphere radius (primitive sphere: half-extents are equal). Avoid extents.magnitude (box diagonal) — it adds large padding.
            float radius = Mathf.Max(0.01f, Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));

            float halfVerticalRad = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalfVertical = Mathf.Tan(halfVerticalRad);
            // Unity uses vertical FOV; fit circle to the tighter of vertical vs horizontal frustum at the target plane.
            float tanHalfMin = tanHalfVertical * Mathf.Min(1f, aspect);
            float distance = radius / tanHalfMin;

            // >1 pulls camera back slightly so the sphere silhouette is not clipped (oblique view tightens the fit).
            const float materialPreviewDistanceFactor = 1.15f;
            distance *= materialPreviewDistanceFactor;

            Vector3 target = bounds.center;
            camera.transform.position = target + MaterialSphereCameraDirection * distance;
            camera.transform.LookAt(target);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2.5f);
            camera.farClipPlane = distance + radius * 4f;
        }
    }

    private static void SetupMaterialPreviewLights(PreviewRenderUtility previewUtility)
    {
        Light key = previewUtility.lights[0];
        Light fill = previewUtility.lights[1];

        if (currentSettings != null)
        {
            key.intensity = currentSettings.keyLightIntensity;
            key.transform.rotation = Quaternion.Euler(currentSettings.keyLightRotation);

            fill.intensity = currentSettings.fillLightIntensity;
            fill.transform.rotation = Quaternion.Euler(currentSettings.fillLightRotation);

            previewUtility.ambientColor = currentSettings.ambientColor;
        }
        else
        {
            // Default material preview lighting
            key.intensity = 1.15f;
            key.transform.rotation = Quaternion.Euler(50f, 35f, 0f);

            fill.intensity = 0.45f;
            fill.transform.rotation = Quaternion.Euler(200f, 145f, 0f);

            previewUtility.ambientColor = new Color(0.32f, 0.32f, 0.32f);
        }
    }

    /// <summary>
    /// Positions the prefab preview camera using <paramref name="settings"/>.
    /// <paramref name="cameraPanWorldOffset"/> trucks / pedestals the camera in the view plane (after aim); it does not move the model — use <see cref="RenderPreviewSettings.modelOffset"/> on the look target for that.
    /// Optional <paramref name="backgroundOverride"/> forces a solid color (Auto Frame probes).
    /// <paramref name="fieldOfViewOverride"/> sets lens vertical FOV for this render only; when null, uses <see cref="RenderPreviewSettings.fieldOfView"/> (the slider value is still the maximum when auto-fit runs).
    /// </summary>
    public static void SetupPrefabPreviewCamera(
        PreviewRenderUtility previewUtility,
        Bounds bounds,
        Vector3 lookTargetWorld,
        int width,
        int height,
        RenderPreviewSettings settings,
        Color? backgroundOverride,
        Vector3 cameraPanWorldOffset = default,
        float? fieldOfViewOverride = null)
    {
        Camera camera = previewUtility.camera;
        camera.clearFlags = CameraClearFlags.Color;

        if (settings != null)
        {
            if (backgroundOverride.HasValue)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = backgroundOverride.Value;
            }
            else if (settings.backgroundType == RenderPreviewSettings.BackgroundType.Transparent)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
            }
            else
            {
                camera.backgroundColor = settings.backgroundColor;
            }

            camera.aspect = width / (float)height;
            float lensVerticalFov = settings.fieldOfView;
            if (fieldOfViewOverride.HasValue)
            {
                lensVerticalFov = Mathf.Clamp(fieldOfViewOverride.Value, 0.5f, settings.fieldOfView);
            }

            camera.fieldOfView = lensVerticalFov;

            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float standoffHalfRad = RenderPreviewSettings.CameraStandoffReferenceFieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = radius / Mathf.Sin(standoffHalfRad);
            distance *= settings.cameraDistance;

            Vector3 target = lookTargetWorld;
            Vector3 cameraDir = settings.cameraDirection.normalized;
            Vector3 nominalCameraPosition = target + cameraDir * distance;
            camera.transform.position = nominalCameraPosition + cameraPanWorldOffset;
            camera.transform.LookAt(target);
            settings.ApplyCameraRoll(camera.transform);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 4f;
        }
        else
        {
            camera.backgroundColor = PreviewBackground;
            camera.aspect = width / (float)height;
            camera.fieldOfView = 30f;

            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            distance *= 1.25f;

            Vector3 target = bounds.center;
            camera.transform.position = target + CameraDirection * distance;
            camera.transform.LookAt(target);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 4f;
        }
    }

    /// <summary>Key/fill/ambient for prefab preview renders.</summary>
    public static void SetupPrefabPreviewLights(PreviewRenderUtility previewUtility, RenderPreviewSettings settings)
    {
        Light key = previewUtility.lights[0];
        Light fill = previewUtility.lights[1];

        if (settings != null)
        {
            key.intensity = settings.keyLightIntensity;
            key.transform.rotation = Quaternion.Euler(settings.keyLightRotation);

            fill.intensity = settings.fillLightIntensity;
            fill.transform.rotation = Quaternion.Euler(settings.fillLightRotation);

            previewUtility.ambientColor = settings.ambientColor;
        }
        else
        {
            key.intensity = 1.1f;
            key.transform.rotation = Quaternion.Euler(40f, 40f, 0f);

            fill.intensity = 0.7f;
            fill.transform.rotation = Quaternion.Euler(340f, 218f, 177f);

            previewUtility.ambientColor = Color.white * 0.55f;
        }
    }

    /// <summary>
    /// Ten neon-green border probes: five fields of view stepping inward from <see cref="RenderPreviewSettings.fieldOfView"/> (max) and five stepping outward from a minimum;
    /// picks the smallest FOV whose border is entirely green, adds a small slack toward wider FOV for full-size output, or returns the max FOV if none pass.
    /// </summary>
    public static float ComputeTightestFieldOfViewForGreenBorder(
        PreviewRenderUtility previewUtility,
        Bounds bounds,
        Vector3 lookTargetWorld,
        Vector3 cameraPanWorld,
        RenderPreviewSettings settings)
    {
        if (settings == null || !settings.autoFitFieldOfView || previewUtility == null)
        {
            return settings != null ? settings.fieldOfView : 30f;
        }

        float fMax = settings.fieldOfView;
        const float absoluteMinFov = 5f;
        float fMin = Mathf.Clamp(fMax * 0.2f, absoluteMinFov, fMax - 0.1f);
        if (fMin >= fMax - 0.01f)
        {
            return fMax;
        }

        float span = fMax - fMin;
        var candidates = new List<float>(12);
        for (int i = 1; i <= 5; i++)
        {
            candidates.Add(Mathf.Clamp(fMax - i * span / 6f, fMin, fMax));
        }

        for (int j = 1; j <= 5; j++)
        {
            candidates.Add(Mathf.Clamp(fMin + j * span / 6f, fMin, fMax));
        }

        candidates.Sort();

        var unique = new List<float>(candidates.Count);
        var seenKeys = new HashSet<int>();
        foreach (float v in candidates)
        {
            int key = Mathf.RoundToInt(v * 1000f);
            if (seenKeys.Add(key))
            {
                unique.Add(v);
            }
        }

        float best = fMax;
        bool anyAllGreen = false;

        foreach (float fov in unique)
        {
            SetupPrefabPreviewCamera(
                previewUtility,
                bounds,
                lookTargetWorld,
                AutoFrameProbeResolution,
                AutoFrameProbeResolution,
                settings,
                AutoFrameProbeClearColor,
                cameraPanWorld,
                fov);
            SetupPrefabPreviewLights(previewUtility, settings);
            previewUtility.BeginStaticPreview(new Rect(0, 0, AutoFrameProbeResolution, AutoFrameProbeResolution));
            RenderPreviewCamera(previewUtility);
            Texture2D tex = previewUtility.EndStaticPreview();
            try
            {
                Color32[] px = tex.GetPixels32();
                ScoreAutoFrameBorder(px, tex.width, tex.height, out _, out _, out bool allGreen);
                if (allGreen)
                {
                    anyAllGreen = true;
                    best = Mathf.Min(best, fov);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        if (!anyAllGreen)
        {
            return fMax;
        }

        // Widen slightly so full-resolution renders are not edge-lit after 64² probes.
        return Mathf.Min(best + AutoFitFieldOfViewSlackDegrees, fMax);
    }

    /// <summary>
    /// Trucks / pedestals the camera in the view plane (same basis as post-<c>LookAt</c> camera right / up) using green-border probes (max <see cref="AutoFrameMaxProbeRenders"/> renders).
    /// Does not change <paramref name="lookTargetWorld"/> (model position stays with focus pivot + <see cref="RenderPreviewSettings.modelOffset"/>).
    /// Returns a world-space offset to add to the nominal camera position; <see cref="Vector3.zero"/> when disabled, below threshold, or already fully framed at zero pan.
    /// </summary>
    /// <param name="probeVerticalFieldOfView">
    /// When set, low-resolution border probes use this vertical FOV (clamped to settings). When null, probes use <see cref="RenderPreviewSettings.fieldOfView"/> (max zoom-out for the preset).
    /// </param>
    public static Vector3 ComputeAutoFrameCameraPanWorld(
        PreviewRenderUtility previewUtility,
        GameObject instance,
        Bounds bounds,
        Vector3 lookTargetWorld,
        RenderPreviewSettings settings,
        float? probeVerticalFieldOfView = null)
    {
        if (settings == null || !settings.autoFrame || previewUtility == null || instance == null)
        {
            return Vector3.zero;
        }

        GetAutoFramePanBasis(settings.cameraDirection, out Vector3 panRight, out Vector3 panUp);
        float extent = Mathf.Max(0.01f, bounds.extents.magnitude);
        float step = Mathf.Clamp(extent * 0.14f, 0.03f, 2.5f);

        int rendersUsed = 0;

        AutoFrameEvalResult Eval(Vector2 plane)
        {
            rendersUsed++;
            Vector3 cameraPanWorldOffset = panRight * plane.x + panUp * plane.y;
            SetupPrefabPreviewCamera(
                previewUtility,
                bounds,
                lookTargetWorld,
                AutoFrameProbeResolution,
                AutoFrameProbeResolution,
                settings,
                AutoFrameProbeClearColor,
                cameraPanWorldOffset,
                probeVerticalFieldOfView);
            SetupPrefabPreviewLights(previewUtility, settings);
            previewUtility.BeginStaticPreview(new Rect(0, 0, AutoFrameProbeResolution, AutoFrameProbeResolution));
            RenderPreviewCamera(previewUtility);
            Texture2D tex = previewUtility.EndStaticPreview();
            try
            {
                Color32[] px = tex.GetPixels32();
                ScoreAutoFrameBorder(px, tex.width, tex.height, out int green, out int edge, out bool allGreen);
                return new AutoFrameEvalResult(green, edge, allGreen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        AutoFrameEvalResult center = Eval(Vector2.zero);
        if (center.AllGreen)
        {
            return Vector3.zero;
        }

        Vector2 bestPlane = Vector2.zero;
        float bestRatio = center.Ratio;

        Vector2[] neighbors =
        {
            new Vector2(step, 0f),
            new Vector2(-step, 0f),
            new Vector2(0f, step),
            new Vector2(0f, -step),
            new Vector2(step, step),
            new Vector2(step, -step),
            new Vector2(-step, step),
            new Vector2(-step, -step),
        };

        for (int i = 0; i < neighbors.Length && rendersUsed < AutoFrameMaxProbeRenders; i++)
        {
            AutoFrameEvalResult r = Eval(neighbors[i]);
            if (r.AllGreen)
            {
                return panRight * neighbors[i].x + panUp * neighbors[i].y;
            }

            if (r.Ratio > bestRatio + 1e-5f)
            {
                bestRatio = r.Ratio;
                bestPlane = neighbors[i];
            }
        }

        if (rendersUsed < AutoFrameMaxProbeRenders && bestPlane.sqrMagnitude > 1e-8f)
        {
            Vector2 refined = bestPlane * 1.2f;
            AutoFrameEvalResult rRef = Eval(refined);
            if (rRef.AllGreen)
            {
                return panRight * refined.x + panUp * refined.y;
            }

            if (rRef.Ratio > bestRatio + 1e-5f)
            {
                bestRatio = rRef.Ratio;
                bestPlane = refined;
            }
        }

        if (bestRatio < AutoFrameMinAcceptBorderGreenRatio)
        {
            return Vector3.zero;
        }

        return panRight * bestPlane.x + panUp * bestPlane.y;
    }

    private static void GetAutoFramePanBasis(Vector3 cameraDirectionFromSettings, out Vector3 right, out Vector3 up)
    {
        Vector3 toCamera = cameraDirectionFromSettings.normalized;
        right = Vector3.Cross(Vector3.up, toCamera);
        if (right.sqrMagnitude < 1e-10f)
        {
            right = Vector3.Cross(Vector3.forward, toCamera);
        }

        right.Normalize();
        up = Vector3.Cross(toCamera, right).normalized;
    }

    private static bool AutoFrameBorderPixelIsGreen(Color32 c)
    {
        return c.g >= 235 && c.r <= 25 && c.b <= 25;
    }

    private static void ScoreAutoFrameBorder(Color32[] pixels, int w, int h, out int greenCount, out int edgeCount, out bool allGreen)
    {
        greenCount = 0;
        edgeCount = 0;
        for (int x = 0; x < w; x++)
        {
            Accumulate(pixels, w, h, x, 0, ref greenCount, ref edgeCount);
            Accumulate(pixels, w, h, x, h - 1, ref greenCount, ref edgeCount);
        }

        for (int y = 1; y < h - 1; y++)
        {
            Accumulate(pixels, w, h, 0, y, ref greenCount, ref edgeCount);
            Accumulate(pixels, w, h, w - 1, y, ref greenCount, ref edgeCount);
        }

        allGreen = edgeCount > 0 && greenCount == edgeCount;
    }

    private static void Accumulate(Color32[] pixels, int w, int h, int x, int y, ref int greenCount, ref int edgeCount)
    {
        edgeCount++;
        if (AutoFrameBorderPixelIsGreen(pixels[y * w + x]))
        {
            greenCount++;
        }
    }

    private readonly struct AutoFrameEvalResult
    {
        public AutoFrameEvalResult(int greenCount, int edgeCount, bool allGreen)
        {
            GreenCount = greenCount;
            EdgeCount = edgeCount;
            AllGreen = allGreen;
        }

        public int GreenCount { get; }
        public int EdgeCount { get; }
        public bool AllGreen { get; }

        public float Ratio => EdgeCount > 0 ? GreenCount / (float)EdgeCount : 0f;
    }

    private static bool TryGetRenderableBounds(GameObject root, out Bounds bounds)
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
}
}
