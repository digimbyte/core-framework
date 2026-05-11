using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Nova;

namespace Core.Animator
{
    /// <summary>
    /// Sequences a list of slide GameObjects with optional fade-in / fade-out transitions
    /// driven by a <see cref="UIBlock2D"/> overlay whose <c>Color.a</c> is the sole visual state.
    ///
    /// Fade contract:
    ///   Cover  (alpha 0 → 1): enable GameObject, lerp to 1, keep active.
    ///   Reveal (alpha 1 → 0): lerp to 0, disable GameObject once fully transparent.
    ///
    /// A private <c>_fadeAlpha</c> field owns the canonical alpha value so we never read
    /// from a potentially-stale disabled Nova object.
    ///
    /// Hold Space or Escape for <see cref="skipHoldDuration"/> seconds → jump to the last slide.
    /// </summary>
    public class PowerPoint : MonoBehaviour
    {
        // ── Slide entry ──────────────────────────────────────────────────────────────

        [System.Serializable]
        [InlineProperty]
        [HideReferenceObjectPicker]
        public class SlideEntry
        {
            [HorizontalGroup("Row", 0.5f), HideLabel]
            public GameObject slide;

        [HorizontalGroup("Row"), ToggleLeft, LabelText("Fade In"), LabelWidth(58)]
            public bool fadeIn = true;

            [HorizontalGroup("Row"), ToggleLeft, LabelText("Fade Out"), LabelWidth(65)]
            public bool fadeOut = true;

            [HorizontalGroup("Row"), ToggleLeft, LabelText("Preserve"), LabelWidth(68)]
            [Tooltip("When true the slide GameObject is never disabled after it has been shown.")]
            public bool preserve = false;
        }

        // ── Inspector ────────────────────────────────────────────────────────────────

        [BoxGroup("Slides", ShowLabel = true)]
        [ListDrawerSettings(DraggableItems = true, Expanded = true, ShowFoldout = false)]
        public List<SlideEntry> slides = new List<SlideEntry>();

        [BoxGroup("Fade", ShowLabel = true)]
        [Tooltip("UIBlock2D used as the full-screen fade overlay.\nIts Body Color.a is driven from 1 (covered) to 0 (transparent).\nThe GameObject is disabled once alpha reaches 0 so it never blocks input.")]
        public UIBlock2D fadeBlock;

        [BoxGroup("Fade")]
        [Range(0.1f, 20f)]
        [Tooltip("Alpha change per second. Higher = faster fade.")]
        public float fadeSpeed = 2f;

        [BoxGroup("Fade")]
        [Range(0.001f, 0.05f)]
        [Tooltip("Alpha distance from target at which we hard-snap to avoid infinite lerp drift.")]
        public float snapThreshold = 0.01f;

        [BoxGroup("Timing", ShowLabel = true)]
        [Tooltip("Seconds to display each slide before advancing.")]
        public float waitDuration = 3f;

        [BoxGroup("Timing")]
        [Range(0.5f, 5f)]
        [Tooltip("Seconds Space/Escape must be held to skip to the last slide.")]
        public float skipHoldDuration = 2f;

        [BoxGroup("Timing")]
        [Tooltip("Start the slideshow automatically when the scene loads.")]
        public bool playOnStart = true;

        [BoxGroup("Skip alignment", ShowLabel = true)]
        [Tooltip("When the viewer skips to the end, the slideshow first enables every preserved slide plus the " +
                 "last slide, then runs Play All on these. Use for login and other UI that must run after that layout " +
                 "is active. Targets should live under an active branch; inactive parents are enabled when possible.")]
        [ListDrawerSettings(DraggableItems = true, Expanded = true, ShowFoldout = false)]
        [SerializeField]
        private List<Animate> onSkipPlayAnimates = new List<Animate>();

        // ── Runtime state ────────────────────────────────────────────────────────────

        // Canonical alpha — never read from the (potentially disabled) Nova block.
        private float _fadeAlpha = 1f;

        private Coroutine _playCoroutine;
        private bool      _skipRequested;
        private float     _skipHoldTimer;
        // When true a single-space press requested advancing to the next slide (consumed by PlaySlides/FadeTo)
        private bool      _advanceRequested;
        // Whether this instance has pushed (claimed) the fade overlay via FadeOverlayStack
        private bool      _havePushedFade = false;
        // If true the overlay was active before Play() started (external owner). In that case we won't release it.
        private bool      _hadExternalFadeAtPlayStart = false;

        [BoxGroup("State", ShowLabel = true)]
        [ReadOnly] public bool isPlaying;
        [BoxGroup("State")]
        [ReadOnly] public int  currentSlideIndex = -1;

        // ── Unity events ─────────────────────────────────────────────────────────────

        private void Start()
        {
            // Initialise overlay: fully opaque but disabled.
            // FadeTo(1f) will re-enable it before any cover transition.
            _fadeAlpha = 1f;
            ApplyFadeAlpha();
            SetFadeActive(false);

            foreach (var s in slides)
                if (s.slide != null) s.slide.SetActive(false);

            if (playOnStart)
                Play();
        }

        private void Update()
        {
            if (!isPlaying) return;

            // Immediate advance when space is pressed once
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _advanceRequested = true;
            }

            bool held = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Escape);
            if (held)
            {
                _skipHoldTimer += Time.deltaTime;
                if (_skipHoldTimer >= skipHoldDuration)
                    _skipRequested = true;
            }
            else
            {
                _skipHoldTimer = 0f;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────────

        public void Play()
        {
            if (_playCoroutine != null) StopCoroutine(_playCoroutine);

            _skipRequested    = false;
            _skipHoldTimer    = 0f;
            currentSlideIndex = -1;
            isPlaying         = true;

            foreach (var s in slides)
                if (s.slide != null) s.slide.SetActive(false);

            // Determine whether the fade overlay is already active (owned externally) so we don't steal/release it.
            _hadExternalFadeAtPlayStart = (fadeBlock != null && fadeBlock.gameObject.activeSelf) || FadeOverlayStack.Count(fadeBlock) > 0;

            // Start fully covered so first slide fades in from solid.
            _fadeAlpha = 1f;
            // Claim the overlay only if nobody else already has it
            if (!_hadExternalFadeAtPlayStart)
            {
                FadeOverlayStack.Push(fadeBlock);
                _havePushedFade = true;
            }

            ApplyFadeAlpha();

            _playCoroutine = StartCoroutine(PlaySlides());
        }

        public void RequestSkip() => _skipRequested = true;

        // ── Coroutines ───────────────────────────────────────────────────────────────

        private IEnumerator PlaySlides()
        {
            if (slides == null || slides.Count == 0)
            {
                isPlaying = false;
                yield break;
            }

            for (int i = 0; i < slides.Count; i++)
            {
                if (_skipRequested)
                {
                    yield return StartCoroutine(GoToLastSlide());
                    yield break;
                }

                currentSlideIndex = i;
                var entry  = slides[i];
                bool isLast = i == slides.Count - 1;

                // Enable slide
                if (entry.slide != null)
                {
                    entry.slide.SetActive(true);

                    PlayAnimatesUnderSlide(entry.slide);
                }

                // Fade in: overlay 1 → 0 (reveal slide)
                if (entry.fadeIn)
                    yield return StartCoroutine(FadeTo(0f));

                // Wait — but bail immediately if skip fires
                float waited = 0f;
                while (waited < waitDuration)
                {
                    if (_skipRequested) break;
                    if (_advanceRequested)
                    {
                        // consume the advance request and break waiting
                        _advanceRequested = false;
                        break;
                    }
                    waited += Time.deltaTime;
                    yield return null;
                }

                if (_skipRequested)
                {
                    yield return StartCoroutine(GoToLastSlide());
                    yield break;
                }

                if (isLast)
                {
                    isPlaying = false;
                    yield break;
                }

                // Fade out: overlay 0 → 1 (cover slide before swap)
                if (entry.fadeOut)
                    yield return StartCoroutine(FadeTo(1f));

                // Disable slide unless it wants to stay visible.
                if (!entry.preserve && entry.slide != null)
                    entry.slide.SetActive(false);
            }

            isPlaying = false;
        }

        private IEnumerator GoToLastSlide()
        {
            // Ensure screen is covered before the jump.
            if (_fadeAlpha < 1f)
                yield return StartCoroutine(FadeTo(1f));

            // Under full cover: turn on every preserved slide plus the last slide, then run only the
            // Skip alignment Animate list (not per-slide Play All).
            int lastIdx = slides.Count - 1;
            yield return StartCoroutine(ApplySkipAnimatePassRoutine(lastIdx));

            currentSlideIndex = lastIdx;

            // Skip still held: FadeTo would snap every reveal. Clear so the final uncover runs a normal fade.
            _skipRequested = false;
            _advanceRequested = false;

            // Reveal the last slide unless the overlay was external to us when we started playing.
            if (!_hadExternalFadeAtPlayStart)
                yield return StartCoroutine(FadeTo(0f));

            isPlaying = false;
        }

        /// <summary>
        /// Runs <see cref="onSkipPlayAnimates"/> after skip layout (preserved + last slides) is applied.
        /// </summary>
        private void PlayAnimatesAfterSkip()
        {
            if (onSkipPlayAnimates == null || onSkipPlayAnimates.Count == 0)
                return;

            for (int i = 0; i < onSkipPlayAnimates.Count; i++)
            {
                var animate = onSkipPlayAnimates[i];
                if (animate == null)
                    continue;

                EnsureAncestorsActiveFor(animate.transform);

                if (!animate.isActiveAndEnabled)
                    continue;

                animate.PlayAllConfigured();
            }
        }

        /// <summary>
        /// Enables inactive ancestors top-down so a nested UI branch can run after skip (slide toggles may leave a reference target disabled).
        /// </summary>
        private static void EnsureAncestorsActiveFor(Transform leaf)
        {
            if (leaf == null)
                return;

            var chain = new List<Transform>();
            for (Transform walk = leaf; walk != null; walk = walk.parent)
                chain.Add(walk);

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                GameObject go = chain[i].gameObject;
                if (!go.activeSelf)
                    go.SetActive(true);
            }
        }

        private static void PlayAnimatesUnderSlide(GameObject slideRoot)
        {
            if (slideRoot == null)
                return;

            foreach (var slideAnimator in slideRoot.GetComponentsInChildren<Animate>(true))
            {
                if (slideAnimator != null)
                    slideAnimator.PlayAllConfigured();
            }
        }

        /// <summary>
        /// Skip path: under full cover, enable every preserved slide and the last slide, disable the rest, then
        /// run <see cref="onSkipPlayAnimates"/>.
        /// </summary>
        private IEnumerator ApplySkipAnimatePassRoutine(int lastIdx)
        {
            if (slides == null)
                yield break;

            for (int i = 0; i < slides.Count; i++)
            {
                SlideEntry entry = slides[i];
                if (entry.slide == null)
                    continue;

                bool staysVisible = i == lastIdx || entry.preserve;
                entry.slide.SetActive(staysVisible);
            }

            yield return null;

            PlayAnimatesAfterSkip();
        }

        // ── Fade implementation ──────────────────────────────────────────────────────

        /// <summary>
        /// Lerps <see cref="_fadeAlpha"/> toward <paramref name="target"/> at <see cref="fadeSpeed"/>
        /// alpha/sec.  Enables the overlay before starting (always required for Nova to render),
        /// and disables it once fully transparent.  Snaps to the exact target when within
        /// <see cref="snapThreshold"/> to avoid infinite-drift artefacts.
        /// </summary>
        private IEnumerator FadeTo(float target)
        {
            if (fadeBlock == null) yield break;

            // State-machine early-out: _fadeAlpha IS the authoritative state.
            // If we're already at (or within snap of) the target, resolve instantly
            // without enabling the overlay or burning a frame on a zero-duration lerp.
            if (Mathf.Abs(_fadeAlpha - target) <= snapThreshold)
            {
                _fadeAlpha = target;
                if (target > 0f) { SetFadeActive(true);  ApplyFadeAlpha(); }
                else             { ApplyFadeAlpha(); SetFadeActive(false); }
                yield break;
            }

            // Enable FIRST — Nova's rendering store must be active before we write Color.
            SetFadeActive(true);

            float start    = _fadeAlpha;
            float delta    = Mathf.Abs(target - start);
            float duration = delta / Mathf.Max(fadeSpeed, 0.001f);


            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed    += Time.deltaTime;
                _fadeAlpha  = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                ApplyFadeAlpha();

                // Buffer snap: close enough → break instead of crawling to the target.
                if (Mathf.Abs(_fadeAlpha - target) <= snapThreshold)
                    break;

                // Immediate responses: finish fade early if user requested advance or skip
                if (_skipRequested || _advanceRequested)
                {
                    _fadeAlpha = target;
                    ApplyFadeAlpha();
                    // consume advance request so it isn't used twice
                    _advanceRequested = false;
                    break;
                }

                yield return null;
            }

            // Hard-snap to exact target value.
            _fadeAlpha = target;
            ApplyFadeAlpha();

            // Disable the overlay once fully transparent so it never blocks input.
            if (target <= 0f)
                SetFadeActive(false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>Writes <see cref="_fadeAlpha"/> into the UIBlock2D's body Color alpha channel.</summary>
        private void ApplyFadeAlpha()
        {
            if (fadeBlock == null) return;
            Color c = fadeBlock.Color;
            c.a = _fadeAlpha;
            fadeBlock.Color = c;
        }

        private void SetFadeActive(bool active)
        {
            if (fadeBlock == null) return;

            if (active)
            {
                // Claim the overlay only if nobody else has already claimed it.
                if (!_havePushedFade && FadeOverlayStack.Count(fadeBlock) == 0 && !fadeBlock.gameObject.activeSelf)
                {
                    FadeOverlayStack.Push(fadeBlock);
                    _havePushedFade = true;
                }
            }
            else
            {
                // Only release if we previously claimed it. Do not disable overlays owned externally.
                if (_havePushedFade)
                {
                    FadeOverlayStack.Pop(fadeBlock);
                    _havePushedFade = false;
                }
            }
        }

        // ── Editor buttons ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [BoxGroup("Controls", ShowLabel = true)]
        [ButtonGroup("Controls/Btns")]
        [Button("▶ Play"), EnableIf("isPlaying", false)]
        private void EditorPlay() => Play();

        [ButtonGroup("Controls/Btns")]
        [Button("⏭ Skip to End"), EnableIf("isPlaying")]
        private void EditorSkip() => RequestSkip();
#endif
    }
}
