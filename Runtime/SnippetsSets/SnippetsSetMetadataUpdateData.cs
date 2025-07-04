
namespace Snippets.Sdk
{
    /// <summary>
    /// Provides the data related to the update of a snippets set.
    /// </summary>
    public class SnippetsSetMetadataUpdateData
    {
        /// <summary>
        /// The original snippets set metadata (i.e. the local downloaded snippets set).
        /// It is null if there is no original snippet (e.g. no local data)
        /// </summary>
        public SnippetsSetMetadata OriginalSnippetsSetMetadata { get; private set; }

        /// <summary>
        /// The updated snippets set metadata (i.e. the new snippets set data got from the server).
        /// It is null if there is no snippet in the update (e.g. the snippet set has been deleted on the server)
        /// </summary>
        public SnippetsSetMetadata UpdatedSnippetsSetMetadata { get; private set; }

        /// <summary>
        /// Constructor with full initialization.
        /// </summary>
        /// <param name="originalSnippetsSetMetadata">The original snippets set data (i.e. the local downloaded snippets set)</param>
        /// <param name="updatedSnippetsSetMetadata">The updated snippets set data (i.e. the new snippets set data got from the server)</param>
        public SnippetsSetMetadataUpdateData(SnippetsSetMetadata originalSnippetsSetMetadata, SnippetsSetMetadata updatedSnippetsSetMetadata)
        {
            OriginalSnippetsSetMetadata = originalSnippetsSetMetadata;
            UpdatedSnippetsSetMetadata = updatedSnippetsSetMetadata;
        }
    }
}