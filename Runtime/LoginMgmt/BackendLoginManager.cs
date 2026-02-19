using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Snippets.Sdk
{
    /// <summary>
    /// Login Manager that communicates with the Snippets backend
    /// </summary>
    public class BackendLoginManager : ILoginManager, IDisposable
    {
        /// <summary>
        /// The key for storing the username in PlayerPrefs.
        /// </summary>
        private const string UsernameKey = "U";

        /// <summary>
        /// The key for storing the token in PlayerPrefs.
        /// </summary>
        private const string TokenKey = "T";

        /// <summary>
        /// The refresh token.
        /// The access token will be saved in <see cref="CurrentToken"/>.
        /// </summary>
        private string m_refreshToken;

        /// <summary>
        /// The element that is responsible for calling the backend APIs.
        /// </summary>
        private IBackendApiCaller m_backendApiCaller;

        /// <summary>
        /// The cancellation token used to kill the token refresher loop
        /// </summary>
        private CancellationTokenSource m_refreshLoopCancelationTokenSource;

        /// <summary>
        /// Flag to detect redundant calls to the dispose method
        /// </summary>
        private bool m_disposed = false;

        /// <inheritdoc/>
        public string CurrentUser { get; private set; }

        /// <inheritdoc/>
        public string CurrentToken { get; private set; }

        /// <inheritdoc/>
        public bool IsLoggedIn => CurrentUser != null && CurrentToken != null;

        /// <summary>
        /// How much time before the expiration of the tokens they should be renewed, in seconds.
        /// </summary>
        public int RefreshBeforeExpiresInterval { get; set; } = 40; //seconds

        /// <summary>
        /// How many times a token refresh should be retried in the refresh loop before giving up.
        /// </summary>
        public int RefreshRetryTimes { get; set; } = 3;

        /// <summary>
        /// Time interval between two consecutive token refresh retries in the refresh loop before giving up.
        /// </summary>
        public int RefreshRetryInterval { get; set; } = 5; //seconds     

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendLoginManager"/> class
        /// getting the backend API caller from the services.
        /// </summary>
        public BackendLoginManager()
        {
            m_backendApiCaller = Services.GetService<IBackendApiCaller>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendLoginManager"/> class
        /// providing the backend API caller.
        /// </summary>
        /// <param name="backendApiCaller">The element that should perform the API calls</param>
        public BackendLoginManager(IBackendApiCaller backendApiCaller)
        {
            m_backendApiCaller = backendApiCaller;
        }

        /// <inheritdoc/>
        public async Task<LoginResponse> LoginWithUserPassword(string username, string password, CancellationToken cancellationToken = default)
        {
            // Check if data is valid
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return new LoginResponse(LoginResult.GenericError, ApiResponseCode.Error, "Username and password cannot be null");
            }

            // Send the request
            var backendResponse = await m_backendApiCaller.JsonRestPostRequest<UserPassLoginRequestData, LoginResponseData>(
                "/auth/jwt/create/",
                new UserPassLoginRequestData() { password = password, username = username },
                null, cancellationToken);

            // If the response is successful
            if (backendResponse.IsSuccessful)
            {
                // Save the credentials
                CurrentUser = username;
                CurrentToken = backendResponse.Value.access;
                m_refreshToken = backendResponse.Value.refresh;
                SavePersistentCredentials();

                // Start the refresh loop to keep the access token alive
                StartRefreshLoop();

                return new LoginResponse(LoginResult.Success, ApiResponseCode.Ok);
            }
            // Else if the response is an error
            else
            {
                // Return the error                
                if (backendResponse.HttpErrorCode == 401) // Currently the server answers with 401 Unauthorized if the credentials are wrong
                    return new LoginResponse(LoginResult.WrongCredentials, ApiResponseCode.Error, backendResponse.Message);
                else
                    return new LoginResponse(LoginResult.GenericError, ApiResponseCode.Error, backendResponse.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<LoginResponse> LoginWithUserToken(string username, string token, CancellationToken cancellationToken = default)
        {
            // Check if data is valid
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
            {
                return new LoginResponse(LoginResult.GenericError, ApiResponseCode.Error, "Username and token cannot be null");
            }

            //refresh the token to obtain a new access token
            CurrentUser = username;
            CurrentToken = null;
            m_refreshToken = token;
            var refreshResponse = await RefreshToken(cancellationToken); //this will automatically update the tokens

            if (refreshResponse.IsSuccessful)
            {
                // Save the credentials
                SavePersistentCredentials();

                // Start the refresh loop to keep the access token alive
                StartRefreshLoop();
            }

            //in any case, return the result of the refresh operation
            return refreshResponse;
        }

        /// <inheritdoc/>
        public Task<LoginResponse> LoginWithLastValidCredentials(CancellationToken cancellationToken = default)
        {
            //validate the credentials
            if (HasPersistentCredentials())
            {
                //load the credentials and fill the username and refresh token fields
                LoadPersistentCredentials();
            }

            return LoginWithUserToken(CurrentUser, m_refreshToken, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<LoginResponse> Logout(CancellationToken cancellationToken = default)
        {
            // Send the request
            var backendResponse = await m_backendApiCaller.JsonRestPostRequest<LogoutRequestData, LogoutResponseData>(
                "/auth/jwt/blacklist/",
                new LogoutRequestData() { refresh = m_refreshToken }, 
                null, cancellationToken);

            // whatever is the answer, delete the credentials, to avoid that a failed logout
            // forces us to be logged in with credentials that may have been invalidated
            CurrentUser = null;
            CurrentToken = null;
            m_refreshToken = null;
            DeletePersistentCredentials();

            //also kill the refresh loop
            CancelRefreshLoop();

            // If the response is successful
            if (backendResponse.IsSuccessful)
            {
                return new LoginResponse(LoginResult.Success, ApiResponseCode.Ok);
            }
            // Else if the response is an error
            else
            {
                // Return the error                
                if (backendResponse.HttpErrorCode == 401) // Currently the server answers with 401 Unauthorized if the refresh token is invalid or expired
                    return new LoginResponse(LoginResult.WrongCredentials, ApiResponseCode.Error, backendResponse.Message);
                else
                    return new LoginResponse(LoginResult.GenericError, ApiResponseCode.Error, backendResponse.Message);
            }
        }

        /// <summary>
        /// Disposes the object (and kills the refresh loop)
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object.
        /// </summary>
        /// <param name="disposing">True if the dispose has been called directly</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    CancelRefreshLoop();
                }

                m_disposed = true;
            }
        }

        /// <summary>
        /// Refreshes the access token using the refresh token.
        /// This call updates the internal token properties of the class with the refreshed tokens.
        /// </summary>
        /// <returns>Response of the refresh operation</returns>
        private async Task<LoginResponse> RefreshToken(CancellationToken cancellationToken = default)
        {
            // Send the request
            var backendResponse = await m_backendApiCaller.JsonRestPostRequest<RefreshTokenRequestData, RefreshTokenResponseData>(
                "/auth/jwt/refresh/",
                new RefreshTokenRequestData() { refresh = m_refreshToken }, 
                null, cancellationToken);

            // If the response is successful
            if (backendResponse.IsSuccessful)
            {
                // memorize the tokens
                CurrentToken = backendResponse.Value.access;
                m_refreshToken = backendResponse.Value.refresh;

                return new LoginResponse(LoginResult.Success, ApiResponseCode.Ok);
            }
            // Else if the response is an error
            else
            {
                // Return the error                
                if (backendResponse.HttpErrorCode == 401) // Currently the server answers with 401 Unauthorized if the refresh token is invalid or expired
                    return new LoginResponse(LoginResult.WrongCredentials, ApiResponseCode.Error, backendResponse.Message);
                else
                    return new LoginResponse(LoginResult.GenericError, ApiResponseCode.Error, backendResponse.Message);
            }
        }

        /// <summary>
        /// Starts the token refresh loop.
        /// </summary>
        /// <returns></returns>
        private void StartRefreshLoop()
        {
            // cancel any previous refresh loop and then start a new one
            CancelRefreshLoop();
            DoRefreshLoop().Forget();
        }

        /// <summary>
        /// The actual token refresh loop.
        /// </summary>
        /// <remarks>
        /// The loop runs on the main thread because anyway any loop iteration is executed like once every hour
        /// and it involves only a few network calls, which are asynchronous
        /// </remarks>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">If the access token contained in this class is not in a valid JWT format</exception>
        private async UniTask DoRefreshLoop()
        {
            try
            {
                //cancel any previous loop
                CancelRefreshLoop();

                //create a new cancellation token source to let outside code cancel the loop
                m_refreshLoopCancelationTokenSource = new CancellationTokenSource();

                do
                {
                    //decode the JWT access token to get the expiration time
                    DateTime accessTokenUtcExpirationTime = GetJwtUtcExpirationTime(CurrentToken);
                    var timeToWaitBeforeTokenRefresh = (accessTokenUtcExpirationTime - DateTime.UtcNow).TotalSeconds - RefreshBeforeExpiresInterval;
                    bool refreshSuccessul = false;

                    //wait until the token is about to expire
                    if (timeToWaitBeforeTokenRefresh > 0)
                        await Task.Delay((int)(timeToWaitBeforeTokenRefresh * 1000));

                    //try to refresh the token. If you fail, retry a few times
                    for (int i = 0; i < RefreshRetryTimes; i++)
                    {
                        try
                        {
                            var refreshResult = await RefreshToken(m_refreshLoopCancelationTokenSource.Token);

                            if (refreshResult.IsSuccessful)
                            {
                                refreshSuccessul = true;
                                Debug.Log($"[Snippets SDK] User {CurrentUser} login refreshed correctly.");

                                break;
                            }
                            else
                                Debug.LogWarning($"[Snippets SDK] Token refresh failed. The system reports: {refreshResult.Message}. Retrying... ");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Snippets SDK] Token refresh failed. The system reports: {e.Message}. Retrying... ");
                        }

                        //wait before retrying
                        await Task.Delay((int)(RefreshRetryInterval * 1000));
                    }

                    //if the refresh was not successful, we log out
                    if (!refreshSuccessul)
                    {
                        CurrentUser = null;
                        CurrentToken = null;
                        m_refreshToken = null;

                        Debug.LogError($"[Snippets SDK] Token refresh failed too many times. Giving up. The user will be disconnected");
                    }

                } while (!m_refreshLoopCancelationTokenSource.IsCancellationRequested);
            }
            catch (JWT.SignatureVerificationException)
            {
                throw new InvalidOperationException("One of the provided authentication token is not valid");
            }
            catch (OperationCanceledException)
            {
                //do nothing, we are just exiting from the loop
            }
        }

        /// <summary>
        /// Stops the currently running refresh loop.
        /// </summary>
        private void CancelRefreshLoop()
        {
            if (m_refreshLoopCancelationTokenSource != null)
                m_refreshLoopCancelationTokenSource.Cancel();
        }

        /// <summary>
        /// Gets the expiration time of a JWT token.
        /// </summary>
        /// <param name="token">Token of interest</param>
        /// <returns>DateTime of the expiration</returns>
        private static DateTime GetJwtUtcExpirationTime(string token)
        {
            // Decode the token
            string jsonPayload = JWT.JsonWebToken.Decode(token, "", false);
            JwtPayload payload = JsonUtility.FromJson<JwtPayload>(jsonPayload);

            // Get the expiration time inside a date time
            var exp = payload.exp;
            return DateTimeOffset.FromUnixTimeSeconds(exp).DateTime;
        }

        /// <summary>
        /// Saves the credentials in PlayerPrefs.
        /// </summary>
        private void SavePersistentCredentials()
        {
            PlayerPrefs.SetString(UsernameKey, CurrentUser);
            PlayerPrefs.SetString(TokenKey, m_refreshToken);
        }

        /// <summary>
        /// Load the credentials (Username and refresh token) from PlayerPrefs.
        /// </summary>
        private void LoadPersistentCredentials()
        {
            CurrentUser = PlayerPrefs.GetString(UsernameKey);
            m_refreshToken = PlayerPrefs.GetString(TokenKey);
        }

        /// <summary>
        /// Delete the credentials from PlayerPrefs.
        /// </summary>
        private void DeletePersistentCredentials()
        {
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.DeleteKey(TokenKey);
        }

        /// <summary>
        /// Check if there are persistent credentials in PlayerPrefs.
        /// </summary>
        /// <returns>True if there are persistent credentials, false otherwise</returns>
        private bool HasPersistentCredentials()
        {
            return PlayerPrefs.HasKey(UsernameKey) && PlayerPrefs.HasKey(TokenKey);
        }

        #region Serialization data structures

        [Serializable]
        public class UserPassLoginRequestData
        {
            public string password;
            public string username;
        }

        [Serializable]
        public class LoginResponseData
        {
            public string refresh;
            public string access;
        }

        [Serializable]
        public class RefreshTokenRequestData
        {
            public string refresh;
        }

        [Serializable]
        public class RefreshTokenResponseData
        {
            public string access;
            public string refresh;
        }

        [Serializable]
        public class LogoutRequestData
        {
            public string refresh;
        }

        [Serializable]
        public class LogoutResponseData
        {
        }

        [Serializable]
        public class JwtPayload
        {
            public long exp;
        }

        #endregion
    }
}