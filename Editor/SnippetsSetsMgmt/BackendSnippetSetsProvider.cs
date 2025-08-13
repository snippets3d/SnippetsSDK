using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Provides the user's snippet sets retrieving them from the backend
    /// </summary>
    public class BackendSnippetSetsProvider: BackendLoggedInApiModule,
        ISnippetSetsProvider
    {
        /// <summary>
        /// The root URL for the snippets thumbnails. All the snippets thumbnails are relative to this URL.
        /// </summary>
        public string SnippetsThumbnailRootUrl { get; set; } = @"https://app.snippets3d.com/";

        /// <summary>
        /// The name of the directory where to save locally the thumbnails
        /// </summary>
        public string SnippetsThumbnailDirectoryName { get; set; } = @"Thumbnails";

        /// <summary>
        /// The settings for the project snippets
        /// </summary>
        private IProjectSnippetsSettings m_projectSnippetsSettings;

        /// <summary>
        /// Constructor with implicit initialization via <see cref="ServicesMgmt.Services"/>
        /// </summary>
        public BackendSnippetSetsProvider() :
            base()
        {
            m_projectSnippetsSettings = Services.GetService<IProjectSnippetsSettings>();
        }

        /// <summary>
        /// Constructor with explicit initialization
        /// </summary>
        /// <param name="loginManager">The login manager to use in this module</param>
        /// <param name="backendApiCaller">The backend API caller to use in this module</param>
        /// <param name="projectSnippetsSettings">The settings for the project snippets</param>
        public BackendSnippetSetsProvider(ILoginManager loginManager, IBackendApiCaller backendApiCaller, IProjectSnippetsSettings projectSnippetsSettings) :
            base(loginManager, backendApiCaller)
        {
            m_projectSnippetsSettings = projectSnippetsSettings;
        }

        /// <inheritdoc />
        public async Task<ApiResponse<SnippetsSetMetadata[]>> GetAllSnippetsSets(CancellationToken cancellationToken = default)
        {
            //login check
            if(!CheckLoginOrErrorResponse<SnippetsSetMetadata[]>(out ApiResponse<SnippetsSetMetadata[]> errorResponse))
            {
                return errorResponse;
            }

            //call the backend API to get the snippets sets list
            var snippetsReponse = await BackendApiCaller.JsonRestGetRequest<GetSnippetsSetsResponseData>("/avatar/snippetsets/", LoginManager.CurrentToken, cancellationToken);

            // if the call was successful, convert the data to the expected format
            if (snippetsReponse.IsSuccessful)
            {
                var snippetsSetsMetadataDto = new SnippetsSetMetadataDto[snippetsReponse.Value.snippetSets.Length];

                for(int i = 0; i < snippetsSetsMetadataDto.Length; i++)
                {
                    var snippetSetData = snippetsReponse.Value.snippetSets[i];

                    snippetsSetsMetadataDto[i] = ConvertResponseDataToMetadataDto(snippetSetData);

                    //download the thumbnail of the snippet set, if any
                    await DownloadThumbnail(snippetsSetsMetadataDto[i], cancellationToken);
                }

                //force an asset reload, so the Unity editor will import all the downloaded thumbnails,
                //then force them to be loaded as Sprites
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                foreach (var snippetSet in snippetsSetsMetadataDto)
                {
                    AssetsUtilities.SetTextureImporterSettings(snippetSet.ThumbnailPath, TextureImporterType.Sprite);
                }

                //convert the DTOs to the expected format
                //(now we have all the thumbs in the project, so we can do that)
                var snippetsSetsMetadata = new SnippetsSetMetadata[snippetsSetsMetadataDto.Length];

                for (int i = 0; i < snippetsSetsMetadataDto.Length; i++)
                {
                    snippetsSetsMetadata[i] = new SnippetsSetMetadata(snippetsSetsMetadataDto[i]);
                }

                //return the converted data
                return new ApiResponse<SnippetsSetMetadata[]>(snippetsSetsMetadata, ApiResponseCode.Ok);
            }
            //else return the error response
            else
            {
                return new ApiResponse<SnippetsSetMetadata[]>(null, snippetsReponse.ResponseCode, snippetsReponse.Message);
            }
        }

        /// <inheritdoc />
        public async Task<ApiResponse<string>> DownloadSnippetsSet(string id, string folder, CancellationToken cancellationToken = default)
        {
            //login check
            if (!CheckLoginOrErrorResponse<string>(out ApiResponse<string> errorResponse))
            {
                return errorResponse;
            }

            //call the backend API to get the data about the snippets set of interest. Notice that we don't use a cached value from the
            //retrieval of snippet set list because the link for the zip download has an expiration time, so we want to retrieve the data now
            var snippetsReponse = await BackendApiCaller.JsonRestGetRequest<ResponseSnippetsSetData>($"/avatar/snippetsets/{id}/?detailed=false", LoginManager.CurrentToken, cancellationToken);

            // if the call was successful
            if (snippetsReponse.IsSuccessful)
            {
                // get the URL of the zip file
                var zipUri = snippetsReponse.Value.zip_url;

                // construct the path where to save the zip file
                var zipFileDownloadPath =  IoUtilities.GetProjectRelativePath(Path.Combine(folder, $"{snippetsReponse.Value.name}.zip"));

                // perform a call to download the zip file
                var downloadResponse = await BackendApiCaller.RestGetDownloadFileRequest(zipUri, zipFileDownloadPath, null, cancellationToken);

                //check for success and return the response
                return downloadResponse.IsSuccessful ?
                    new ApiResponse<string>(zipFileDownloadPath, ApiResponseCode.Ok) :
                    new ApiResponse<string>(null, downloadResponse.ResponseCode, downloadResponse.Message);
            }
            //else if snippets set data retrieval went wrong, return the error response
            else
            {
                return new ApiResponse<string>(null, snippetsReponse.ResponseCode, snippetsReponse.Message);
            }
        }

        /// <summary>
        /// Downloads the thumbnail image for a snippet set and change the path to the local one
        /// </summary>
        /// <param name="snippetsSet">The snippets set for which the thumbnail should be downloaded</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation</param>
        /// <returns></returns>
        private async Task DownloadThumbnail(SnippetsSetMetadataDto snippetsSet, CancellationToken cancellationToken = default)
        {
            // If the thumbnail path is empty, return
            if (string.IsNullOrEmpty(snippetsSet.ThumbnailPath))
            {
                snippetsSet.ThumbnailPath = null; //if this is null, the UI will use the standard thumbnail
                return;
            }

            try
            {
                // get the absolute thumbnail URI
                var thumbnailUri = new Uri(SnippetsThumbnailRootUrl + snippetsSet.ThumbnailPath);

                // get where to locally download the thumbnail. We'll put them in the folder for raw snippets data, in a dedicated subfolder
                var thumbnailFileName = Path.GetFileName(thumbnailUri.LocalPath);
                var thumbnailLocalPath = IoUtilities.GetProjectRelativePath(Path.Combine(m_projectSnippetsSettings.RawSnippetsDownloadFolder, SnippetsThumbnailDirectoryName, thumbnailFileName));

                // if the thumbnail is not already downloaded, download it.
                // Notice that we are assuming that thumbnails are unique and non-modifiable, so 
                // if the file already exists we are already sure to have the thumbnail we need.
                if (!File.Exists(thumbnailLocalPath))
                {
                    //download the thumbnail
                    var downloadResponse = await BackendApiCaller.RestGetDownloadFileRequest(thumbnailUri.ToString(), thumbnailLocalPath, null, cancellationToken);

                    // if the download was not successful, log the error and return. We can survive without the thumbnail
                    if (!downloadResponse.IsSuccessful)
                    {
                        snippetsSet.ThumbnailPath = null; //if this is null, the UI will use the standard thumbnail
                        Debug.LogError($"[Snippets SDK] Error downloading thumbnail for snippet set {snippetsSet.Name}. The system reports: {downloadResponse.Message}");

                        return;
                    }
                }

                //if we are here, the thumbnail has been downloaded successfully. Save its local path
                snippetsSet.ThumbnailPath = thumbnailLocalPath;

                return;          
            }
            catch (Exception e)
            {
                // Log the error and go on
                snippetsSet.ThumbnailPath = null; //if this is null, the UI will use the standard thumbnail
                Debug.LogError($"[Snippets SDK] Error downloading thumbnail for snippet set {snippetsSet.Name}. The system reports: {e.Message}");
            }
        }

        /// <summary>
        /// Converts the response data to the expected metadata DTO
        /// </summary>
        /// <param name="snippetSetData">Data from the backend</param>
        /// <returns>Metadata of snippets set</returns>
        private SnippetsSetMetadataDto ConvertResponseDataToMetadataDto(ResponseSnippetsSetData snippetSetData)
        {
            return new SnippetsSetMetadataDto()
            {
                Id = snippetSetData.id,
                Name = snippetSetData.name,
                Description = snippetSetData.description ?? string.Empty,
                ThumbnailPath = snippetSetData.thumbnail_path,
                Version = snippetSetData.version,
                Downloadable = snippetSetData.zip_download_ready
            };
        }

        #region Serialization data structures

        [Serializable]
        public class GetSnippetsSetsResponseData
        {
            public ResponseSnippetsSetData[] snippetSets;
        }

        [Serializable]
        public class ResponseSnippetsSetData
        {
            public string id;
            public string name;
            public string description;
            public string thumbnail_path;
            public string version;
            public string api_version;
            public bool zip_download_ready;
            public DateTime updated_at;
            public string zip_url;
        }

        #endregion
    }
}