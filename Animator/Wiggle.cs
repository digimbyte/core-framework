using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Animator
{
    /// <summary>
    /// Procedural wiggle / shake using layered Perlin noise.
    ///
    /// Speed controls the character of the motion:
    ///   ~0.5  → slow, dreamy float
    ///   ~2    → hand-held camera
    ///   ~15+  → rapid vibration / impact buzz
    ///
    /// Noise Factor blends in a second octave for choppier, less rhythmic motion.
    /// Amplitude per axis is hard-bounded — the object never drifts beyond ±Amplitude.
    /// </summary>
    public class Wiggle : MonoBehaviour
    {
        [System.Flags]
        public enum AxisMask
        {
            None = 0,
            X    = 1 << 0,
            Y    = 1 << 1,
            Z    = 1 << 2
        }

        // ── Position ────────────────────────────────────────────────────────────────

        [BoxGroup("Position", ShowLabel = true)]
        [LabelText("Axes")]
        public AxisMask positionAxes = AxisMask.X | AxisMask.Y;

        [BoxGroup("Position")]
        [LabelText("Amplitude")]
        [Tooltip("Maximum deviation from rest position per axis (local units).")]
        public Vector3 positionAmplitude = new Vector3(0.05f, 0.05f, 0f);

        // ── Rotation ────────────────────────────────────────────────────────────────

        [BoxGroup("Rotation", ShowLabel = true)]
        [LabelText("Axes")]
        public AxisMask rotationAxes = AxisMask.Z;

        [BoxGroup("Rotation")]
        [LabelText("Amplitude (degrees)")]
        [Tooltip("Maximum rotation deviation from rest rotation per axis (degrees).")]
        public Vector3 rotationAmplitude = new Vector3(0f, 0f, 1f);

        // ── Behaviour ───────────────────────────────────────────────────────────────

        [BoxGroup("Behaviour", ShowLabel = true)]
        [Range(0.05f, 30f)]
        [Tooltip("How fast the noise time advances.\n0.5 = slow float  |  2 = hand-cam  |  15+ = vibration")]
        public float speed = 2f;

        [BoxGroup("Behaviour")]
        [Range(0f, 1f)]
        [Tooltip("0 = smooth single-frequency Perlin.\n1 = blends in a second octave for choppier, more erratic motion.")]
        public float noiseFactor = 0.25f;

        [BoxGroup("Behaviour")]
        [Tooltip("Apply offsets in local space (recommended). Disable for world-space shake.")]
        public bool localSpace = true;

        [BoxGroup("Behaviour")]
        [Tooltip("Automatically play when this component is enabled.")]
        public bool playOnEnable = true;

        [BoxGroup("Behaviour")]
        [ReadOnly]
        public bool isPlaying;

        // ── Runtime state ────────────────────────────────────────────────────────────

        private Vector3    _restLocalPosition;
        private Quaternion _restLocalRotation;   // stored as Quaternion to avoid Euler round-trip drift
        private float      _time;

        // Unique seed offsets per axis so the axes are never in phase with each other.
        private const float PX = 0f,      PY = 31.41f,  PZ = 62.82f;   // position seeds
        private const float RX = 94.23f,  RY = 125.64f, RZ = 157.05f;  // rotation seeds

        // ── Unity events ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            CaptureRestPose();
            if (playOnEnable) Play();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void LateUpdate()
        {
            if (!isPlaying) return;

            _time += Time.deltaTime * speed;

            var rotOffset = Vector3.zero;

            // Position — only add offset on active axes; unmasked axes stay exactly at rest.
            Vector3 pos = _restLocalPosition;
            if ((positionAxes & AxisMask.X) != 0) pos.x += Sample(_time, PX) * positionAmplitude.x;
            if ((positionAxes & AxisMask.Y) != 0) pos.y += Sample(_time, PY) * positionAmplitude.y;
            if ((positionAxes & AxisMask.Z) != 0) pos.z += Sample(_time, PZ) * positionAmplitude.z;

            // Rotation — build a delta Euler from only active axes, then combine with the rest
            // Quaternion.  This avoids the Euler↔Quaternion round-trip that introduces
            // sub-0.001° drift on untouched axes, which causes z-tearing on UI elements.
            if ((rotationAxes & AxisMask.X) != 0) rotOffset.x = Sample(_time, RX) * rotationAmplitude.x;
            if ((rotationAxes & AxisMask.Y) != 0) rotOffset.y = Sample(_time, RY) * rotationAmplitude.y;
            if ((rotationAxes & AxisMask.Z) != 0) rotOffset.z = Sample(_time, RZ) * rotationAmplitude.z;

            if (localSpace)
            {
                transform.localPosition = pos;
                transform.localRotation = _restLocalRotation * Quaternion.Euler(rotOffset);
            }
            else
            {
                Vector3 worldRest = transform.parent != null
                    ? transform.parent.TransformPoint(_restLocalPosition)
                    : _restLocalPosition;

                transform.position    = worldRest + (pos - _restLocalPosition);
                transform.rotation    = _restLocalRotation * Quaternion.Euler(rotOffset);
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────────

        /// <summary>Begins wiggling from the current transform position.</summary>
        public void Play()
        {
            CaptureRestPose();
            // Randomise the noise phase so each play feels different.
            _time     = Random.Range(0f, 1000f);
            isPlaying = true;
        }

        /// <summary>Stops wiggling and snaps the transform back to rest.</summary>
        public void Stop()
        {
            isPlaying = false;
            transform.localPosition = _restLocalPosition;
            transform.localRotation = _restLocalRotation;
        }

        /// <summary>Re-snaps the rest pose to the current transform without stopping playback.</summary>
        public void CaptureRestPose()
        {
            _restLocalPosition = transform.localPosition;
            _restLocalRotation = transform.localRotation;
        }

        // ── Noise helper ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a value in [-1, 1].
        /// At noiseFactor 0: smooth single-octave Perlin.
        /// At noiseFactor 1: first octave blended with a faster second octave for chop.
        /// </summary>
        private float Sample(float t, float seed)
        {
            float smooth = Mathf.PerlinNoise(t + seed, seed * 0.5f) * 2f - 1f;
            if (noiseFactor <= 0f) return smooth;
            float choppy = Mathf.PerlinNoise(t * 3.7f + seed + 17f, seed * 1.3f) * 2f - 1f;
            return Mathf.Lerp(smooth, smooth * 0.6f + choppy * 0.4f, noiseFactor);
        }

        // ── Editor buttons ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [BoxGroup("Behaviour")]
        [ButtonGroup("Behaviour/Controls")]
        [Button("▶ Play"), EnableIf("@!isPlaying")]
        private void EditorPlay() => Play();

        [ButtonGroup("Behaviour/Controls")]
        [Button("■ Stop"), EnableIf("isPlaying")]
        private void EditorStop() => Stop();
#endif
    }
}
