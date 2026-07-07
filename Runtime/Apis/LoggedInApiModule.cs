
using System;

namespace Snippets.Sdk
{
    /// <summary>
    /// Base class for API modules, that is modules that call APIs,
    /// that require the user to be logged in
    /// </summary>
    public abstract class LoggedInApiModule
    {
        /// <summary>
        /// The login manager that handles the login of the user
        /// (and that knows the logged in user)
        /// </summary>
        protected ILoginManager LoginManager { get; set; }

        /// <summary>
        /// Constructor with implicit initialization via <see cref="ServicesMgmt.Services"/>
        /// </summary>
        protected LoggedInApiModule() 
        {
            if(LoginManager == null)
            {
                LoginManager = Services.GetService<ILoginManager>();
            }
        }

        /// <summary>
        /// Constructor with explicit initialization
        /// </summary>
        /// <param name="loginManager">The login manager to use in this module</param>
        protected LoggedInApiModule(ILoginManager loginManager)
        {
            LoginManager = loginManager;
        }

        /// <summary>
        /// Checks if the user is logged in
        /// </summary>
        /// <returns>True if the user is logged in, false otherwise</returns>
        protected bool CheckLogin()
        {
            return !(LoginManager == null || !LoginManager.IsLoggedIn);
        }

        /// <summary>
        /// Checks if the user is logged in, and throws an exception if it is not
        /// </summary>
        /// <exception cref="ApiResponseException">Thrown if the user is not logged in</exception>
        protected void CheckLoginOrThrow()
        {
            if (!CheckLogin())
            {
                throw new ApiResponseException($"Unable to call the APIs of {this.GetType()} if the user is not logged in", ApiResponseCode.Unauthorized);
            }
        }

        /// <summary>
        /// Checks if the user is logged in, and returns an error response if not
        /// </summary>
        /// <param name="response">The response to return if the user is not logged in</param>
        /// <returns>True if the user is logged in, false otherwise</returns>
        protected bool CheckLoginOrErrorResponse<TResponseType>(out ApiResponse<TResponseType> response)
        {
            if (!CheckLogin())
            {
                response = new ApiResponse<TResponseType>(default, ApiResponseCode.Unauthorized, "Log in the user before performing any backend call");

                return false;
            }

            response = null;

            return true;
        }
    }
}