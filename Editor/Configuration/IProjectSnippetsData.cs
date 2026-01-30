using System.Collections.Generic;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Base interface for elements that saves data for the Snippets imported in the project and makes it persist among sessions.
    /// </summary>
    public interface IProjectSnippetsData
    {
        /// <summary>
        /// The last user doing some operations on Snippet Data.
        /// </summary>
        [SerializeField]
        string LastUsername { get; }

        /// <summary>
        /// The list of Snippets Sets imported in the project.
        /// </summary>
        [SerializeField]
        List<SnippetsSetMetadata> LocalSnippetSets { get; }

        /// <summary>
        /// Notifies this class that a new Snippets Set has been imported.
        /// </summary>
        /// <param name="addedSnippetsSet">The metadata of the imported snippet set</param>
        /// <param name="username">The user performing the operation</param>
        void SnippetsSetImported(SnippetsSetMetadata addedSnippetsSet, string username);

        /// <summary>
        /// Notifies this class that a Snippets Set has been removed.
        /// </summary>
        /// <param name="removedSnippetsSet">The metadata of the removed snippet set</param>
        /// <param name="username">The user performing the operation</param>
        void SnippetsSetRemoved(SnippetsSetMetadata removedSnippetsSet, string username);
    }
}