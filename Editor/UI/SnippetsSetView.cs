using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Snippets.Sdk.Ui
{
    /// <summary>
    /// Represents a view for displaying and interacting with a snippets set.
    /// </summary>
    public class SnippetsSetView : BindableElement, INotifyValueChanged<SnippetsSetMetadataUpdateData>
    {
        /// <summary>
        /// The name of the resource for the thumbnail image to use in case a snippet does not have one.
        /// </summary>
        private string k_defaultThumbnail = "default-snippets-icon";

        /// <summary>
        /// Factory class for creating instances of <see cref="SnippetsSetView"/>.
        /// </summary>
        public new class UxmlFactory : UxmlFactory<SnippetsSetView> { }

        /// <summary>
        /// The USS class name for the snippets set view element.
        /// </summary>
        public static readonly string ussClassName = "snippets-set-view-element";

        /// <summary>
        /// The current value of the snippets set metadata, both online and local
        /// </summary>
        private SnippetsSetMetadataUpdateData m_value;

        /// <summary>
        /// The default thumbnail image to use in case a snippet does not have one.
        /// </summary>
        private Sprite m_defaultThumbnail;

        /// <summary>
        /// Gets or sets the value of the snippets set metadata.
        /// </summary>
        public SnippetsSetMetadataUpdateData value
        {
            get => m_value;
            set
            {
                if (value == this.value)
                    return;

                var previous = this.value;
                SetValueWithoutNotify(value);

                using (var evt = ChangeEvent<SnippetsSetMetadataUpdateData>.GetPooled(previous, value))
                {
                    evt.target = this;
                    SendEvent(evt);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnippetsSetView"/> class.
        /// </summary>
        public SnippetsSetView()
        {
            AddToClassList(ussClassName);

            // Import UXML
            Add(Resources.Load<VisualTreeAsset>("SnippetsSetView").Instantiate());

            // Load the default thumbnail image
            m_defaultThumbnail = Resources.Load<Sprite>(k_defaultThumbnail);
        }

        /// <summary>
        /// Sets the value without notifying any listeners.
        /// </summary>
        /// <param name="newValue">The new value to set.</param>
        /// <exception cref="ArgumentException">Thrown when the provided value is not of type <see cref="SnippetsSetMetadataUpdateData"/>.</exception>
        public void SetValueWithoutNotify(SnippetsSetMetadataUpdateData newValue)
        {
            m_value = newValue;

            // Assign the values of the element to the visual UI. Give precedence to the local metadata, if available,
            // Because if a snippet set is installed we need to show to the user what is installed
            var valueToUse = m_value.OriginalSnippetsSetMetadata ?? m_value.UpdatedSnippetsSetMetadata;
            contentContainer.Q<Label>("name-label").text = valueToUse.Name + BuildSnippetStatusString();
            contentContainer.Q<Label>("description-label").text = valueToUse.Description;
            contentContainer.Q<Image>("thumbnail-image").sprite = (valueToUse.Thumbnail != null) ? valueToUse.Thumbnail : m_defaultThumbnail;
            contentContainer.Q<Image>("thumbnail-image").scaleMode = ScaleMode.ScaleToFit;

            // Set the visibility of the buttons depending on the update situation of the snippet set
            SetButtonsVisibility(contentContainer.Q<Button>("import-button"), contentContainer.Q<Button>("update-button"), contentContainer.Q<Button>("remove-button"));

            // Register the click events for the buttons
            contentContainer.Q<Button>("import-button").RegisterCallback<ClickEvent>(ImportButtonClicked);
            contentContainer.Q<Button>("update-button").RegisterCallback<ClickEvent>(UpdateButtonClicked);
            contentContainer.Q<Button>("remove-button").RegisterCallback<ClickEvent>(RemoveButtonClicked);
        }

        /// <summary>
        /// Builds the string to show the status of the snippet set.
        /// </summary>
        /// <returns>String to write next to the name of the snippet set to show its situation</returns>
        private string BuildSnippetStatusString()
        {
            if (value.OriginalSnippetsSetMetadata != null)
            {
                if (value.UpdatedSnippetsSetMetadata == null)
                    return " [Local Deprecated]"; // The snippet set is not available anymore on the server
                else if(value.UpdatedSnippetsSetMetadata != null && !value.UpdatedSnippetsSetMetadata.Downloadable)
                    return " [Update Processing]"; // The snippet set update is still processing so can't be updated yet
                else
                    return " [Imported]";
            }
            else if (value.UpdatedSnippetsSetMetadata != null && !value.UpdatedSnippetsSetMetadata.Downloadable)
                return " [Processing/Unpublished]"; //the snippet is still processing so can't be installed
            else
                return string.Empty; //if it is not installed, do not show anything
        }

        /// <summary>
        /// Sets the visibility of the buttons depending on the update situation of the snippet set.
        /// </summary>
        /// <param name="importButton">Reference to the input button of the view</param>
        /// <param name="updateButton">Reference to the update button of the view</param>
        /// <param name="removeButton">Reference to the remove button of the view</param>
        private void SetButtonsVisibility(Button importButton, Button updateButton, Button removeButton)
        {
            //remember to hide the import or update buttons if the snippet set is not downloadable
            importButton.style.display = (value.OriginalSnippetsSetMetadata == null && value.UpdatedSnippetsSetMetadata != null && value.UpdatedSnippetsSetMetadata.Downloadable) ? 
                DisplayStyle.Flex : DisplayStyle.None;

            updateButton.style.display = (value.OriginalSnippetsSetMetadata != null && value.UpdatedSnippetsSetMetadata != null && 
                value.OriginalSnippetsSetMetadata.Version != value.UpdatedSnippetsSetMetadata.Version && value.UpdatedSnippetsSetMetadata.Downloadable)
                ? DisplayStyle.Flex : DisplayStyle.None;

            removeButton.style.display = value.OriginalSnippetsSetMetadata != null ? 
                DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Handles the import button click event.
        /// </summary>
        /// <param name="evt">The click event.</param>
        private void ImportButtonClicked(ClickEvent evt)
        {
            Debug.Log("[Snippets SDK] Importing snippet set: " + value.UpdatedSnippetsSetMetadata.Name);

            //shows the import window and sets the metadata of the snippet set to import
            EditorWindow.GetWindow<SnippetsImportingWindow>().ToOperateSnippetsSetMetadata = value.UpdatedSnippetsSetMetadata;
            EditorWindow.GetWindow<SnippetsImportingWindow>().ToOperateSnippetsSetPreviousMetadata = null;

            //closes the current window to avoid multiple parallel operations that may lead to confusion
            EditorWindow.GetWindow<SnippetsSetsWindow>().Close();
        }

        /// <summary>
        /// Handles the update button click event.
        /// </summary>
        /// <param name="evt">The click event.</param>
        private void UpdateButtonClicked(ClickEvent evt)
        {
            Debug.Log("[Snippets SDK] Updating snippet set: " + value.OriginalSnippetsSetMetadata.Name);

            //shows the import window and sets the metadata of the snippet set to import
            EditorWindow.GetWindow<SnippetsImportingWindow>().ToOperateSnippetsSetMetadata = value.UpdatedSnippetsSetMetadata;
            EditorWindow.GetWindow<SnippetsImportingWindow>().ToOperateSnippetsSetPreviousMetadata = value.OriginalSnippetsSetMetadata;

            //closes the current window to avoid multiple parallel operations that may lead to confusion
            EditorWindow.GetWindow<SnippetsSetsWindow>().Close();
        }

        /// <summary>
        /// Handles the remove button click event.
        /// </summary>
        /// <param name="evt">The click event.</param>
        private void RemoveButtonClicked(ClickEvent evt)
        {
            Debug.Log("[Snippets SDK] Removing snippet set: " + value.OriginalSnippetsSetMetadata.Name);

            //shows the import window and sets the metadata of the snippet set to import
            EditorWindow.GetWindow<SnippetsRemovalWindow>().ToOperateSnippetsSetMetadata = value.OriginalSnippetsSetMetadata;

            //closes the current window to avoid multiple parallel operations that may lead to confusion
            EditorWindow.GetWindow<SnippetsSetsWindow>().Close();
        }

        /// <summary>
        /// On disable
        /// </summary>
        private void OnDisable()
        {
            // Unregister from all the events
            contentContainer.Q<Button>("import-button").UnregisterCallback<ClickEvent>(ImportButtonClicked);
            contentContainer.Q<Button>("update-button").UnregisterCallback<ClickEvent>(UpdateButtonClicked);
            contentContainer.Q<Button>("remove-button").UnregisterCallback<ClickEvent>(RemoveButtonClicked);
        }

    }

}