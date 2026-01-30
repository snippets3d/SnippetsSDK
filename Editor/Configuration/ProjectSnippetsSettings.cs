using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Stores the per-project settings for the Snippets SDK.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectSnippetsSettings", menuName = "Snippets/Project Snippets Settings", order = 1)]
    public class ProjectSnippetsSettings : ScriptableObject, IProjectSnippetsSettings
    {
        /// <inheritdoc />
        [field: SerializeField]
        public string RawSnippetsDownloadFolder { get; set; } = "Assets/_Snippets/Raw";

        /// <inheritdoc />
        [field: SerializeField]
        public string GeneratedSnippetsDownloadFolder { get; set; } = "Assets/_Snippets/Generated";
    }
}
