namespace Snippets.Sdk
{
    /// <summary>
    /// Interface for storing the per-project settings for the Snippets SDK.
    /// </summary>
    public interface IProjectSnippetsSettings
    {
        /// <summary>
        /// Gets or sets the raw snippets folder name or path.
        /// The raw snippets for each set are stored as a subfolder inside that set's generated folder.
        /// If a full path is provided, the last path segment is used as the subfolder name.
        /// </summary>
        string RawSnippetsDownloadFolder { get; set; }

        /// <summary>
        /// Gets or sets the folder where the actual snippets are generated
        /// (i.e. prefabs)
        /// </summary>
        string GeneratedSnippetsDownloadFolder { get; set; }
    }
}
