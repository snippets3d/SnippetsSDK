using System;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Complete data of a set of snippets.
    /// </summary>
    [Serializable]
    public class SnippetsSetData
    {
        /// <summary>
        /// Gets or sets the metadata for the snippet set.
        /// </summary>
        [field: SerializeField]
        public SnippetsSetMetadata Metadata { get; set; }

        /// <summary>
        /// The Snippets associated with the set.
        /// </summary>
        [field: SerializeField]
        public SnippetData[] Snippets { get; set; }

        /// <summary>
        /// Simple constructor
        /// </summary>
        public SnippetsSetData()
        {
            Metadata = new SnippetsSetMetadata();
            Snippets = new SnippetData[0];
        }

#if UNITY_EDITOR

        /// <summary>
        /// Constructor starting from a serialized DTO
        /// </summary>
        /// <param name="setDto">Serializable version of the set</param>
        public SnippetsSetData(SnippetsSetDataDto setDto)
        {
            Metadata = new SnippetsSetMetadata(setDto.Metadata);
            Snippets = new SnippetData[setDto.Snippets.Length];

            for (int i = 0; i < setDto.Snippets.Length; i++)
            {
                Snippets[i] = new SnippetData(setDto.Snippets[i]);
            }
        }

#endif
    }
}

