using System;
using UnityEngine;

namespace Snippets.Sdk
{
    [Serializable]
    public class SnippetData
    {
        [field: SerializeField]
        public string Id { get; set; }

        [field: SerializeField]
        public string Name { get; set; }

        [field: SerializeField]
        public string Text { get; set; }

        [field: SerializeField]
        public AudioClip Sound { get; set; }

        [field: SerializeField]
        public SnippetAudioTimestampsData AudioTimestamps { get; set; }

        [field: SerializeField]
        public AnimationClip Animation { get; set; }

        public bool IsValid => !string.IsNullOrEmpty(Id);

        public SnippetData()
        {
            Id = string.Empty;
            Name = string.Empty;
            Text = string.Empty;
            Sound = null;
            AudioTimestamps = new SnippetAudioTimestampsData();
            Animation = null;
        }

#if UNITY_EDITOR

        public SnippetData(SnippetDataDto snippetDto)
        {
            string relativeSoundPath = IoUtilities.GetProjectRelativePath(snippetDto.SoundFilePath); //LoadAssetAtPath requires a relative path
            string relativeAnimationPath = IoUtilities.GetProjectRelativePath(snippetDto.AnimationFilePath);

            Id = snippetDto.Id;
            Name = snippetDto.Name;
            Text = snippetDto.Text;
            Sound = string.IsNullOrEmpty(relativeSoundPath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(relativeSoundPath);
            AudioTimestamps = new SnippetAudioTimestampsData(snippetDto.AudioTimestamps);
            Animation = string.IsNullOrEmpty(relativeAnimationPath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(relativeAnimationPath);
        }

#endif
    }
}
