// Copyright (c) Supernova Technologies LLC
using System.Collections;
using Nova;
using UnityEngine;
using UnityEngine.Events;

namespace Nova
{
    /// <summary>
    /// Shrinks a `UIBlock`'s reference width from 100% to 0% over `Duration` seconds,
    /// then disables the GameObject this component is on.
    ///
    /// Usage: add to the same GameObject as a `UIBlock` (or assign a target),
    /// then call `StartTimeout()` or enable `Play On Enable`.
    /// </summary>
    [AddComponentMenu("Nova/Timeout Disabled")]
    [RequireComponent(typeof(UIBlock))]
    public class TimeoutDisabled : MonoBehaviour
    {
        [Tooltip("Target UIBlock. If null, the UIBlock on this GameObject will be used.")]
        public UIBlock Target;

        [Tooltip("Delay before shrinking starts (seconds)")]
        public float Delay = 0f;

        [Tooltip("Duration of the shrink (seconds)")]
        public float Duration = 0.5f;

        [Tooltip("Timeline easing curve; evaluated in [0,1]")]
        public AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("If true, force the target width to 100% at the start.")]
        public bool StartAt100 = true;

        [Tooltip("Disable this GameObject after the shrink completes.")]
        public bool DisableWhenDone = true;

        [Tooltip("Destroy this GameObject after the shrink completes (takes priority over Disable When Done).")]
        public bool DestroyOnEnd = false;

        [Tooltip("Override which GameObject is disabled/destroyed on end. If null, uses this GameObject.")]
        public GameObject DisableTarget;

        [Tooltip("Automatically start when the component is enabled.")]
        public bool PlayOnEnable = true;

        [Tooltip("Optional event invoked after shrink completes (before disable).")]
        public UnityEvent OnComplete;

        private Coroutine running;

        private void Reset()
        {
            Target = GetComponent<UIBlock>();
        }

        private void Awake()
        {
            if (Target == null)
            {
                Target = GetComponent<UIBlock>();
            }
        }

        private void OnEnable()
        {
            if (PlayOnEnable)
            {
                StartTimeout();
            }
        }

        /// <summary>
        /// Start the shrink+disable sequence.
        /// </summary>
        public void StartTimeout()
        {
            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(RunTimeout());
        }

        /// <summary>
        /// Cancel an in-progress timeout.
        /// </summary>
        public void CancelTimeout()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }

        /// <summary>
        /// Clears AutoSize on X and forces the width to LengthType.Percent so the shrink
        /// animation works regardless of whether the UIBlock was using raw units or AutoSize.Expand.
        /// </summary>
        private void EnsurePercentMode()
        {
            if (Target == null) return;

            // Disable AutoSize on X (Expand / Shrink both prevent explicit size from taking effect)
            if (Target.AutoSize[0] != AutoSize.None)
            {
                Target.AutoSize[0] = AutoSize.None;
            }

            // If the X axis is stored as a raw value, convert it to an equivalent Percent
            ref Length3 size = ref Target.Size;
            if (size.X.Type != LengthType.Percent)
            {
                float parentWidth = Target.Parent != null ? Target.Parent.PaddedSize.x : 0f;
                float percent = parentWidth > 1e-6f ? Mathf.Clamp01(size.X.Value / parentWidth) : 1f;
                size.X = Length.Percentage(percent);
            }
        }

        private IEnumerator RunTimeout()
        {
            if (Target == null)
            {
                yield break;
            }

            if (Delay > 0f)
            {
                yield return new WaitForSeconds(Delay);
            }

            // Ensure the X axis is driven by an explicit Percent value, not AutoSize or raw units
            EnsurePercentMode();

            // Force start width to 100% if requested
            if (StartAt100)
            {
                Target.SetSizeAxes(100f, null, null, Length3Extensions.LengthInputSpace.PercentUI_0_100);
            }

            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = Duration > 0f ? Mathf.Clamp01(elapsed / Duration) : 1f;
                float u = Ease != null ? Ease.Evaluate(t) : t;
                float percent = Mathf.Lerp(100f, 0f, u);

                Target.SetSizeAxes(percent, null, null, Length3Extensions.LengthInputSpace.PercentUI_0_100);

                yield return null;
            }

            // Ensure final 0%
            Target.SetSizeAxes(0f, null, null, Length3Extensions.LengthInputSpace.PercentUI_0_100);

            // Give the engine a frame to process layout if needed
            yield return new WaitForEndOfFrame();

            OnComplete?.Invoke();

            running = null;

            GameObject endTarget = DisableTarget != null ? DisableTarget : gameObject;

            if (DestroyOnEnd)
            {
                Destroy(endTarget);
            }
            else if (DisableWhenDone)
            {
                endTarget.SetActive(false);
            }
        }
    }
}
