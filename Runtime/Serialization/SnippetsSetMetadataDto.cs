
using System;
using System.IO;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Serializable form of the metadata for a set of snippets.
    /// </summary>
    [Serializable]
    public class SnippetsSetMetadataDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the snippet set.
        /// </summary>
        [field: SerializeField]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the snippet set.
        /// </summary>
        [field: SerializeField]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the snippet set.
        /// </summary>
        [field: SerializeField]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the version of the snippet set.
        /// </summary>
        [field: SerializeField]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the path of the file of the thumbnail image for the snippet set..
        /// The path is relative to the root folder where the snippet set has been downloaded
        /// </summary>
        [field: SerializeField]
        public string ThumbnailPath { get; set; }

        /// <summary>
        /// Gets or sets if the snippet set is downloadable.
        /// </summary>
        [field: SerializeField]
        public bool Downloadable { get; set; }

        /// <summary>
        /// Simple constructor
        /// </summary>
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

        /// <summary>
        /// Constructor that creates a DTO from a <see cref="SnippetsSetMetadata"/> object.
        /// </summary>
        /// <param name="snippetsSetMetadata">The object to create a DTO from</param>
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
