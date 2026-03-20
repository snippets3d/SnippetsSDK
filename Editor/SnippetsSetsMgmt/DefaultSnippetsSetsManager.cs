using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Default implementation of the <see cref="ISnippetsSetsManager"/> interface
    /// </summary>
    public class DefaultSnippetsSetsManager : ISnippetsSetsManager
    {
        /// <summary>
        /// The project settings for the Snippets SDK, 
        /// where the folders for the raw and generated snippets are stored
        /// </summary>
        private IProjectSnippetsSettings m_projectSettings;

        /// <summary>
        /// The provider of the snippet sets, most probably a wrapper around a REST API
        /// to obtain the ZIP files of the snippet sets
        /// </summary>
        private ISnippetSetsProvider m_snippetSetsProvider;

        /// <summary>
        /// The zipper for the snippet sets, to unzip the downloaded ZIP files
        /// </summary>
        private ISnippetsSetZipper m_snippetsSetZipper;

        /// <inheritdoc />
        public ISnippetGenerator SnippetGenerator { get; set; }

        /// <inheritdoc />
        public Action<string> OnSnippetsSetImported { get; set; }

        /// <inheritdoc />
        public Action<string> OnSnippetsSetUpdated { get; set; }

        /// <inheritdoc />
        public Action<string> OnSnippetsSetRemoved { get; set; }

        /// <summary>
        /// Constructor with partial initialization.
        /// If you use it, you MUST manually set the <see cref="SnippetGenerator"/> property 
        /// before calling any method of this class.
        /// </summary>
        /// <param name="projectSettings">The project settings for the Snippets SDK</param>
        /// <param name="snippetSetsProvider">The provider of the snippet sets</param>
        /// <param name="snippetsSetZipper">The zipper for the snippet sets, to unzip the downloaded ZIP files</param>
        public DefaultSnippetsSetsManager(IProjectSnippetsSettings projectSettings, ISnippetSetsProvider snippetSetsProvider, ISnippetsSetZipper snippetsSetZipper)
        {
            m_projectSettings = projectSettings;
            m_snippetSetsProvider = snippetSetsProvider;
            m_snippetsSetZipper = snippetsSetZipper;
        }

        /// <summary>
        /// Constructor with full initialization
        /// </summary>
        /// <param name="projectSettings">The project settings for the Snippets SDK</param>
        /// <param name="snippetSetsProvider">The provider of the snippet sets</param>
        /// <param name="snippetsSetZipper">The zipper for the snippet sets, to unzip the downloaded ZIP files</param>
        /// <param name="snippetGenerator">The snippet generator to use to generate the single snippets</param>        
        public DefaultSnippetsSetsManager(IProjectSnippetsSettings projectSettings, ISnippetSetsProvider snippetSetsProvider, ISnippetsSetZipper snippetsSetZipper,
            ISnippetGenerator snippetGenerator) :
            this(projectSettings, snippetSetsProvider, snippetsSetZipper)
        {
            SnippetGenerator = snippetGenerator;
        }

        /// <inheritdoc />
        public async Task<string> ImportSnippetsSet(SnippetsSetMetadata snippetSetMetaData, IProgress<float> progress = null)
        {
            const float progressStartValue = 0;
            const float progressDownloadedValue = 0.4f;
            const float progressLoadedValue = 0.8f;            
            const float progressEndValue = 1;

            //check if the SnippetGenerator property has been set
            if (SnippetGenerator == null)
            {
                throw new InvalidOperationException("The SnippetGenerator property must be set before calling any method of this class");
            }

            string rawFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsRawFolder(m_projectSettings, snippetSetMetaData);
            string generatedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, snippetSetMetaData);
            progress?.Report(progressStartValue);

            var snippetsSetData = await DownloadAndLoadSnippetsSet(snippetSetMetaData, rawFolder, progressDownloadedValue, progressLoadedValue, progress);
            SynchronizeGeneratedPrefabs(snippetsSetData, generatedFolder, progressLoadedValue, progressEndValue, progress);

            progress?.Report(progressEndValue);

            // Trigger the event
            OnSnippetsSetImported?.Invoke(snippetSetMetaData.Name);

            //return the generated folder
            return generatedFolder;
        }

        /// <inheritdoc />
        public async Task<string> UpdateSnippetsSet(SnippetsSetMetadata oldSnippetSetMetaData, SnippetsSetMetadata newSnippetSetMetaData, IProgress<float> progress = null)
        {
            const float progressStartValue = 0;
            const float progressPreparedValue = 0.2f;
            const float progressDownloadedValue = 0.55f;
            const float progressLoadedValue = 0.75f;
            const float progressEndValue = 1;

            //check if the SnippetGenerator property has been set
            if (SnippetGenerator == null)
            {
                throw new InvalidOperationException("The SnippetGenerator property must be set before calling any method of this class");
            }

            string generatedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, newSnippetSetMetaData);
            string rawFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsRawFolder(m_projectSettings, newSnippetSetMetaData);
            string rawTempFolder = $"{rawFolder}__SnippetsUpdateTemp";
            bool generatedFolderMoved = false;

            progress?.Report(progressStartValue);

            try
            {
                generatedFolder = EnsureGeneratedFolderForUpdate(oldSnippetSetMetaData, newSnippetSetMetaData, out generatedFolderMoved);
                DeleteDirectoryIfExists(rawTempFolder);

                progress?.Report(progressPreparedValue);

                var snippetsSetData = await DownloadAndLoadSnippetsSetForStableUpdate(newSnippetSetMetaData, rawFolder, rawTempFolder, progressDownloadedValue, progressLoadedValue, progress);
                SynchronizeGeneratedPrefabs(snippetsSetData, generatedFolder, progressLoadedValue, progressEndValue, progress);
                DeleteDirectoryIfExists(rawTempFolder);
            }
            catch
            {
                DeleteDirectoryIfExists(rawTempFolder);
                RestoreGeneratedFolderAfterFailedUpdate(oldSnippetSetMetaData, newSnippetSetMetaData, generatedFolderMoved);
                throw;
            }

            // Trigger the event
            OnSnippetsSetUpdated?.Invoke(newSnippetSetMetaData.Name);
            progress?.Report(progressEndValue);

            return generatedFolder;
        }

        /// <inheritdoc />
        public async Task<string> RemoveSnippetsSet(SnippetsSetMetadata snippetSetMetaData, IProgress<float> progress = null)
        {
            progress?.Report(0);

            //remove both the raw and generated folders of the snippets set
            //and return the generated folder
            //(we use a Task not to block the main thread with IO operations)
            var returnValue = await Task.Run(async () =>
            {
                var rawFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsRawFolder(m_projectSettings, snippetSetMetaData);
                var generatedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, snippetSetMetaData);

                if (Directory.Exists(rawFolder))
                    Directory.Delete(rawFolder, true);
                else
                    Debug.LogWarning($"[Snippets SDK] The folder of the raw snippets set {snippetSetMetaData.Name} was not found at {rawFolder}");

                progress?.Report(0.5f);

                if (Directory.Exists(generatedFolder))
                    Directory.Delete(generatedFolder, true);
                else
                    Debug.LogWarning($"[Snippets SDK] The folder of the generated snippets set {snippetSetMetaData.Name} was not found at {generatedFolder}");

                Debug.Log($"[Snippets SDK] Snippets set {snippetSetMetaData.Name} removed");

                // Trigger the event
                OnSnippetsSetRemoved?.Invoke(snippetSetMetaData.Name);

                return generatedFolder;
            });

            progress?.Report(1);

            return returnValue;
        }

        private async Task<SnippetsSetData> DownloadAndLoadSnippetsSet(SnippetsSetMetadata snippetSetMetaData, string rawFolder,
            float progressDownloadedValue, float progressLoadedValue, IProgress<float> progress)
        {
            var downloadResponse = await m_snippetSetsProvider.DownloadSnippetsSet(snippetSetMetaData.Id, rawFolder);

            if (!downloadResponse.IsSuccessful)
            {
                throw new IOException($"Error downloading the snippets set {snippetSetMetaData.Name}: {downloadResponse.Message}");
            }

            progress?.Report(progressDownloadedValue);

            try
            {
                return m_snippetsSetZipper.DecompressSnippetsSet(downloadResponse.Value, rawFolder);
            }
            finally
            {
                if (File.Exists(downloadResponse.Value))
                {
                    File.Delete(downloadResponse.Value);
                }

                progress?.Report(progressLoadedValue);
            }
        }

        private async Task<SnippetsSetData> DownloadAndLoadSnippetsSetForStableUpdate(SnippetsSetMetadata snippetSetMetaData, string rawFolder,
            string rawTempFolder, float progressDownloadedValue, float progressLoadedValue, IProgress<float> progress)
        {
            EnsureProjectFolderExists(rawTempFolder);

            var downloadResponse = await m_snippetSetsProvider.DownloadSnippetsSet(snippetSetMetaData.Id, rawTempFolder);

            if (!downloadResponse.IsSuccessful)
            {
                throw new IOException($"Error downloading the snippets set {snippetSetMetaData.Name}: {downloadResponse.Message}");
            }

            progress?.Report(progressDownloadedValue);

            try
            {
                var snippetsSetDataDto = m_snippetsSetZipper.DecompressSnippetsSetDto(downloadResponse.Value, rawTempFolder);

                SynchronizeRawAssets(rawTempFolder, rawFolder);
                RemapDtoPaths(snippetsSetDataDto, rawTempFolder, rawFolder);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (!string.IsNullOrEmpty(snippetsSetDataDto.Metadata?.ThumbnailPath))
                {
                    AssetsUtilities.SetTextureImporterSettingsIfNeeded(
                        IoUtilities.GetProjectRelativePath(snippetsSetDataDto.Metadata.ThumbnailPath),
                        TextureImporterType.Sprite);
                }

                return new SnippetsSetData(snippetsSetDataDto);
            }
            finally
            {
                if (File.Exists(downloadResponse.Value))
                {
                    File.Delete(downloadResponse.Value);
                }

                progress?.Report(progressLoadedValue);
            }
        }

        private void SynchronizeGeneratedPrefabs(SnippetsSetData snippetsSetData, string generatedFolder,
            float progressStartValue, float progressEndValue, IProgress<float> progress)
        {
            Directory.CreateDirectory(generatedFolder);

            var existingGeneratedPrefabs = BuildExistingGeneratedPrefabsIndex(generatedFolder);

            DeleteRemovedGeneratedPrefabs(existingGeneratedPrefabs, snippetsSetData);

            int totalSnippets = snippetsSetData.Snippets.Length;

            for (int i = 0; i < totalSnippets; i++)
            {
                var snippetData = snippetsSetData.Snippets[i];

                try
                {
                    if (TryGetExistingGeneratedPrefabPath(existingGeneratedPrefabs, snippetData.Id, out string existingPrefabPath))
                    {
                        UpdateExistingGeneratedPrefab(existingPrefabPath, snippetData);
                    }
                    else
                    {
                        SnippetGenerator.GenerateSnippetPrefab(snippetData, generatedFolder);
                    }
                }
                catch (Exception e)
                {
                    string snippetName = string.IsNullOrWhiteSpace(snippetData?.Name) ? snippetData?.Id ?? "(unknown snippet)" : snippetData.Name;
                    throw new IOException($"Error synchronizing prefab for snippet '{snippetName}' in set '{snippetsSetData.Metadata?.Name ?? "(unknown set)"}'.", e);
                }

                float normalizedProgress = totalSnippets == 0 ? 1f : (i + 1f) / totalSnippets;
                progress?.Report(Mathf.Lerp(progressStartValue, progressEndValue, normalizedProgress));
            }

            if (totalSnippets == 0)
            {
                progress?.Report(progressEndValue);
            }
        }

        private string EnsureGeneratedFolderForUpdate(SnippetsSetMetadata oldSnippetSetMetaData, SnippetsSetMetadata newSnippetSetMetaData, out bool folderMoved)
        {
            string oldGeneratedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, oldSnippetSetMetaData);
            string newGeneratedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, newSnippetSetMetaData);
            folderMoved = false;

            if (PathsEqual(oldGeneratedFolder, newGeneratedFolder))
            {
                return newGeneratedFolder;
            }

            if (!Directory.Exists(oldGeneratedFolder))
            {
                return newGeneratedFolder;
            }

            string oldGeneratedAssetPath = IoUtilities.GetProjectRelativePath(oldGeneratedFolder);
            string newGeneratedAssetPath = IoUtilities.GetProjectRelativePath(newGeneratedFolder);

            if (string.IsNullOrWhiteSpace(oldGeneratedAssetPath) || string.IsNullOrWhiteSpace(newGeneratedAssetPath))
            {
                throw new IOException("The generated snippets folder must stay inside the Unity project's Assets folder to support stable updates.");
            }

            if (AssetDatabase.IsValidFolder(newGeneratedAssetPath))
            {
                throw new IOException($"Can not move snippets set '{oldSnippetSetMetaData.Name}' to '{newGeneratedAssetPath}' because that folder already exists.");
            }

            string targetParentFolder = Path.GetDirectoryName(newGeneratedFolder);
            if (!string.IsNullOrWhiteSpace(targetParentFolder))
            {
                EnsureProjectFolderExists(targetParentFolder);
            }

            string moveError = AssetDatabase.MoveAsset(oldGeneratedAssetPath, newGeneratedAssetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                throw new IOException($"Failed to move snippets set folder from '{oldGeneratedAssetPath}' to '{newGeneratedAssetPath}'. Unity reports: {moveError}");
            }

            folderMoved = true;
            return newGeneratedFolder;
        }

        private void RestoreGeneratedFolderAfterFailedUpdate(SnippetsSetMetadata oldSnippetSetMetaData, SnippetsSetMetadata newSnippetSetMetaData, bool folderMoved)
        {
            if (!folderMoved)
            {
                return;
            }

            string oldGeneratedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, oldSnippetSetMetaData);
            string newGeneratedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, newSnippetSetMetaData);

            if (!Directory.Exists(newGeneratedFolder) || Directory.Exists(oldGeneratedFolder))
            {
                return;
            }

            string oldGeneratedAssetPath = IoUtilities.GetProjectRelativePath(oldGeneratedFolder);
            string newGeneratedAssetPath = IoUtilities.GetProjectRelativePath(newGeneratedFolder);
            if (string.IsNullOrWhiteSpace(oldGeneratedAssetPath) || string.IsNullOrWhiteSpace(newGeneratedAssetPath))
            {
                return;
            }

            string targetParentFolder = Path.GetDirectoryName(oldGeneratedFolder);
            if (!string.IsNullOrWhiteSpace(targetParentFolder))
            {
                EnsureProjectFolderExists(targetParentFolder);
            }

            string moveError = AssetDatabase.MoveAsset(newGeneratedAssetPath, oldGeneratedAssetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogWarning($"[Snippets SDK] Failed to restore snippets set folder after an update error. Unity reports: {moveError}");
                return;
            }

        }

        private static Dictionary<string, string> BuildExistingGeneratedPrefabsIndex(string generatedFolder)
        {
            var existingGeneratedPrefabs = new Dictionary<string, string>(StringComparer.Ordinal);
            string generatedFolderAssetPath = IoUtilities.GetProjectRelativePath(generatedFolder);

            if (string.IsNullOrWhiteSpace(generatedFolderAssetPath) || !AssetDatabase.IsValidFolder(generatedFolderAssetPath))
            {
                return existingGeneratedPrefabs;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { generatedFolderAssetPath });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabAssetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
                var snippetPlayer = prefabAsset != null ? prefabAsset.GetComponent<SnippetPlayer>() : null;
                string snippetId = snippetPlayer?.Value?.Id;

                if (string.IsNullOrWhiteSpace(snippetId))
                {
                    continue;
                }

                if (!existingGeneratedPrefabs.ContainsKey(snippetId))
                {
                    existingGeneratedPrefabs.Add(snippetId, prefabAssetPath);
                }
            }

            return existingGeneratedPrefabs;
        }

        private static void DeleteRemovedGeneratedPrefabs(Dictionary<string, string> existingGeneratedPrefabs, SnippetsSetData snippetsSetData)
        {
            var incomingSnippetIds = new HashSet<string>(
                snippetsSetData.Snippets
                    .Where(snippet => snippet != null && !string.IsNullOrWhiteSpace(snippet.Id))
                    .Select(snippet => snippet.Id),
                StringComparer.Ordinal);

            foreach (var existingGeneratedPrefab in existingGeneratedPrefabs.ToArray())
            {
                if (incomingSnippetIds.Contains(existingGeneratedPrefab.Key))
                {
                    continue;
                }

                if (!AssetDatabase.DeleteAsset(existingGeneratedPrefab.Value))
                {
                    throw new IOException($"Failed to remove generated prefab '{existingGeneratedPrefab.Value}' for deleted snippet '{existingGeneratedPrefab.Key}'.");
                }

                existingGeneratedPrefabs.Remove(existingGeneratedPrefab.Key);
            }
        }

        private static bool TryGetExistingGeneratedPrefabPath(Dictionary<string, string> existingGeneratedPrefabs, string snippetId, out string existingPrefabPath)
        {
            existingPrefabPath = null;

            return !string.IsNullOrWhiteSpace(snippetId) &&
                existingGeneratedPrefabs.TryGetValue(snippetId, out existingPrefabPath) &&
                !string.IsNullOrWhiteSpace(existingPrefabPath);
        }

        private static void UpdateExistingGeneratedPrefab(string prefabAssetPath, SnippetData snippetData)
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(prefabAssetPath);

            try
            {
                var snippetPlayer = prefabContents.GetComponent<SnippetPlayer>();
                if (snippetPlayer == null)
                {
                    throw new InvalidOperationException($"The generated prefab at '{prefabAssetPath}' no longer contains a SnippetPlayer component.");
                }

                snippetPlayer.Value = snippetData;

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabAssetPath);
                if (savedPrefab == null)
                {
                    throw new IOException($"Unity failed to update the generated prefab at '{prefabAssetPath}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static void SynchronizeRawAssets(string sourceRootFolder, string destinationRootFolder)
        {
            Directory.CreateDirectory(destinationRootFolder);

            var sourceFilesByRelativePath = Directory
                .GetFiles(sourceRootFolder, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    path => GetRelativePathNormalized(sourceRootFolder, path),
                    path => path,
                    StringComparer.OrdinalIgnoreCase);

            var destinationFiles = Directory
                .GetFiles(destinationRootFolder, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var destinationFile in destinationFiles)
            {
                string relativePath = GetRelativePathNormalized(destinationRootFolder, destinationFile);
                if (sourceFilesByRelativePath.ContainsKey(relativePath))
                {
                    continue;
                }

                File.Delete(destinationFile);

                string metaFilePath = $"{destinationFile}.meta";
                if (File.Exists(metaFilePath))
                {
                    File.Delete(metaFilePath);
                }
            }

            foreach (var sourceFileEntry in sourceFilesByRelativePath)
            {
                string destinationFilePath = Path.Combine(destinationRootFolder, sourceFileEntry.Key.Replace('/', Path.DirectorySeparatorChar));
                string destinationDirectory = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourceFileEntry.Value, destinationFilePath, true);
            }

            DeleteEmptyDirectories(destinationRootFolder);
        }

        private static void RemapDtoPaths(SnippetsSetDataDto snippetsSetDataDto, string originalRootFolder, string destinationRootFolder)
        {
            if (snippetsSetDataDto == null)
            {
                return;
            }

            snippetsSetDataDto.Metadata.ThumbnailPath = RemapPath(snippetsSetDataDto.Metadata.ThumbnailPath, originalRootFolder, destinationRootFolder);

            for (int i = 0; i < snippetsSetDataDto.Snippets.Length; i++)
            {
                snippetsSetDataDto.Snippets[i].SoundFilePath = RemapPath(snippetsSetDataDto.Snippets[i].SoundFilePath, originalRootFolder, destinationRootFolder);
                snippetsSetDataDto.Snippets[i].AnimationFilePath = RemapPath(snippetsSetDataDto.Snippets[i].AnimationFilePath, originalRootFolder, destinationRootFolder);
            }
        }

        private static void PrepareBackupFolder(string rawBackupFolder)
        {
            DeleteDirectoryIfExists(rawBackupFolder);
        }

        private static void BackupRawFolder(string rawFolder, string rawBackupFolder)
        {
            if (!Directory.Exists(rawFolder))
            {
                return;
            }

            MoveProjectFolder(rawFolder, rawBackupFolder);
        }

        private static void RestoreRawFolderBackup(string rawFolder, string rawBackupFolder)
        {
            DeleteDirectoryIfExists(rawFolder);

            if (!Directory.Exists(rawBackupFolder))
            {
                return;
            }

            MoveProjectFolder(rawBackupFolder, rawFolder);
        }

        private static void DeleteDirectoryIfExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            string assetPath = IoUtilities.GetProjectRelativePath(directoryPath);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                if (!AssetDatabase.DeleteAsset(assetPath))
                {
                    throw new IOException($"Failed to delete asset folder '{assetPath}'.");
                }

                return;
            }

            Directory.Delete(directoryPath, true);
        }

        private static void MoveProjectFolder(string sourceFolder, string destinationFolder)
        {
            string sourceAssetPath = IoUtilities.GetProjectRelativePath(sourceFolder);
            string destinationAssetPath = IoUtilities.GetProjectRelativePath(destinationFolder);

            if (string.IsNullOrWhiteSpace(sourceAssetPath) || string.IsNullOrWhiteSpace(destinationAssetPath))
            {
                throw new IOException("Expected to move a folder inside the Unity project's Assets folder.");
            }

            string destinationParentFolder = Path.GetDirectoryName(destinationFolder);
            if (!string.IsNullOrWhiteSpace(destinationParentFolder))
            {
                EnsureProjectFolderExists(destinationParentFolder);
            }

            string moveError = AssetDatabase.MoveAsset(sourceAssetPath, destinationAssetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                throw new IOException($"Failed to move asset folder from '{sourceAssetPath}' to '{destinationAssetPath}'. Unity reports: {moveError}");
            }
        }

        private static void EnsureProjectFolderExists(string fullFolderPath)
        {
            string assetPath = IoUtilities.GetProjectRelativePath(fullFolderPath);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Directory.CreateDirectory(fullFolderPath);
                return;
            }

            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string[] pathParts = assetPath.Split('/');
            if (pathParts.Length == 0 || !string.Equals(pathParts[0], "Assets", StringComparison.Ordinal))
            {
                throw new IOException($"Expected a project-relative folder under Assets, but got '{assetPath}'.");
            }

            string currentPath = "Assets";
            for (int i = 1; i < pathParts.Length; i++)
            {
                string nextPath = $"{currentPath}/{pathParts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    string guid = AssetDatabase.CreateFolder(currentPath, pathParts[i]);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        throw new IOException($"Failed to create folder '{nextPath}' in the Unity AssetDatabase.");
                    }
                }

                currentPath = nextPath;
            }
        }

        private static string RemapPath(string pathToRemap, string originalRootFolder, string destinationRootFolder)
        {
            if (string.IsNullOrWhiteSpace(pathToRemap))
            {
                return pathToRemap;
            }

            string normalizedOriginalRoot = Path.GetFullPath(originalRootFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(pathToRemap);

            if (!normalizedPath.StartsWith(normalizedOriginalRoot, StringComparison.OrdinalIgnoreCase))
            {
                return pathToRemap;
            }

            string relativePath = normalizedPath.Substring(normalizedOriginalRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(destinationRootFolder, relativePath);
        }

        private static string GetRelativePathNormalized(string rootFolder, string filePath)
        {
            string normalizedRoot = Path.GetFullPath(rootFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedFilePath = Path.GetFullPath(filePath);

            if (!normalizedFilePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"The path '{filePath}' is not inside the expected root folder '{rootFolder}'.");
            }

            return normalizedFilePath.Substring(normalizedRoot.Length).Replace('\\', '/');
        }

        private static void DeleteEmptyDirectories(string rootFolder)
        {
            if (!Directory.Exists(rootFolder))
            {
                return;
            }

            var directories = Directory.GetDirectories(rootFolder, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .ToArray();

            foreach (var directory in directories)
            {
                bool hasFiles = Directory.EnumerateFiles(directory).Any();
                bool hasDirectories = Directory.EnumerateDirectories(directory).Any();
                if (!hasFiles && !hasDirectories)
                {
                    Directory.Delete(directory);

                    string metaFilePath = $"{directory}.meta";
                    if (File.Exists(metaFilePath))
                    {
                        File.Delete(metaFilePath);
                    }
                }
            }
        }

        private static bool PathsEqual(string firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            string normalizedFirstPath = firstPath.Replace('\\', '/').TrimEnd('/');
            string normalizedSecondPath = secondPath.Replace('\\', '/').TrimEnd('/');

            return string.Equals(normalizedFirstPath, normalizedSecondPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
