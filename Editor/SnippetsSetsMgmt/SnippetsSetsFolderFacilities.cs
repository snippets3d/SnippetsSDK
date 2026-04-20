
using System.IO;

namespace Snippets.Sdk
{
    /// <summary>
    /// Facilities for managing the folders of the snippets sets
    /// </summary>
    public static class SnippetsSetsFolderFacilities
    {
        private const string DefaultRawSubfolderName = "Raw";

        /// <summary>
        /// Gets the folder where the raw (images, animations, etc...) snippets of a snippet set should be stored.
        /// The raw folder is nested inside the generated folder for each snippet set.
        /// </summary>
        /// <param name="projectSettings">Project settings that store the folders' info</param>
        /// <param name="snippetSetMetaData">The snippet set of interest</param>
        /// <returns>Folder of interest</returns>
        public static string GetSnippetsSetsRawFolder(IProjectSnippetsSettings projectSettings, SnippetsSetMetadata snippetSetMetaData)
        {
            //notice that we use trim because a directory with spaces in the end causes issues
            string generatedFolder = GetSnippetsSetsGeneratedFolder(projectSettings, snippetSetMetaData);
            return GetSnippetsSetsRawFolder(projectSettings, generatedFolder);
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

        /// <summary>
        /// Gets the raw snippets folder nested inside the provided generated folder path.
        /// </summary>
        /// <param name="projectSettings">Project settings that store the folders' info</param>
        /// <param name="generatedFolder">The generated snippets set folder</param>
        /// <returns>Folder of interest</returns>
        public static string GetSnippetsSetsRawFolder(IProjectSnippetsSettings projectSettings, string generatedFolder)
        {
            string rawSubfolderName = GetRawSubfolderName(projectSettings);
            return Path.Combine(generatedFolder, rawSubfolderName);
        }

        /// <summary>
        /// Gets the configured raw snippets subfolder name.
        /// </summary>
        /// <param name="projectSettings">Project settings that store the folders' info</param>
        /// <returns>The raw snippets subfolder name</returns>
        public static string GetRawSubfolderName(IProjectSnippetsSettings projectSettings)
        {
            if (projectSettings == null)
            {
                return DefaultRawSubfolderName;
            }

            string rawFolderSetting = projectSettings.RawSnippetsDownloadFolder;

            if (string.IsNullOrWhiteSpace(rawFolderSetting))
            {
                return DefaultRawSubfolderName;
            }

            rawFolderSetting = rawFolderSetting.Trim();

            if (rawFolderSetting.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
            {
                rawFolderSetting = rawFolderSetting.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                rawFolderSetting = Path.GetFileName(rawFolderSetting);
            }

            return string.IsNullOrWhiteSpace(rawFolderSetting) ? DefaultRawSubfolderName : rawFolderSetting;
        }
    }
}
