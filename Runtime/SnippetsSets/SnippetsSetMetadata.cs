using System;
using UnityEngine;

namespace Snippets.Sdk
{
    [Serializable]
    public class SnippetsSetMetadata
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
        public Sprite Thumbnail { get; set; }

        [field: SerializeField]
        public bool Downloadable { get; set; }

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

        public SnippetsSetMetadata(SnippetsSetMetadataDto metadataDto)
        {
            string thumbnailRelativePath = IoUtilities.GetProjectRelativePath(metadataDto.ThumbnailPath); //LoadAssetAtPath requires a relative path

            Id = metadataDto.Id;
            Name = metadataDto.Name;
            Description = metadataDto.Description;
            Version = metadataDto.Version;
            if (string.IsNullOrEmpty(thumbnailRelativePath))
            {
                Thumbnail = null;
            }
            else
            {
                Thumbnail = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(thumbnailRelativePath);

                // Fallback for cases where the asset is imported as Texture instead of Sprite.
                if (Thumbnail == null)
                {
                    var thumbnailTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnailRelativePath);
                    if (thumbnailTexture != null)
                    {
                        Thumbnail = Sprite.Create(thumbnailTexture,
                            new Rect(0, 0, thumbnailTexture.width, thumbnailTexture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                    }
                }
            }
            Downloadable = metadataDto.Downloadable;
        }

#endif
    }
}

