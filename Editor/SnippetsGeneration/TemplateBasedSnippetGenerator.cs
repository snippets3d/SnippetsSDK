using UnityEngine;
using UnityEditor;
using System.IO;
using System;

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

            if (!PrefabUtility.IsPartOfPrefabAsset(SnippetTemplate.gameObject))
            {
                throw new ArgumentException("The snippet template must be a prefab asset stored in the project.", nameof(snippetTemplate));
            }
        }

        /// <inheritdoc />
        public GameObject GenerateSnippetPrefab(SnippetData snippetData, string outputFolder)
        {
            if (snippetData == null)
            {
                throw new ArgumentNullException(nameof(snippetData));
            }

            Directory.CreateDirectory(outputFolder);

            var prefabPath = GetPrefabAssetPath(snippetData, outputFolder);
            return GenerateSnippetPrefabAtPath(snippetData, prefabPath);
        }

        internal GameObject GenerateSnippetPrefabAtPath(SnippetData snippetData, string prefabAssetPath)
        {
            if (snippetData == null)
            {
                throw new ArgumentNullException(nameof(snippetData));
            }

            if (string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                throw new ArgumentException("A valid prefab asset path is required.", nameof(prefabAssetPath));
            }

            GameObject snippetInstantiatedPrefab = null;

            try
            {
                // Instantiate the template in the scene
                snippetInstantiatedPrefab = PrefabUtility.InstantiatePrefab(SnippetTemplate.gameObject) as GameObject;

                if (snippetInstantiatedPrefab == null)
                {
                    throw new InvalidOperationException("Failed to instantiate the snippet template prefab.");
                }

                // Set the snippet data in the prefab instantiation
                var snippetPlayer = snippetInstantiatedPrefab.GetComponent<SnippetPlayer>();
                if (snippetPlayer == null)
                {
                    throw new InvalidOperationException("The snippet template prefab must contain a SnippetPlayer component.");
                }

                snippetPlayer.Value = snippetData;

                // Save the modified instantiation as a new prefab (variant)
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(snippetInstantiatedPrefab, prefabAssetPath);
                if (savedPrefab == null)
                {
                    throw new IOException($"Unity failed to save the generated prefab at '{prefabAssetPath}'.");
                }

                return savedPrefab;
            }
            finally
            {
                if (snippetInstantiatedPrefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(snippetInstantiatedPrefab);
                }
            }
        }

        private static string GetPrefabAssetPath(SnippetData snippetData, string outputFolder)
        {
            string baseFileName = SanitizeFileName(snippetData.Name);
            if (string.IsNullOrWhiteSpace(baseFileName))
            {
                baseFileName = SanitizeFileName(snippetData.Id);
            }

            if (string.IsNullOrWhiteSpace(baseFileName))
            {
                baseFileName = "Snippet";
            }

            string candidateFullPath = Path.Combine(outputFolder, $"{baseFileName}.prefab");
            string candidateAssetPath = IoUtilities.GetProjectRelativePath(candidateFullPath);

            if (string.IsNullOrWhiteSpace(candidateAssetPath))
            {
                throw new InvalidOperationException($"The generated snippets folder '{outputFolder}' must be inside the Unity project Assets folder.");
            }

            if (!File.Exists(candidateFullPath))
            {
                return candidateAssetPath;
            }

            string fallbackSuffix = SanitizeFileName(snippetData.Id);
            if (string.IsNullOrWhiteSpace(fallbackSuffix))
            {
                fallbackSuffix = Guid.NewGuid().ToString("N");
            }

            candidateFullPath = Path.Combine(outputFolder, $"{baseFileName}_{fallbackSuffix}.prefab");
            candidateAssetPath = IoUtilities.GetProjectRelativePath(candidateFullPath);

            if (string.IsNullOrWhiteSpace(candidateAssetPath))
            {
                throw new InvalidOperationException($"The generated snippets folder '{outputFolder}' must be inside the Unity project Assets folder.");
            }

            return candidateAssetPath;
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            string sanitized = fileName.Trim();

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            sanitized = sanitized.Trim().TrimEnd('.');
            return sanitized;
        }
    }
}
