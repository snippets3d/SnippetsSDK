namespace Snippets.Sdk
{
    /// <summary>
    /// Interface for storing the per-project settings for the Snippets SDK.
    /// </summary>
    public interface IProjectSnippetsSettings
    {
        /// <summary>
        /// Gets or sets the folder where the raw snippets are downloaded
        /// (i.e. images, texts, audio files, etc...)
        /// </summary>
        string RawSnippetsDownloadFolder { get; set; }

        /// <summary>
        /// Gets or sets the folder where the actual snippets are generated
        /// (i.e. prefabs)
        /// </summary>
        string GeneratedSnippetsDownloadFolder { get; set; }
    }
}