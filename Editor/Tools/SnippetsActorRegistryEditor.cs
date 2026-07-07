#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Snippets.Sdk;

[CustomEditor(typeof(SnippetsActorRegistry))]
public class SnippetsActorRegistryEditor : Editor
{
    SerializedProperty _actorsProp;
    SerializedProperty _forceIdleOnEnableProp;
    SerializedProperty _crossFadeSecondsProp;

    bool _showSettings;
    readonly Dictionary<int, bool> _showAdvancedActors = new();
    readonly Dictionary<int, bool> _showSnippetLists = new();
    readonly Dictionary<int, bool> _showCustomAnimationLists = new();

    void OnEnable()
    {
        _actorsProp = serializedObject.FindProperty("actors");
        _forceIdleOnEnableProp = serializedObject.FindProperty("forceIdleOnEnable");
        _crossFadeSecondsProp = serializedObject.FindProperty("crossFadeSeconds");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawRegistrySettings();
        EditorGUILayout.Space(10f);
        DrawActorsHeader();
        EditorGUILayout.Space(6f);
        DrawActors();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawRegistrySettings()
    {
        _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings", true);
        if (!_showSettings)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("These usually work well as-is.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);

        _forceIdleOnEnableProp.boolValue = EditorGUILayout.Toggle(
            new GUIContent("Start In Idle", "When enabled, actors are forced into their idle animations when the registry starts in Play Mode."),
            _forceIdleOnEnableProp.boolValue
        );

        _crossFadeSecondsProp.floatValue = EditorGUILayout.Slider(
            new GUIContent("Transition Time", "How long it takes to blend between idle, walk, and snippet animations."),
            _crossFadeSecondsProp.floatValue,
            0f,
            2f
        );

        EditorGUILayout.EndVertical();
    }

    void DrawActorsHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Drag actors in. Setup happens automatically.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(6f);

        DrawActorDropArea();
        EditorGUILayout.Space(6f);

        using (new EditorGUI.DisabledScope(_actorsProp.arraySize == 0))
        {
            if (GUILayout.Button("Refresh All"))
                ApplyActionToAllActors(RefreshActor, "Refresh Actors");
        }

        EditorGUILayout.EndVertical();
    }

    void DrawActorDropArea()
    {
        Rect dropRect = GUILayoutUtility.GetRect(0f, 62f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drag and drop your actors here", EditorStyles.helpBox);

        var labelRect = new Rect(dropRect.x + 10f, dropRect.y + 22f, dropRect.width - 20f, 18f);
        EditorGUI.LabelField(labelRect, "Drop scene actors from the Hierarchy to create and auto-configure new actor entries.", EditorStyles.centeredGreyMiniLabel);

        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
            return;

        switch (evt.type)
        {
            case EventType.DragUpdated:
                if (CanAcceptDraggedActors(DragAndDrop.objectReferences))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                break;

            case EventType.DragPerform:
                if (!CanAcceptDraggedActors(DragAndDrop.objectReferences))
                    break;

                DragAndDrop.AcceptDrag();
                AddDraggedActors(DragAndDrop.objectReferences);
                evt.Use();
                break;
        }
    }

    void DrawActors()
    {
        if (_actorsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Drop one or more scene actors into the box above to create actor entries automatically.", MessageType.Info);
            return;
        }

        var registry = (SnippetsActorRegistry)target;

        for (int i = 0; i < _actorsProp.arraySize; i++)
        {
            var actorProp = _actorsProp.GetArrayElementAtIndex(i);
            var actor = registry.actors != null && i < registry.actors.Count ? registry.actors[i] : null;
            bool showAdvanced = _showAdvancedActors.TryGetValue(i, out var storedAdvanced) && storedAdvanced;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(GetActorTitle(actor, i), EditorStyles.boldLabel);
            DrawActorSummary(actor);
            DrawActorToolbar(i, showAdvanced);

            if (showAdvanced)
                DrawActorProperties(actorProp, actor, i);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }
    }

    void DrawActorToolbar(int actorIndex, bool showAdvanced)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh Snippets"))
            RunActorAction(actorIndex, DiscoverSnippetsFromActorFolder, "Discover Actor Snippets");

        if (GUILayout.Button(showAdvanced ? "Collapse" : "Expand"))
            _showAdvancedActors[actorIndex] = !showAdvanced;

        if (GUILayout.Button("Remove"))
        {
            RemoveActor(actorIndex);
            return;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6f);
    }

    void DrawActorProperties(SerializedProperty actorProp, SnippetsActorRegistry.Actor actor, int actorIndex)
    {
        var registry = (SnippetsActorRegistry)target;

        EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(actorProp.FindPropertyRelative("name"));
        EditorGUILayout.PropertyField(actorProp.FindPropertyRelative("player"));
        EditorGUILayout.PropertyField(actorProp.FindPropertyRelative("walker"));
        EditorGUILayout.PropertyField(
            actorProp.FindPropertyRelative("gazeDriver"),
            new GUIContent("Gaze Driver", "SnippetsGazeDriver component for this actor.")
        );
        EditorGUILayout.PropertyField(actorProp.FindPropertyRelative("legacyAnimation"));
        var defaultLoopAnimationsProp = actorProp.FindPropertyRelative("defaultLoopAnimations");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            defaultLoopAnimationsProp,
            new GUIContent("Default Loop Animations", "Auto uses the actor's Gaze Driver preset when available. Override to Male Default or Female Default when the actor should use a specific built-in pair. Custom keeps the current clip assignments.")
        );
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            if (actor != null)
            {
                Undo.RecordObject(registry, "Change Default Loop Animations");
                ApplyDefaultLoopClips(actor);
                MarkRegistryDirty(registry);
            }

            serializedObject.Update();
            actorProp = _actorsProp.GetArrayElementAtIndex(actorIndex);
        }
        EditorGUILayout.PropertyField(actorProp.FindPropertyRelative("idleClip"));
        EditorGUILayout.PropertyField(actorProp.FindPropertyRelative("walkClip"));

        if (actor != null && actor.defaultLoopAnimations == SnippetsActorRegistry.DefaultLoopAnimationMode.Auto && actor.gazeDriver == null)
        {
            EditorGUILayout.HelpBox("Auto works best when the actor has a Gaze Driver with the correct preset. If that is not available, switch Default Loop Animations to Male Default, Female Default, or Custom.", MessageType.None);
        }

        string folderPath = GetSnippetSetFolder(actor);
        if (!string.IsNullOrEmpty(folderPath))
            EditorGUILayout.LabelField("Snippet Set Folder", folderPath, EditorStyles.miniLabel);
        else if (actor != null && actor.player != null)
            EditorGUILayout.HelpBox("Refresh Snippets expects this actor's player to come from a prefab asset in an imported snippet set folder.", MessageType.None);

        var snippetsProp = actorProp.FindPropertyRelative("snippets");
        int snippetCount = actor != null && actor.snippets != null ? actor.snippets.Count : 0;
        int customAnimationCount = actor != null && actor.customAnimations != null ? actor.customAnimations.Count : 0;
        bool showSnippetList = _showSnippetLists.TryGetValue(actorIndex, out var storedSnippetList) && storedSnippetList;
        bool showCustomAnimationList = _showCustomAnimationLists.TryGetValue(actorIndex, out var storedCustomAnimationList) && storedCustomAnimationList;

        string customAnimationsToggleLabel = showCustomAnimationList
            ? $"Hide Custom Animations ({customAnimationCount})"
            : $"Show Custom Animations ({customAnimationCount})";
        if (GUILayout.Button(customAnimationsToggleLabel, EditorStyles.miniButton))
            showCustomAnimationList = !showCustomAnimationList;

        _showCustomAnimationLists[actorIndex] = showCustomAnimationList;

        if (showCustomAnimationList)
        {
            EditorGUILayout.Space(4f);
            DrawCustomAnimationList(actorProp.FindPropertyRelative("customAnimations"), actor);
        }

        string snippetsToggleLabel = showSnippetList ? $"Hide Snippets ({snippetCount})" : $"Show Snippets ({snippetCount})";
        if (GUILayout.Button(snippetsToggleLabel, EditorStyles.miniButton))
            showSnippetList = !showSnippetList;

        _showSnippetLists[actorIndex] = showSnippetList;

        if (showSnippetList)
            DrawSnippetList(snippetsProp, actor);
    }

    void ApplyActionToAllActors(Action<SnippetsActorRegistry.Actor> action, string undoLabel)
    {
        serializedObject.ApplyModifiedProperties();

        var registry = (SnippetsActorRegistry)target;
        if (registry.actors == null || registry.actors.Count == 0)
            return;

        Undo.RecordObject(registry, undoLabel);
        foreach (var actor in registry.actors)
            action(actor);

        MarkRegistryDirty(registry);
        serializedObject.Update();
    }

    void RunActorAction(int actorIndex, Action<SnippetsActorRegistry.Actor> action, string undoLabel)
    {
        serializedObject.ApplyModifiedProperties();

        var registry = (SnippetsActorRegistry)target;
        if (registry.actors == null || actorIndex < 0 || actorIndex >= registry.actors.Count)
            return;

        Undo.RecordObject(registry, undoLabel);
        action(registry.actors[actorIndex]);
        MarkRegistryDirty(registry);
        serializedObject.Update();
    }

    void RemoveActor(int actorIndex)
    {
        if (actorIndex < 0 || actorIndex >= _actorsProp.arraySize)
            return;

        _actorsProp.DeleteArrayElementAtIndex(actorIndex);
        _showAdvancedActors.Remove(actorIndex);
        _showSnippetLists.Remove(actorIndex);
        _showCustomAnimationLists.Remove(actorIndex);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }

    void AddDraggedActors(UnityEngine.Object[] draggedObjects)
    {
        if (draggedObjects == null || draggedObjects.Length == 0)
            return;

        serializedObject.ApplyModifiedProperties();

        var registry = (SnippetsActorRegistry)target;
        Undo.RecordObject(registry, "Add Actors From Drag And Drop");

        foreach (var obj in draggedObjects)
        {
            var player = ExtractSnippetPlayer(obj);
            if (player == null)
                continue;

            AddActorFromPlayer(registry, player);
        }

        MarkRegistryDirty(registry);
        serializedObject.Update();
    }

    static bool CanAcceptDraggedActors(UnityEngine.Object[] draggedObjects)
    {
        return SnippetsActorSetupUtility.CanAcceptDraggedActors(draggedObjects);
    }

    static SnippetPlayer ExtractSnippetPlayer(UnityEngine.Object obj)
    {
        return SnippetsActorSetupUtility.ExtractSnippetPlayer(obj);
    }

    static void AddActorFromPlayer(SnippetsActorRegistry registry, SnippetPlayer player)
    {
        SnippetsActorSetupUtility.AddActorFromPlayer(registry, player);
    }

    static string GetActorTitle(SnippetsActorRegistry.Actor actor, int actorIndex)
    {
        if (actor != null)
        {
            if (!string.IsNullOrWhiteSpace(actor.name))
                return $"Actor {actorIndex + 1}: {actor.name}";

            if (actor.player != null && !string.IsNullOrWhiteSpace(actor.player.name))
                return $"Actor {actorIndex + 1}: {actor.player.name}";
        }

        return $"Actor {actorIndex + 1}";
    }

    static void DrawActorSummary(SnippetsActorRegistry.Actor actor)
    {
        string playerLabel = actor != null && actor.player != null ? actor.player.name : "Not assigned";
        EditorGUILayout.LabelField($"Player: {playerLabel}", EditorStyles.miniLabel);

        if (actor == null)
        {
            EditorGUILayout.LabelField("No actor data available.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);
            return;
        }

        var parts = new List<string>();
        int snippetCount = actor.snippets != null ? actor.snippets.Count : 0;
        int customAnimationCount = actor.customAnimations != null ? actor.customAnimations.Count : 0;
        parts.Add($"{snippetCount} snippet{(snippetCount == 1 ? string.Empty : "s")}");
        parts.Add($"{customAnimationCount} custom anim{(customAnimationCount == 1 ? string.Empty : "s")}");
        parts.Add(GetClipSummary(actor));
        if (actor.walker != null)
            parts.Add("Walker found");
        if (actor.gazeDriver != null)
            parts.Add("Gaze Driver found");
        if (actor.legacyAnimation != null)
            parts.Add("Animation found");

        EditorGUILayout.LabelField(string.Join(" | ", parts), EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);
    }

    void DrawSnippetList(SerializedProperty snippetsProp, SnippetsActorRegistry.Actor actor)
    {
        if (snippetsProp == null)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Assigned snippets", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("Auto-discovered by default. You can adjust this list manually if needed.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);

        if (snippetsProp.arraySize == 0)
        {
            EditorGUILayout.LabelField("No snippets assigned.", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < snippetsProp.arraySize; i++)
            {
                var element = snippetsProp.GetArrayElementAtIndex(i);
                var current = element.objectReferenceValue as SnippetPlayer;
                string label = current != null ? current.name : $"Snippet {i + 1}";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(element, new GUIContent(label));

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(38f)))
                        snippetsProp.MoveArrayElement(i, i - 1);
                }

                using (new EditorGUI.DisabledScope(i >= snippetsProp.arraySize - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(52f)))
                        snippetsProp.MoveArrayElement(i, i + 1);
                }

                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    snippetsProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Slot"))
            snippetsProp.InsertArrayElementAtIndex(snippetsProp.arraySize);
        if (GUILayout.Button("Clear Empty"))
            RemoveEmptySnippetEntries(snippetsProp);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    void DrawCustomAnimationList(SerializedProperty customAnimationsProp, SnippetsActorRegistry.Actor actor)
    {
        if (customAnimationsProp == null)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Assigned custom animations", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("Reusable legacy clips for custom motions like grabbing, pushing buttons, or operating devices.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);

        if (customAnimationsProp.arraySize == 0)
        {
            EditorGUILayout.LabelField("No custom animations assigned.", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < customAnimationsProp.arraySize; i++)
            {
                var element = customAnimationsProp.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("name");
                var clipProp = element.FindPropertyRelative("clip");

                string label = !string.IsNullOrWhiteSpace(nameProp.stringValue)
                    ? nameProp.stringValue
                    : (clipProp.objectReferenceValue != null ? clipProp.objectReferenceValue.name : $"Custom Animation {i + 1}");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
                EditorGUILayout.PropertyField(clipProp, new GUIContent("Clip"));

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(38f)))
                        customAnimationsProp.MoveArrayElement(i, i - 1);
                }

                using (new EditorGUI.DisabledScope(i >= customAnimationsProp.arraySize - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(52f)))
                        customAnimationsProp.MoveArrayElement(i, i + 1);
                }

                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    customAnimationsProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Slot"))
            customAnimationsProp.InsertArrayElementAtIndex(customAnimationsProp.arraySize);
        if (GUILayout.Button("Clear Empty"))
            RemoveEmptyCustomAnimationEntries(customAnimationsProp);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    static void RemoveEmptySnippetEntries(SerializedProperty snippetsProp)
    {
        if (snippetsProp == null)
            return;

        for (int i = snippetsProp.arraySize - 1; i >= 0; i--)
        {
            var element = snippetsProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == null)
                snippetsProp.DeleteArrayElementAtIndex(i);
        }
    }

    static void RemoveEmptyCustomAnimationEntries(SerializedProperty customAnimationsProp)
    {
        if (customAnimationsProp == null)
            return;

        for (int i = customAnimationsProp.arraySize - 1; i >= 0; i--)
        {
            var element = customAnimationsProp.GetArrayElementAtIndex(i);
            var clipProp = element.FindPropertyRelative("clip");
            var nameProp = element.FindPropertyRelative("name");

            bool hasClip = clipProp != null && clipProp.objectReferenceValue != null;
            bool hasName = nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue);

            if (!hasClip && !hasName)
                customAnimationsProp.DeleteArrayElementAtIndex(i);
        }
    }

    static string GetClipSummary(SnippetsActorRegistry.Actor actor)
    {
        bool hasIdle = actor != null && actor.idleClip != null;
        bool hasWalk = actor != null && actor.walkClip != null;
        bool hasBoth = hasIdle && hasWalk;

        if (actor == null)
            return "Clips missing";

        if (actor.defaultLoopAnimations == SnippetsActorRegistry.DefaultLoopAnimationMode.None)
            return hasBoth ? "Custom clips assigned" : "Custom clips missing";

        return hasBoth ? "Default clips assigned" : "Default clips missing";
    }

    static void AutoAssignActorComponents(SnippetsActorRegistry.Actor actor)
    {
        SnippetsActorSetupUtility.AutoAssignActorComponents(actor);
    }

    static void SetupActor(SnippetsActorRegistry.Actor actor)
    {
        SnippetsActorSetupUtility.SetupActor(actor);
    }

    static void RefreshActor(SnippetsActorRegistry.Actor actor)
    {
        SnippetsActorSetupUtility.RefreshActor(actor);
    }

    static void ApplyDefaultLoopClips(SnippetsActorRegistry.Actor actor)
    {
        SnippetsActorSetupUtility.ApplyDefaultLoopClips(actor);
    }

    static void DiscoverSnippetsFromActorFolder(SnippetsActorRegistry.Actor actor)
    {
        SnippetsActorSetupUtility.DiscoverSnippetsFromActorFolder(actor);
    }

    static string GetSnippetSetFolder(SnippetsActorRegistry.Actor actor)
    {
        return SnippetsActorSetupUtility.GetSnippetSetFolder(actor);
    }

    static void MarkRegistryDirty(SnippetsActorRegistry registry)
    {
        SnippetsActorSetupUtility.MarkRegistryDirty(registry);
    }
}
#endif
