
namespace Snippets.Sdk
{
    /// <summary>
    /// Base interface for response classes for API operations related to a remote backend
    /// </summary>
    public interface IBackendApiResponse : IApiResponse
    {
        /// <summary>
        /// The HTTP error code returned by the backend API
        /// </summary>
        public long HttpErrorCode { get; }
    }

    /// <summary>
    /// Response class for API operations related to a remote backend
    /// </summary>
    public class BackendApiResponse : ApiResponse,
        IBackendApiResponse
    {
        /// <inheritdoc />
        public long HttpErrorCode { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendApiResponse"/> class.
        /// </summary>
        /// <param name="responseCode">The response code.</param>
        /// <param name="httpErrorCode">The HTTP error code returned by the backend API.</param>
        /// <param name="message">The message, if any.</param>
        public BackendApiResponse(ApiResponseCode responseCode, long httpErrorCode, string message = null) :
            base(responseCode, message)
        {
            HttpErrorCode = httpErrorCode;
        }
    }

    /// <summary>
    /// Base interface for response classes for API operations related to a remote backend
    /// </summary>
    public interface IBackendApiResponse<ResponseValueType> : IBackendApiResponse, IApiResponse<ResponseValueType>
    {
    }

    /// <summary>
    /// Response class for API operations related to a remote backend
    /// </summary>
    public class BackendApiResponse<ResponseValueType> : ApiResponse<ResponseValueType>,
        IBackendApiResponse<ResponseValueType>
    {
        /// <inheritdoc />
        public long HttpErrorCode { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendApiResponse{ResponseValueType}"/> class.
        /// </summary>
        /// <param name="apiResponseValue">The value returned by the operation.</param>
        /// <param name="responseCode">The response code.</param>
        /// <param name="httpErrorCode">The HTTP error code returned by the backend API.</param>
        /// <param name="message">The message, if any.</param>
        public BackendApiResponse(ResponseValueType apiResponseValue, ApiResponseCode responseCode, long httpErrorCode, string message = null) :
            base(apiResponseValue, responseCode, message)
        {
            HttpErrorCode = httpErrorCode;
        }
    }

}