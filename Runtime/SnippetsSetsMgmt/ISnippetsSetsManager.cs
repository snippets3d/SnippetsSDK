using System;
using System.Threading.Tasks;

namespace Snippets.Sdk
{
    /// <summary>
    /// Interface for elements that can manage (e.g. import, remove, etc.) snippets sets 
    /// </summary>
    public interface ISnippetsSetsManager
    {
        /// <summary>
        /// The snippet generator to use to generate the snippets.
        /// It MUST be set before calling any method of this interface.
        /// </summary>
        ISnippetGenerator SnippetGenerator { get; set; }

        /// <summary>
        /// Event that is triggered when a snippets set is imported in the project
        /// </summary>
        Action<string> OnSnippetsSetImported { get; set; }

        /// <summary>
        /// Event that is triggered when a snippets set is updated in the project
        /// </summary>
        Action<string> OnSnippetsSetUpdated { get; set; }

        /// <summary>
        /// Event that is triggered when a snippets set is removed from the project
        /// </summary>
        Action<string> OnSnippetsSetRemoved { get; set; }

        /// <summary>
        /// Imports a snippets set in the project
        /// </summary>
        /// <param name="snippetSetMetaData">The metadata of the snippets set to the import</param>
        /// <param name="progress">Reference to an element that can report the progress of the import operation. The range is [0, 1]</param>
        /// <returns>Folder where the Snippet prefabs have been created</returns>
        /// <exception cref="InvalidOperationException">Thrown if the <see cref="SnippetGenerator"/> property is not set</exception>
        Task<string> ImportSnippetsSet(SnippetsSetMetadata snippetSetMetaData, IProgress<float> progress = null);

        /// <summary>
        /// Updates a snippets set in the project
        /// </summary>
        /// <param name="oldSnippetSetMetaData">The old metadata of the snippets set to update</param>
        /// <param name="newSnippetSetMetaData">The new metadata of the snippets set to update</param>
        /// <param name="progress">Reference to an element that can report the progress of the import operation. The range is [0, 1]</param>
        /// <returns>Folder where the Snippet prefabs have been created</returns>
        /// <exception cref="InvalidOperationException">Thrown if the <see cref="SnippetGenerator"/> property is not set</exception>
        Task<string> UpdateSnippetsSet(SnippetsSetMetadata oldSnippetSetMetaData, SnippetsSetMetadata newSnippetSetMetaData, IProgress<float> progress = null);

        /// <summary>
        /// Removes a snippets set from the project
        /// </summary>
        /// <param name="snippetSetMetaData">The metadata of the snippets set to the import</param>
        /// <param name="progress">Reference to an element that can report the progress of the import operation. The range is [0, 1]</param>
        /// <returns>Folder where the Snippet prefabs were and that has been removed</returns>
        Task<string> RemoveSnippetsSet(SnippetsSetMetadata snippetSetMetaData, IProgress<float> progress = null);
    }
}