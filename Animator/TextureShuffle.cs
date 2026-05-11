using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Nova;

namespace Core.Animator
{
    /// <summary>
    /// Randomly cycles through a list of <see cref="Texture2D"/> assets and applies them
    /// to a <see cref="UIBlock2D"/> via <c>SetImage</c>.
    ///
    /// Each component instance generates its own independent random seed on Start so that
    /// multiple instances running simultaneously will not be in phase with each other.
    ///
    /// Behaviour contract:
    ///   - <c>applyOnStart</c>  → immediately pick and apply a texture when the scene starts.
    ///   - <c>applyOnce</c>    → stop after the first application (either on-start or after
    ///                           the first interval, depending on the other settings).
    ///   - interval / mode     → when <c>applyOnce</c> is false, keep cycling every X frames
    ///                           or X seconds.
    /// </summary>
    public class TextureShuffle : MonoBehaviour
    {
        public enum IntervalMode
        {
            Seconds,
            Frames
        }

        // ── Target ───────────────────────────────────────────────────────────────────

        [BoxGroup("Target", ShowLabel = true)]
        [ListDrawerSettings(DraggableItems = true, Expanded = true, ShowFoldout = false)]
        [Tooltip("All UIBlock2D targets that will receive the same randomly chosen texture each shuffle.")]
        public List<UIBlock2D> targets = new List<UIBlock2D>();

        // ── Textures ─────────────────────────────────────────────────────────────────

        [BoxGroup("Textures", ShowLabel = true)]
        [ListDrawerSettings(DraggableItems = true, Expanded = true, ShowFoldout = false)]
        [Tooltip("Pool of textures to pick from. Each pick is uniformly random.")]
        public List<Texture2D> textures = new List<Texture2D>();

        // ── Behaviour ────────────────────────────────────────────────────────────────

        [BoxGroup("Behaviour", ShowLabel = true)]
        [Tooltip("Apply a random texture immediately when this component starts.")]
        public bool applyOnStart = true;

        [BoxGroup("Behaviour")]
        [Tooltip("When enabled the texture is applied only once and the component stops.\n" +
                 "Combined with Apply On Start: applies on Start and never repeats.\n" +
                 "Without Apply On Start: waits one interval, applies once, then stops.")]
        public bool applyOnce = false;

        [BoxGroup("Behaviour")]
        [HideIf("@applyOnce")]
        [LabelText("Interval Mode")]
        [Tooltip("Whether the repeat interval is measured in seconds or frames.")]
        public IntervalMode intervalMode = IntervalMode.Seconds;

        [BoxGroup("Behaviour")]
        [HideIf("@applyOnce")]
        [LabelText("Interval")]
        [Tooltip("How many seconds (or frames) to wait between each texture change.")]
        [Min(0.01f)]
        public float interval = 1f;

        // ── Runtime state ────────────────────────────────────────────────────────────

        // Each instance owns its own RNG so multiple components on the same frame
        // never produce the same sequence.
        private System.Random _rng;

        // ── Unity lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            // XOR tick-count with instance ID so simultaneous starts still diverge.
            _rng = new System.Random(System.Environment.TickCount ^ GetInstanceID());

            if (applyOnStart)
                ApplyRandom();

            // If applyOnce is true and we just applied on start, there is nothing more to do.
            bool finishedAfterStart = applyOnStart && applyOnce;
            if (finishedAfterStart) return;

            StartCoroutine(intervalMode == IntervalMode.Frames ? LoopByFrames() : LoopBySeconds());
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private void ApplyRandom()
        {
            if (targets == null || targets.Count == 0) return;
            if (textures == null || textures.Count == 0) return;

            int index = _rng.Next(textures.Count);
            Texture2D tex = textures[index];
            if (tex == null) return;

            foreach (UIBlock2D t in targets)
            {
                if (t != null)
                    t.SetImage(tex);
            }
        }

        private IEnumerator LoopBySeconds()
        {
            do
            {
                yield return new WaitForSeconds(interval);
                ApplyRandom();
            }
            while (!applyOnce);
        }

        private IEnumerator LoopByFrames()
        {
            int frameCount = Mathf.Max(1, Mathf.RoundToInt(interval));
            do
            {
                for (int i = 0; i < frameCount; i++)
                    yield return null;
                ApplyRandom();
            }
            while (!applyOnce);
        }
    }
}
