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

        /// <summary>
        /// Assigns whether animation playback is owned by an external controller for the current runtime context.
        /// </summary>
        public virtual void SetAnimationDrivenExternally(bool isExternallyDriven)
        {
        }

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
