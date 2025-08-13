using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Snippets.Sdk.Ui
{
    /// <summary>
    /// Editor window view to make the user remove snippet sets from the project
    /// </summary>
    public class SnippetsRemovalWindow : SnippetsAddRemovalWindow
    {
        /// <summary>
        /// The VisualTreeAsset used to instantiate the UI elements of the window.
        /// Most probably, it is created with the UI Builder.
        /// </summary>
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        /// <summary>
        /// Opens the Snippets Remove window.
        /// </summary>
        //Enable this line to create the menu item for debugging purposes
        //[MenuItem("Snippets/Remove Snippets Set Confirmation")]
        public static void SnippetsRemoval()
        {
            SnippetsRemovalWindow wnd = GetWindow<SnippetsRemovalWindow>();
        }

        /// <summary>
        /// Creates the GUI for the login window.
        /// </summary>
        public override void CreateGUI()
        {
            // Limit size of the window
            this.minSize = new Vector2(800, 400);
            this.maxSize = new Vector2(800, 400);

            // Set title of the window
            titleContent = new GUIContent("Removing Snippets");

            // Call the base class method to initialize the window UI
            base.CreateGUI();
        }

        /// <summary>
        /// Callback called when the confirm button is clicked
        /// </summary>
        protected override async void OnConfirmButtonClicked()
        {
            try
            {
                //show waiting panel
                ToggleWaitingPanelVisibility(true);

                //remove the snippet set
                await SnippetsSetsManager.RemoveSnippetsSet(ToOperateSnippetsSetMetadata, OperationProgress);

                //notify the data manager about the removal
                ProjectSnippetsData.SnippetsSetRemoved(ToOperateSnippetsSetMetadata, LoginManager.CurrentUser);

                //give the user time to see the bar at 100%
                await System.Threading.Tasks.Task.Delay(500);

                Debug.Log($"[Snippets SDK] Successfully removed snippet set {ToOperateSnippetsSetMetadata.Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Snippets SDK] Failed to remove snippet set {ToOperateSnippetsSetMetadata.Name}. The system reports: {e.Message}");
            }

            //hide waiting panel
            ToggleWaitingPanelVisibility(false);

            //base callback will close the window
            base.OnConfirmButtonClicked();
        }
    }
}
