#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SnippetsFlowController))]
public class SnippetsFlowControllerEditor : Editor
{
    SerializedProperty _registryProp;
    SerializedProperty _stepsProp;

    SerializedProperty _playOnStartProp;
    SerializedProperty _loopSequenceProp;
    SerializedProperty _autoProgressProp;

    SerializedProperty _enableKeyboardProp;
    SerializedProperty _keyProp;

    ReorderableList _stepsList;

    static readonly GUIContent GC_Action = new("Action");
    static readonly GUIContent GC_Actor = new("Actor");
    static readonly GUIContent GC_Snippet = new("Snippet");
    static readonly GUIContent GC_Waypoint = new("Waypoint");
    static readonly GUIContent GC_WaitForTrigger = new("Wait For Trigger");
    static readonly GUIContent GC_Seconds = new("Seconds");

    void OnEnable()
    {
        _registryProp = serializedObject.FindProperty("registry");
        _stepsProp = serializedObject.FindProperty("steps");

        _playOnStartProp = serializedObject.FindProperty("playOnStart");
        _loopSequenceProp = serializedObject.FindProperty("loopSequence");
        _autoProgressProp = serializedObject.FindProperty("autoProgress");

        _enableKeyboardProp = serializedObject.FindProperty("enableKeyboard");
        _keyProp = serializedObject.FindProperty("key");

        BuildStepsList();
    }

    void BuildStepsList()
    {
        _stepsList = new ReorderableList(serializedObject, _stepsProp, true, true, true, true);

        _stepsList.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Steps"); };

        _stepsList.elementHeightCallback = index =>
        {
            var el = _stepsProp.GetArrayElementAtIndex(index);
            return ComputeElementHeight(el);
        };

        _stepsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var el = _stepsProp.GetArrayElementAtIndex(index);
            DrawStepElement(rect, index, el, isActive);
        };

        _stepsList.onAddCallback = list =>
        {
            int i = _stepsProp.arraySize;
            _stepsProp.InsertArrayElementAtIndex(i);

            var el = _stepsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("type").enumValueIndex = (int)SnippetsFlowController.StepType.Snippet;
            el.FindPropertyRelative("actorIndex").intValue = 0;
            el.FindPropertyRelative("snippetIndex").intValue = 0;
            el.FindPropertyRelative("waypointIndex").intValue = 0;
            el.FindPropertyRelative("waitForTrigger").boolValue = false;
            el.FindPropertyRelative("seconds").floatValue = 0f;

            serializedObject.ApplyModifiedProperties();
        };
    }

    public override void OnInspectorGUI()
    {
        if (target == null) return;

        serializedObject.Update();

        EditorGUILayout.LabelField("Registry", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_registryProp);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Flow", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_playOnStartProp);
        EditorGUILayout.PropertyField(_loopSequenceProp);
        EditorGUILayout.PropertyField(_autoProgressProp);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Keyboard (Start + Resume)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_enableKeyboardProp);
        using (new EditorGUI.DisabledScope(!_enableKeyboardProp.boolValue))
            EditorGUILayout.PropertyField(_keyProp);

        EditorGUILayout.Space(10);
        _stepsList?.DoLayoutList();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Flow Controls", EditorStyles.boldLabel);

        var ctrl = (SnippetsFlowController)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start")) ctrl.StartFlow();
            if (GUILayout.Button("Stop")) ctrl.StopFlow();
            if (GUILayout.Button("Reset")) ctrl.ResetFlow();
            EditorGUILayout.EndHorizontal();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use Start/Stop/Reset.", MessageType.None);
    }

    float ComputeElementHeight(SerializedProperty stepProp)
    {
        const float line = 18f;
        const float gap = 4f;
        const float padTop = 6f;
        const float padBottom = 6f;

        if (stepProp == null) return padTop + line + padBottom;

        var typeProp = stepProp.FindPropertyRelative("type");
        var type = (SnippetsFlowController.StepType)(typeProp != null ? typeProp.enumValueIndex : 0);

        int lines = 0;

        lines += 1; // title
        lines += 1; // action

        if (type != SnippetsFlowController.StepType.Pause)
            lines += 1; // actor

        switch (type)
        {
            case SnippetsFlowController.StepType.Snippet:
                lines += 1; // snippet
                break;
            case SnippetsFlowController.StepType.Walk:
                lines += 1; // waypoint
                break;
            case SnippetsFlowController.StepType.Pause:
                lines += 1; // waitForTrigger
                var waitProp = stepProp.FindPropertyRelative("waitForTrigger");
                if (waitProp != null && !waitProp.boolValue) lines += 1; // seconds
                break;
        }

        return padTop + (lines * line) + ((lines - 1) * gap) + padBottom;
    }

    void DrawStepElement(Rect rect, int index, SerializedProperty stepProp, bool isActive)
    {
        if (stepProp == null) return;

        const float line = 18f;
        const float gap = 4f;
        const float pad = 6f;

        // Background
        var bg = rect;
        bg.x += 2;
        bg.width -= 4;
        bg.height -= 2;
        bg.y += 1;

        Color c = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, isActive ? 0.06f : 0.03f)
            : new Color(0f, 0f, 0f, isActive ? 0.06f : 0.03f);

        EditorGUI.DrawRect(bg, c);

        rect.x += pad;
        rect.width -= pad * 2;
        rect.y += pad;

        var typeProp = stepProp.FindPropertyRelative("type");
        var actorProp = stepProp.FindPropertyRelative("actorIndex");
        var snippetIndexProp = stepProp.FindPropertyRelative("snippetIndex");
        var waypointIndexProp = stepProp.FindPropertyRelative("waypointIndex");
        var waitProp = stepProp.FindPropertyRelative("waitForTrigger");
        var secondsProp = stepProp.FindPropertyRelative("seconds");

        var type = (SnippetsFlowController.StepType)(typeProp != null ? typeProp.enumValueIndex : 0);
        var newType = type;

        // Registry access + step object access (for title formatting)
        var ctrl = target as SnippetsFlowController;
        var registry = ctrl != null ? ctrl.registry : null;

        // Title (NEW)
        string title = ctrl != null ? ctrl.GetStepDisplayLabel(index) : $"Step {index + 1}: {type}";
        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, line), title, EditorStyles.boldLabel);
        rect.y += line + gap;

        // Action dropdown
        EditorGUI.BeginChangeCheck();
        newType = (SnippetsFlowController.StepType)EditorGUI.EnumPopup(
            new Rect(rect.x, rect.y, rect.width, line),
            GC_Action,
            type
        );
        if (EditorGUI.EndChangeCheck() && typeProp != null)
            typeProp.enumValueIndex = (int)newType;

        rect.y += line + gap;

        // Actor dropdown (not for Pause)
        int actorIndex = actorProp != null ? actorProp.intValue : 0;
        if (newType != SnippetsFlowController.StepType.Pause && actorProp != null)
        {
            if (registry != null && registry.ActorCount > 0)
            {
                int actorCount = registry.ActorCount;
                string[] actorNames = new string[actorCount];
                for (int i = 0; i < actorCount; i++)
                    actorNames[i] = registry.GetActorDisplayName(i);

                actorIndex = Mathf.Clamp(actorIndex, 0, actorCount - 1);

                int newActorIndex = EditorGUI.Popup(
                    new Rect(rect.x, rect.y, rect.width, line),
                    $"{GC_Actor.text} [{actorIndex}]",
                    actorIndex,
                    actorNames
                );

                if (newActorIndex != actorIndex)
                {
                    actorProp.intValue = newActorIndex;
                    if (snippetIndexProp != null) snippetIndexProp.intValue = 0;
                    if (waypointIndexProp != null) waypointIndexProp.intValue = 0;
                }
            }
            else
            {
                actorProp.intValue = EditorGUI.IntField(
                    new Rect(rect.x, rect.y, rect.width, line),
                    new GUIContent($"{GC_Actor.text} [{actorIndex}]"),
                    actorIndex
                );
            }

            rect.y += line + gap;
        }

        // Type-specific fields
        switch (newType)
        {
            case SnippetsFlowController.StepType.Snippet:
            {
                if (snippetIndexProp == null) break;

                int snIdx = snippetIndexProp.intValue;

                if (registry != null)
                {
                    var snippets = registry.GetSnippets(actorProp != null ? actorProp.intValue : 0);
                    int snCount = snippets != null ? snippets.Count : 0;

                    if (snCount > 0)
                    {
                        string[] snippetNames = new string[snCount];
                        for (int i = 0; i < snCount; i++)
                            snippetNames[i] = registry.GetSnippetDisplayName(actorProp.intValue, i);

                        snIdx = Mathf.Clamp(snIdx, 0, snCount - 1);

                        int newSnIdx = EditorGUI.Popup(
                            new Rect(rect.x, rect.y, rect.width, line),
                            $"{GC_Snippet.text} [{snIdx}]",
                            snIdx,
                            snippetNames
                        );

                        if (newSnIdx != snIdx)
                            snippetIndexProp.intValue = newSnIdx;
                    }
                    else
                    {
                        snippetIndexProp.intValue = EditorGUI.IntField(
                            new Rect(rect.x, rect.y, rect.width, line),
                            new GUIContent($"{GC_Snippet.text} [{snIdx}]"),
                            snIdx
                        );
                    }
                }
                else
                {
                    snippetIndexProp.intValue = EditorGUI.IntField(
                        new Rect(rect.x, rect.y, rect.width, line),
                        new GUIContent($"{GC_Snippet.text} [{snIdx}]"),
                        snIdx
                    );
                }

                break;
            }

            case SnippetsFlowController.StepType.Walk:
            {
                if (waypointIndexProp == null) break;

                int wIdx = waypointIndexProp.intValue;

                // Waypoint dropdown by name using the actor's walker waypoints
                if (registry != null)
                {
                    var walker = registry.GetWalker(actorProp != null ? actorProp.intValue : 0);
                    if (walker != null && walker.waypoints != null && walker.waypoints.Length > 0)
                    {
                        int wpCount = walker.waypoints.Length;
                        string[] wpNames = new string[wpCount];
                        for (int i = 0; i < wpCount; i++)
                            wpNames[i] = walker.waypoints[i] != null ? walker.waypoints[i].name : $"Waypoint {i}";

                        wIdx = Mathf.Clamp(wIdx, 0, wpCount - 1);

                        int newWIdx = EditorGUI.Popup(
                            new Rect(rect.x, rect.y, rect.width, line),
                            $"{GC_Waypoint.text} [{wIdx}]",
                            wIdx,
                            wpNames
                        );

                        if (newWIdx != wIdx)
                            waypointIndexProp.intValue = newWIdx;

                        break;
                    }
                }

                // Fallback: int field
                waypointIndexProp.intValue = EditorGUI.IntField(
                    new Rect(rect.x, rect.y, rect.width, line),
                    new GUIContent($"{GC_Waypoint.text} [{wIdx}]"),
                    wIdx
                );

                break;
            }

            case SnippetsFlowController.StepType.Pause:
            {
                if (waitProp == null) break;

                waitProp.boolValue = EditorGUI.Toggle(
                    new Rect(rect.x, rect.y, rect.width, line),
                    GC_WaitForTrigger,
                    waitProp.boolValue
                );
                rect.y += line + gap;

                if (!waitProp.boolValue && secondsProp != null)
                {
                    float s = EditorGUI.FloatField(
                        new Rect(rect.x, rect.y, rect.width, line),
                        GC_Seconds,
                        secondsProp.floatValue
                    );
                    secondsProp.floatValue = Mathf.Max(0f, s);
                }
                break;
            }
        }
    }
}
#endif
