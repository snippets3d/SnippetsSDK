using System.IO.Compression;
using System.IO;
using UnityEngine;
using UnityEditor;
using System;

namespace Snippets.Sdk
{
    /// <summary>
    /// Utility class to compress and decompress Snippets Set data into a zip file.
    /// </summary>
    public class SnippetsSetZipper : ISnippetsSetZipper
    {
        /// <summary>
        /// The name of the metadata file in the zip file.
        /// </summary>
        public const string MetaDataFilename = "metadata.json";

        /// <summary>
        /// The suffix added to the animation files when unzipped from the zip file.
        /// This is necessary to disambiguate the Snippets' animation files from other files in the project.
        /// </summary>
        public const string AnimationFileSuffix = "_SNP";

#if UNITY_EDITOR
        /// <inheritdoc />        
        public string CompressSnippetsSet(SnippetsSetData snippetsSet, string outputFolder)
        {
            return CompressSnippetsSet(new SnippetsSetDataDto(snippetsSet), outputFolder);
        }
#endif

        /// <inheritdoc />      
        public string CompressSnippetsSet(SnippetsSetDataDto snippetsSet, string outputFolderPath)
        {
            //create a snippet set dto to point to the relative paths of files. This is necessary because the snippet set
            //will contain absolute paths, but we need to create a zip file with a main "folder"
            //and then an internal folder for every snippet. So the manifest contained in the zip, which
            //is a serialization of the Snippet Set DTO, must contain relative paths to the file
            SnippetsSetDataDto snippetsSetDtoRelativePaths = DeepCopyDto(snippetsSet);

            //create the output directory if it does not exist
            Directory.CreateDirectory(outputFolderPath);

            //create a zip file to host the snippet set data. If the file exists, delete it
            var zipFileName = $"{snippetsSet.Metadata.Id}.zip";
            var zipFilePath = Path.Combine(outputFolderPath, zipFileName);

            if (File.Exists(zipFilePath))
                File.Delete(zipFilePath);

            using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                //if there is a path to the thumbnail, add it to the archive.
                if (!string.IsNullOrEmpty(snippetsSet.Metadata.ThumbnailPath))
                {
                    var relativeFileName = GetArchiveFileRelativePath(snippetsSet.Metadata.ThumbnailPath, "");
                    archive.CreateEntryFromFile(snippetsSet.Metadata.ThumbnailPath, relativeFileName);
                    snippetsSetDtoRelativePaths.Metadata.ThumbnailPath = relativeFileName;
                }

                //for each snippet in the set, add the sound and animation files to the archive.
                for (int i = 0; i < snippetsSet.Snippets.Length; i++)
                {
                    var snippetDto = snippetsSet.Snippets[i];

                    //create the archive entries and also add the relative paths to the snippet set dto
                    var relativeFileName = GetArchiveFileRelativePath(snippetDto.SoundFilePath, snippetDto.Id);
                    archive.CreateEntryFromFile(snippetDto.SoundFilePath, relativeFileName);
                    snippetsSetDtoRelativePaths.Snippets[i].SoundFilePath = relativeFileName;

                    relativeFileName = GetArchiveFileRelativePath(snippetDto.AnimationFilePath, snippetDto.Id);
                    archive.CreateEntryFromFile(snippetDto.AnimationFilePath, relativeFileName);
                    snippetsSetDtoRelativePaths.Snippets[i].AnimationFilePath = relativeFileName;
                }

                //serialize the snippet set dto with the relative paths and add it to the archive, so that it acts
                //as a manifest for the snippets contained in the zip
                string relativeFilesMetadata = JsonUtility.ToJson(snippetsSetDtoRelativePaths, true);

                using (StreamWriter writer = new StreamWriter(archive.CreateEntry(MetaDataFilename).Open()))
                {
                    writer.Write(relativeFilesMetadata);
                }

                //return the path of the archive
                return zipFilePath;
            }
        }

#if UNITY_EDITOR

        /// <inheritdoc />      
        public SnippetsSetData DecompressSnippetsSet(string zipFilePath, string outputFolderPath)
        {
            // Decompress to DTO first
            SnippetsSetDataDto snippetsSetDto = DecompressSnippetsSetDto(zipFilePath, outputFolderPath);

            // Wait for the files unzipped in the project folder to be imported
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Set the import settings of the thumbnail to Sprite (by default, it is Texture).
            // This is necessary because the Snippets data structures use Sprites internally
            SetTextureImporterSettings(IoUtilities.GetProjectRelativePath(snippetsSetDto.Metadata.ThumbnailPath), TextureImporterType.Sprite);

            // Convert DTO to SnippetsSetData
            return new SnippetsSetData(snippetsSetDto);
        }

        /// <summary>
        /// Sets the import settings for a texture asset.
        /// </summary>
        /// <param name="assetPath">The path of the asset to set the import settings for.</param>
        /// <param name="importerType">The type of the texture importer.</param>
        private void SetTextureImporterSettings(string assetPath, UnityEditor.TextureImporterType importerType)
        {
            UnityEditor.TextureImporter textureImporter = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = importerType;
                textureImporter.SaveAndReimport();
            }
        }
#endif

        /// <inheritdoc />      
        public SnippetsSetDataDto DecompressSnippetsSetDto(string zipFilePath, string outputFolderPath)
        {
            //create the output directory if it does not exist
            Directory.CreateDirectory(outputFolderPath);

            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                //extract all files in the archive to the output folder
                archive.ExtractToDirectory(outputFolderPath);

                //read the metadata file
                string metadataFilePath = Path.Combine(outputFolderPath, MetaDataFilename);
                string metadataJson = File.ReadAllText(metadataFilePath);

                //deserialize the metadata, imagining it is in the SDK format
                SnippetsSetDataDto snippetsSetDto = GetDtoFromSdkJsonMetadata(metadataJson);

                //if the metadata is not in the SDK format, try to deserialize it as a server format
                if (snippetsSetDto == null)
                {
                    snippetsSetDto = GetDtoFromServerJsonMetadata(metadataJson);
                }

                //if the metadata is not in any of the expected formats, throw an exception
                if (snippetsSetDto == null)
                {
                    throw new System.ArgumentException("The metadata file in the ZIP file is not in any of the expected formats.");
                }

                //update the paths in the DTO to be absolute paths
                if (!string.IsNullOrEmpty(snippetsSetDto.Metadata.ThumbnailPath))
                {
                    snippetsSetDto.Metadata.ThumbnailPath = Path.Combine(outputFolderPath, snippetsSetDto.Metadata.ThumbnailPath);
                }

                for (int i = 0; i < snippetsSetDto.Snippets.Length; i++)
                {
                    snippetsSetDto.Snippets[i].SoundFilePath = Path.Combine(outputFolderPath, snippetsSetDto.Snippets[i].SoundFilePath);

                    // Add the AnimationFileSuffix to the animation file name. This will help disambiguate which animation files are from Snippets
                    // so the GLTF importer can treat them in a different way
                    string animationFilePath = Path.Combine(outputFolderPath, snippetsSetDto.Snippets[i].AnimationFilePath);
                    string newAnimationFilePath = Path.Combine(Path.GetDirectoryName(animationFilePath), 
                        Path.GetFileNameWithoutExtension(animationFilePath) + AnimationFileSuffix + Path.GetExtension(animationFilePath));
                    File.Move(animationFilePath, newAnimationFilePath);
                    snippetsSetDto.Snippets[i].AnimationFilePath = newAnimationFilePath;
                }

                //return the SnippetsSetDataDto object
                return snippetsSetDto;
            }
        }

        /// <summary>
        /// Performs a deep copy of a <see cref="SnippetsSetDto"/> object
        /// </summary>
        /// <param name="original">Snippets set dto to copy</param>
        /// <returns>Deep copy of the snippets set</returns>
        private SnippetsSetDataDto DeepCopyDto(SnippetsSetDataDto original)
        {
            //perform a deep copy of the snippets set
            var copySnippetsSet = new SnippetsSetDataDto
            {
                Metadata = new SnippetsSetMetadataDto
                {
                    Id = original.Metadata.Id,
                    Name = original.Metadata.Name,
                    Description = original.Metadata.Description,
                    Version = original.Metadata.Version,
                    ThumbnailPath = original.Metadata.ThumbnailPath
                },
                Snippets = new SnippetDataDto[original.Snippets.Length]
            };

            //copy the snippets
            for (int i = 0; i < original.Snippets.Length; i++)
            {
                copySnippetsSet.Snippets[i] = new SnippetDataDto
                {
                    Id = original.Snippets[i].Id,
                    Name = original.Snippets[i].Name,
                    Text = original.Snippets[i].Text,
                    SoundFilePath = original.Snippets[i].SoundFilePath,
                    AnimationFilePath = original.Snippets[i].AnimationFilePath
                };
            }

            return copySnippetsSet;
        }

        /// <summary>
        /// Gets the relative path of a file that should be saved in the zip file.
        /// </summary>
        /// <param name="filePath">The absolute path of the file</param>
        /// <param name="id">Id of the snippet the file belongs to</param>
        private string GetArchiveFileRelativePath(string filePath, string id)
        {
            return Path.Combine(id, Path.GetFileName(filePath));
        }

        /// <summary>
        /// Tries to decode the text of a ZIP metadata file presuming it is in the SDK format.
        /// (The SDK format is created when the file is compressed with the methods of this class)
        /// </summary>
        /// <param name="sdkJsonMetadata">Text of the metadata file</param>
        /// <returns>Valid Snippets set DTO data, or null if the decoding failed</returns>
        private SnippetsSetDataDto GetDtoFromSdkJsonMetadata(string sdkJsonMetadata)
        {
            //try to deserialize the metadata
            SnippetsSetDataDto snippetsSetDto = JsonUtility.FromJson<SnippetsSetDataDto>(sdkJsonMetadata);

            //if the metadata is not valid (not even the id is retrieved), return null
            if (snippetsSetDto.Metadata == null || string.IsNullOrEmpty(snippetsSetDto.Metadata.Id))
                return null;
            //if data makes sense, return it
            else
                return snippetsSetDto;
        }

        /// <summary>
        /// Tries to decode the text of a ZIP metadata file presuming it is in the server format.
        /// (The server format is created when the file is compressed by the server)
        /// </summary>
        /// <param name="serverJsonMetadata">Text of the metadata file</param>
        /// <returns>Valid Snippets set DTO data, or null if the decoding failed</returns>
        private SnippetsSetDataDto GetDtoFromServerJsonMetadata(string serverJsonMetadata)
        {
            //try to deserialize the metadata
            ServerSnippetsSetDataDto serverSnippetsSetDto = JsonUtility.FromJson<ServerSnippetsSetDataDto>(serverJsonMetadata);

            //if the metadata is not valid (not even the id is retrieved), return null
            if (serverSnippetsSetDto.metadata == null || string.IsNullOrEmpty(serverSnippetsSetDto.metadata.id))
            {
                return null;
            }

            //else, it means data is valid, so create a SnippetsSetDataDto object from it 
            //by just copying the data
            SnippetsSetDataDto snippetsSetDataDto = new SnippetsSetDataDto
            {
                Metadata = new SnippetsSetMetadataDto
                {
                    Id = serverSnippetsSetDto.metadata.id,
                    Name = serverSnippetsSetDto.metadata.name,
                    Description = serverSnippetsSetDto.metadata.description,
                    Version = serverSnippetsSetDto.metadata.version,
                    ThumbnailPath = serverSnippetsSetDto.metadata.thumbnail_path
                },
                Snippets = new SnippetDataDto[serverSnippetsSetDto.snippets.Length]
            };

            for (int i = 0; i < serverSnippetsSetDto.snippets.Length; i++)
            {
                snippetsSetDataDto.Snippets[i] = new SnippetDataDto
                {
                    Id = serverSnippetsSetDto.snippets[i].id,
                    Text = serverSnippetsSetDto.snippets[i].text,
                    Name = serverSnippetsSetDto.snippets[i].name ?? "Snippet" + serverSnippetsSetDto.snippets[i].id,
                    SoundFilePath = serverSnippetsSetDto.snippets[i].sound_file_path,
                    AnimationFilePath = serverSnippetsSetDto.snippets[i].animation_file_path
                };
            }

            //return the converted data
            return snippetsSetDataDto;
        }

        #region Serialization data structures

        /// <summary>
        /// Serializable form of the data of a set of snippets, as it is provided by the server.
        /// </summary>
        [Serializable]
        public class ServerSnippetsSetDataDto
        {
            public ServerSnippetsSetMetadataDto metadata;
            public ServerSnippetDataDto[] snippets;
        }

        [Serializable]
        public class ServerSnippetsSetMetadataDto
        {
            public string id;
            public string name;
            public string description;
            public string version;
            public string thumbnail_path;
        }

        [Serializable]
        public class ServerSnippetDataDto
        {
            public string id;
            public string text;
            public string name;
            public string sound_file_path;
            public string animation_file_path;
        }

        #endregion
    }

}