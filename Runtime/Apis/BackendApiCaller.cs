using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Snippets.Sdk
{
    /// <summary>
    /// Provides facilities to call the cloud backend REST API related to Snippets
    /// </summary>
    public class BackendApiCaller : IBackendApiCaller
    {
        /// <summary>
        /// The default base URL for the Snippets API
        /// </summary>
        public const string DefaultBaseUrl = "https://api.snippets3d.com/api/v1";

        /// <summary>
        /// The base URL for the REST API calls to perform
        /// </summary>
        private string m_baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendApiCaller"/> class.
        /// </summary>
        /// <param name="baseUrl">The base URL for the backend calls that use a relative endpoint</param>
        public BackendApiCaller(string baseUrl = DefaultBaseUrl)
        {
            m_baseUrl = baseUrl;
        }

        /// <inheritdoc />
        public async Task<IBackendApiResponse<TReturnValueType>> JsonRestPostRequest<TBodyValueType, TReturnValueType>(string endpoint,
            TBodyValueType parameter, string authToken = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Prepare the request
                var jsonData = JsonUtility.ToJson(parameter);

                // if the uri is an absolute one, use it as is. If it is relative, use it as a relative path to the base URL
                var uri = new Uri(endpoint, UriKind.RelativeOrAbsolute);
                var fullEndpoint = uri.IsAbsoluteUri ? endpoint : m_baseUrl + endpoint;

                using (UnityWebRequest www = UnityWebRequest.Post(fullEndpoint, jsonData, "application/json"))
                {
                    // Add authentication header, if needed
                    if (!string.IsNullOrEmpty(authToken))
                    {
                        www.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    }

                    // Perform the request
                    var asyncOperation = www.SendWebRequest();

                    // Wait for the operation to complete or be cancelled
                    await asyncOperation.ToUniTask(cancellationToken: cancellationToken);

                    // Check for success. In some cases failure will be signaled with the return value, and in some cases with an exception
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        // Decode the answer
                        var returnValue = JsonUtility.FromJson<TReturnValueType>(www.downloadHandler.text);

                        return new BackendApiResponse<TReturnValueType>(returnValue, ApiResponseCode.Ok, www.responseCode);
                    }
                    // The request failed
                    else
                    {
                        return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Error, www.responseCode, www.error);
                    }
                }
            }
            // The request threw a web exception
            catch (UnityWebRequestException e)
            {
                return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Error, e.ResponseCode, e.Message);
            }
            // The request was canceled
            catch (OperationCanceledException)
            {
                return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Canceled, 0, "The operation was canceled");
            }
            // The request threw another exception
            catch (Exception e)
            {
                return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Error, 0, e.Message);
            }
        }

        /// <inheritdoc />
        public async Task<IBackendApiResponse<TReturnValueType>> JsonRestGetRequest<TReturnValueType>(string endpoint,
            string authToken = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var uri = new Uri(endpoint, UriKind.RelativeOrAbsolute);
                var fullEndpoint = uri.IsAbsoluteUri ? endpoint : m_baseUrl + endpoint;

                using (UnityWebRequest www = UnityWebRequest.Get(fullEndpoint))
                {
                    // Add authentication header, if needed
                    if (!string.IsNullOrEmpty(authToken))
                    {
                        www.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    }

                    // Perform the request
                    var asyncOperation = www.SendWebRequest();

                    // Wait for the operation to complete or be cancelled
                    await asyncOperation.ToUniTask(cancellationToken: cancellationToken);

                    // Check for success. In some cases failure will be signaled with the return value, and in some cases with an exception
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        // Decode the answer
                        var returnValue = JsonUtility.FromJson<TReturnValueType>(www.downloadHandler.text);

                        return new BackendApiResponse<TReturnValueType>(returnValue, ApiResponseCode.Ok, www.responseCode);
                    }
                    // The request failed
                    else
                    {
                        return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Error, www.responseCode, www.error);
                    }
                }
            }
            // The request threw a web exception
            catch (UnityWebRequestException e)
            {
                return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Error, e.ResponseCode, e.Message);
            }
            // The request was canceled
            catch (OperationCanceledException)
            {
                return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Canceled, 0, "The operation was canceled");
            }
            // The request threw another exception
            catch (Exception e)
            {
                return new BackendApiResponse<TReturnValueType>(default(TReturnValueType), ApiResponseCode.Error, 0, e.Message);
            }
        }

        /// <inheritdoc />
        public async Task<IBackendApiResponse> RestGetDownloadFileRequest(string endpoint, string downloadFilePath,
            string authToken = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var uri = new Uri(endpoint, UriKind.RelativeOrAbsolute);
                var fullEndpoint = uri.IsAbsoluteUri ? endpoint : m_baseUrl + endpoint;

                using (UnityWebRequest www = UnityWebRequest.Get(fullEndpoint))
                {
                    // Ensure the destination directory exists
                    var directory = System.IO.Path.GetDirectoryName(downloadFilePath);

                    if (!System.IO.Directory.Exists(directory))
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }

                    // Add authentication header, if needed
                    if (!string.IsNullOrEmpty(authToken))
                    {
                        www.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    }

                    // Prepare the download handler so that the file gets directly saved to the specified path
                    // without buffering everything in memory
                    DownloadHandlerFile downloadHandler = new DownloadHandlerFile(downloadFilePath);
                    www.downloadHandler = downloadHandler;

                    // Perform the request
                    var asyncOperation = www.SendWebRequest();

                    // Wait for the operation to complete or be cancelled
                    await asyncOperation.ToUniTask(cancellationToken: cancellationToken);

                    // Check for success. In some cases failure will be signaled with the return value, and in some cases with an exception
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        return new BackendApiResponse(ApiResponseCode.Ok, www.responseCode);
                    }
                    // The request failed
                    else
                    {
                        File.Delete(downloadFilePath);

                        return new BackendApiResponse(ApiResponseCode.Error, www.responseCode, www.error);
                    }
                }
            }
            // The request threw a web exception
            catch (UnityWebRequestException e)
            {
                File.Delete(downloadFilePath);

                return new BackendApiResponse(ApiResponseCode.Error, e.ResponseCode, e.Message);
            }
            // The request was canceled
            catch (OperationCanceledException)
            {
                File.Delete(downloadFilePath);

                return new BackendApiResponse(ApiResponseCode.Canceled, 0, "The operation was canceled");
            }
            // The request threw another exception
            catch (Exception e)
            {
                File.Delete(downloadFilePath);

                return new BackendApiResponse(ApiResponseCode.Error, 0, e.Message);
            }
        }
    }
}
