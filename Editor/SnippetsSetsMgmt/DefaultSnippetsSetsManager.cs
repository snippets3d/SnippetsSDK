using System;
using System.IO;
using System.Threading.Tasks;
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

            //download the raw snippets zip
            string rawFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsRawFolder(m_projectSettings, snippetSetMetaData);

            progress?.Report(progressStartValue);

            var downloadResponse = await m_snippetSetsProvider.DownloadSnippetsSet(snippetSetMetaData.Id, rawFolder);

            if (!downloadResponse.IsSuccessful)
            {
                throw new IOException($"Error downloading the snippets set {snippetSetMetaData.Name}: {downloadResponse.Message}");
            }

            progress?.Report(progressDownloadedValue);

            //unzip the raw snippets in the zip folder, get the data about the decompressed snippet set
            //then delete the zip
            var snippetsSetData = m_snippetsSetZipper.DecompressSnippetsSet(downloadResponse.Value, rawFolder);
            File.Delete(downloadResponse.Value);

            progress?.Report(progressLoadedValue);

            //loop through the snippet data and generate the prefabs
            string generatedFolder = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, snippetSetMetaData);

            for(int i = 0; i < snippetsSetData.Snippets.Length; i++)
            {
                var snippetData = snippetsSetData.Snippets[i];
                
                SnippetGenerator.GenerateSnippetPrefab(snippetData, generatedFolder);
                progress?.Report(progressLoadedValue + (progressEndValue - progressLoadedValue) * i / snippetsSetData.Snippets.Length);
            }

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
            const float progressRemovedValue = 0.2f;
            const float progressEndValue = 1;

            //check if the SnippetGenerator property has been set
            if (SnippetGenerator == null)
            {
                throw new InvalidOperationException("The SnippetGenerator property must be set before calling any method of this class");
            }

            //we can not pass directly pass the progress object to the async methods,
            //or the removal will go from 0 o 1 and then the import will go from 0 to 1 again.
            //We need to create custom progress objects with custom ranges for both operations
            ProgressWithCustomFloatRange progressWithCustomFloatRangeRemoval = progress == null ? null : new ProgressWithCustomFloatRange(progressStartValue, progressRemovedValue);
            ProgressWithCustomFloatRange progressWithCustomFloatRangeImporting = progress == null ? null : new ProgressWithCustomFloatRange(progressRemovedValue, progressEndValue);

            //map the progress of the custom progress objects to the original progress object
            //(no need to deregister because these objects will be garbage collected at the end of this method)
            if (progress != null)
            {
                progressWithCustomFloatRangeRemoval.ProgressChanged += (sender, value) => progress?.Report(value);
                progressWithCustomFloatRangeImporting.ProgressChanged += (sender, value) => progress?.Report(value);
            }

            // For now, for simplicity, we consider an update like a remove and 
            // a new import of the same snippets set
            await RemoveSnippetsSet(oldSnippetSetMetaData, progressWithCustomFloatRangeRemoval);
            var result = await ImportSnippetsSet(newSnippetSetMetaData, progressWithCustomFloatRangeImporting);

            // Trigger the event
            OnSnippetsSetUpdated?.Invoke(newSnippetSetMetaData.Name);

            return result;
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
    }
}