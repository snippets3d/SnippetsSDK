using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Implements the playback of the audio of a snippet using an AudioSource component.
    /// </summary>
    public class SnippetAudioSourceSoundPlayer : SnippetSoundPlayer
    {
        /// <summary>
        /// The AudioSource component used for playing the audio clip.
        /// </summary>
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

        /// <summary>
        /// Stops the playback of the element.
        /// </summary>
        /// <param name="notifyEvent">True to send the stop event, false otherwise</param>
        private void StopInternal(bool notifyEvent = true)
        {
            m_audioSource.Stop();
            IsPlaying = false;

            if(notifyEvent)
                PlaybackStopped?.Invoke();
        }

        /// <summary>
        /// Update is called once per frame
        /// </summary>
        private void Update()
        {
            //check if audio has finished playing and if so, trigger the stop event
            if (IsPlaying && !m_audioSource.isPlaying)
            {
                Stop();
            }
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
