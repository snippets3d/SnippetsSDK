
using System.Threading;
using System.Threading.Tasks;

namespace Snippets.Sdk
{
    /// <summary>
    /// Interface for managing login operations.
    /// </summary>
    public interface ILoginManager
    {
        /// <summary>
        /// Gets the current logged in user.
        /// It is null if no user is logged in.
        /// </summary>
        string CurrentUser { get; }

        /// <summary>
        /// Gets the current token.
        /// It is null if no user is logged in.
        /// </summary>
        string CurrentToken { get; }

        /// <summary>
        /// Gets a value indicating whether the user is logged in.
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// Logs in with username and password.
        /// </summary>
        /// <remarks>
        /// If the operation is successful, some credentials will be saved
        /// for future logins with <see cref="LoginWithLastValidCredentials"/>.
        /// </remarks>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="cancellationToken">The cancellation token to abort the operation.</param>
        /// <returns>The result of the login operation.</returns>
        Task<LoginResponse> LoginWithUserPassword(string username, string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs in with username and token.
        /// </summary>
        /// <remarks>
        /// If the operation is successful, some credentials will be saved
        /// for future logins with <see cref="LoginWithLastValidCredentials"/>.
        /// </remarks>
        /// <param name="username">The username.</param>
        /// <param name="token">The token.</param>
        /// <param name="cancellationToken">The cancellation token to abort the operation.</param>
        /// <returns>The result of the login operation.</returns>
        Task<LoginResponse> LoginWithUserToken(string username, string token, CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs in with the last valid credentials.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to abort the operation.</param>
        /// <returns>The result of the login operation.</returns>
        Task<LoginResponse> LoginWithLastValidCredentials(CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs out.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to abort the operation.</param>
        /// <returns>The result of the logout operation.</returns>
        Task<LoginResponse> Logout(CancellationToken cancellationToken = default);
    }
}