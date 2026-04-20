using UnityEngine;
using UnityEngine.Events;

namespace Snippets.Sdk
{
    /// <summary>
    /// Base class for the playback of the text component of a snippet.
    /// </summary>
    public abstract class SnippetTextPlayer : MonoBehaviour, IPlayablePresenter<string>
    {
        /// <summary>
        /// Provides the full snippet data to the text player, including optional timed-text metadata.
        /// </summary>
        /// <param name="snippetData">Snippet data to present.</param>
        public virtual void SetSnippetData(SnippetData snippetData)
        {
            Value = snippetData != null ? snippetData.Text : string.Empty;
        }

        /// <inheritdoc />
        public abstract string Value { get; set; }

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
