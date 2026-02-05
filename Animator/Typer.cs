using System;
using System.Text;
using TMPro;
using System.Collections;
using UnityEngine;

namespace Animator
{
    /// <summary>
    /// Universal text typing controller for TextMesh Pro.
    /// Supports insert/overwrite modes, prefill spacing, visible blinking cursor,
    /// random typos with backtracking speed and other pacing controls.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [RequireComponent(typeof(AudioSource))]
    public class Typer : MonoBehaviour
    {
        private TMP_Text target;

        [Header("Source Text")]
        [TextArea] [SerializeField] private string textToType = "";
        [Tooltip("If true, uses the target's current text as the source when starting unless an override is provided.")]
        [SerializeField] private bool useExistingTextAsSource = true;

        [Header("Playback")]
        [SerializeField] private bool playOnStart = true;
        [Tooltip("If true, restart typing from the beginning when this object becomes enabled (including when a parent enables it).")]
        [SerializeField] private bool restartOnEnable = false;
        [SerializeField] private float charactersPerSecond = 32f;
        private const float MaxCharactersPerSecond = 125f;
        [Tooltip("Sound played for each typed character.")]
        [SerializeField] private AudioClip typeSound = null;
        [Tooltip("Sound played when a character is deleted/backspaced.")]
        [SerializeField] private AudioClip deleteSound = null;
        [Tooltip("Sound played when a typo (wrong character) is typed.")]
        [SerializeField] private AudioClip typoSound = null;
        [Tooltip("AudioSource used to play typing sounds. If empty, the component on this GameObject will be used.")]
        [SerializeField] private AudioSource audioSource = null;
        [Header("Multi-Typing")]
        [Tooltip("Chance (0-1) that multiple characters will be emitted in a single batch. 0.5 = 50% of the time.")]
        [Range(0f, 1f)] [SerializeField] private float multiTypingChance = 0.5f;
        [Tooltip("Maximum characters emitted in a multi-typing batch (minimum 1).")]
        [SerializeField] private int maxMultiChars = 3;
        [Tooltip("If true, characters are written over existing positions instead of appended.")]
        [SerializeField] private bool insertMode = false;
        [Tooltip("When insert mode is enabled, prefill the field with whitespace matching the source length.")]
        [SerializeField] private bool prefillWhitespace = true;

        [Header("Typos & Corrections")]
        [Range(0f, 1f)] [SerializeField] private float typoChance = 0.05f;
        [SerializeField] private float typoHoldSeconds = 0.12f;
        [SerializeField] private float backspacePerSecond = 60f;
        [SerializeField] private string glitchCharacters = "abcdefghijklmnopqrstuvwxyz0123456789!?%$#@";

        [Header("Cursor")]
        [SerializeField] private bool showCursor = true;
        [SerializeField] private char cursorChar = '▌';
        [SerializeField] private float blinkInterval = 0.4f;

        private Coroutine typingCoroutine;
        private Coroutine cursorCoroutine;
        private string capturedSource = null;
        private bool started = false;

        private bool cursorVisible = true;
        private bool isTyping;

        private void Awake()
        {
            target = GetComponent<TMP_Text>(); // guaranteed by RequireComponent
            // no preservation/restoration; typer will use configured source or existing target text as source
            // Capture the existing target text once at Awake if configured to use existing text as source.
            // This prevents later updates (e.g. animation-driven text) from being used as the source.
            if (useExistingTextAsSource)
            {
                capturedSource = target.text;
            }
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            if (audioSource == null && (typeSound != null || deleteSound != null || typoSound != null))
            {
                Debug.LogWarning("Typer: No AudioSource found to play typing sounds.", this);
            }
        }

        private void OnEnable()
        {
            // Ignore the initial OnEnable that runs during scene load before Start()
            if (!started) return;

            if (restartOnEnable)
            {
                StopTyping(manualStop: false);
                StartTyping();
            }
        }

        private void Start()
        {
            started = true;
            if (playOnStart && gameObject.activeInHierarchy)
            {
                StartTyping();
            }
        }

        private void OnDisable()
        {
            StopTyping(manualStop: false);
        }

        /// <summary>
        /// Type with empty override and append mode enabled. Useful for Unity Events.
        /// </summary>
        public void Type()
        {
            // Use configured/existing source. Append=true means "merge/fill" behavior (do not clear) when possible.
            StartTyping(null, true);
        }

        /// <summary>
        /// Start typing using the configured source text or an override.
        /// </summary>
        /// <param name="overrideText">Optional text to type instead of the configured source.</param>
        /// <param name="append">If true, append to existing text. If false, replace with fresh text. Defaults to false.</param>
        public void StartTyping(string overrideText = null, bool append = false)
        {
            // Stop any existing typing
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            if (cursorCoroutine != null)
            {
                StopCoroutine(cursorCoroutine);
                cursorCoroutine = null;
            }

            // Base source comes from configuration / captured target
            string baseSource;
            if (useExistingTextAsSource && capturedSource != null)
            {
                baseSource = capturedSource;
            }
            else
            {
                baseSource = textToType;
            }

            // Append semantics:
            // - append=false: overrideText replaces the base source
            // - append=true : overrideText is appended to the base source ("type base + extra")
            // Treat empty-string override as "no override" so "" and null behave the same.
            string extra = string.IsNullOrEmpty(overrideText) ? string.Empty : overrideText;

            string source = append ? (baseSource + extra) : (string.IsNullOrEmpty(overrideText) ? baseSource : overrideText);

            // Instant mode: cps == 0 is explicit "dump" behavior. Also treat overly high cps as dump.
            bool instant = charactersPerSecond <= 0f || charactersPerSecond > MaxCharactersPerSecond;
            if (instant)
            {
                StopTyping(manualStop: false);
                target.text = source;
                return;
            }

            // Clear text if not appending
            if (!append)
            {
                target.text = "";
            }

            // Start typing coroutine
            if (showCursor)
            {
                cursorCoroutine = StartCoroutine(CursorBlinkRoutine());
            }
            typingCoroutine = StartCoroutine(TypeRoutine(source, append));
        }

        /// <summary>
        /// Immediately finishes typing and shows the full text.
        /// </summary>
        public void SkipToEnd(string overrideText = null)
        {
            string sourceToShow;
            if (overrideText != null)
            {
                sourceToShow = overrideText;
            }
            else if (useExistingTextAsSource && capturedSource != null)
            {
                sourceToShow = capturedSource;
            }
            else
            {
                sourceToShow = textToType;
            }
            StopTyping(manualStop: false);
            target.text = sourceToShow;
        }

        /// <summary>
        /// Stop typing. Optionally restores preserved original text.
        /// </summary>
        public void StopTyping(bool manualStop = true)
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            if (cursorCoroutine != null)
            {
                StopCoroutine(cursorCoroutine);
                cursorCoroutine = null;
            }
            isTyping = false;
            cursorVisible = false;
        }

        /// <summary>
        /// Delete characters from the start of the text.
        /// </summary>
        /// <param name="count">Number of characters to delete from the start. If 0 or negative, deletes entire text.</param>
        public void Delete(int count = 0)
        {
            StopTyping(manualStop: false);
            string current = target.text;
            if (count <= 0)
            {
                // Delete entire text
                target.text = "";
            }
            else
            {
                // Delete from start up to count characters
                int deleteCount = Mathf.Min(count, current.Length);
                target.text = current.Substring(deleteCount);
            }
        }

        /// <summary>
        /// Delete characters from the end of the text (backspace).
        /// </summary>
        /// <param name="count">Number of characters to delete from the end. If 0 or negative, deletes entire text.</param>
        public void Backspace(int count = 0)
        {
            StopTyping(manualStop: false);
            string current = target.text;
            if (count <= 0)
            {
                // Delete entire text
                target.text = "";
            }
            else
            {
                // Delete from end, up to count characters
                int deleteCount = Mathf.Min(count, current.Length);
                target.text = current.Substring(0, current.Length - deleteCount);
            }
        }

        private IEnumerator TypeRoutine(string source, bool append = false)
        {
            isTyping = true;

            // In TypeRoutine we assume non-instant mode; StartTyping short-circuits the instant case.
            float cps = Mathf.Min(charactersPerSecond, MaxCharactersPerSecond);

            // Strip any visible cursor from existing text before we use it as a base
            string existing = StripCursor(target.text);

            StringBuilder output;
            int idx;

            if (append)
            {
                // Merge/fill behavior: keep what's already in the target, but skip the leading characters
                // that already match the intended source (so we don't retype them).
                output = new StringBuilder(existing);
                idx = FindLongestMatchingPrefixLength(existing, source);
            }
            else
            {
                if (insertMode && prefillWhitespace && !string.IsNullOrEmpty(source))
                {
                    output = new StringBuilder(new string(' ', source.Length));
                }
                else
                {
                    output = new StringBuilder();
                }

                idx = 0;
            }

            float charDelay = cps > 0f ? 1f / cps : 0f;
            float backspaceDelay = backspacePerSecond > 0 ? 1f / backspacePerSecond : 0f;
            while (idx < source.Length)
            {
                // Decide how many characters to emit in this batch (1..maxMultiChars) based on chance
                int batch = (UnityEngine.Random.value < multiTypingChance)
                    ? UnityEngine.Random.Range(1, Mathf.Max(1, maxMultiChars) + 1)
                    : 1;

                for (int b = 0; b < batch && idx < source.Length; b++)
                {
                    // Maybe make a typo first for this character
                    if (UnityEngine.Random.value < typoChance)
                    {
                        char wrong = glitchCharacters.Length > 0
                            ? glitchCharacters[UnityEngine.Random.Range(0, glitchCharacters.Length)]
                            : (char)UnityEngine.Random.Range(33, 126);

                        WriteChar(output, idx, wrong, append);
                        UpdateTarget(output);
                        PlayTypoSound();
                        if (typoHoldSeconds > 0f) yield return new WaitForSeconds(typoHoldSeconds);

                        // backtrack the wrong character
                        if (insertMode || append)
                        {
                            WriteChar(output, idx, ' ', append);
                            PlayDeleteSound();
                        }
                        else
                        {
                            output.Length = Mathf.Max(0, output.Length - 1);
                            PlayDeleteSound();
                        }
                        UpdateTarget(output);
                        if (backspaceDelay > 0f) yield return new WaitForSeconds(backspaceDelay);
                    }

                    // write the correct character at idx
                    WriteChar(output, idx, source[idx], append);
                    idx++;
                    UpdateTarget(output);
                    PlayTypeSound();
                    if (charDelay > 0f) yield return new WaitForSeconds(charDelay);
                }
            }

            isTyping = false;
            cursorVisible = false;

            // If we started from pre-existing text (append/merge), we may have extra trailing characters.
            // Remove them at backspace speed so we always converge exactly to the intended source.
            while (output.Length > source.Length)
            {
                output.Length = Mathf.Max(0, output.Length - 1);
                UpdateTarget(output);
                PlayDeleteSound();

                if (backspaceDelay > 0f)
                {
                    yield return new WaitForSeconds(backspaceDelay);
                }
                else
                {
                    // Backspace speed invalid => delete tail immediately.
                    continue;
                }
            }

            UpdateTarget(output); // final write without cursor
        }

        private void WriteChar(StringBuilder sb, int index, char c, bool forceInsertMode = false)
        {
            // Use insert mode if explicitly enabled OR if forceInsertMode is true (from append)
            bool useInsert = insertMode || forceInsertMode;
            if (useInsert)
            {
                if (index < sb.Length)
                {
                    sb[index] = c;
                }
                else
                {
                    // safeguard for cases when prefill is off
                    while (sb.Length < index) sb.Append(' ');
                    sb.Append(c);
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        private IEnumerator CursorBlinkRoutine()
        {
            var wait = new WaitForSeconds(blinkInterval);
            while (isTyping)
            {
                cursorVisible = !cursorVisible;
                yield return wait;
            }
            cursorVisible = false;
        }

        private void UpdateTarget(StringBuilder content)
        {
            if (showCursor && cursorVisible && isTyping)
            {
                target.text = content + cursorChar.ToString();
            }
            else
            {
                target.text = content.ToString();
            }
        }

        private string StripCursor(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Only strip a trailing cursor character if configured to show the cursor
            if (showCursor && text.Length > 0 && text[text.Length - 1] == cursorChar)
            {
                return text.Substring(0, text.Length - 1);
            }

            return text;
        }

        private int FindLongestMatchingPrefixLength(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0;

            int len = Mathf.Min(a.Length, b.Length);
            int i = 0;
            for (; i < len; i++)
            {
                if (a[i] != b[i])
                    break;
            }
            return i;
        }

        private void PlayTypeSound()
        {
            if (audioSource == null || typeSound == null) return;
            audioSource.PlayOneShot(typeSound);
        }

        private void PlayDeleteSound()
        {
            if (audioSource == null || deleteSound == null) return;
            audioSource.PlayOneShot(deleteSound);
        }

        private void PlayTypoSound()
        {
            if (audioSource == null || typoSound == null) return;
            audioSource.PlayOneShot(typoSound);
        }
    }
}
