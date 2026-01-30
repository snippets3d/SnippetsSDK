using UnityEngine;
using UnityEngine.Events;

namespace Snippets.Sdk
{
    /// <summary>
    /// Base class for the playback of the audio component of a snippet.
    /// </summary>
    public abstract class SnippetSoundPlayer : MonoBehaviour, IPlayablePresenter<AudioClip>
    {
        /// <inheritdoc />
        public abstract AudioClip Value { get; set; }

        /// <inheritdoc />
        public abstract bool IsPlaying { get; protected set; }

        /// <inheritdoc />
        public abstract void Play();

        /// <inheritdoc />
        public abstract void Stop();

        /// <inheritdoc />
        [field: SerializeField]
        public UnityEvent PlaybackStarted { get; set; } = new UnityEvent();

        /// <inheritdoc />
        [field: SerializeField]
        public UnityEvent PlaybackStopped { get; set; } = new UnityEvent();
    }
}
