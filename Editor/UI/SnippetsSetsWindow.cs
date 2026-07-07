using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Snippets.Sdk.Ui
{
    public class SnippetsSetsWindow : EditorWindow
    {
        private const float k_DefaultWidth = 800f;
        private const float k_DefaultHeight = 400f;
        private const float k_MaxHeight = 1200f;
        private const string k_RefreshIconResource = "RefreshIcon";
        private const float k_RefreshLeftOffset = 35f;

        private static bool s_resetSizeOnShow = false;

        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private bool m_loginCheckStarted = false;

        private bool m_snippetsSetsInitialized = false;

        private ISnippetSetsProvider m_snippetsSetsProvider;

        private IProjectSnippetsData m_projectSnippetData;

        protected ILoginManager m_loginManager { get; set; }

        private Button m_refreshButton;
        private bool m_refreshInProgress = false;
        private VisualElement m_waitPanel;
        private VisualElement m_headerPanel;
        private Label m_headerTitle;
        private bool m_layoutReady = false;
        private bool m_initialOverlayPending = true;

        [MenuItem("Snippets/Import or Update Snippet Sets")]
        public static void ShowUsersSnippetsSets()
        {
            s_resetSizeOnShow = true;
            SnippetsSetsWindow wnd = GetWindow<SnippetsSetsWindow>();
            if (wnd != null)
                wnd.titleContent = new GUIContent("My Snippet Sets");
        }

        public void CreateGUI()
        {
            minSize = new Vector2(k_DefaultWidth, k_DefaultHeight);
            maxSize = new Vector2(k_DefaultWidth, k_MaxHeight);

            titleContent = new GUIContent("My Snippet Sets");

            m_snippetsSetsProvider = Services.GetService<ISnippetSetsProvider>();
            m_projectSnippetData = Services.GetService<IProjectSnippetsData>();
            m_loginManager = Services.GetService<ILoginManager>();

            VisualElement root = rootVisualElement;
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);

            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

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

        private async void Update()
        {
            if (!m_layoutReady)
                return;

            if (m_snippetsSetsInitialized || m_loginCheckStarted)
                return;

            m_loginCheckStarted = true;

            // Run the login recovery after the first valid layout; doing it during CreateGUI can throw.
            if (!await EnsureLoggedInAsync())
                return;

            await InitializeSnippetsSets();
            m_snippetsSetsInitialized = true;
        }

        private async Task InitializeSnippetsSets()
        {
            SetLoadingState(true, !m_snippetsSetsInitialized);
            var response = await GetSnippetsSetsUpdateSituation();
            SetLoadingState(false, !m_snippetsSetsInitialized);
            if (m_headerTitle != null)
                m_headerTitle.text = $"Snippets Sets of {m_loginManager.CurrentUser}";

            if (response.IsSuccessful)
            {
                var container = rootVisualElement.Q<ScrollView>();

                for (int i = 0; i < response.Value.Length; i++)
                {
                    var snippetsSet = response.Value[i];

                    if (i < container.childCount)
                        container.ElementAt(i).Q<SnippetsSetView>().value = snippetsSet;
                    else
                    {
                        SnippetsSetView elementDataView = new SnippetsSetView();
                        elementDataView.value = snippetsSet;
                        container.Add(elementDataView);
                    }
                }

                for (int i = container.childCount - 1; i >= response.Value.Length; i--)
                    container.RemoveAt(i);

                Debug.Log($"[Snippets SDK] Snippets Sets retrieval succeeded. The current user has {response.Value.Length} associated (local or remote) snippets sets.");
            }
            else
            {
                Debug.Log($"[Snippets SDK] Snippets Sets retrieval failed. The system returned error code '{response.ResponseCode}'. The message is: {response.Message}");
            }

        }

        private async Task<ApiResponse<SnippetsSetMetadataUpdateData[]>> GetSnippetsSetsUpdateSituation()
        {
            var serverResponse = await m_snippetsSetsProvider.GetAllSnippetsSets();

            if (!serverResponse.IsSuccessful)
                return new ApiResponse<SnippetsSetMetadataUpdateData[]>(new SnippetsSetMetadataUpdateData[0], serverResponse.ResponseCode, serverResponse.Message);

            var localSnippetsSets = m_projectSnippetData.LocalSnippetSets;
            List<SnippetsSetMetadataUpdateData> snippetsSetUpdateData = new List<SnippetsSetMetadataUpdateData>();
            var localSnippetsDict = localSnippetsSets.ToDictionary(s => s.Id);

            foreach (var serverSnippet in serverResponse.Value)
            {
                if (localSnippetsDict.TryGetValue(serverSnippet.Id, out var localSnippet))
                {
                    snippetsSetUpdateData.Add(new SnippetsSetMetadataUpdateData(localSnippet, serverSnippet));
                    localSnippetsDict.Remove(serverSnippet.Id);
                }
                else
                {
                    snippetsSetUpdateData.Add(new SnippetsSetMetadataUpdateData(null, serverSnippet));
                }
            }

            foreach (var localSnippet in localSnippetsDict.Values)
            {
                snippetsSetUpdateData.Add(new SnippetsSetMetadataUpdateData(localSnippet, null));
            }

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
