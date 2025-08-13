
namespace Snippets.Sdk
{
    /// <summary>
    /// Base class for API modules, that is modules that call APIs,
    /// that are on the backend and require the user to be logged in
    /// </summary>
    public abstract class BackendLoggedInApiModule: LoggedInApiModule
    {
        /// <summary>
        /// The element that handles the REST calls towards a backend
        /// </summary>
        protected IBackendApiCaller BackendApiCaller { get; set; }

        /// <summary>
        /// Constructor with implicit initialization via <see cref="ServicesMgmt.Services"/>
        /// </summary>
        protected BackendLoggedInApiModule() :
            base()
        {
            if(BackendApiCaller == null)
            {
                BackendApiCaller = Services.GetService<IBackendApiCaller>();
            }
        }

        /// <summary>
        /// Constructor with explicit initialization
        /// </summary>
        /// <param name="loginManager">The login manager to use in this module</param>
        /// <param name="backendApiCaller">The backend API caller to use in this module</param>
        protected BackendLoggedInApiModule(ILoginManager loginManager, IBackendApiCaller backendApiCaller) :
            base(loginManager)
        {
            BackendApiCaller = backendApiCaller;
        }

    }
}