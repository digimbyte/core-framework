using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Framework.Editor.RenderPreview
{
[Serializable]
public class RenderPreviewSettings
{
    public string presetName = "Default";
    public int previewSize = 256;
    public Vector3 cameraDirection = new Vector3(1f, 0.65f, -1f);
    public float cameraDistance = 1.25f;
    public float fieldOfView = 30f;
    /// <summary>
    /// Degrees. Used only to compute camera stand-off from bounds. Actual <see cref="fieldOfView"/> still controls the lens (zoom).
    /// If stand-off used the same value as <see cref="fieldOfView"/>, changing FOV would move the camera to keep the same fit and cancel zoom.
    /// </summary>
    public const float CameraStandoffReferenceFieldOfView = 30f;
    /// <summary>Degrees around the view axis after aiming at the subject (0–360).</summary>
    public float cameraRoll = 0f;

    /// <summary>
    /// When true (prefab previews only), probes five narrower and five wider fields of view up to <see cref="fieldOfView"/> (max),
    /// using the neon-green border test; picks the lowest FOV whose border is still fully green, then widens slightly for margin at export resolution.
    /// Falls back to <see cref="fieldOfView"/> if none qualify.
    /// </summary>
    public bool autoFitFieldOfView = false;

    public BackgroundType backgroundType = BackgroundType.Color;
    public Color backgroundColor = new Color(0.19f, 0.19f, 0.19f);
    public float keyLightIntensity = 1.1f;
    public Vector3 keyLightRotation = new Vector3(40f, 40f, 0f);
    public float fillLightIntensity = 0.7f;
    public Vector3 fillLightRotation = new Vector3(340f, 218f, 177f);
    public Color ambientColor = Color.white * 0.55f;
    /// <summary>Added to the render bounds center so the camera aims above/below center (e.g. headshots). World units.</summary>
    public Vector3 modelOffset = Vector3.zero;

    /// <summary>
    /// When true (prefab previews only), runs up to ten low-resolution probes with a neon green background and trucks / pedestals the camera
    /// in the view plane so edge pixels stay green when possible; does not change <see cref="modelOffset"/> or the look target.
    /// </summary>
    public bool autoFrame = false;

    /// <summary>Whether framing uses mesh bounds, a hierarchy bone path, or substring patterns on transform names.</summary>
    public FocusPivot focusPivot = FocusPivot.BoundsCenter;

    /// <summary>
    /// Path from the prefab instance root to the bone transform (names separated by /).
    /// Empty string means the prefab root transform when <see cref="focusPivot"/> is <see cref="FocusPivot.Bone"/>.
    /// </summary>
    public string focusBonePath = "";

    /// <summary>
    /// When <see cref="focusPivot"/> is <see cref="FocusPivot.CustomPivot"/>, the first transform under the instance
    /// whose <c>name</c> contains this substring (case-insensitive) is the look target. If empty, primary is skipped.
    /// </summary>
    public string customPivotPrimary = "";

    /// <summary>
    /// When no transform matches <see cref="customPivotPrimary"/>, the first match for this substring is used.
    /// If empty, fallback is skipped and framing uses bounds center when nothing matched primary.
    /// </summary>
    public string customPivotFallback = "";

    /// <summary>
    /// When false (default), batch render and live preview pick only assets saved directly in each selected folder.
    /// When true, nested folders are included (Unity search is recursive under each selected folder).
    /// </summary>
    public bool includeSubfolders = false;

    public enum FocusPivot
    {
        BoundsCenter,
        Bone,
        CustomPivot
    }

    public enum BackgroundType
    {
        Color,
        Transparent
    }

    /// <summary>Call after <c>LookAt</c>: rotates the camera around its forward axis.</summary>
    public void ApplyCameraRoll(Transform cameraTransform)
    {
        if (Mathf.Approximately(cameraRoll, 0f))
        {
            return;
        }

        cameraTransform.Rotate(0f, 0f, cameraRoll, Space.Self);
    }

    public RenderPreviewSettings Clone()
    {
        return new RenderPreviewSettings
        {
            presetName = presetName,
            previewSize = previewSize,
            cameraDirection = cameraDirection,
            cameraDistance = cameraDistance,
            fieldOfView = fieldOfView,
            autoFitFieldOfView = autoFitFieldOfView,
            cameraRoll = cameraRoll,
            backgroundType = backgroundType,
            backgroundColor = backgroundColor,
            keyLightIntensity = keyLightIntensity,
            keyLightRotation = keyLightRotation,
            fillLightIntensity = fillLightIntensity,
            fillLightRotation = fillLightRotation,
            ambientColor = ambientColor,
            modelOffset = modelOffset,
            autoFrame = autoFrame,
            focusPivot = focusPivot,
            focusBonePath = focusBonePath,
            customPivotPrimary = customPivotPrimary,
            customPivotFallback = customPivotFallback,
            includeSubfolders = includeSubfolders
        };
    }

    public static List<RenderPreviewSettings> GetDefaultPresets()
    {
        var presets = new List<RenderPreviewSettings>();

        // Default - Current behavior
        presets.Add(new RenderPreviewSettings
        {
            presetName = "Default",
            previewSize = 256,
            cameraDirection = new Vector3(1f, 0.65f, -1f).normalized,
            cameraDistance = 1.25f,
            fieldOfView = 30f,
            backgroundType = BackgroundType.Color,
            backgroundColor = new Color(0.19f, 0.19f, 0.19f),
            keyLightIntensity = 1.1f,
            keyLightRotation = new Vector3(40f, 40f, 0f),
            fillLightIntensity = 0.7f,
            fillLightRotation = new Vector3(340f, 218f, 177f),
            ambientColor = Color.white * 0.55f
        });

        // Character Mugshot - Front view
        presets.Add(new RenderPreviewSettings
        {
            presetName = "Character Mugshot (Front)",
            previewSize = 512,
            cameraDirection = new Vector3(0f, 0.15f, -1f).normalized,
            cameraDistance = 1.1f,
            fieldOfView = 25f,
            backgroundType = BackgroundType.Transparent,
            backgroundColor = Color.clear,
            keyLightIntensity = 1.2f,
            keyLightRotation = new Vector3(30f, 0f, 0f),
            fillLightIntensity = 0.5f,
            fillLightRotation = new Vector3(330f, 180f, 0f),
            ambientColor = Color.white * 0.6f
        });

        // Character Profile - Side view
        presets.Add(new RenderPreviewSettings
        {
            presetName = "Character Profile (Side)",
            previewSize = 512,
            cameraDirection = new Vector3(1f, 0.15f, 0f).normalized,
            cameraDistance = 1.1f,
            fieldOfView = 25f,
            backgroundType = BackgroundType.Transparent,
            backgroundColor = Color.clear,
            keyLightIntensity = 1.2f,
            keyLightRotation = new Vector3(30f, 90f, 0f),
            fillLightIntensity = 0.5f,
            fillLightRotation = new Vector3(330f, 270f, 0f),
            ambientColor = Color.white * 0.6f
        });

        // Weapon Side Profile
        presets.Add(new RenderPreviewSettings
        {
            presetName = "Weapon Side Profile",
            previewSize = 512,
            cameraDirection = new Vector3(0f, 0.35f, -1f).normalized,
            cameraDistance = 1.15f,
            fieldOfView = 20f,
            backgroundType = BackgroundType.Transparent,
            backgroundColor = Color.clear,
            keyLightIntensity = 1.3f,
            keyLightRotation = new Vector3(35f, 45f, 0f),
            fillLightIntensity = 0.6f,
            fillLightRotation = new Vector3(325f, 225f, 0f),
            ambientColor = Color.white * 0.5f
        });

        // Top-Down View
        presets.Add(new RenderPreviewSettings
        {
            presetName = "Top-Down View",
            previewSize = 256,
            cameraDirection = new Vector3(0f, 1f, -0.1f).normalized,
            cameraDistance = 1.3f,
            fieldOfView = 35f,
            backgroundType = BackgroundType.Color,
            backgroundColor = new Color(0.19f, 0.19f, 0.19f),
            keyLightIntensity = 1.0f,
            keyLightRotation = new Vector3(45f, 45f, 0f),
            fillLightIntensity = 0.8f,
            fillLightRotation = new Vector3(315f, 225f, 0f),
            ambientColor = Color.white * 0.6f
        });

        // Isometric View
        presets.Add(new RenderPreviewSettings
        {
            presetName = "Isometric View",
            previewSize = 256,
            cameraDirection = new Vector3(1f, 1f, -1f).normalized,
            cameraDistance = 1.25f,
            fieldOfView = 30f,
            backgroundType = BackgroundType.Transparent,
            backgroundColor = Color.clear,
            keyLightIntensity = 1.1f,
            keyLightRotation = new Vector3(50f, 50f, 0f),
            fillLightIntensity = 0.65f,
            fillLightRotation = new Vector3(330f, 230f, 0f),
            ambientColor = Color.white * 0.55f
        });

        return presets;
    }
}
}
