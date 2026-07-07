
namespace Snippets.Sdk
{
    /// <summary>
    /// Response class for login operations.
    /// </summary>
    public class LoginResponse : IApiResponse<LoginResult>
    {
        /// <inheritdoc/>
        public LoginResult Value { get; }

        /// <inheritdoc/>
        public ApiResponseCode ResponseCode { get; }

        /// <inheritdoc/>
        public string Message { get; }

        /// <inheritdoc/>
        public bool IsSuccessful => ResponseCode == ApiResponseCode.Ok;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginResponse"/> class.
        /// </summary>
        /// <param name="loginResult">The result of the login operation.</param>
        /// <param name="responseCode">The response code.</param>
        /// <param name="message">The message, if any.</param>
        public LoginResponse(LoginResult loginResult, ApiResponseCode responseCode, string message = null)
        {
            Value = loginResult;
            ResponseCode = responseCode;
            Message = message;
        }
    }

    /// <summary>
    /// Result of the login operation
    /// </summary>
    public enum LoginResult
    {
        /// <summary>
        /// The login was successful
        /// </summary>
        Success,

        /// <summary>
        /// The login failed because the credentials provided were wrong
        /// </summary>
        WrongCredentials,

        /// <summary>
        /// The login failed due to a generic error (e.g. server error)
        /// </summary>
        GenericError
    }
}