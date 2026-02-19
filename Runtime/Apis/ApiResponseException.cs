using System;

namespace Snippets.Sdk
{
    /// <summary>
    /// Exception thrown when the some apis returns an error.
    /// </summary>
    public class ApiResponseException : Exception
    {
        /// <summary>
        /// Response code returned by the backend.
        /// </summary>
        public ApiResponseCode ResponseCode { get; }

        /// <summary>
        /// Constructor with full initialization
        /// </summary>
        /// <param name="message">Textual explanation of the exception</param>
        /// <param name="responseCode">Response code returned by the backend the caused this exception</param>
        public ApiResponseException(string message, ApiResponseCode responseCode = ApiResponseCode.Error) : base(message)
        {
            ResponseCode = responseCode;
        }
    }
}