using UnityEngine;
using UnityEditor;
using System.IO;

namespace Snippets.Sdk
{
    /// <summary>
    /// Generates snippets using as prefab variants of a prefab template snippet.
    /// </summary>
    public class TemplateBasedSnippetGenerator : ISnippetGenerator
    {
        /// <summary>
        /// The prefab template to be used to generate the snippet.
        /// </summary>
        public SnippetPlayer SnippetTemplate { get; private set; }

        /// <summary>
        /// Generates a snippet prefab based on the given snippet data.
        /// </summary>
        /// <param name="snippetTemplate">Prefab of Snippet Player to be used to generate the snippets </param>
        /// <exception cref="ArgumentNullException">Thrown when the snippet template is null</exception>
        public TemplateBasedSnippetGenerator(SnippetPlayer snippetTemplate)
        {
            SnippetTemplate = snippetTemplate;

            // Ensure the template is not null
            if (SnippetTemplate == null)
            {
                throw new System.ArgumentNullException(nameof(snippetTemplate));
            }
        }

        /// <inheritdoc />
        public GameObject GenerateSnippetPrefab(SnippetData snippetData, string outputFolder)
        {
            // Instantiate the template in the scene
            var snippetInstantiatedPrefab = PrefabUtility.InstantiatePrefab(SnippetTemplate.gameObject) as GameObject;

            // Set the snippet data in the prefab instantiation
            var snippetPlayer = snippetInstantiatedPrefab.GetComponent<SnippetPlayer>();
            snippetPlayer.Value = snippetData;

            // Save the modified instantiation as a new prefab (variant)
            Directory.CreateDirectory(outputFolder); // Ensure the output folder exists
            var prefabPath = IoUtilities.GetProjectRelativePath($"{outputFolder}/{snippetData.Name}.prefab");
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(snippetInstantiatedPrefab, prefabPath);

            // Destroy the instance in the scene
            Object.DestroyImmediate(snippetInstantiatedPrefab);

            // Return the created prefab
            return savedPrefab;
        }
    }
}