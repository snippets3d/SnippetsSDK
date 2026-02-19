using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Snippets.Sdk
{
    /// <summary>
    /// Interface for elements that provide facilities to call the cloud backend REST API related to Snippets
    /// </summary>
    public interface IBackendApiCaller
    {
        /// <summary>
        /// Performs a REST POST request to the backend API where the body is a JSON object and the return value is a JSON object
        /// </summary>
        /// <typeparam name="TBodyValueType">Object type to be encoded into the body of the request</typeparam>
        /// <typeparam name="TReturnValueType">Expected object type of the </typeparam>
        /// <param name="endpoint">The endpoint to send the request to. It will be concatenated to the base url</param>
        /// <param name="parameter">The parameter of the API call to encode into the body</param>
        /// <param name="authToken">The authentication token to use in the request. It can be null if no authentication is needed</param>
        /// <param name="cancellationToken">The cancellation token to abort the operation</param>
        /// <returns>Result of the request</returns>
        public Task<IBackendApiResponse<TReturnValueType>> JsonRestPostRequest<TBodyValueType, TReturnValueType>(string endpoint, 
            TBodyValueType parameter, string authToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a REST GET request to the backend API where the return value is a JSON object
        /// </summary>
        /// <typeparam name="TReturnValueType">Expected object type of the </typeparam>
        /// <param name="endpoint">The endpoint to send the request to, including all the GET parameters. It will be concatenated to the base url</param>
        /// <param name="authToken">The authentication token to use in the request. It can be null if no authentication is needed</param>
        /// <param name="cancellationToken">The cancellation token to abort the operation</param>
        /// <returns>Result of the request</returns>
        public Task<IBackendApiResponse<TReturnValueType>> JsonRestGetRequest<TReturnValueType>(string endpoint,
            string authToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a REST GET request to the backend API where the return value is a JSON object
        /// </summary>
        /// <typeparam name="TReturnValueType">Expected object type of the </typeparam>
        /// <param name="endpoint">The endpoint to send the request to, including all the GET parameters. It will be concatenated to the base url</param>
        /// <param name="downloadFilePath">The full path (file name included) where to save the downloaded file. If the folder does not exist, it gets created</param>
        /// <param name="authToken">The authentication token to use in the request. It can be null if no authentication is needed</param>
        /// <param name="cancellationToken">The cancellation token to abort the operation</param>
        /// <returns>Result of the request</returns>
        public Task<IBackendApiResponse> RestGetDownloadFileRequest(string endpoint, string downloadFilePath,
            string authToken = null, CancellationToken cancellationToken = default);

    }
}
