using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Optional speech timing data used to synchronize text presentation with snippet audio.
    /// </summary>
    [Serializable]
    public class SnippetAudioTimestampsData
    {
        [field: SerializeField]
        public List<string> Characters { get; set; } = new List<string>();

        [field: SerializeField]
        public List<float> CharacterStartTimes { get; set; } = new List<float>();

        [field: SerializeField]
        public List<float> CharacterEndTimes { get; set; } = new List<float>();

        /// <summary>
        /// Gets whether the timing payload contains enough information to drive timed text.
        /// </summary>
        public bool HasUsableData =>
            Characters != null &&
            CharacterStartTimes != null &&
            CharacterEndTimes != null &&
            Characters.Count > 0 &&
            CharacterStartTimes.Count > 0 &&
            CharacterEndTimes.Count > 0;

        public SnippetAudioTimestampsData()
        {
        }

        public SnippetAudioTimestampsData(SnippetAudioTimestampsData other)
        {
            if (other == null)
            {
                return;
            }

            Characters = other.Characters != null ? new List<string>(other.Characters) : new List<string>();
            CharacterStartTimes = other.CharacterStartTimes != null ? new List<float>(other.CharacterStartTimes) : new List<float>();
            CharacterEndTimes = other.CharacterEndTimes != null ? new List<float>(other.CharacterEndTimes) : new List<float>();
        }
    }
}
