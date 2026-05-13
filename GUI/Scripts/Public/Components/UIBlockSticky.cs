// Copyright (c) Supernova Technologies LLC
using Nova.Internal;
using Nova.Internal.Core;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities.Extensions;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Smooths the visual pose/size of a `UIBlock` independently from the layout-engine truth.
    /// This component does NOT change layout buffers or hit geometry; it only overrides
    /// `LayoutDataStore.Instance.LocalToWorldMatrices[...]` just before render so the
    /// rendering path draws the smoothed pose.
    ///
    /// Two follow modes are supported:
    /// - Damped: SmoothDamp-style follow with per-frame velocity.
    /// - Timed: Interpolate over a fixed duration with easing; restarts when the ideal jumps.
    ///
    /// This approach intentionally detaches visuals from layout (so layout, hit tests,
    /// and bounds remain correct) while allowing a resistive, slerp-like visual follow.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIBlock))]
    [AddComponentMenu("Nova/UI Block Sticky (Visual)")]
    public sealed class UIBlockSticky : MonoBehaviour
    {
        public enum FollowMode
        {
            Damped = 0,
            Timed = 1,
        }

        public enum TimedEasing
        {
            Linear = 0,
            SmoothStep = 1,
            InOutQuad = 2,
            InOutCubic = 3,
        }

        [Header("Enable")]
        [SerializeField]
        private bool stickyPosition = true;

        [SerializeField]
        private bool stickySize = false;

        [Header("General")]
        [SerializeField]
        private FollowMode followMode = FollowMode.Damped;

        [Header("Damped")]
        [Tooltip("Seconds; smaller = faster catch-up.")]
        [SerializeField]
        private float positionSmoothTime = 0.12f;

        [Tooltip("0 = unlimited; units per second" )]
        [SerializeField]
        private float maxSpeed = 10f;

        [Header("Timed")]
        [SerializeField]
        private float timedDuration = 0.2f;

        [SerializeField]
        private TimedEasing timedEasing = TimedEasing.SmoothStep;

        [Tooltip("When the ideal jumps farther than this (local units), restart the timed segment")] 
        [SerializeField]
        private float timedRestartThreshold = 0.01f;

        [Header("Snap")]
        [SerializeField]
        private float snapDistance = 0.002f;

        [SerializeField]
        private float snapSpeed = 0.02f;

        [Header("Size (render-only)")]
        [SerializeField]
        private float sizeSmoothTime = 0.12f;

        [SerializeField]
        private float sizeSnapDistance = 0.002f;

        [Header("Resistance")]
        [Tooltip("When enabled, smooths the layout-published ideal position before the sticky follow.")]
        [SerializeField]
        private bool smoothIdeal = true;

        [Tooltip("Seconds; how quickly the ideal follows sudden layout changes. Larger = slower ideal response.")]
        [SerializeField]
        private float idealSmoothTime = 0.08f;

        [Tooltip("Maximum units per second the ideal may move. 0 = unlimited.")]
        [SerializeField]
        private float idealMaxSpeed = 0f;

        private UIBlock uiBlock;

        private int layoutIndex = -1;

        // display state
        private Vector3 displayPos;
        private Vector3 displaySize;
        private Vector3 velocity;
        private Vector3 sizeVelocity;

        // timed mode state
        private float timedProgress = -1f;
        private Vector3 timedOriginPos;
        private Vector3 lastIdealBase;
        private Vector3 timedOriginSize;

        // ideal smoothing buffer
        private Vector3 smoothedIdealPos;
        private Vector3 smoothedIdealVelocity;

        private bool initialized = false;

        private void OnEnable()
        {
            uiBlock = GetComponent<UIBlock>();
            InitializeIfNeeded();
            Application.onBeforeRender += OnBeforeRenderOverride;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRenderOverride;
        }

        private void InitializeIfNeeded()
        {
            if (uiBlock == null)
            {
                uiBlock = GetComponent<UIBlock>();
            }

            if (uiBlock == null || !((IDataStoreElement)uiBlock).Index.IsValid)
            {
                layoutIndex = -1;
                initialized = false;
                return;
            }

            layoutIndex = ((IDataStoreElement)uiBlock).Index;

            // Seed display pose from the current rendered matrix if possible, otherwise layout position.
            if (LayoutDataStore.Instance != null && layoutIndex >= 0 && layoutIndex < LayoutDataStore.Instance.TransformLocalPositions.Length)
            {
                float3 raw = LayoutDataStore.Instance.TransformLocalPositions[layoutIndex];
                displayPos = new Vector3(raw.x, raw.y, raw.z);
                displaySize = uiBlock.CalculatedSize.Value;
            }
            else
            {
                displayPos = uiBlock.transform.localPosition;
                displaySize = uiBlock.CalculatedSize.Value;
            }

            velocity = Vector3.zero;
            sizeVelocity = Vector3.zero;
            timedProgress = -1f;
            // seed smoothed ideal and baseline using the engine-calculated ideal
            smoothedIdealPos = uiBlock.GetCalculatedTransformLocalPosition();
            lastIdealBase = smoothedIdealPos;
            initialized = true;
        }

        private void Update()
        {
            if (!stickyPosition && !stickySize)
            {
                return;
            }

            if (!initialized)
            {
                InitializeIfNeeded();
            }

            if (layoutIndex < 0 || LayoutDataStore.Instance == null)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.333f);

            // Raw engine ideal (layout truth)
            Vector3 rawIdeal = uiBlock.GetCalculatedTransformLocalPosition();

            // Smooth the ideal itself to resist sudden layout jumps from neighbors (optional)
            if (smoothIdeal)
            {
                float smoothT = Mathf.Max(1e-5f, idealSmoothTime);
                smoothedIdealPos = Vector3.SmoothDamp(smoothedIdealPos, rawIdeal, ref smoothedIdealVelocity, smoothT, idealMaxSpeed > 0f ? idealMaxSpeed : Mathf.Infinity, dt);
            }
            else
            {
                smoothedIdealPos = rawIdeal;
            }

            // Use the (possibly smoothed) ideal as the target for the visual follow
            Vector3 targetPos = smoothedIdealPos;

            // Size target (render-only)
            Vector3 targetSize = uiBlock.CalculatedSize.Value;

            // POSITION
            if (stickyPosition)
            {
                if (followMode == FollowMode.Damped)
                {
                    float smoothT = Mathf.Max(1e-4f, positionSmoothTime);
                    displayPos = Vector3.SmoothDamp(displayPos, targetPos, ref velocity, smoothT, maxSpeed > 0f ? maxSpeed : Mathf.Infinity, dt);
                    // Try snap
                    if ((displayPos - targetPos).sqrMagnitude <= snapDistance * snapDistance && velocity.sqrMagnitude <= snapSpeed * snapSpeed)
                    {
                        displayPos = targetPos;
                        velocity = Vector3.zero;
                    }
                }
                else // Timed
                {
                    bool restart = timedProgress < 0f;
                    float thrSq = timedRestartThreshold * timedRestartThreshold;
                    // Restart if the raw layout ideal moved sufficiently (not the smoothed buffer)
                    if (!restart && (rawIdeal - lastIdealBase).sqrMagnitude > thrSq)
                    {
                        restart = true;
                    }

                    if (restart)
                    {
                        timedOriginPos = displayPos;
                        timedOriginSize = displaySize;
                        timedProgress = 0f;
                    }

                    float invDur = 1f / Mathf.Max(1e-4f, timedDuration);
                    timedProgress += dt * invDur;
                    float u = ApplyTimedEase(Mathf.Clamp01(timedProgress), timedEasing);
                    displayPos = Vector3.Lerp(timedOriginPos, targetPos, u);

                    if (timedProgress >= 1f - 1e-5f || (displayPos - targetPos).sqrMagnitude <= snapDistance * snapDistance)
                    {
                        displayPos = targetPos;
                        timedProgress = 1f;
                        velocity = Vector3.zero;
                    }

                    // record the raw ideal as the baseline for future restarts
                    lastIdealBase = rawIdeal;
                }
            }
            else
            {
                // If position not sticky, keep display = target
                displayPos = targetPos;
                velocity = Vector3.zero;
            }

            // SIZE (render-only)
            if (stickySize)
            {
                float smoothSizeT = sizeSmoothTime > 0f ? sizeSmoothTime : positionSmoothTime;
                displaySize = Vector3.SmoothDamp(displaySize, targetSize, ref sizeVelocity, smoothSizeT, Mathf.Infinity, dt);
                if ((displaySize - targetSize).sqrMagnitude <= sizeSnapDistance * sizeSnapDistance)
                {
                    displaySize = targetSize;
                    sizeVelocity = Vector3.zero;
                }
            }
            else
            {
                displaySize = targetSize;
                sizeVelocity = Vector3.zero;
            }
        }

        private void OnBeforeRenderOverride()
        {
            if (!initialized || layoutIndex < 0 || LayoutDataStore.Instance == null)
            {
                return;
            }

            // Compose local TRS for this element from display values and the element's rotation/scale from the datastore
            Quaternion rot = Quaternion.identity;
            Vector3 scale = Vector3.one;

            if (layoutIndex >= 0 && layoutIndex < LayoutDataStore.Instance.TransformLocalRotations.Length)
            {
                quaternion q = LayoutDataStore.Instance.TransformLocalRotations[layoutIndex];
                rot = new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
            }

            if (layoutIndex >= 0 && layoutIndex < LayoutDataStore.Instance.TransformLocalScales.Length)
            {
                float3 s = LayoutDataStore.Instance.TransformLocalScales[layoutIndex];
                scale = new Vector3(s.x, s.y, s.z);
            }

            Matrix4x4 localMatrix = Matrix4x4.TRS(displayPos, rot, scale);

            // Parent world matrix (if any)
            Matrix4x4 parentWorld = Matrix4x4.identity;
            IUIBlock parent = uiBlock.GetParentBlock();
            if (parent != null && parent.Index.IsValid)
            {
                int pIndex = parent.Index;
                if (pIndex >= 0 && pIndex < LayoutDataStore.Instance.LocalToWorldMatrices.Length)
                {
                    parentWorld = (Matrix4x4)LayoutDataStore.Instance.LocalToWorldMatrices[pIndex];
                }
                else if (uiBlock.transform.parent != null)
                {
                    parentWorld = uiBlock.transform.parent.localToWorldMatrix;
                }
            }
            else if (uiBlock.transform.parent != null)
            {
                parentWorld = uiBlock.transform.parent.localToWorldMatrix;
            }

            Matrix4x4 final = parentWorld * localMatrix;

            // Write the smoothed matrix into the engine's render matrix buffer so the renderer draws the smoothed pose.
            if (layoutIndex >= 0 && layoutIndex < LayoutDataStore.Instance.LocalToWorldMatrices.Length)
            {
                LayoutDataStore.Instance.LocalToWorldMatrices[layoutIndex] = final;
            }
        }

        private static float ApplyTimedEase(float u, TimedEasing ease)
        {
            switch (ease)
            {
                case TimedEasing.SmoothStep:
                    return u * u * (3f - 2f * u);
                case TimedEasing.InOutQuad:
                    if (u < 0.5f) return 2f * u * u;
                    float v = 2f - 2f * u; return 1f - 0.5f * v * v;
                case TimedEasing.InOutCubic:
                    if (u < 0.5f) return 4f * u * u * u;
                    float w = -2f * u + 2f; return 1f - w * w * w * 0.5f;
                default:
                    return u;
            }
        }
    }
}
