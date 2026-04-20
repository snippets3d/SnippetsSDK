using UnityEngine.Events;

namespace Snippets.Sdk
{
    /// <summary>
    /// Represents a generic interface for presenters that can playback some kind of content
    /// </summary>
    /// <typeparam name="TValueType">The type of content to be played.</typeparam>
    public interface IPlayablePresenter<TValueType>
    {
        /// <summary>
        /// Gets or sets the value this presenter has to show (and hence, play).
        /// </summary>
        TValueType Value { get; set; }

        /// <summary>
        /// True if the content is currently playing, false otherwise.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Plays the content represented by the <see cref="Value"> property.
        /// </summary>
        void Play();

        /// <summary>
        /// Stops the playback of the content represented by the <see cref="Value"> property
        /// </summary>
        void Stop();

        /// <summary>
        /// Event that is triggered when the playback starts
        /// </summary>
        UnityEvent PlaybackStarted { get; set; }

        /// <summary>
        /// Event that is triggered when the playback stops
        /// </summary>
        UnityEvent PlaybackStopped { get; set; }
    }
}
