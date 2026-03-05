using TMPro;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Implements the playback of the text of a snippet using TextMeshPro text component.
    /// </summary>
    public class SnippetTmpTextPlayer : SnippetTextPlayer
    {
        /// <summary>
        /// The TextMeshPro text component used for displaying the text.
        /// </summary>
        [SerializeField]
        private TMP_Text m_textView;

        /// <summary>
        /// True if the text should be disabled when not playing, false to not make this
        /// object change the activation status of the text component 
        /// (the developer should handle it manually)
        /// </summary>
        [SerializeField]
        private bool m_disableTextWhenNotPlaying;

        // ================= BACKGROUND BOX =================

        [Header("Background Box (Optional)")]
        [Tooltip("Optional SpriteRenderer used as the background box behind the text.")]
        [SerializeField]
        private SpriteRenderer m_backgroundBoxRenderer;

        [Tooltip("If assigned, the SpriteRenderer will be enabled/disabled together with the text (when m_disableTextWhenNotPlaying is true).")]
        [SerializeField]
        private bool m_disableBackgroundBoxWhenNotPlaying = true;

        /// <inheritdoc />
        public override string Value
        {
            get => m_textView.text;
            set => m_textView.text = value;
        }

        /// <inheritdoc />
        public override bool IsPlaying { get; protected set; }

        /// <inheritdoc />
        public override void Play()
        {
            // enable the text, if requested
            if (m_disableTextWhenNotPlaying)
            {
                m_textView.enabled = true;

                // enable background box too, if requested
                if (m_disableBackgroundBoxWhenNotPlaying && m_backgroundBoxRenderer != null)
                    m_backgroundBoxRenderer.enabled = true;
            }

            IsPlaying = true;
            PlaybackStarted?.Invoke();
        }

        /// <inheritdoc />
        public override void Stop()
        {
            StopInternal();
        }

        /// <summary>
        /// Stops the playback of the text.
        /// </summary>
        /// <param name="notifyEvent">True to send the stop event, false otherwise</param>
        private void StopInternal(bool notifyEvent = true)
        {
            // disable the text, if requested
            if (m_disableTextWhenNotPlaying)
            {
                m_textView.enabled = false;

                // disable background box too, if requested
                if (m_disableBackgroundBoxWhenNotPlaying && m_backgroundBoxRenderer != null)
                    m_backgroundBoxRenderer.enabled = false;
            }

            IsPlaying = false;

            if (notifyEvent)
                PlaybackStopped?.Invoke();
        }

        /// <summary>
        /// Awake
        /// </summary>
        private void Awake()
        {
            // Auto-wire BackgroundBox if not assigned
            if (m_backgroundBoxRenderer == null)
            {
                var bg = transform.Find("BackgroundBox");
                if (bg != null)
                    m_backgroundBoxRenderer = bg.GetComponent<SpriteRenderer>();
            }

            StopInternal(false);
        }
    }
}
