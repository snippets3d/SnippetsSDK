using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Snippets.Sdk.Ui
{
    /// <summary>
    /// Editor window view to make the user import snippet sets from the project
    /// </summary>
    public class SnippetsImportingWindow : SnippetsAddRemovalWindow
    {
        /// <summary>
        /// Stores the last template used for snippet creation so that the user does not have to select it again.
        /// </summary>
        private static GameObject m_savedSnippetsCreationTemplate;

        /// <summary>
        /// The metadata of the previous snippets set to be imported/removed.
        /// If this value is null, then the operation is an import, otherwise it is an update.
        /// </summary>
        public SnippetsSetMetadata ToOperateSnippetsSetPreviousMetadata { get; set; } = null;

        /// <summary>
        /// Opens the Snippets Import window.
        /// </summary>
        //Enable this line to create the menu item for debugging purposes
        //[MenuItem("Snippets/Import Snippets Set Confirmation")]
        public static void SnippetsImport()
        {
            SnippetsImportingWindow wnd = GetWindow<SnippetsImportingWindow>();
        }

        /// <summary>
        /// Creates the GUI for the login window.
        /// </summary>
        public override void CreateGUI()
        {
            // Call the base class method to initialize the window UI
            base.CreateGUI();

            // Limit size of the window
            this.minSize = new Vector2(800, 400);
            this.maxSize = new Vector2(800, 400);

            // Set title of the window
            titleContent = new GUIContent("Importing Snippets");

            // Initialize the prefab template with the last one set during this session (if any)
            rootVisualElement.Q<ObjectField>("snippets-creation-template").value = m_savedSnippetsCreationTemplate;

            // Set the title depending on the operation
            rootVisualElement.Q<Label>("header-title").text = ToOperateSnippetsSetPreviousMetadata == null ? "Importing Snippets" : "Updating Snippets";         
        }

        /// <summary>
        /// Callback called when the confirm button is clicked
        /// </summary>
        protected override async void OnConfirmButtonClicked()
        {
            try
            {
                // Save the last template used for snippet creation
                m_savedSnippetsCreationTemplate = rootVisualElement.Q<ObjectField>("snippets-creation-template").value as GameObject;

                //show waiting panel
                ToggleWaitingPanelVisibility(true);

                //create a snippet generator using the provided template gameobject 
                //and assign it to the snippet manager so that it can be used to generate the snippets
                SnippetsSetsManager.SnippetGenerator = new TemplateBasedSnippetGenerator(
                    (rootVisualElement.Q<ObjectField>("snippets-creation-template").value as GameObject).GetComponent<SnippetPlayer>());

                //add or update the snippet set
                if (ToOperateSnippetsSetPreviousMetadata == null)
                    await SnippetsSetsManager.ImportSnippetsSet(ToOperateSnippetsSetMetadata, OperationProgress);
                else
                    await SnippetsSetsManager.UpdateSnippetsSet(ToOperateSnippetsSetPreviousMetadata, ToOperateSnippetsSetMetadata, OperationProgress);

                //notify the snippet data manager about this import operation
                ProjectSnippetsData.SnippetsSetImported(ToOperateSnippetsSetMetadata, LoginManager.CurrentUser);

                //give the user time to see the bar at 100%
                await System.Threading.Tasks.Task.Delay(500);

                Debug.Log($"[Snippets SDK] Successfully imported snippet set {ToOperateSnippetsSetMetadata.Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Snippets SDK] Failed to imported snippet set {ToOperateSnippetsSetMetadata.Name}. The system reports: {e.Message}");
            }

            //hide waiting panel
            ToggleWaitingPanelVisibility(false);

            //base callback will close the window
            base.OnConfirmButtonClicked();
        }

    }
}
