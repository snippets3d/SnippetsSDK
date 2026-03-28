using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Snippets.Sdk.Ui
{
    public class SnippetsSetView : BindableElement, INotifyValueChanged<SnippetsSetMetadataUpdateData>
    {
        private string k_defaultThumbnail = "default-snippets-icon";

        public new class UxmlFactory : UxmlFactory<SnippetsSetView> { }

        public static readonly string ussClassName = "snippets-set-view-element";

        private SnippetsSetMetadataUpdateData m_value;

        private Sprite m_defaultThumbnail;

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

        public SnippetsSetView()
        {
            AddToClassList(ussClassName);

            Add(Resources.Load<VisualTreeAsset>("SnippetsSetView").Instantiate());
            m_defaultThumbnail = Resources.Load<Sprite>(k_defaultThumbnail);
        }

        public void SetValueWithoutNotify(SnippetsSetMetadataUpdateData newValue)
        {
            m_value = newValue;

            var valueToUse = m_value.OriginalSnippetsSetMetadata ?? m_value.UpdatedSnippetsSetMetadata;
            var thumbnailToUse = m_value.UpdatedSnippetsSetMetadata?.Thumbnail ?? m_value.OriginalSnippetsSetMetadata?.Thumbnail;
            contentContainer.Q<Label>("name-label").text = valueToUse.Name + BuildSnippetStatusString();
            contentContainer.Q<Label>("description-label").text = valueToUse.Description;
            contentContainer.Q<Image>("thumbnail-image").sprite = thumbnailToUse != null ? thumbnailToUse : m_defaultThumbnail;
            contentContainer.Q<Image>("thumbnail-image").scaleMode = ScaleMode.ScaleToFit;

            SetButtonsVisibility(contentContainer.Q<Button>("import-button"), contentContainer.Q<Button>("update-button"), contentContainer.Q<Button>("remove-button"));
            contentContainer.Q<Button>("import-button").RegisterCallback<ClickEvent>(ImportButtonClicked);
            contentContainer.Q<Button>("update-button").RegisterCallback<ClickEvent>(UpdateButtonClicked);
            contentContainer.Q<Button>("remove-button").RegisterCallback<ClickEvent>(RemoveButtonClicked);
        }

        private string BuildSnippetStatusString()
        {
            if (value.OriginalSnippetsSetMetadata != null)
            {
                if (value.UpdatedSnippetsSetMetadata == null)
                    return " [Local Deprecated]";
                else if(value.UpdatedSnippetsSetMetadata != null && !value.UpdatedSnippetsSetMetadata.Downloadable)
                    return " [Update Processing]";
                else
                    return " [Imported]";
            }
            else if (value.UpdatedSnippetsSetMetadata != null && !value.UpdatedSnippetsSetMetadata.Downloadable)
                return " [Processing/Unpublished]";
            else
                return string.Empty;
        }

        private void SetButtonsVisibility(Button importButton, Button updateButton, Button removeButton)
        {
            importButton.style.display = (value.OriginalSnippetsSetMetadata == null && value.UpdatedSnippetsSetMetadata != null && value.UpdatedSnippetsSetMetadata.Downloadable) ? 
                DisplayStyle.Flex : DisplayStyle.None;

            updateButton.style.display = (value.OriginalSnippetsSetMetadata != null && value.UpdatedSnippetsSetMetadata != null && 
                value.OriginalSnippetsSetMetadata.Version != value.UpdatedSnippetsSetMetadata.Version && value.UpdatedSnippetsSetMetadata.Downloadable)
                ? DisplayStyle.Flex : DisplayStyle.None;

            removeButton.style.display = value.OriginalSnippetsSetMetadata != null ? 
                DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ImportButtonClicked(ClickEvent evt)
        {
            Debug.Log("[Snippets SDK] Importing snippet set: " + value.UpdatedSnippetsSetMetadata.Name);

            var importingWindow = EditorWindow.GetWindow<SnippetsImportingWindow>();
            importingWindow.ToOperateSnippetsSetPreviousMetadata = null;
            importingWindow.ToOperateSnippetsSetMetadata = value.UpdatedSnippetsSetMetadata;
            importingWindow.RefreshOperationSpecificTexts();

            EditorWindow.GetWindow<SnippetsSetsWindow>().Close();
        }

        private void UpdateButtonClicked(ClickEvent evt)
        {
            Debug.Log("[Snippets SDK] Updating snippet set: " + value.OriginalSnippetsSetMetadata.Name);

            var importingWindow = EditorWindow.GetWindow<SnippetsImportingWindow>();
            importingWindow.ToOperateSnippetsSetPreviousMetadata = value.OriginalSnippetsSetMetadata;
            importingWindow.ToOperateSnippetsSetMetadata = value.UpdatedSnippetsSetMetadata;
            importingWindow.RefreshOperationSpecificTexts();

            EditorWindow.GetWindow<SnippetsSetsWindow>().Close();
        }

        private void RemoveButtonClicked(ClickEvent evt)
        {
            Debug.Log("[Snippets SDK] Removing snippet set: " + value.OriginalSnippetsSetMetadata.Name);

            EditorWindow.GetWindow<SnippetsRemovalWindow>().ToOperateSnippetsSetMetadata = value.OriginalSnippetsSetMetadata;

            EditorWindow.GetWindow<SnippetsSetsWindow>().Close();
        }

        private void OnDisable()
        {
            contentContainer.Q<Button>("import-button").UnregisterCallback<ClickEvent>(ImportButtonClicked);
            contentContainer.Q<Button>("update-button").UnregisterCallback<ClickEvent>(UpdateButtonClicked);
            contentContainer.Q<Button>("remove-button").UnregisterCallback<ClickEvent>(RemoveButtonClicked);
        }

    }

}

