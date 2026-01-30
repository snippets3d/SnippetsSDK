using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Snippets.Sdk.Ui
{
    /// <summary>
    /// Editor window view to make the user see the snippets sets belonging to the user
    /// </summary>
    public class SnippetsSetsWindow : EditorWindow
    {
        /// <summary>
        /// The VisualTreeAsset used to instantiate the UI elements of the window.
        /// Most probably, it is created with the UI Builder.
        /// </summary>
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        /// <summary>
        /// Counts how many times the update method has been called, so
        /// that we can perform a login check only once.
        /// </summary>
        private int m_updateCounter = 0;

        /// <summary>
        /// The snippets sets provider service
        /// </summary>
        private ISnippetSetsProvider m_snippetsSetsProvider;

        /// <summary>
        /// The service that provides data about the snippets downloaded in the project
        /// </summary>
        private IProjectSnippetsData m_projectSnippetData;

        /// <summary>
        /// The login manager service to get the logged in user from
        /// </summary>
        protected ILoginManager m_loginManager { get; set; }

        /// <summary>
        /// Opens the Snippets login window.
        /// </summary>
        [MenuItem("Snippets/Show User's Snippets Sets")]
        public static void ShowUsersSnippetsSets()
        {
            SnippetsSetsWindow wnd = GetWindow<SnippetsSetsWindow>();            
        }

        /// <summary>
        /// Creates the GUI for the login window.
        /// </summary>
        public void CreateGUI()
        {
            // Limit size of the window
            minSize = new Vector2(800, 400);
            maxSize = new Vector2(800, 400);

            // Set title of the window
            titleContent = new GUIContent("User's Snippets Sets");

            //initialize services
            m_snippetsSetsProvider = Services.GetService<ISnippetSetsProvider>();
            m_projectSnippetData = Services.GetService<IProjectSnippetsData>();
            m_loginManager = Services.GetService<ILoginManager>();

            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            // Initialize the list of snippets sets
            InitializeSnippetsSets();
        }

        /// <summary>
        /// Update
        /// </summary>
        private void Update()
        {
            if (m_updateCounter++ == 0)
                //check if there is a logged in user. If not, show the login window.
                //we do this check in update and not in CreateGui, because if we do that
                //in creategui, exceptions are thrown
                if (!m_loginManager.IsLoggedIn)
                {
                    Debug.LogError("[Snippets SDK] No user is logged in. Please log in first performing any operation.");

                    GetWindow<LoginWindow>();
                    Close();

                    return;
                }
        }

        /// <summary>
        /// Initializes the list of snippets sets by retrieving them from the backend
        /// </summary>
        private async void InitializeSnippetsSets()
        {
            //show the please wait panel
            rootVisualElement.Q("please-wait-panel").style.display = DisplayStyle.Flex;

            //get snippets sets from the server and associate them with the list of local snippets set
            var response = await GetSnippetsSetsUpdateSituation();

            //hide the please wait panel
            rootVisualElement.Q("please-wait-panel").style.display = DisplayStyle.None;

            //set the header of the window with the name of the logged in user
            rootVisualElement.Q<Label>("header-title").text = $"Snippets Sets of {m_loginManager.CurrentUser}";

            //if the retrieval was successful, populate the list
            if (response.IsSuccessful)
            {
                //get the container of the elements to show
                var container = rootVisualElement.Q<ScrollView>();

                //loop through the snippets sets 
                for (int i = 0; i < response.Value.Length; i++)
                {
                    var snippetsSet = response.Value[i];

                    //if we already have an element in the container, update it with the snippet value
                    if (i < container.childCount)
                        container.ElementAt(i).Q<SnippetsSetView>().value = snippetsSet;
                    //else, add a new element and initialize it with the snippet value
                    else
                    {
                        SnippetsSetView elementDataView = new SnippetsSetView();
                        elementDataView.value = snippetsSet;
                        container.Add(elementDataView);
                    }
                }

                Debug.Log($"[Snippets SDK] Snippets Sets retrieval succeeded. The current user has {response.Value.Length} associated (local or remote) snippets sets.");
            }
            //else, log an error
            else
            {
                Debug.Log($"[Snippets SDK] Snippets Sets retrieval failed. The system returned error code '{response.ResponseCode}'. The message is: {response.Message}");
            }

        }

        /// <summary>
        /// Build the list of snippet metadata to show in the window.
        /// For every snippet will be identified if it is only local, only remote, or both and what
        /// are its metadata in each case.
        /// </summary>
        /// <returns>List of local+remote metadata couples for the snippets sets of the user</returns>
        private async Task<ApiResponse<SnippetsSetMetadataUpdateData[]>> GetSnippetsSetsUpdateSituation()
        {
            //get the snippets sets from the server
            var serverResponse = await m_snippetsSetsProvider.GetAllSnippetsSets();

            if (!serverResponse.IsSuccessful)
                return new ApiResponse<SnippetsSetMetadataUpdateData[]>(new SnippetsSetMetadataUpdateData[0], serverResponse.ResponseCode, serverResponse.Message);

            //if the call succeeded, get the local snippets sets
            var localSnippetsSets = m_projectSnippetData.LocalSnippetSets;

            //create the data structure that maps local snippets to the new downloaded ones
            List<SnippetsSetMetadataUpdateData> snippetsSetUpdateData = new List<SnippetsSetMetadataUpdateData>();

            //create a dictionary for quick lookup of local snippets by ID
            var localSnippetsDict = localSnippetsSets.ToDictionary(s => s.Id);

            //iterate over the server snippets
            foreach (var serverSnippet in serverResponse.Value)
            {
                //check if there is a matching local snippet
                if (localSnippetsDict.TryGetValue(serverSnippet.Id, out var localSnippet))
                {
                    //add the matched snippets to the update data
                    snippetsSetUpdateData.Add(new SnippetsSetMetadataUpdateData(localSnippet, serverSnippet));
                    //remove the matched local snippet from the dictionary
                    localSnippetsDict.Remove(serverSnippet.Id);
                }
                else
                {
                    //add the server snippet with null local snippet
                    snippetsSetUpdateData.Add(new SnippetsSetMetadataUpdateData(null, serverSnippet));
                }
            }

            //add the remaining local snippets with null server snippet
            foreach (var localSnippet in localSnippetsDict.Values)
            {
                snippetsSetUpdateData.Add(new SnippetsSetMetadataUpdateData(localSnippet, null));
            }

            //return the update data
            return new ApiResponse<SnippetsSetMetadataUpdateData[]>(snippetsSetUpdateData.ToArray(), ApiResponseCode.Ok);
        }
    }
}
