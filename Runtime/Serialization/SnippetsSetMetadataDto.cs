
using System;
using System.IO;
using UnityEngine;

namespace Snippets.Sdk
{
    [Serializable]
    public class SnippetsSetMetadataDto
    {
        [field: SerializeField]
        public string Id { get; set; }

        [field: SerializeField]
        public string Name { get; set; }

        [field: SerializeField]
        public string Description { get; set; }

        [field: SerializeField]
        public string Version { get; set; }

        [field: SerializeField]
        public string ThumbnailPath { get; set; }

        [field: SerializeField]
        public bool Downloadable { get; set; }

        public SnippetsSetMetadataDto()
        {
            Id = "";
            Name = "";
            Description = "";
            Version = "";
            ThumbnailPath = "";
            Downloadable = false;
        }

#if UNITY_EDITOR

        public SnippetsSetMetadataDto(SnippetsSetMetadata snippetsSetMetadata)
        {
            Id = snippetsSetMetadata.Id;
            Name = snippetsSetMetadata.Name;
            Description = snippetsSetMetadata.Description;
            Version = snippetsSetMetadata.Version;
            ThumbnailPath = snippetsSetMetadata.Thumbnail != null ?
                IoUtilities.MergePaths(Application.dataPath, UnityEditor.AssetDatabase.GetAssetPath(snippetsSetMetadata.Thumbnail)) :
                string.Empty;
            Downloadable = snippetsSetMetadata.Downloadable;
        }

#endif

    }
}
