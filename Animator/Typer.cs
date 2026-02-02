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

            string source;
            if (overrideText != null)
            {
                source = overrideText;
            }
            else if (useExistingTextAsSource && capturedSource != null)
            {
                source = capturedSource;
            }
            else
            {
                source = textToType;
            }

            // If configured, pre-populate the target with whitespace immediately
            if (insertMode && prefillWhitespace && source != null)
            {
                try { target.text = new string(' ', source.Length); } catch { }
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

        private IEnumerator TypeRoutine(string source, bool append = false)
        {
            isTyping = true;

            StringBuilder output;
            if (append)
            {
                // Append mode: start with existing text
                output = new StringBuilder(target.text);
            }
            else if (insertMode && prefillWhitespace)
            {
                // Fresh overwrite mode with insert: prefill with spaces
                output = new StringBuilder(new string(' ', source.Length));
            }
            else
            {
                // Fresh overwrite mode: start empty
                output = new StringBuilder();
            }

            float charDelay = charactersPerSecond > 0 ? 1f / charactersPerSecond : 0f;
            float backspaceDelay = backspacePerSecond > 0 ? 1f / backspacePerSecond : 0f;

            int idx = 0;
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

                        WriteChar(output, idx, wrong);
                        UpdateTarget(output);
                        PlayTypoSound();
                        if (typoHoldSeconds > 0f) yield return new WaitForSeconds(typoHoldSeconds);

                        // backtrack the wrong character
                        if (insertMode)
                        {
                            WriteChar(output, idx, ' ');
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
                    WriteChar(output, idx, source[idx]);
                    idx++;
                    UpdateTarget(output);
                    PlayTypeSound();
                    if (charDelay > 0f) yield return new WaitForSeconds(charDelay);
                }
            }

            isTyping = false;
            cursorVisible = false;
            UpdateTarget(output); // final write without cursor
        }

        private void WriteChar(StringBuilder sb, int index, char c)
        {
            if (insertMode)
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
