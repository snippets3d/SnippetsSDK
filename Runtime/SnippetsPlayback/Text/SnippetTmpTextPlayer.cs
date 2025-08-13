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
                m_textView.enabled = true;

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
                m_textView.enabled = false;

            IsPlaying = false;

            if (notifyEvent)
                PlaybackStopped?.Invoke();
        }

        /// <summary>
        /// Awake
        /// </summary>
        private void Awake()
        {
            StopInternal(false);
        }
    }
}
