using System.Threading;
using System.Threading.Tasks;

namespace Snippets.Sdk
{
    /// <summary>
    /// Interface for providers of snippet sets.
    /// </summary>
    public interface ISnippetSetsProvider 
    {
        /// <summary>
        /// Retrieves the metadata for all available snippet sets (associated with the current user).
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation</param>
        /// <returns>Api response with the list of retrieved snippets</returns>
        Task<ApiResponse<SnippetsSetMetadata[]>> GetAllSnippetsSets(CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the snippet set with the specified ID to the specified folder as a zip file.
        /// </summary>
        /// <param name="id">ID of the snippet set to download</param>
        /// <param name="folder">The folder where to download </param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation</param>
        /// <returns>Response with the full path of the archive containing the snippet set</returns>
        Task<ApiResponse<string>> DownloadSnippetsSet(string id, string folder, CancellationToken cancellationToken = default);
    }
}