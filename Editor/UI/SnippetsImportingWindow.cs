using System;
using UnityEditor;
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
        /// Prevents initialization from overwriting the saved template.
        /// </summary>
        private bool m_isTemplateFieldInitializing;

        /// <summary>
        /// Session key used to persist the template prefab across domain reloads.
        /// </summary>
        private const string k_snippetTemplateSessionKey = "Snippets.SnippetTemplatePrefabGuid";

        /// <summary>
        /// Default template prefab paths (project asset first, then package fallback).
        /// </summary>
        private static readonly string[] k_defaultTemplatePrefabPaths =
        {
            "Assets/_Snippets/SnippetsSDK/Editor/SnippetsTemplates/SnippetTemplate.prefab",
            "Packages/com.snippets.sdk/Editor/SnippetsTemplates/SnippetTemplate.prefab",
        };

        /// <summary>
        /// The metadata of the previous snippets set to be imported/removed.
        /// If this value is null, then the operation is an import, otherwise it is an update.
        /// </summary>
        private SnippetsSetMetadata m_toOperateSnippetsSetPreviousMetadata;

        public SnippetsSetMetadata ToOperateSnippetsSetPreviousMetadata
        {
            get => m_toOperateSnippetsSetPreviousMetadata;
            set
            {
                m_toOperateSnippetsSetPreviousMetadata = value;
                RefreshOperationSpecificTexts();
            }
        }

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

            bool isUpdateOperation = ToOperateSnippetsSetPreviousMetadata != null;

            // Limit size of the window
            this.minSize = new Vector2(700, 400);
            this.maxSize = new Vector2(700, 400);

            // Set title of the window
            titleContent = new GUIContent(isUpdateOperation ? "Updating Snippets" : "Importing Snippets");

            // Initialize the prefab template with the last one set during this session (if any).
            // Schedule the apply so it runs after UIElements view-data restoration.
            var templateField = rootVisualElement.Q<ObjectField>("snippets-creation-template");
            if (templateField != null)
            {
                m_isTemplateFieldInitializing = true;
                templateField.allowSceneObjects = false;

                // Disable view-data persistence for this field (we persist manually).
                templateField.viewDataKey = null;

                templateField.RegisterValueChangedCallback(evt =>
                {
                    if (m_isTemplateFieldInitializing)
                        return;

                    SaveSnippetsCreationTemplate(evt.newValue as GameObject);
                });

                rootVisualElement.schedule.Execute(() =>
                {
                    ApplySnippetsCreationTemplate(templateField, LoadSnippetsCreationTemplate());
                    m_isTemplateFieldInitializing = false;
                });
            }

            ConfigureOperationSpecificTexts(isUpdateOperation);
        }

        public void RefreshOperationSpecificTexts()
        {
            if (rootVisualElement == null || rootVisualElement.childCount == 0)
                return;

            ConfigureOperationSpecificTexts(ToOperateSnippetsSetPreviousMetadata != null);
        }

        /// <summary>
        /// Callback called when the confirm button is clicked
        /// </summary>
        protected override async void OnConfirmButtonClicked()
        {
            try
            {
                var selectedTemplate = rootVisualElement.Q<ObjectField>("snippets-creation-template").value as GameObject;
                ValidateTemplateSelection(selectedTemplate);

                // Save the last template used for snippet creation
                SaveSnippetsCreationTemplate(selectedTemplate);

                //show waiting panel
                ToggleWaitingPanelVisibility(true);

                //create a snippet generator using the provided template gameobject 
                //and assign it to the snippet manager so that it can be used to generate the snippets
                SnippetsSetsManager.SnippetGenerator = new TemplateBasedSnippetGenerator(
                    selectedTemplate.GetComponent<SnippetPlayer>());

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

        /// <summary>
        /// Loads the last saved template prefab from session state.
        /// </summary>
        private static GameObject LoadSnippetsCreationTemplate()
        {
            if (m_savedSnippetsCreationTemplate != null)
                return m_savedSnippetsCreationTemplate;

            string guid = SessionState.GetString(k_snippetTemplateSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(guid))
                return LoadDefaultTemplatePrefab();

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
                return LoadDefaultTemplatePrefab();

            m_savedSnippetsCreationTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return m_savedSnippetsCreationTemplate != null
                ? m_savedSnippetsCreationTemplate
                : LoadDefaultTemplatePrefab();
        }

        /// <summary>
        /// Persists the selected template prefab for this editor session.
        /// </summary>
        private static void SaveSnippetsCreationTemplate(GameObject template)
        {
            m_savedSnippetsCreationTemplate = template;

            if (template == null)
            {
                SessionState.SetString(k_snippetTemplateSessionKey, string.Empty);
                return;
            }

            string path = AssetDatabase.GetAssetPath(template);
            if (string.IsNullOrWhiteSpace(path))
            {
                SessionState.SetString(k_snippetTemplateSessionKey, string.Empty);
                return;
            }

            SessionState.SetString(k_snippetTemplateSessionKey, AssetDatabase.AssetPathToGUID(path));
        }

        /// <summary>
        /// Applies the template prefab to the field and forces a repaint so the label updates.
        /// </summary>
        private static void ApplySnippetsCreationTemplate(ObjectField templateField, GameObject template)
        {
            if (templateField == null)
                return;

            templateField.SetValueWithoutNotify(template);
            templateField.MarkDirtyRepaint();
        }

        /// <summary>
        /// Loads the default template prefab from known locations.
        /// </summary>
        private static GameObject LoadDefaultTemplatePrefab()
        {
            for (int i = 0; i < k_defaultTemplatePrefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_defaultTemplatePrefabPaths[i]);
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        /// <summary>
        /// Ensures the selected template is a prefab asset with the expected component.
        /// </summary>
        private static void ValidateTemplateSelection(GameObject template)
        {
            if (template == null)
            {
                throw new InvalidOperationException("Please select a snippet template prefab before importing.");
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(template))
            {
                throw new InvalidOperationException("The snippet template must be a prefab asset from the Project window, not a scene object.");
            }

            if (template.GetComponent<SnippetPlayer>() == null)
            {
                throw new InvalidOperationException("The selected template prefab must contain a SnippetPlayer component.");
            }
        }

        private void ConfigureOperationSpecificTexts(bool isUpdateOperation)
        {
            rootVisualElement.Q<Label>("header-title").text = isUpdateOperation ? "Updating Snippets" : "Importing Snippets";

            var confirmButton = rootVisualElement.Q<Button>("confirm-button");
            if (confirmButton != null)
                confirmButton.text = isUpdateOperation ? "Update" : "Import";

            var setDataSectionTitle = rootVisualElement.Q<VisualElement>("SnippetsAddRemovalSetDataTemplate")?.Q<Label>("section-title");
            if (setDataSectionTitle != null)
                setDataSectionTitle.text = isUpdateOperation ? "Snippet set updated:" : "Snippet set imported:";

            var templateSectionTitle = rootVisualElement.Q<VisualElement>("SnippetsAddRemovalParamsTemplate")?.Q<Label>("section-title");
            if (templateSectionTitle != null)
            {
                templateSectionTitle.text = isUpdateOperation
                    ? "Template for newly added snippets:"
                    : "Snippets Template used:";
            }
        }

    }
}
