using System;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Represents the data structure completely representing a snippet, including text, sound, and animation components.
    /// </summary>
    [Serializable]
    public class SnippetData
    {
        /// <summary>
        /// Gets or sets the unique identifier for the snippet.
        /// </summary>
        [field: SerializeField]
        public string Id { get; set; }

        /// <summary>
        /// The readable name of the snippet.
        /// </summary>
        [field: SerializeField]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the text content of the snippet.
        /// </summary>
        [field: SerializeField]
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the audio associated with the snippet.
        /// </summary>
        [field: SerializeField]
        public AudioClip Sound { get; set; }

        /// <summary>
        /// Gets or sets the animation data for the snippet.
        /// </summary>
        [field: SerializeField]
        public AnimationClip Animation { get; set; }

        /// <summary>
        /// Gets a value indicating whether the snippet data is valid.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Id);

        /// <summary>
        /// Simple constructor
        /// </summary>
        public SnippetData()
        {
            Id = string.Empty;
            Name = string.Empty;
            Text = string.Empty;
            Sound = null;
            Animation = null;
        }

#if UNITY_EDITOR

        /// <summary>
        /// Constructor starting from a serialized DTO
        /// </summary>
        /// <param name="snippetDto">Serializable version of the snippet</param>
        public SnippetData(SnippetDataDto snippetDto)
        {
            string relativeSoundPath = IoUtilities.GetProjectRelativePath(snippetDto.SoundFilePath); //LoadAssetAtPath requires a relative path
            string relativeAnimationPath = IoUtilities.GetProjectRelativePath(snippetDto.AnimationFilePath);

            Id = snippetDto.Id;
            Name = snippetDto.Name;
            Text = snippetDto.Text;
            Sound = string.IsNullOrEmpty(relativeSoundPath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(relativeSoundPath);
            Animation = string.IsNullOrEmpty(relativeAnimationPath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(relativeAnimationPath);
        }

#endif
    }
}
