
using System.IO;

namespace Snippets.Sdk
{
    /// <summary>
    /// Facilities for managing the folders of the snippets sets
    /// </summary>
    public static class SnippetsSetsFolderFacilities
    {
        /// <summary>
        /// Gets the folder where the raw (images, animations, etc...) snippets of a snippet set should be stored
        /// </summary>
        /// <param name="projectSettings">Project settings that store the folders' info</param>
        /// <param name="snippetSetMetaData">The snippet set of interest</param>
        /// <returns>Folder of interest</returns>
        public static string GetSnippetsSetsRawFolder(IProjectSnippetsSettings projectSettings, SnippetsSetMetadata snippetSetMetaData)
        {
            //notice that we use trim because a directory with spaces in the end causes issues
            return Path.Combine(projectSettings.RawSnippetsDownloadFolder, snippetSetMetaData.Name.Trim());
        }

        /// <summary>
        /// Gets the folder where the generated (prefabs) snippets of a snippet set should be stored
        /// </summary>
        /// <param name="projectSettings">Project settings that store the folders' info</param>
        /// <param name="snippetSetMetaData">The snippet set of interest</param>
        /// <returns>Folder of interest</returns>
        public static string GetSnippetsSetsGeneratedFolder(IProjectSnippetsSettings projectSettings, SnippetsSetMetadata snippetSetMetaData)
        {
            //notice that we use trim because a directory with spaces in the end causes issues
            return Path.Combine(projectSettings.GeneratedSnippetsDownloadFolder, snippetSetMetaData.Name.Trim());
        }
    }
}