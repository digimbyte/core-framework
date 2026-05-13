// Copyright (c) Supernova Technologies LLC
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Nova
{
    /// <summary>
    /// Helper that auto-adjusts a `UIBlock2D`'s image tiling/scale when the
    /// `ImageAdjustment.ScaleMode` is set to `Manual`.
    ///
    /// Place on the same GameObject as a `UIBlock2D` (or assign one manually).
    /// Runs in edit mode and at runtime.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Nova/Utilities/UIBlock2D Texture Helper")]
    [DisallowMultipleComponent]
    public class UIBlock2DTextureHelper : MonoBehaviour
    {
        public enum AdjustmentMode
        {
            /// <summary>
            /// Compute tiling based on the block's on-screen pixel size and the
            /// source texture's pixel dimensions (best for screen-space UI).
            /// </summary>
            AutoBasedOnScreenPixels,

            /// <summary>
            /// Compute tiling from a target pixels-per-unit value.
            /// </summary>
            UseTargetPixelsPerUnit,

            /// <summary>
            /// Do not change anything automatically.
            /// </summary>
            Disabled
        }

        public enum AxisReference
        {
            None,
            X,
            Y
        }

        [SerializeField]
        private UIBlock2D uiBlock;

        [SerializeField]
        private AdjustmentMode mode = AdjustmentMode.AutoBasedOnScreenPixels;

        [SerializeField]
        private AxisReference axisReference = AxisReference.None;

        [SerializeField]
        private float targetPixelsPerUnit = 1f;

        [SerializeField]
        private bool preserveAspect = true;

        [SerializeField]
        private bool onlyWhenManual = true;

        [SerializeField]
        private bool applyInEditMode = true;

        private void OnEnable()
        {
            if (uiBlock == null)
            {
                uiBlock = GetComponent<UIBlock2D>();
            }

            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            ApplyIfNeeded();
#if UNITY_EDITOR
            SubscribeEditorUpdate();
#endif
        }

        private void OnValidate()
        {
            if (applyInEditMode)
            {
                ApplyIfNeeded();
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                ApplyIfNeeded();
            }
        }

        [ContextMenu("Apply Texture Adjustment")]
        public void ApplyNow() => ApplyIfNeeded();

        /// <summary>
        /// Apply the automatic adjustment (if configured).
        /// </summary>
        public void ApplyIfNeeded()
        {
            if (uiBlock == null)
            {
                uiBlock = GetComponent<UIBlock2D>();
                if (uiBlock == null)
                {
                    return;
                }
            }

            // Get a reference to the underlying adjustment struct
            ref ImageAdjustment adj = ref uiBlock.ImageAdjustment;

            if (onlyWhenManual && adj.ScaleMode != ImageScaleMode.Manual)
            {
                return;
            }

            Texture tex = uiBlock.Texture as Texture;
            Sprite sprite = uiBlock.Sprite;

            int texW = 0;
            int texH = 0;
            if (sprite != null)
            {
                texW = (int)sprite.rect.width;
                texH = (int)sprite.rect.height;
            }
            else if (tex != null)
            {
                texW = tex.width;
                texH = tex.height;
            }
            else
            {
                return;
            }

            if (texW <= 0 || texH <= 0)
            {
                return;
            }

            Vector2 blockPixelSize = GetBlockPixelSize(uiBlock);
            if (blockPixelSize.x <= 0f || blockPixelSize.y <= 0f)
            {
                return;
            }

            float tileCountX;
            float tileCountY;

            // Note: only record Undo when a change is about to be made (moved further down)

            if (mode == AdjustmentMode.UseTargetPixelsPerUnit)
            {
                // Interpret targetPixelsPerUnit as how many texture pixels should map
                // to one local UI unit. Compute how many tiles that produces.
                Vector3 blockUnits = uiBlock.CalculatedSize.Value;
                tileCountX = (blockUnits.x * targetPixelsPerUnit) / (float)texW;
                tileCountY = (blockUnits.y * targetPixelsPerUnit) / (float)texH;

                // Also store the multiplier so sliced/tiled modes can use it
                adj.PixelsPerUnitMultiplier = targetPixelsPerUnit;
            }
            else // AutoBasedOnScreenPixels
            {
                tileCountX = blockPixelSize.x / (float)texW;
                tileCountY = blockPixelSize.y / (float)texH;
            }

            // Avoid degenerate values
            tileCountX = Mathf.Max(1e-4f, tileCountX);
            tileCountY = Mathf.Max(1e-4f, tileCountY);

            if (preserveAspect)
            {
                float avg = 0.5f * (tileCountX + tileCountY);
                tileCountX = tileCountY = avg;
            }

            // Axis reference handling: normalize so the chosen axis becomes 1
            switch (axisReference)
            {
                case AxisReference.X:
                {
                    float refVal = tileCountX;
                    refVal = Mathf.Max(1e-6f, refVal);
                    tileCountY = tileCountY / refVal;
                    tileCountX = 1f;
                    break;
                }
                case AxisReference.Y:
                {
                    float refVal = tileCountY;
                    refVal = Mathf.Max(1e-6f, refVal);
                    tileCountX = tileCountX / refVal;
                    tileCountY = 1f;
                    break;
                }
                default:
                    break;
            }

            Vector2 desiredUVScale = new Vector2(1f / tileCountX, 1f / tileCountY);

            // Determine if any change is necessary (ScaleMode or UVScale change)
            bool needsScaleModeChange = adj.ScaleMode != ImageScaleMode.Manual;
            bool needsUVChange = !Approximately(adj.UVScale, desiredUVScale);

            if (needsScaleModeChange || needsUVChange)
            {
#if UNITY_EDITOR
                Undo.RecordObject(uiBlock, "Adjust UIBlock2D Image Adjustment");
#endif
                if (needsScaleModeChange)
                {
                    adj.ScaleMode = ImageScaleMode.Manual;
                }

                if (needsUVChange)
                {
                    adj.UVScale = desiredUVScale;
                }

                // Mark dirty so Nova picks up the change in editor and at runtime
                try
                {
                    uiBlock.EditorOnly_MarkDirty();
                }
                catch (Exception)
                {
                    // EditorOnly_MarkDirty is internal - if this helper ends up in a
                    // different assembly the call may fail. Swallow silently.
                }

#if UNITY_EDITOR
                EditorUtility.SetDirty(uiBlock);
                if (uiBlock.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(uiBlock.gameObject.scene);
                }
#endif
            }
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            UnsubscribeEditorUpdate();
        }

        private void OnDestroy()
        {
            UnsubscribeEditorUpdate();
        }

        private void SubscribeEditorUpdate()
        {
            if (!applyInEditMode || Application.isPlaying)
            {
                return;
            }

            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
        }

        private void UnsubscribeEditorUpdate()
        {
            EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            if (!this || Application.isPlaying || !applyInEditMode)
            {
                return;
            }

            // Call ApplyIfNeeded regularly while editing so size changes are captured
            ApplyIfNeeded();
        }
#endif

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }

        private Vector2 GetBlockPixelSize(UIBlock2D block)
        {
            // If this block is under a ScreenSpace root, the CalculatedSize is
            // already in pixel units, so prefer that.
            ScreenSpace ss = block.GetComponentInParent<ScreenSpace>();
            if (ss != null)
            {
                return new Vector2(block.CalculatedSize.Value.x, block.CalculatedSize.Value.y);
            }

            // Fallback: project local corners to the main camera to estimate pixel size
            Camera cam = Camera.main;
            if (ss != null && ss.TargetCamera != null)
            {
                cam = ss.TargetCamera;
            }

            if (cam == null)
            {
                return Vector2.zero;
            }

            Vector3 half = new Vector3(block.CalculatedSize.Value.x * 0.5f, block.CalculatedSize.Value.y * 0.5f, 0f);

            Transform t = block.transform;
            Vector3 pMin = t.TransformPoint(new Vector3(-half.x, -half.y, 0f));
            Vector3 pMax = t.TransformPoint(new Vector3(half.x, half.y, 0f));

            Vector3 sMin = cam.WorldToScreenPoint(pMin);
            Vector3 sMax = cam.WorldToScreenPoint(pMax);

            return new Vector2(Mathf.Abs(sMax.x - sMin.x), Mathf.Abs(sMax.y - sMin.y));
        }
    }
}
