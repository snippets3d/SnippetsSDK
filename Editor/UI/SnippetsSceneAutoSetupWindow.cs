#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Snippets.Sdk;

namespace Snippets.Sdk.Ui
{
    public class SnippetsSceneAutoSetupWindow : EditorWindow
    {
        const float k_MinWidth = 520f;
        const float k_MinHeight = 560f;

        [SerializeField]
        List<SnippetPlayer> m_actorPlayers = new();

        [SerializeField]
        bool m_createActorRegistry = true;

        [SerializeField]
        bool m_createFlowController = true;

        [SerializeField]
        bool m_createGazeFlowController = true;

        [SerializeField]
        bool m_createSimpleController = true;

        [SerializeField]
        bool m_addGazeDriversToActors = true;

        [SerializeField]
        bool m_addWalkersToActors = true;

        [SerializeField]
        Vector2 m_scrollPosition;

        [SerializeField]
        string m_lastSummary = string.Empty;

        [MenuItem("Snippets/Scene Auto Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<SnippetsSceneAutoSetupWindow>();
            if (window != null)
            {
                window.titleContent = new GUIContent("Scene Auto Setup");
                window.minSize = new Vector2(k_MinWidth, k_MinHeight);
            }
        }

        void OnGUI()
        {
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Snippets Scene Auto Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag one scene snippet instance per actor. Best practice: after you import a snippet set, drag one snippet from that imported folder into the scene for each actor, rename that scene object to the actor name you want, then drop it here.",
                MessageType.Info);

            DrawActorsSection();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("What To Set Up", EditorStyles.boldLabel);
            DrawOption(
                ref m_createActorRegistry,
                "Actor Registry",
                "Central list of your actors and their snippet libraries. Flow and Simple controllers both rely on this.");
            DrawOption(
                ref m_createFlowController,
                "Flow Controller",
                "Creates a step-based controller for snippet, walk, and pause sequences. Reuses or creates an Actor Registry as needed.");
            DrawOption(
                ref m_createGazeFlowController,
                "Gaze Flow Controller",
                "Adds per-step gaze timing on top of the Flow Controller. Reuses or creates the Flow Controller and Actor Registry as needed.");
            DrawOption(
                ref m_createSimpleController,
                "Simple Controller",
                "Adds a lightweight trigger for one snippet or one walk action. Reuses or creates an Actor Registry as needed.");
            DrawOption(
                ref m_addGazeDriversToActors,
                "Gaze Driver On Actors",
                "Lets characters look at the camera, objects, or other actors. Also helps the actor registry choose the right default idle and walk clip set.");
            DrawOption(
                ref m_addWalkersToActors,
                "Walker On Actors",
                "Lets characters move between waypoints. You can leave this off for static characters.");

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(!HasAnyWorkSelected()))
            {
                if (GUILayout.Button("Create Or Update Scene Setup", GUILayout.Height(34f)))
                    ApplySetup();
            }

            if (!string.IsNullOrWhiteSpace(m_lastSummary))
            {
                EditorGUILayout.Space(10f);
                EditorGUILayout.HelpBox(m_lastSummary, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawActorsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("These should be scene instances, not prefab assets.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);

            DrawActorDropArea();

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Selected"))
                AddActors(Selection.objects);
            if (GUILayout.Button("Clear Missing"))
                RemoveMissingActors();
            if (GUILayout.Button("Clear All"))
                m_actorPlayers.Clear();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            if (m_actorPlayers.Count == 0)
            {
                EditorGUILayout.LabelField("No actors added yet.", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < m_actorPlayers.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    m_actorPlayers[i] = (SnippetPlayer)EditorGUILayout.ObjectField($"Actor {i + 1}", m_actorPlayers[i], typeof(SnippetPlayer), true);
                    if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                    {
                        m_actorPlayers.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        void DrawActorDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 68f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop actor snippet instances here", EditorStyles.helpBox);

            var labelRect = new Rect(dropRect.x + 12f, dropRect.y + 24f, dropRect.width - 24f, 18f);
            EditorGUI.LabelField(
                labelRect,
                "Use one snippet instance per actor. The tool will reuse that scene object as the actor reference.",
                EditorStyles.centeredGreyMiniLabel);

            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
                return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    if (SnippetsActorSetupUtility.CanAcceptDraggedActors(DragAndDrop.objectReferences))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    if (!SnippetsActorSetupUtility.CanAcceptDraggedActors(DragAndDrop.objectReferences))
                        break;

                    DragAndDrop.AcceptDrag();
                    AddActors(DragAndDrop.objectReferences);
                    evt.Use();
                    break;
            }
        }

        void DrawOption(ref bool value, string label, string description)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            value = EditorGUILayout.ToggleLeft(label, value, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        void AddActors(IEnumerable<Object> objects)
        {
            if (objects == null)
                return;

            foreach (var obj in objects)
            {
                var player = SnippetsActorSetupUtility.ExtractSnippetPlayer(obj);
                if (player == null || m_actorPlayers.Contains(player))
                    continue;

                m_actorPlayers.Add(player);
            }
        }

        void RemoveMissingActors()
        {
            m_actorPlayers = m_actorPlayers
                .Where(player => player != null)
                .Distinct()
                .ToList();
        }

        void ApplySetup()
        {
            RemoveMissingActors();

            var result = SnippetsSceneAutoSetupUtility.Apply(
                m_actorPlayers,
                new SnippetsSceneAutoSetupOptions
                {
                    createActorRegistry = m_createActorRegistry,
                    createFlowController = m_createFlowController,
                    createGazeFlowController = m_createGazeFlowController,
                    createSimpleController = m_createSimpleController,
                    addGazeDriversToActors = m_addGazeDriversToActors,
                    addWalkersToActors = m_addWalkersToActors
                });

            if (result.toolsRoot != null)
                Selection.activeGameObject = result.toolsRoot;
            else if (m_actorPlayers.Count > 0 && m_actorPlayers[0] != null)
                Selection.activeObject = m_actorPlayers[0];

            m_lastSummary =
                $"Actors added: {result.actorsAdded}, actors refreshed: {result.actorsUpdated}, " +
                $"gaze drivers added: {result.gazeDriversAdded}, walkers added: {result.walkersAdded}.";
        }

        bool HasAnyWorkSelected()
        {
            return m_createActorRegistry
                || m_createFlowController
                || m_createGazeFlowController
                || m_createSimpleController
                || m_addGazeDriversToActors
                || m_addWalkersToActors;
        }
    }
}
#endif
