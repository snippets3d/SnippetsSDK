using UnityEngine;
using UnityEngine.Events;

namespace Snippets.Sdk
{
    /// <summary>
    /// Manages the playback of a snippet, coordinating the playback 
    /// of all its components (text, sound, and animation).
    /// </summary>
    [DefaultExecutionOrder(+1)] //so that when Awake is executed, all the associated sub-players are already initialized
    public class SnippetPlayer : MonoBehaviour, IPlayablePresenter<SnippetData>
    {
        /// <summary>
        /// The element responsible for playing the text component of the snippet.
        /// It can be null if we do not want to show the text of the snippet.
        /// </summary>
        [SerializeField]
        private SnippetTextPlayer m_snippetTextPlayer;

        /// <summary>
        /// The element responsible for playing the animation component of the snippet.
        /// It can be null if we do not want to play the audio of the snippet.
        /// </summary>
        [SerializeField]
        private SnippetSoundPlayer m_snippetSoundPlayer;

        /// <summary>
        /// The element responsible for playing the animation component of the snippet.
        /// It can be null if we do not want to play the avatar animation of the snippet.
        /// </summary>
        [SerializeField]
        private SnippetAvatarPlayer m_snippetAvatarPlayer;

        /// <summary>
        /// Defines whether the snippet should play automatically on the enable of the component.
        /// </summary>
        [SerializeField]
        private bool m_playOnEnable;

        /// <summary>
        /// The snippet data to be played.
        /// </summary>
        [SerializeField]
        private SnippetData m_value;

        /// <inheritdoc />
        public SnippetData Value
        {
            get => m_value;
            set
            {
                m_value = value;
                StopInternal(false); //if we are changing the snippet, we need to stop the currently playing one, if any

                // set the value of the components
                if (m_snippetTextPlayer != null)
                    m_snippetTextPlayer.Value = m_value.Text;

                if (m_snippetSoundPlayer != null)
                    m_snippetSoundPlayer.Value = m_value.Sound;

                if (m_snippetAvatarPlayer != null)
                    m_snippetAvatarPlayer.Value = m_value.Animation;
            }
        }

        /// <inheritdoc />
        public bool IsPlaying { get; protected set; }

        /// <inheritdoc />
        [field: SerializeField]
        public UnityEvent PlaybackStarted { get; set; } = new UnityEvent();

        /// <inheritdoc />
        [field: SerializeField]
        public UnityEvent PlaybackStopped { get; set; } = new UnityEvent();

        /// <inheritdoc />
        public void Play()
        {
            // play all the components of the snippet
            if (m_value != null && m_value.IsValid)
            {
                if (m_snippetTextPlayer != null)
                    m_snippetTextPlayer.Play();

                if (m_snippetSoundPlayer != null)
                    m_snippetSoundPlayer.Play();

                if (m_snippetAvatarPlayer != null)
                    m_snippetAvatarPlayer.Play();

                IsPlaying = true;
                PlaybackStarted?.Invoke();
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            StopInternal();
        }

        /// <summary>
        /// Stops the playback of the element.
        /// </summary>
        /// <param name="notifyEvent">True to send the stop event, false otherwise</param>
        private void StopInternal(bool notifyEvent = true)
        {
            // stop all the components of the snippet
            if (m_snippetTextPlayer != null && m_snippetTextPlayer.IsPlaying)
                m_snippetTextPlayer.Stop();

            if (m_snippetSoundPlayer != null && m_snippetSoundPlayer.IsPlaying)
                m_snippetSoundPlayer.Stop();

            if (m_snippetAvatarPlayer != null && m_snippetAvatarPlayer.IsPlaying)
                m_snippetAvatarPlayer.Stop();

            IsPlaying = false;

            if(notifyEvent)
                PlaybackStopped?.Invoke();
        }

        /// <summary>
        /// Awake
        /// </summary>
        private void Awake()
        {
            // set the initial value, if any
            if (m_value != null && m_value.IsValid)
                Value = m_value;
            // if there is no value, still initialize the system by stopping all playbacks
            else
                StopInternal(false);
        }

        /// <summary>
        /// On Enable
        /// </summary>
        private void OnEnable()
        {
            // Subscribe to the PlaybackStopped event of the sub-players
            if (m_snippetSoundPlayer != null)
            {
                m_snippetSoundPlayer.PlaybackStopped.AddListener(OnSubPlayerStopped);
            }
            else if (m_snippetAvatarPlayer != null)
            {
                m_snippetAvatarPlayer.PlaybackStopped.AddListener(OnSubPlayerStopped);
            }
            else if (m_snippetTextPlayer != null)
            {
                m_snippetTextPlayer.PlaybackStopped.AddListener(OnSubPlayerStopped);
            }

            if (m_playOnEnable)
            {
                Play();
            }
        }

        /// <summary>
        /// On Disable
        /// </summary>
        private void OnDisable()
        {
            // Unsubscribe from the PlaybackStopped event of the sub-players
            if (m_snippetSoundPlayer != null)
            {
                m_snippetSoundPlayer.PlaybackStopped.RemoveListener(OnSubPlayerStopped);
            }
            else if (m_snippetAvatarPlayer != null)
            {
                m_snippetAvatarPlayer.PlaybackStopped.RemoveListener(OnSubPlayerStopped);
            }
            else if (m_snippetTextPlayer != null)
            {
                m_snippetTextPlayer.PlaybackStopped.RemoveListener(OnSubPlayerStopped);
            }
        }

        /// <summary>
        /// Called when a sub-player stops playback
        /// </summary>
        private void OnSubPlayerStopped()
        {
            Stop();
        }
    }
}
