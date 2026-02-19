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
        private const float k_DefaultWidth = 800f;
        private const float k_DefaultHeight = 400f;
        private const float k_MaxHeight = 1200f;
        private const string k_RefreshIconResource = "RefreshIcon";
        private const float k_RefreshLeftOffset = 35f;

        private static bool s_resetSizeOnShow = false;

        /// <summary>
        /// The VisualTreeAsset used to instantiate the UI elements of the window.
        /// Most probably, it is created with the UI Builder.
        /// </summary>
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        /// <summary>
        /// True after we kicked off the login check.
        /// </summary>
        private bool m_loginCheckStarted = false;

        /// <summary>
        /// True after we initialized the snippets list.
        /// </summary>
        private bool m_snippetsSetsInitialized = false;

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

        private Button m_refreshButton;
        private bool m_refreshInProgress = false;
        private VisualElement m_waitPanel;
        private VisualElement m_headerPanel;
        private Label m_headerTitle;
        private bool m_layoutReady = false;
        private bool m_initialOverlayPending = true;

        /// <summary>
        /// Opens the Snippets login window.
        /// </summary>
        [MenuItem("Snippets/Import or Update Snippet Sets")]
        public static void ShowUsersSnippetsSets()
        {
            s_resetSizeOnShow = true;
            SnippetsSetsWindow wnd = GetWindow<SnippetsSetsWindow>();
            if (wnd != null)
                wnd.titleContent = new GUIContent("My Snippet Sets");
        }

        /// <summary>
        /// Creates the GUI for the login window.
        /// </summary>
        public void CreateGUI()
        {
            // Limit size of the window (fixed width, resizable height).
            minSize = new Vector2(k_DefaultWidth, k_DefaultHeight);
            maxSize = new Vector2(k_DefaultWidth, k_MaxHeight);

            // Set title of the window
            titleContent = new GUIContent("My Snippet Sets");

            //initialize services
            m_snippetsSetsProvider = Services.GetService<ISnippetSetsProvider>();
            m_projectSnippetData = Services.GetService<IProjectSnippetsData>();
            m_loginManager = Services.GetService<ILoginManager>();

            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            // Wire up refresh button
            m_refreshButton = root.Q<Button>("refresh-button");
            if (m_refreshButton != null)
            {
                m_refreshButton.tooltip = "Refresh snippet sets";
                var refreshSprite = Resources.Load<Sprite>(k_RefreshIconResource);
                if (refreshSprite != null)
                    m_refreshButton.style.backgroundImage = new StyleBackground(refreshSprite);

                m_refreshButton.clicked += RefreshSnippetsSets;
            }

            m_waitPanel = root.Q<VisualElement>("please-wait-panel");
            if (m_waitPanel != null)
            {
                // Ensure the overlay stretches to the full window so the label centers vertically.
                m_waitPanel.style.position = Position.Absolute;
                m_waitPanel.style.top = 0;
                m_waitPanel.style.right = 0;
                m_waitPanel.style.bottom = 0;
                m_waitPanel.style.left = 0;
                m_waitPanel.style.width = Length.Percent(100);
                m_waitPanel.style.height = Length.Percent(100);
                m_waitPanel.style.flexGrow = 1;
                m_waitPanel.style.justifyContent = Justify.Center;
                m_waitPanel.style.alignItems = Align.Center;
                m_waitPanel.style.marginTop = 0;
                m_waitPanel.style.marginBottom = 0;
                m_waitPanel.style.marginLeft = 0;
                m_waitPanel.style.marginRight = 0;
                m_waitPanel.style.paddingTop = 0;
                m_waitPanel.style.paddingBottom = 0;
                m_waitPanel.style.paddingLeft = 0;
                m_waitPanel.style.paddingRight = 0;
            }

            var rootPanel = root.Q<VisualElement>("root-panel");
            if (rootPanel != null)
            {
                rootPanel.style.flexGrow = 1;
                rootPanel.style.width = Length.Percent(100);
                rootPanel.style.height = Length.Percent(100);
            }
            m_headerPanel = root.Q<VisualElement>("header-panel");
            m_headerTitle = root.Q<Label>("header-title");

            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void OnEnable()
        {
            if (!s_resetSizeOnShow)
                return;

            s_resetSizeOnShow = false;
            EditorApplication.delayCall += ResetWindowSize;
        }

        private void ResetWindowSize()
        {
            if (this == null)
                return;

            var currentPosition = position;
            position = new Rect(currentPosition.x, currentPosition.y, k_DefaultWidth, k_DefaultHeight);
        }

        /// <summary>
        /// Update
        /// </summary>
        private async void Update()
        {
            if (!m_layoutReady)
                return;

            if (m_snippetsSetsInitialized || m_loginCheckStarted)
                return;

            m_loginCheckStarted = true;

            //check if there is a logged in user. If not, try to recover the session once.
            //we do this check in update and not in CreateGui, because if we do that
            //in creategui, exceptions are thrown
            if (!await EnsureLoggedInAsync())
                return;

            await InitializeSnippetsSets();
            m_snippetsSetsInitialized = true;
        }

        /// <summary>
        /// Initializes the list of snippets sets by retrieving them from the backend
        /// </summary>
        private async Task InitializeSnippetsSets()
        {
            //show the please wait panel
            SetLoadingState(true, !m_snippetsSetsInitialized);

            //get snippets sets from the server and associate them with the list of local snippets set
            var response = await GetSnippetsSetsUpdateSituation();

            //hide the please wait panel
            SetLoadingState(false, !m_snippetsSetsInitialized);

            //set the header of the window with the name of the logged in user
            if (m_headerTitle != null)
                m_headerTitle.text = $"Snippets Sets of {m_loginManager.CurrentUser}";

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

                //remove any extra elements if the list shrank
                for (int i = container.childCount - 1; i >= response.Value.Length; i--)
                    container.RemoveAt(i);

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

        private async void RefreshSnippetsSets()
        {
            if (m_refreshInProgress)
                return;

            m_refreshInProgress = true;

            try
            {
                if (!await EnsureLoggedInAsync())
                    return;

                await InitializeSnippetsSets();
            }
            finally
            {
                m_refreshInProgress = false;
            }
        }

        private async Task<bool> EnsureLoggedInAsync()
        {
            if (m_loginManager == null)
                m_loginManager = Services.GetService<ILoginManager>();

            if (m_loginManager == null)
                return false;

            if (m_loginManager.IsLoggedIn)
                return true;

            SetLoadingState(true, !m_snippetsSetsInitialized);

            var loginResponse = await m_loginManager.LoginWithLastValidCredentials();
            if (!loginResponse.IsSuccessful || !m_loginManager.IsLoggedIn)
            {
                Debug.LogError("[Snippets SDK] No user is logged in or the session refresh failed. Please log in first performing any operation.");

                GetWindow<LoginWindow>();
                Close();

                return false;
            }

            return true;
        }

        private void OnDisable()
        {
            if (m_refreshButton != null)
                m_refreshButton.clicked -= RefreshSnippetsSets;

            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.newRect.width <= 0 || evt.newRect.height <= 0)
                return;

            if (evt.newRect.width < position.width * 0.9f || evt.newRect.height < position.height * 0.9f)
                return;

            ApplyWaitPanelSize(evt.newRect);

            if (m_initialOverlayPending)
            {
                m_initialOverlayPending = false;
                // Show after first valid layout so centering uses the real window size.
                SetLoadingState(true, true);
            }

            m_layoutReady = true;
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void ApplyWaitPanelSize(Rect rect)
        {
            if (m_waitPanel == null)
                return;

            m_waitPanel.style.width = rect.width;
            m_waitPanel.style.height = rect.height;
        }

        private void SetLoadingState(bool isLoading, bool hideHeader)
        {
            if (m_waitPanel != null)
                m_waitPanel.style.display = isLoading ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hideHeader)
                return;

            if (m_headerPanel != null)
            {
                m_headerPanel.style.display = isLoading ? DisplayStyle.None : DisplayStyle.Flex;
                return;
            }

            if (m_headerTitle != null)
                m_headerTitle.style.display = isLoading ? DisplayStyle.None : DisplayStyle.Flex;

            if (m_refreshButton != null)
                m_refreshButton.style.display = isLoading ? DisplayStyle.None : DisplayStyle.Flex;
        }

    }
}
