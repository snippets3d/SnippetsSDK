using System;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Serializable form of the data of a set of snippets.
    /// It is the serializable version of <see cref="SnippetsSetData"/>.
    /// </summary>
    [Serializable]
    public class SnippetsSetDataDto
    {
        /// <summary>
        /// Gets or sets the metadata for the snippet set.
        /// </summary>
        [field: SerializeField]
        public SnippetsSetMetadataDto Metadata { get; set; }

        /// <summary>
        /// The Snippets associated with the set.
        /// </summary>
        [field: SerializeField]
        public SnippetDataDto[] Snippets { get; set; }

        /// <summary>
        /// Simple constructor
        /// </summary>
        public SnippetsSetDataDto()
        {
            Metadata = new SnippetsSetMetadataDto();
            Snippets = new SnippetDataDto[0];
        }

#if UNITY_EDITOR

        /// <summary>
        /// Constructor that creates a DTO from a <see cref="SnippetsSetData"/> object.
        /// </summary>
        /// <param name="snippetsSetData">The data of a snippets set to create a DTO from</param>
        public SnippetsSetDataDto(SnippetsSetData snippetsSetData)
        {
            Metadata = new SnippetsSetMetadataDto(snippetsSetData.Metadata);
            Snippets = new SnippetDataDto[snippetsSetData.Snippets.Length];

            for (int i = 0; i < snippetsSetData.Snippets.Length; i++)
            {
                Snippets[i] = new SnippetDataDto(snippetsSetData.Snippets[i]);
            }

        }
#endif
    }
}

