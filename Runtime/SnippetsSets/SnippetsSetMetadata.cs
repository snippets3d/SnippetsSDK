using System;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Represents the metadata for a set of snippets.
    /// </summary>
    [Serializable]
    public class SnippetsSetMetadata
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
        /// Gets or sets the thumbnail image for the snippet set.
        /// </summary>
        [field: SerializeField]
        public Sprite Thumbnail { get; set; }

        /// <summary>
        /// Gets or sets if the snippet set is downloadable.
        /// </summary>
        [field: SerializeField]
        public bool Downloadable { get; set; }

        /// <summary>
        /// Simple constructor
        /// </summary>
        public SnippetsSetMetadata()
        {
            Id = string.Empty;
            Name = string.Empty;
            Description = string.Empty;
            Version = string.Empty;
            Thumbnail = null;
            Downloadable = false;
        }

#if UNITY_EDITOR

        /// <summary>
        /// Constructor starting from a serialized DTO
        /// </summary>
        /// <param name="metadataDto">Serializable version of the metadata</param>
        /// <remarks>
        /// The method currently only supports assets that are in the project (in the Assets folder or its subfolders).
        /// </remarks>
        public SnippetsSetMetadata(SnippetsSetMetadataDto metadataDto)
        {
            string thumbnailRelativePath = IoUtilities.GetProjectRelativePath(metadataDto.ThumbnailPath); //LoadAssetAtPath requires a relative path

            Id = metadataDto.Id;
            Name = metadataDto.Name;
            Description = metadataDto.Description;
            Version = metadataDto.Version;
            Thumbnail = string.IsNullOrEmpty(thumbnailRelativePath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(thumbnailRelativePath);
            Downloadable = metadataDto.Downloadable;
        }

#endif
    }
}
