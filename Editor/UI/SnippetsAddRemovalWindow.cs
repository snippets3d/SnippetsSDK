using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

namespace Snippets.Sdk.Ui
{
    /// <summary>
    /// Base class for editor window views to make the user import/update/remove snippet sets from the project
    /// </summary>
    public abstract class SnippetsAddRemovalWindow : EditorWindow
    {
        /// <summary>
        /// The VisualTreeAsset used to instantiate the UI elements of the window.
        /// Most probably, it is created with the UI Builder.
        /// </summary>
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        /// <summary>
        /// The metadata of the snippets set to be imported/removed.
        /// </summary>
        private SnippetsSetMetadata m_toOperateSnippetsSetMetadata;

        /// <summary>
        /// The project settings that store the folders' info.
        /// </summary>
        private IProjectSnippetsSettings m_projectSettings;

        /// <summary>
        /// Counts how many times the update method has been called, so
        /// that we can perform a login check only once.
        /// </summary>
        private int m_updateCounter = 0;

        /// <summary>
        /// Element that maps the progress of the current operation, so that we can update the UI accordingly.
        /// </summary>
        protected System.IProgress<float> OperationProgress { get; set; }

        /// <summary>
        /// The snippets sets manager service
        /// </summary>
        protected ISnippetsSetsManager SnippetsSetsManager { get; set; }

        /// <summary>
        /// The service that manages the data about the snippets downloaded in the project
        /// </summary>
        protected IProjectSnippetsData ProjectSnippetsData { get; set; }

        /// <summary>
        /// The login manager service to get the logged in user from
        /// </summary>
        protected ILoginManager LoginManager { get; set; }

        /// <summary>
        /// The metadata of the snippets set to be imported/removed.
        /// </summary>
        public SnippetsSetMetadata ToOperateSnippetsSetMetadata
        {
            get => m_toOperateSnippetsSetMetadata;
            set
            {
                m_toOperateSnippetsSetMetadata = value;

                //refresh the UI
                UpdateUI();
            }
        }

        /// <summary>
        /// Creates the GUI for the window.
        /// </summary>
        public virtual void CreateGUI()
        {
            //Initialize the services
            m_projectSettings = Services.GetService<IProjectSnippetsSettings>();
            SnippetsSetsManager = Services.GetService<ISnippetsSetsManager>();
            ProjectSnippetsData = Services.GetService<IProjectSnippetsData>();
            LoginManager = Services.GetService<ILoginManager>();

            //Initialize the progress element
            OperationProgress = new System.Progress<float>(OnProgressUpdate);

            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            // Update the UI
            UpdateUI();

            // Register the event handlers
            root.Q<Button>("confirm-button").clicked += OnConfirmButtonClicked;
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
                if (!LoginManager.IsLoggedIn)
                {
                    Debug.LogError("[Snippets SDK] No user is logged in. Please log in first performing any operation.");

                    GetWindow<LoginWindow>();
                    Close();

                    return;
                }
        }

        /// <summary>
        /// Update the UI because something has changed (e.g. the reference to the snippets set to be imported)
        /// </summary>
        protected virtual void UpdateUI()
        {
            if (ToOperateSnippetsSetMetadata == null)
                return;

            // Update the UI with the metadata of the snippets set to be imported/removed
            // (Description has been temporarily disabled because it is not used on the website for now)
            rootVisualElement.Q<Label>("snippet-set-name-label").text = ToOperateSnippetsSetMetadata.Name;
            //rootVisualElement.Q<Label>("snippet-set-description-label").text = ToOperateSnippetsSetMetadata.Description;
            rootVisualElement.Q<Label>("snippet-set-version-label").text = string.IsNullOrWhiteSpace(ToOperateSnippetsSetMetadata.Version) ? 
                "(Unknown version)" :
                ToOperateSnippetsSetMetadata.Version;

            // Update the UI with the directories affected by the import/removal operation
            rootVisualElement.Q<Label>("raw-directory-label").text = SnippetsSetsFolderFacilities.GetSnippetsSetsRawFolder(m_projectSettings, ToOperateSnippetsSetMetadata);
            rootVisualElement.Q<Label>("generated-directory-label").text = SnippetsSetsFolderFacilities.GetSnippetsSetsGeneratedFolder(m_projectSettings, ToOperateSnippetsSetMetadata);
        }

        /// <summary>
        /// Callback called when the confirm button is clicked
        /// </summary>
        protected virtual void OnConfirmButtonClicked()
        {
            //refresh the assetdatabase to let the user see changes in a coherent way
            AssetDatabase.Refresh();

            //close the window
            Close();

            //open the snippets sets window to see the updated list after this operation
            GetWindow<SnippetsSetsWindow>();
        }

        /// <summary>
        /// Callback called when the progress of the operation is updated
        /// </summary>
        /// <param name="progress">Progress value in the range [0, 1]</param>
        protected virtual void OnProgressUpdate(float progress)
        {
            //update the UI with the progress
            rootVisualElement.Q<ProgressBar>().value = progress * 100;
        }

        /// <summary>
        /// Toggle the visibility of the waiting panel with the progress bar
        /// </summary>
        /// <param name="isVisible">Bool to show the panel, false to hide it</param>
        protected void ToggleWaitingPanelVisibility(bool isVisible)
        {
            rootVisualElement.Q("SnippetsAddRemovalWaitTemplate").style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// On Disable
        /// </summary>
        public virtual void OnDisable()
        {
            //unregister from all the events
            rootVisualElement.Q<Button>("confirm-button").clicked -= OnConfirmButtonClicked;
        }
    }
}
