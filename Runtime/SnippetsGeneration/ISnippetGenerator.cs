using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Base interface for elements that are able to generate snippets prefabs.
    /// </summary>
    public interface ISnippetGenerator
    {
        /// <summary>
        /// Generates a snippet prefab based on the given snippet data.
        /// </summary>
        /// <param name="snippetData">Snippet data of interest</param>
        /// <param name="outputFolder">Folder where the prefab will be saved</param>
        /// <returns>Prefab to play back the snippet indicated in the data</returns>
        GameObject GenerateSnippetPrefab(SnippetData snippetData, string outputFolder);
    }
}