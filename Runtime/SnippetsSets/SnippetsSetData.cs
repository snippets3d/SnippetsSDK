using System;
using UnityEngine;

namespace Snippets.Sdk
{
    [Serializable]
    public class SnippetsSetData
    {
        [field: SerializeField]
        public SnippetsSetMetadata Metadata { get; set; }

        [field: SerializeField]
        public SnippetData[] Snippets { get; set; }

        public SnippetsSetData()
        {
            Metadata = new SnippetsSetMetadata();
            Snippets = new SnippetData[0];
        }

#if UNITY_EDITOR

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

