
namespace Snippets.Sdk
{
    /// <summary>
    /// Represents a generic answer from the API
    /// </summary>
    public interface IApiResponse
    {
        /// <summary>
        /// The result of the request.
        /// Notice that these are generic codes that evaluate if the request was correctly handled, 
        /// for more specific errors check <see cref="ErrorMessage"/>
        /// or eventual additional codes specified by the subclasses
        /// </summary>
        ApiResponseCode ResponseCode { get; }

        /// <summary>
        /// Message reported by the API.
        /// In case of successful request, this can be null
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Get whether the request was successful
        /// </summary>
        bool IsSuccessful => ResponseCode == ApiResponseCode.Ok;
    }

    /// <summary>
    /// Generic response class for API operations.
    /// </summary>
    public class ApiResponse : IApiResponse
    {
        /// <inheritdoc/>
        public ApiResponseCode ResponseCode { get; }

        /// <inheritdoc/>
        public string Message { get; }

        /// <inheritdoc/>
        public bool IsSuccessful => ResponseCode == ApiResponseCode.Ok;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiResponse{ResponseValueType}"/> class.
        /// </summary>
        /// <param name="responseCode">The response code.</param>
        /// <param name="message">The message, if any.</param>
        public ApiResponse(ApiResponseCode responseCode, string message = null)
        {
            ResponseCode = responseCode;
            Message = message;
        }
    }

    /// <summary>
    /// Represents a generic answer from the API that carries a value
    /// </summary>
    /// <typeparam name="ResponseValueType">The type of value contained in the response</typeparam>
    public interface IApiResponse<ResponseValueType>: IApiResponse
    {
        /// <summary>
        /// The value returned by the API
        /// </summary>
        public ResponseValueType Value { get; }
    }

    /// <summary>
    /// Generic response class for API operations that carry a value
    /// </summary>
    public class ApiResponse<ResponseValueType> : ApiResponse, IApiResponse<ResponseValueType>
    {
        /// <inheritdoc/>
        public ResponseValueType Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiResponse{ResponseValueType}"/> class.
        /// </summary>
        /// <param name="apiResponseValue">The value returned by the operation.</param>
        /// <param name="responseCode">The response code.</param>
        /// <param name="message">The message, if any.</param>
        public ApiResponse(ResponseValueType apiResponseValue, ApiResponseCode responseCode, string message = null) :
            base(responseCode, message)
        {
            Value = apiResponseValue;
        }
    }

    /// <summary>
    /// Error codes returned by the APIs
    /// </summary>
    public enum ApiResponseCode
    {
        /// <summary>
        /// Request was successful
        /// </summary>
        Ok,

        /// <summary>
        /// Requested resource was not found
        /// </summary>
        NotFound,

        /// <summary>
        /// Request was canceled
        /// </summary>
        Canceled,

        /// <summary>
        /// The backend is unavailable
        /// </summary>
        Unavailable,

        /// <summary>
        /// The caller was not authorized to perform the request
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Request timed out
        /// </summary>
        Timeout,

        /// <summary>
        /// Generic request failed error
        /// </summary>
        Error
    }
}