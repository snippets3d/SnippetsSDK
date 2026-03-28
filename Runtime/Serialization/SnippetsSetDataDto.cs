using System;
using UnityEngine;

namespace Snippets.Sdk
{
    [Serializable]
    public class SnippetsSetDataDto
    {
        [field: SerializeField]
        public SnippetsSetMetadataDto Metadata { get; set; }

        [field: SerializeField]
        public SnippetDataDto[] Snippets { get; set; }

        public SnippetsSetDataDto()
        {
            Metadata = new SnippetsSetMetadataDto();
            Snippets = new SnippetDataDto[0];
        }

#if UNITY_EDITOR

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

