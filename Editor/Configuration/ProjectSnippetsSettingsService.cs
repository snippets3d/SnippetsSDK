using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Provides the per-project settings for the Snippets SDK through a ScriptableObject
    /// that is stored in the project resources.
    /// </summary>
    public class ProjectSnippetsSettingsService : IProjectSnippetsSettings
    {
        /// <inheritdoc />
        public string RawSnippetsDownloadFolder
        {
            get => ScriptableAssetInstance.RawSnippetsDownloadFolder;
            set => ScriptableAssetInstance.RawSnippetsDownloadFolder = value;
        }

        /// <inheritdoc />
        public string GeneratedSnippetsDownloadFolder
        {
            get => ScriptableAssetInstance.GeneratedSnippetsDownloadFolder;
            set => ScriptableAssetInstance.GeneratedSnippetsDownloadFolder = value;
        }

        /// <summary>
        /// The instance of the ScriptableObject that stores the settings.
        /// </summary>
        private IProjectSnippetsSettings m_scriptableAssetInstance;

        /// <summary>
        /// Gets the instance of the ScriptableObject that stores the settings.
        /// If the instance is null, it loads it from the Resources folder.
        /// </summary>
        private IProjectSnippetsSettings ScriptableAssetInstance
        {
            get
            {
                if (m_scriptableAssetInstance == null)
                {
                    m_scriptableAssetInstance = Resources.Load<ProjectSnippetsSettings>("ProjectSnippetsSettings");
                }

                return m_scriptableAssetInstance;
            }
        }
    }
    
}