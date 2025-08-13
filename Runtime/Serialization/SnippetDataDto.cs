using System;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Represents the data structure of a serialized snippet.
    /// It is the serializable version of <see cref="SnippetData"/>.
    /// </summary>
    [Serializable]
    public class SnippetDataDto
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
        /// Gets or sets the path of the audio file associated with the snippet.
        /// The path is relative to the root folder where the snippet set has been downloaded
        /// </summary>
        [field: SerializeField]
        public string SoundFilePath { get; set; }

        /// <summary>
        /// Gets or sets the path of the animation data for the snippet.
        /// The path is relative to the root folder where the snippet set has been downloaded
        /// </summary>
        [field: SerializeField]
        public string AnimationFilePath { get; set; }

        /// <summary>
        /// Gets a value indicating whether the snippet data is valid.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Id);

        /// <summary>
        /// Simple constructor
        /// </summary>
        public SnippetDataDto()
        {
            Id = "";
            Name = "";
            Text = "";
            SoundFilePath = "";
            AnimationFilePath = "";
        }

#if UNITY_EDITOR

        /// <summary>
        /// Constructor that creates a DTO from a <see cref="SnippetData"/> object.
        /// </summary>
        /// <param name="snippetData">The snippet to create the DTO from</param>
        public SnippetDataDto(SnippetData snippetData)
        {
            Id = snippetData.Id;
            Name = snippetData.Name;
            Text = snippetData.Text;
            SoundFilePath = snippetData.Sound != null ?
                IoUtilities.MergePaths(Application.dataPath, UnityEditor.AssetDatabase.GetAssetPath(snippetData.Sound)) :
                string.Empty;
            AnimationFilePath = snippetData.Animation != null ?
                IoUtilities.MergePaths(Application.dataPath, UnityEditor.AssetDatabase.GetAssetPath(snippetData.Animation)) : 
                string.Empty;
        }
#endif

    }
}
