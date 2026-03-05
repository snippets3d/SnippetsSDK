using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Snippets.Sdk.Ui
{
    /// <summary>
    /// Editor window view to login to the Snippets platform.
    /// </summary>
    public class LoginWindow : EditorWindow
    {
        /// <summary>
        /// The possible status of the login window
        /// </summary>
        private enum LoginWindowStatus
        {
            LoggedOut,
            LoggedIn,
            LoggingIn,
            LoggingOut,
        };
        
        /// <summary>
        /// The VisualTreeAsset used to instantiate the UI elements of the window.
        /// Most probably, it is created with the UI Builder.
        /// </summary>
        [SerializeField]
        private VisualTreeAsset m_visualTreeAsset = default;

        /// <summary>
        /// The login manager service
        /// </summary>
        private ILoginManager m_loginManager;

        /// <summary>
        /// The login status of the window
        /// </summary>
        private LoginWindowStatus m_loginStatus = LoginWindowStatus.LoggedOut;

        /// <summary>
        /// Get or set the login status of the window
        /// </summary>
        private LoginWindowStatus LoginStatus
        {
            get => m_loginStatus;
            set
            {
                m_loginStatus = value;
                
                if(rootVisualElement != null)
                {
                    //show a different panel depending on the status
                    rootVisualElement.Q("login-panel").style.display = (value == LoginWindowStatus.LoggingIn || value == LoginWindowStatus.LoggedOut) ? 
                        DisplayStyle.Flex : DisplayStyle.None;
                    rootVisualElement.Q("loggedin-panel").style.display = (value == LoginWindowStatus.LoggingOut || value == LoginWindowStatus.LoggedIn) ? 
                        DisplayStyle.Flex : DisplayStyle.None;    
                    rootVisualElement.Q("loggingin-panel").style.display = (value == LoginWindowStatus.LoggingIn || value == LoginWindowStatus.LoggingOut) ? 
                        DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        /// <summary>
        /// Opens the Snippets login window.
        /// </summary>
        [MenuItem("Snippets/Log In")]
        public static void SnippetsLogin()
        {
            LoginWindow wnd = GetWindow<LoginWindow>();       
        }

        /// <summary>
        /// Creates the GUI for the login window.
        /// </summary>
        public void CreateGUI()
        {
            // Limit size of the window
            minSize = new Vector2(500, 230);
            maxSize = new Vector2(500, 230);

            // Set title of the window
            titleContent = new GUIContent("Snippets Log In");

            //get the required services 
            m_loginManager = Services.GetService<ILoginManager>();

            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_visualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            // Register to button events
            root.Q<Button>("login-button").clicked += OnLoginButtonClicked;
            root.Q<Button>("logout-button").clicked += OnLogoutButtonClicked;

            // Hide error message
            SetErrorMessage("");

            // Check if we are already logged in. If yes, show this to the user. s
            if(m_loginManager.IsLoggedIn)
            {
                OnLoginSucceeded(true);
            }
            // Otherwise try to login with the last valid credential, so that to recover the last session
            else
            {
                TryLoginWithLastValidCredentials();
            }
        }

        /// <summary>
        /// Tries to login with the last valid saved credentials.
        /// </summary>
        private async void TryLoginWithLastValidCredentials()
        {
            // we are trying to login
            LoginStatus = LoginWindowStatus.LoggingIn;

            // contact the backend to login with the last valid credentials
            var response = await m_loginManager.LoginWithLastValidCredentials();

            // if login is successful, we are logged in. Show this to the user
            if (response.IsSuccessful)
            {
                Debug.Log($"[Snippets SDK] Login with last valid credentials succeeded");

                OnLoginSucceeded();
            }
            // if we failed, we log out
            else
            {
                LoginStatus = LoginWindowStatus.LoggedOut;

                Debug.Log($"[Snippets SDK] Login with last valid credentials failed");
            }
        }

        /// <summary>
        /// Event handler for the login button click.
        /// </summary>
        private async void OnLoginButtonClicked()
        {
            // we are trying to login
            LoginStatus = LoginWindowStatus.LoggingIn;

            // contact the backend to login with username and password
            var response = await m_loginManager.LoginWithUserPassword(
                rootVisualElement.Q<TextField>("username-text-field").text, 
                rootVisualElement.Q<TextField>("password-text-field").text);

            // if login is successful, we are logged in. Show this to the user
            if(response.IsSuccessful)
            {
                OnLoginSucceeded();
            }
            //if login is not successful, we are still logged out. Show an error message
            else
            {
                OnLoginFailed(response);
            }
        }

        /// <summary>
        /// Handles successful login.
        /// </summary>
        private void OnLoginSucceeded(bool wasAlreadyLoggedIn = false)
        {
            LoginStatus = LoginWindowStatus.LoggedIn;
            rootVisualElement.Q<Label>("loggedin-user-label").text = $"You are currently logged in as {m_loginManager.CurrentUser}";
            SetErrorMessage("");

            //if we were already logged in, we don't need to show the message
            if (!wasAlreadyLoggedIn)
                Debug.Log($"[Snippets SDK] Successfully logged in as {m_loginManager.CurrentUser}");
        }

        /// <summary>
        /// Handles failed login.
        /// </summary>
        /// <param name="response">The login response.</param>
        private void OnLoginFailed(LoginResponse response)
        {
            LoginStatus = LoginWindowStatus.LoggedOut;
            SetErrorMessage(response.Value == LoginResult.WrongCredentials ? "Wrong credentials" : "Generic error");

            Debug.LogError($"[Snippets SDK] Login failed. The system reported the login response '{response.Value}' with error code '{response.ResponseCode}'. The message is: {response.Message}");
        }

        /// <summary>
        /// Event handler for the logout button click.
        /// </summary>
        private async void OnLogoutButtonClicked()
        {
            // we are trying to logout
            LoginStatus = LoginWindowStatus.LoggingOut;

            // contact the backend to logout
            var response = await m_loginManager.Logout();

            // whatever is the answer, go to log out stage, to avoid that a failed logout
            // forces us to be logged in with credentials that may have been invalidated
            LoginStatus = LoginWindowStatus.LoggedOut;

            //log the result of the operation
            if (response.IsSuccessful)
                Debug.Log($"[Snippets SDK] Successfully logged out");
            else
                Debug.LogError($"[Snippets SDK] Logout failed. The system reports an error code {response.ResponseCode} with the message {response.Message}");
        }

        /// <summary>
        /// Sets the error message in the window
        /// </summary>
        /// <param name="message">Error message. Use the empty string to hide the error label</param>
        private void SetErrorMessage(string message)
        {
            rootVisualElement.Q<Label>("error-label").text = message;
        }     

        /// <summary>
        /// On Disable
        /// </summary>
        public void OnDisable()
        {
            //unregister from all the events
            rootVisualElement.Q<Button>("login-button").clicked -= OnLoginButtonClicked;
            rootVisualElement.Q<Button>("logout-button").clicked -= OnLogoutButtonClicked;
        }

    }
}
