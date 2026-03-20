using UnityEngine;

namespace Snippets.Sdk
{
    public class SnippetAudioSourceSoundPlayer : SnippetSoundPlayer
    {
        [SerializeField]
        private AudioSource m_audioSource;

        /// <inheritdoc />
        public override AudioClip Value
        {
            get => m_audioSource.clip;
            set => m_audioSource.clip = value;
        }

        /// <inheritdoc />
        public override bool IsPlaying { get; protected set; }

        /// <inheritdoc />
        public override void Play()
        {
            if (m_audioSource.clip != null && !m_audioSource.isPlaying)
            {
                m_audioSource.Play();
                IsPlaying = true;
                PlaybackStarted?.Invoke();
            }
        }

        /// <inheritdoc />
        public override void Stop()
        {
            StopInternal();
        }

        private void StopInternal(bool notifyEvent = true)
        {
            m_audioSource.Stop();
            IsPlaying = false;

            if(notifyEvent)
                PlaybackStopped?.Invoke();
        }

        private void Update()
        {
            if (IsPlaying && !m_audioSource.isPlaying)
            {
                Stop();
            }
        }

        private void Awake()
        {
            StopInternal(false);
        }
    }
}
