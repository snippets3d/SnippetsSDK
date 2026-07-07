using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snippets.Sdk
{
    /// <summary>
    /// Implements the playback of the text of a snippet using TextMeshPro text components.
    /// </summary>
    public class SnippetTmpTextPlayer : SnippetTextPlayer
    {
        private struct WordRange
        {
            public WordRange(int startIndex, int endExclusive)
            {
                StartIndex = startIndex;
                EndExclusive = endExclusive;
            }

            public int StartIndex { get; }
            public int EndExclusive { get; }
        }

        private struct SentenceRange
        {
            public SentenceRange(int startWordIndex, int endWordIndex, int startCharIndex, int endCharIndex)
            {
                StartWordIndex = startWordIndex;
                EndWordIndex = endWordIndex;
                StartCharIndex = startCharIndex;
                EndCharIndex = endCharIndex;
            }

            public int StartWordIndex { get; }
            public int EndWordIndex { get; }
            public int StartCharIndex { get; }
            public int EndCharIndex { get; }
        }

        [SerializeField]
        private TMP_Text m_textView;

        [SerializeField]
        private bool m_disableTextWhenNotPlaying;

        [Header("Background Box (Optional)")]
        [Tooltip("Optional SpriteRenderer used as the background box behind the text.")]
        [SerializeField]
        private SpriteRenderer m_backgroundBoxRenderer;

        [Tooltip("If assigned, the SpriteRenderer will be enabled/disabled together with the text.")]
        [SerializeField]
        private bool m_disableBackgroundBoxWhenNotPlaying = true;

        [Header("Timed Text")]
        [SerializeField]
        private SnippetTextDisplayMode m_displayMode = SnippetTextDisplayMode.HighlightAsSpoken;

        [SerializeField]
        private SnippetTextSegmentationMode m_segmentationMode = SnippetTextSegmentationMode.Sentences;

        [Tooltip("Used by highlight and subtitle-style modes.")]
        [SerializeField]
        [Min(1)]
        private int m_highlightedWordCount = 3;

        [SerializeField]
        [Range(0f, 0.5f)]
        private float m_highlightLagSeconds = 0.08f;

        [SerializeField]
        private Color m_highlightMarkColor = new Color32(0x1D, 0x94, 0xFF, 0x1A);

        [SerializeField]
        private AudioSource m_timingAudioSource;

        [Header("Screen Subtitles")]
        [Tooltip("Optional screen-space subtitle target. If left empty, one is created automatically when needed.")]
        [SerializeField]
        private TextMeshProUGUI m_screenSubtitleTextView;

        [SerializeField]
        private bool m_autoCreateScreenSubtitleView = true;

        [SerializeField]
        [Min(1f)]
        private float m_screenSubtitleFontSize = 42f;

        [SerializeField]
        [Min(0f)]
        private float m_screenSubtitleBottomMargin = 72f;

        [SerializeField]
        [Min(0f)]
        private float m_screenSubtitleHorizontalMargin = 96f;

        private string m_rawText = string.Empty;
        private SnippetAudioTimestampsData m_audioTimestamps = new SnippetAudioTimestampsData();
        private readonly List<WordRange> m_wordRanges = new List<WordRange>();
        private readonly List<SentenceRange> m_sentenceRanges = new List<SentenceRange>();
        private int[] m_audioToTextCharIndex;
        private float m_localPlaybackStartTime;
        private int m_lastRenderKey = int.MinValue;
        private TextMeshProUGUI m_runtimeCreatedScreenSubtitleTextView;
        private GameObject m_runtimeCreatedScreenSubtitleCanvas;

        public override string Value
        {
            get => m_rawText;
            set
            {
                m_rawText = value ?? string.Empty;
                RebuildWordRanges(m_rawText);
                RebuildSentenceRanges(m_rawText);
                BuildAudioToTextCharMap();
                ResetRenderState();
                ApplyInitialTextState();
            }
        }

        public override bool IsPlaying { get; protected set; }

        public override void SetSnippetData(SnippetData snippetData)
        {
            m_audioTimestamps = snippetData != null && snippetData.AudioTimestamps != null
                ? new SnippetAudioTimestampsData(snippetData.AudioTimestamps)
                : new SnippetAudioTimestampsData();

            Value = snippetData != null ? snippetData.Text : string.Empty;
        }

        public override void Play()
        {
            m_localPlaybackStartTime = Time.unscaledTime;
            ResetRenderState();
            IsPlaying = true;
            ApplyInitialTextState();
            PlaybackStarted?.Invoke();
        }

        public override void Stop()
        {
            StopInternal();
        }

        private void Awake()
        {
            if (m_backgroundBoxRenderer == null)
            {
                Transform background = transform.Find("BackgroundBox");
                if (background != null)
                {
                    m_backgroundBoxRenderer = background.GetComponent<SpriteRenderer>();
                }
            }

            if (m_timingAudioSource == null)
            {
                SnippetPlayer snippetPlayer = GetComponentInParent<SnippetPlayer>();
                if (snippetPlayer != null)
                {
                    m_timingAudioSource = snippetPlayer.GetComponentInChildren<AudioSource>(true);
                }
            }

            StopInternal(false);
        }

        private void OnDestroy()
        {
            if (m_runtimeCreatedScreenSubtitleCanvas != null)
            {
                Destroy(m_runtimeCreatedScreenSubtitleCanvas);
            }
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            TMP_Text playbackTarget = GetPlaybackTextTarget();
            if (playbackTarget == null)
            {
                return;
            }

            if (!HasUsableTimingData())
            {
                return;
            }

            float playbackTime = GetPlaybackTimeSeconds();
            if (!TryGetCurrentTextPosition(playbackTime, out int textCharIndex, out int wordIndex))
            {
                return;
            }

            switch (m_displayMode)
            {
                case SnippetTextDisplayMode.FullText:
                {
                    int visibleStartChar = GetVisibleStartCharIndex(wordIndex);
                    int visibleEndChar = GetVisibleEndCharForSentenceAwareMode(wordIndex);
                    int key = BuildRenderKey(visibleStartChar, visibleEndChar, 0);
                    if (key == m_lastRenderKey)
                    {
                        return;
                    }

                    SetTargetText(playbackTarget, BuildSlice(visibleStartChar, visibleEndChar));
                    m_lastRenderKey = key;
                    break;
                }
                case SnippetTextDisplayMode.HighlightAsSpoken:
                {
                    GetChunkWordRange(wordIndex, out int startWord, out int endWord);
                    SentenceRange sentenceRange = GetSentenceRangeForWord(wordIndex);
                    int key = BuildRenderKey(startWord, endWord, sentenceRange.StartWordIndex + 1);
                    if (key == m_lastRenderKey)
                    {
                        return;
                    }

                    ApplyHighlight(playbackTarget, startWord, endWord, sentenceRange);
                    m_lastRenderKey = key;
                    break;
                }
                case SnippetTextDisplayMode.BuildUpText:
                {
                    int visibleStartChar = GetVisibleStartCharIndex(wordIndex);
                    int visibleEndChar = GetVisibleEndCharForWord(wordIndex);
                    int key = BuildRenderKey(visibleStartChar, visibleEndChar, 2);
                    if (key == m_lastRenderKey)
                    {
                        return;
                    }

                    SetTargetText(playbackTarget, BuildSlice(visibleStartChar, visibleEndChar));
                    m_lastRenderKey = key;
                    break;
                }
                case SnippetTextDisplayMode.RollingSubtitles:
                case SnippetTextDisplayMode.ScreenSubtitles:
                {
                    GetChunkWordRange(wordIndex, out int startWord, out int endWord);
                    int key = BuildRenderKey(startWord, endWord, 3);
                    if (key == m_lastRenderKey)
                    {
                        return;
                    }

                    SetTargetText(playbackTarget, BuildWordWindow(startWord, endWord));
                    m_lastRenderKey = key;
                    break;
                }
                case SnippetTextDisplayMode.TwoLineSubtitles:
                {
                    GetChunkWordRange(wordIndex, out int startWord, out int endWord);
                    int key = BuildRenderKey(startWord, endWord, 5);
                    if (key == m_lastRenderKey)
                    {
                        return;
                    }

                    SetTargetText(playbackTarget, BuildTwoLineWordWindow(startWord, endWord));
                    m_lastRenderKey = key;
                    break;
                }
                case SnippetTextDisplayMode.Typewriter:
                {
                    int visibleStartChar = GetVisibleStartCharForTypewriter(wordIndex);
                    int visibleEndChar = Mathf.Clamp(textCharIndex + 1, visibleStartChar, m_rawText.Length);
                    int key = BuildRenderKey(visibleStartChar, visibleEndChar, 4);
                    if (key == m_lastRenderKey)
                    {
                        return;
                    }

                    SetTargetText(playbackTarget, BuildSlice(visibleStartChar, visibleEndChar));
                    m_lastRenderKey = key;
                    break;
                }
            }
        }

        private void StopInternal(bool notifyEvent = true)
        {
            IsPlaying = false;
            ResetRenderState();
            ApplyInitialTextState();

            if (notifyEvent)
            {
                PlaybackStopped?.Invoke();
            }
        }

        private void ApplyInitialTextState()
        {
            if (m_textView != null)
            {
                m_textView.richText = true;
            }

            TMP_Text playbackTarget = IsPlaying ? GetPlaybackTextTarget() : null;
            if (playbackTarget != null)
            {
                playbackTarget.richText = true;
            }

            UpdatePresentationVisibility(IsPlaying);

            if (!IsPlaying)
            {
                if (m_textView != null && !m_disableTextWhenNotPlaying)
                {
                    SetTargetText(m_textView, m_rawText);
                }

                TMP_Text existingScreenTarget = GetScreenSubtitleTextTarget(false);
                if (existingScreenTarget != null)
                {
                    SetTargetText(existingScreenTarget, string.Empty);
                }

                return;
            }

            if (playbackTarget == null)
            {
                return;
            }

            if (!HasUsableTimingData())
            {
                SetTargetText(playbackTarget, m_rawText);
                return;
            }

            switch (m_displayMode)
            {
                case SnippetTextDisplayMode.FullText:
                    if (m_segmentationMode == SnippetTextSegmentationMode.Sentences)
                    {
                        SetTargetText(playbackTarget, string.Empty);
                    }
                    else
                    {
                        SetTargetText(playbackTarget, m_rawText);
                    }
                    break;
                case SnippetTextDisplayMode.HighlightAsSpoken:
                    if (m_segmentationMode == SnippetTextSegmentationMode.Sentences)
                    {
                        SetTargetText(playbackTarget, string.Empty);
                    }
                    else
                    {
                        SetTargetText(playbackTarget, m_rawText);
                    }
                    break;
                case SnippetTextDisplayMode.BuildUpText:
                case SnippetTextDisplayMode.RollingSubtitles:
                case SnippetTextDisplayMode.ScreenSubtitles:
                case SnippetTextDisplayMode.TwoLineSubtitles:
                case SnippetTextDisplayMode.Typewriter:
                    SetTargetText(playbackTarget, string.Empty);
                    break;
            }
        }

        private void UpdatePresentationVisibility(bool isPlaying)
        {
            bool useScreenTarget = isPlaying && IsScreenSubtitleMode();
            bool localVisible = useScreenTarget ? false : (isPlaying || !m_disableTextWhenNotPlaying);

            if (m_textView != null)
            {
                m_textView.enabled = localVisible;
            }

            if (m_backgroundBoxRenderer != null)
            {
                m_backgroundBoxRenderer.enabled = localVisible;
            }

            TMP_Text screenTarget = GetScreenSubtitleTextTarget(false);
            if (screenTarget != null)
            {
                screenTarget.enabled = useScreenTarget;
            }
        }

        private bool HasUsableTimingData()
        {
            return !string.IsNullOrEmpty(m_rawText) &&
                   m_audioTimestamps != null &&
                   m_audioTimestamps.HasUsableData &&
                   m_wordRanges.Count > 0 &&
                   m_audioToTextCharIndex != null &&
                   m_audioToTextCharIndex.Length > 0;
        }

        private bool IsScreenSubtitleMode()
        {
            return m_displayMode == SnippetTextDisplayMode.ScreenSubtitles ||
                   m_displayMode == SnippetTextDisplayMode.TwoLineSubtitles;
        }

        private TMP_Text GetPlaybackTextTarget()
        {
            return IsScreenSubtitleMode() ? GetScreenSubtitleTextTarget(true) : m_textView;
        }

        private TMP_Text GetScreenSubtitleTextTarget(bool createIfMissing)
        {
            if (m_screenSubtitleTextView != null)
            {
                return m_screenSubtitleTextView;
            }

            if (!createIfMissing || !m_autoCreateScreenSubtitleView)
            {
                return m_runtimeCreatedScreenSubtitleTextView;
            }

            if (m_runtimeCreatedScreenSubtitleTextView != null)
            {
                return m_runtimeCreatedScreenSubtitleTextView;
            }

            m_runtimeCreatedScreenSubtitleCanvas = new GameObject(
                "ScreenSubtitleCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            m_runtimeCreatedScreenSubtitleCanvas.transform.SetParent(transform, false);

            Canvas canvas = m_runtimeCreatedScreenSubtitleCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = m_runtimeCreatedScreenSubtitleCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject textObject = new GameObject(
                "ScreenSubtitleText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(m_runtimeCreatedScreenSubtitleCanvas.transform, false);

            TextMeshProUGUI screenText = textObject.GetComponent<TextMeshProUGUI>();
            RectTransform rectTransform = screenText.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.offsetMin = new Vector2(m_screenSubtitleHorizontalMargin, m_screenSubtitleBottomMargin);
            rectTransform.offsetMax = new Vector2(-m_screenSubtitleHorizontalMargin, m_screenSubtitleBottomMargin + 180f);

            if (m_textView != null)
            {
                screenText.font = m_textView.font;
                screenText.color = m_textView.color;
            }

            screenText.fontSize = m_screenSubtitleFontSize;
            screenText.alignment = TextAlignmentOptions.Bottom;
            screenText.enableWordWrapping = true;
            screenText.overflowMode = TextOverflowModes.Overflow;
            screenText.richText = true;
            screenText.text = string.Empty;
            screenText.enabled = false;

            m_runtimeCreatedScreenSubtitleTextView = screenText;
            return m_runtimeCreatedScreenSubtitleTextView;
        }

        private float GetPlaybackTimeSeconds()
        {
            if (m_timingAudioSource != null && m_timingAudioSource.clip != null)
            {
                return Mathf.Max(0f, m_timingAudioSource.time - m_highlightLagSeconds);
            }

            return Mathf.Max(0f, (Time.unscaledTime - m_localPlaybackStartTime) - m_highlightLagSeconds);
        }

        private void RebuildWordRanges(string text)
        {
            m_wordRanges.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int index = 0;
            while (index < text.Length)
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                {
                    index++;
                }

                if (index >= text.Length)
                {
                    break;
                }

                int start = index;
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                {
                    index++;
                }

                m_wordRanges.Add(new WordRange(start, index));
            }
        }

        private void RebuildSentenceRanges(string text)
        {
            m_sentenceRanges.Clear();
            if (string.IsNullOrEmpty(text) || m_wordRanges.Count == 0)
            {
                return;
            }

            int sentenceStartWordIndex = 0;
            int sentenceStartCharIndex = m_wordRanges[0].StartIndex;

            for (int i = 0; i < m_wordRanges.Count; i++)
            {
                if (!DoesWordEndSentence(text, m_wordRanges[i]))
                {
                    continue;
                }

                m_sentenceRanges.Add(new SentenceRange(
                    sentenceStartWordIndex,
                    i,
                    sentenceStartCharIndex,
                    m_wordRanges[i].EndExclusive));

                int nextWordIndex = i + 1;
                if (nextWordIndex < m_wordRanges.Count)
                {
                    sentenceStartWordIndex = nextWordIndex;
                    sentenceStartCharIndex = m_wordRanges[nextWordIndex].StartIndex;
                }
            }

            SentenceRange? lastSentence = m_sentenceRanges.Count > 0
                ? m_sentenceRanges[m_sentenceRanges.Count - 1]
                : null;

            if (!lastSentence.HasValue || lastSentence.Value.EndWordIndex < m_wordRanges.Count - 1)
            {
                int startWordIndex = lastSentence.HasValue ? lastSentence.Value.EndWordIndex + 1 : 0;
                m_sentenceRanges.Add(new SentenceRange(
                    startWordIndex,
                    m_wordRanges.Count - 1,
                    m_wordRanges[startWordIndex].StartIndex,
                    m_wordRanges[m_wordRanges.Count - 1].EndExclusive));
            }
        }

        private static bool DoesWordEndSentence(string text, WordRange wordRange)
        {
            for (int i = wordRange.EndExclusive - 1; i >= wordRange.StartIndex; i--)
            {
                char character = text[i];
                if (character == '"' || character == '\'' || character == ')' || character == ']' || character == '}')
                {
                    continue;
                }

                return character == '.' || character == '!' || character == '?';
            }

            return false;
        }

        private void BuildAudioToTextCharMap()
        {
            if (m_audioTimestamps == null || m_audioTimestamps.Characters == null || string.IsNullOrEmpty(m_rawText))
            {
                m_audioToTextCharIndex = null;
                return;
            }

            m_audioToTextCharIndex = new int[m_audioTimestamps.Characters.Count];
            int searchIndex = 0;

            for (int i = 0; i < m_audioTimestamps.Characters.Count; i++)
            {
                char audioCharacter = GetAudioCharacter(i);
                if (audioCharacter == '\0')
                {
                    m_audioToTextCharIndex[i] = Mathf.Clamp(searchIndex, 0, Mathf.Max(0, m_rawText.Length - 1));
                    continue;
                }

                int mappedIndex = FindMatchingTextIndex(audioCharacter, searchIndex);
                if (mappedIndex < 0)
                {
                    mappedIndex = Mathf.Clamp(searchIndex, 0, Mathf.Max(0, m_rawText.Length - 1));
                }

                m_audioToTextCharIndex[i] = mappedIndex;
                searchIndex = Mathf.Min(mappedIndex + 1, m_rawText.Length);
            }
        }

        private char GetAudioCharacter(int index)
        {
            if (m_audioTimestamps == null ||
                m_audioTimestamps.Characters == null ||
                index < 0 ||
                index >= m_audioTimestamps.Characters.Count ||
                string.IsNullOrEmpty(m_audioTimestamps.Characters[index]))
            {
                return '\0';
            }

            return m_audioTimestamps.Characters[index][0];
        }

        private int FindMatchingTextIndex(char audioCharacter, int searchIndex)
        {
            for (int i = Mathf.Clamp(searchIndex, 0, m_rawText.Length); i < m_rawText.Length; i++)
            {
                if (CharactersMatch(m_rawText[i], audioCharacter))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool CharactersMatch(char textCharacter, char audioCharacter)
        {
            if (textCharacter == audioCharacter)
            {
                return true;
            }

            if (char.IsWhiteSpace(textCharacter) && char.IsWhiteSpace(audioCharacter))
            {
                return true;
            }

            return char.ToLowerInvariant(textCharacter) == char.ToLowerInvariant(audioCharacter);
        }

        private bool TryGetCurrentTextPosition(float playbackTime, out int textCharIndex, out int wordIndex)
        {
            textCharIndex = -1;
            wordIndex = -1;

            if (!HasUsableTimingData())
            {
                return false;
            }

            int count = Mathf.Min(
                m_audioTimestamps.CharacterStartTimes.Count,
                m_audioTimestamps.CharacterEndTimes.Count,
                m_audioToTextCharIndex.Length);

            int activeCharIndex = -1;
            for (int i = 0; i < count; i++)
            {
                float start = m_audioTimestamps.CharacterStartTimes[i];
                float end = m_audioTimestamps.CharacterEndTimes[i];
                if (playbackTime < start)
                {
                    break;
                }

                if (playbackTime <= end)
                {
                    activeCharIndex = i;
                    break;
                }

                activeCharIndex = i;
            }

            if (activeCharIndex < 0)
            {
                return false;
            }

            textCharIndex = Mathf.Clamp(m_audioToTextCharIndex[activeCharIndex], 0, Mathf.Max(0, m_rawText.Length - 1));
            wordIndex = FindWordIndexForTextCharIndex(textCharIndex);
            return wordIndex >= 0;
        }

        private int FindWordIndexForTextCharIndex(int textCharIndex)
        {
            for (int i = 0; i < m_wordRanges.Count; i++)
            {
                WordRange range = m_wordRanges[i];
                if (textCharIndex >= range.StartIndex && textCharIndex < range.EndExclusive)
                {
                    return i;
                }

                if (textCharIndex < range.StartIndex)
                {
                    return Mathf.Max(0, i - 1);
                }
            }

            return m_wordRanges.Count > 0 ? m_wordRanges.Count - 1 : -1;
        }

        private void GetChunkWordRange(int currentWordIndex, out int startWordIndex, out int endWordIndex)
        {
            int wordsPerChunk = Mathf.Max(1, m_highlightedWordCount);
            int clampedWordIndex = Mathf.Clamp(currentWordIndex, 0, Mathf.Max(0, m_wordRanges.Count - 1));

            if (m_segmentationMode == SnippetTextSegmentationMode.Sentences)
            {
                SentenceRange sentenceRange = GetSentenceRangeForWord(clampedWordIndex);
                int relativeWordIndex = clampedWordIndex - sentenceRange.StartWordIndex;
                startWordIndex = sentenceRange.StartWordIndex + (relativeWordIndex / wordsPerChunk) * wordsPerChunk;
                endWordIndex = Mathf.Min(startWordIndex + wordsPerChunk - 1, sentenceRange.EndWordIndex);
                return;
            }

            startWordIndex = (clampedWordIndex / wordsPerChunk) * wordsPerChunk;
            endWordIndex = Mathf.Min(startWordIndex + wordsPerChunk - 1, m_wordRanges.Count - 1);
        }

        private int GetVisibleStartCharIndex(int currentWordIndex)
        {
            if (m_segmentationMode != SnippetTextSegmentationMode.Sentences)
            {
                return 0;
            }

            return GetSentenceRangeForWord(currentWordIndex).StartCharIndex;
        }

        private int GetVisibleStartCharForTypewriter(int currentWordIndex)
        {
            return GetVisibleStartCharIndex(currentWordIndex);
        }

        private int GetVisibleEndCharForWord(int currentWordIndex)
        {
            if (m_wordRanges.Count == 0)
            {
                return 0;
            }

            int clampedWordIndex = Mathf.Clamp(currentWordIndex, 0, m_wordRanges.Count - 1);
            return m_wordRanges[clampedWordIndex].EndExclusive;
        }

        private int GetVisibleEndCharForSentenceAwareMode(int currentWordIndex)
        {
            if (m_segmentationMode != SnippetTextSegmentationMode.Sentences)
            {
                return m_rawText.Length;
            }

            return GetSentenceRangeForWord(currentWordIndex).EndCharIndex;
        }

        private SentenceRange GetSentenceRangeForWord(int wordIndex)
        {
            if (m_sentenceRanges.Count == 0)
            {
                if (m_wordRanges.Count == 0)
                {
                    return new SentenceRange(0, 0, 0, 0);
                }

                return new SentenceRange(0, m_wordRanges.Count - 1, m_wordRanges[0].StartIndex, m_wordRanges[m_wordRanges.Count - 1].EndExclusive);
            }

            for (int i = 0; i < m_sentenceRanges.Count; i++)
            {
                SentenceRange sentenceRange = m_sentenceRanges[i];
                if (wordIndex >= sentenceRange.StartWordIndex && wordIndex <= sentenceRange.EndWordIndex)
                {
                    return sentenceRange;
                }
            }

            return m_sentenceRanges[m_sentenceRanges.Count - 1];
        }

        private void ApplyHighlight(TMP_Text target, int startWordIndex, int endWordIndex, SentenceRange sentenceRange)
        {
            if (m_wordRanges.Count == 0)
            {
                SetTargetText(target, m_rawText);
                return;
            }

            int clampedStartWord = Mathf.Clamp(startWordIndex, 0, m_wordRanges.Count - 1);
            int clampedEndWord = Mathf.Clamp(endWordIndex, clampedStartWord, m_wordRanges.Count - 1);
            int windowStart = m_segmentationMode == SnippetTextSegmentationMode.Sentences ? sentenceRange.StartCharIndex : 0;
            int windowEnd = m_segmentationMode == SnippetTextSegmentationMode.Sentences ? sentenceRange.EndCharIndex : m_rawText.Length;
            int highlightStart = m_wordRanges[clampedStartWord].StartIndex;
            int highlightEnd = m_wordRanges[clampedEndWord].EndExclusive;

            var builder = new StringBuilder((windowEnd - windowStart) + 64);
            builder.Append(m_rawText, windowStart, highlightStart - windowStart);
            builder.Append("<mark=").Append(ToMarkHex(m_highlightMarkColor)).Append(">");
            builder.Append("<color=#FFFFFFFF>");
            builder.Append(m_rawText, highlightStart, highlightEnd - highlightStart);
            builder.Append("</color></mark>");

            if (highlightEnd < windowEnd)
            {
                builder.Append(m_rawText, highlightEnd, windowEnd - highlightEnd);
            }

            SetTargetText(target, builder.ToString());
        }

        private string BuildWordWindow(int startWordIndex, int endWordIndex)
        {
            if (m_wordRanges.Count == 0)
            {
                return m_rawText;
            }

            int clampedStartWord = Mathf.Clamp(startWordIndex, 0, m_wordRanges.Count - 1);
            int clampedEndWord = Mathf.Clamp(endWordIndex, clampedStartWord, m_wordRanges.Count - 1);
            int startChar = m_wordRanges[clampedStartWord].StartIndex;
            int endChar = m_wordRanges[clampedEndWord].EndExclusive;
            return BuildSlice(startChar, endChar);
        }

        private string BuildTwoLineWordWindow(int startWordIndex, int endWordIndex)
        {
            if (m_wordRanges.Count == 0)
            {
                return m_rawText;
            }

            int clampedStartWord = Mathf.Clamp(startWordIndex, 0, m_wordRanges.Count - 1);
            int clampedEndWord = Mathf.Clamp(endWordIndex, clampedStartWord, m_wordRanges.Count - 1);
            int wordCount = clampedEndWord - clampedStartWord + 1;
            if (wordCount <= 2)
            {
                return BuildWordWindow(clampedStartWord, clampedEndWord);
            }

            int splitWord = FindBestTwoLineSplit(clampedStartWord, clampedEndWord);
            string firstLine = BuildWordWindow(clampedStartWord, splitWord);
            string secondLine = BuildWordWindow(splitWord + 1, clampedEndWord);
            if (string.IsNullOrEmpty(secondLine))
            {
                return firstLine;
            }

            return firstLine + "\n" + secondLine;
        }

        private int FindBestTwoLineSplit(int startWordIndex, int endWordIndex)
        {
            int bestSplit = startWordIndex;
            int bestBalance = int.MaxValue;

            for (int split = startWordIndex; split < endWordIndex; split++)
            {
                int firstLength = GetWordSpanCharacterLength(startWordIndex, split);
                int secondLength = GetWordSpanCharacterLength(split + 1, endWordIndex);
                int balance = Mathf.Abs(firstLength - secondLength);
                if (balance < bestBalance)
                {
                    bestBalance = balance;
                    bestSplit = split;
                }
            }

            return bestSplit;
        }

        private int GetWordSpanCharacterLength(int startWordIndex, int endWordIndex)
        {
            if (m_wordRanges.Count == 0 || startWordIndex > endWordIndex)
            {
                return 0;
            }

            int clampedStartWord = Mathf.Clamp(startWordIndex, 0, m_wordRanges.Count - 1);
            int clampedEndWord = Mathf.Clamp(endWordIndex, clampedStartWord, m_wordRanges.Count - 1);
            int startChar = m_wordRanges[clampedStartWord].StartIndex;
            int endChar = m_wordRanges[clampedEndWord].EndExclusive;
            return Mathf.Max(0, endChar - startChar);
        }

        private string BuildSlice(int startCharIndex, int endCharIndex)
        {
            if (string.IsNullOrEmpty(m_rawText))
            {
                return string.Empty;
            }

            int clampedStart = Mathf.Clamp(startCharIndex, 0, m_rawText.Length);
            int clampedEnd = Mathf.Clamp(endCharIndex, clampedStart, m_rawText.Length);
            if (clampedEnd <= clampedStart)
            {
                return string.Empty;
            }

            return m_rawText.Substring(clampedStart, clampedEnd - clampedStart);
        }

        private static string ToMarkHex(Color color)
        {
            Color32 color32 = color;
            return $"#{color32.r:X2}{color32.g:X2}{color32.b:X2}{color32.a:X2}";
        }

        private static int BuildRenderKey(int firstValue, int secondValue, int salt)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + firstValue;
                hash = hash * 31 + secondValue;
                hash = hash * 31 + salt;
                return hash;
            }
        }

        private void ResetRenderState()
        {
            m_lastRenderKey = int.MinValue;
        }

        private static void SetTargetText(TMP_Text target, string text)
        {
            if (target == null)
            {
                return;
            }

            target.text = text ?? string.Empty;
        }
    }
}
