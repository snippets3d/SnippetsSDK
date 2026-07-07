using System;
using UnityEngine;

namespace Snippets.Sdk
{
    [Serializable]
    public class SnippetDataDto
    {
        [field: SerializeField]
        public string Id { get; set; }

        [field: SerializeField]
        public string Name { get; set; }

        [field: SerializeField]
        public string Text { get; set; }

        [field: SerializeField]
        public string SoundFilePath { get; set; }

        [field: SerializeField]
        public string AnimationFilePath { get; set; }

        [field: SerializeField]
        public SnippetAudioTimestampsData AudioTimestamps { get; set; }

        public bool IsValid => !string.IsNullOrEmpty(Id);

        public SnippetDataDto()
        {
            Id = "";
            Name = "";
            Text = "";
            SoundFilePath = "";
            AnimationFilePath = "";
            AudioTimestamps = new SnippetAudioTimestampsData();
        }

#if UNITY_EDITOR

        public SnippetDataDto(SnippetData snippetData)
        {
            Id = snippetData.Id;
            Name = snippetData.Name;
            Text = snippetData.Text;
            SoundFilePath = snippetData.Sound != null ?
                IoUtilities.GetAbsoluteAssetPath(UnityEditor.AssetDatabase.GetAssetPath(snippetData.Sound)) :
                string.Empty;
            AnimationFilePath = snippetData.Animation != null ?
                IoUtilities.GetAbsoluteAssetPath(UnityEditor.AssetDatabase.GetAssetPath(snippetData.Animation)) :
                string.Empty;
            AudioTimestamps = new SnippetAudioTimestampsData(snippetData.AudioTimestamps);
        }
#endif

    }
}
