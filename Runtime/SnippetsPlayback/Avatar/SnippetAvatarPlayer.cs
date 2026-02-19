using UnityEngine;
using UnityEngine.Events;

namespace Snippets.Sdk
{
    /// <summary>
    /// Base class for the playback of the animation component of a snippet.
    /// </summary>
    public abstract class SnippetAvatarPlayer : MonoBehaviour, IPlayablePresenter<AnimationClip>
    {
        /// <inheritdoc />
        public abstract AnimationClip Value { get; set; }

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
