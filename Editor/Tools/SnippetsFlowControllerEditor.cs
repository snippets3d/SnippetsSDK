#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
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
    bool _showSettings;

    static readonly GUIContent GC_Action = new("Action");
    static readonly GUIContent GC_Actor = new("Actor");
    static readonly GUIContent GC_Snippet = new("Snippet");
    static readonly GUIContent GC_CustomAnimation = new("Custom Animation");
    static readonly GUIContent GC_SnippetMask = new("Snippet Mask");
    static readonly GUIContent GC_Completion = new("Completion");
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
            el.FindPropertyRelative("guid").stringValue = string.Empty;
            el.FindPropertyRelative("type").enumValueIndex = (int)SnippetsFlowController.StepType.Snippet;
            el.FindPropertyRelative("actorIndex").intValue = 0;
            el.FindPropertyRelative("snippetIndex").intValue = 0;
            el.FindPropertyRelative("customAnimationIndex").intValue = 0;
            el.FindPropertyRelative("completionPolicy").enumValueIndex = (int)SnippetsFlowController.CompletionPolicy.WaitForBoth;
            el.FindPropertyRelative("snippetMaskMode").enumValueIndex = (int)SnippetsActorRegistry.SnippetMaskMode.None;
            el.FindPropertyRelative("waypointIndex").intValue = 0;
            el.FindPropertyRelative("waitForTrigger").boolValue = false;
            el.FindPropertyRelative("seconds").floatValue = 0f;

            serializedObject.ApplyModifiedProperties();
        };
    }

    public override void OnInspectorGUI()
    {
        if (target == null) return;

        EditorGUI.BeginChangeCheck();
        serializedObject.Update();

        EditorGUILayout.PropertyField(_registryProp, new GUIContent("Registry"));

        EditorGUILayout.Space(8f);
        DrawSettings();

        EditorGUILayout.Space(10);
        _stepsList?.DoLayoutList();

        serializedObject.ApplyModifiedProperties();

        if (EditorGUI.EndChangeCheck())
            NotifyLinkedGazeControllers();

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

    void DrawSettings()
    {
        _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings", true);
        if (!_showSettings)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("These usually work well as-is.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);

        EditorGUILayout.PropertyField(_playOnStartProp);
        EditorGUILayout.PropertyField(_loopSequenceProp);
        EditorGUILayout.PropertyField(_autoProgressProp);

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(_enableKeyboardProp);
        using (new EditorGUI.DisabledScope(!_enableKeyboardProp.boolValue))
            EditorGUILayout.PropertyField(_keyProp);

        EditorGUILayout.EndVertical();
    }

    void NotifyLinkedGazeControllers()
    {
        var flow = target as SnippetsFlowController;
        if (flow == null) return;

        flow.EnsureStepGuids();
        EditorUtility.SetDirty(flow);

        var gazeControllers = Resources.FindObjectsOfTypeAll<SnippetsGazeFlowController>();
        for (int i = 0; i < gazeControllers.Length; i++)
        {
            var gaze = gazeControllers[i];
            if (gaze == null || gaze.flow != flow || EditorUtility.IsPersistent(gaze))
                continue;

            if (gaze.autoSyncToFlowSteps)
            {
                if (gaze.autoLabelFromFlow)
                    gaze.SyncNow();
                else
                    gaze.MatchStepsFromFlow();
            }
            else if (gaze.autoLabelFromFlow)
            {
                gaze.RelabelFromFlow();
            }
            else
            {
                continue;
            }

            EditorUtility.SetDirty(gaze);
        }
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

            case SnippetsFlowController.StepType.CustomAnim:
                lines += 1; // custom animation
                break;

            case SnippetsFlowController.StepType.SnippetWithCustomAnim:
                lines += 1; // snippet
                lines += 1; // custom animation
                lines += 1; // snippet mask
                lines += 1; // completion
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
        var customAnimationIndexProp = stepProp.FindPropertyRelative("customAnimationIndex");
        var snippetMaskModeProp = stepProp.FindPropertyRelative("snippetMaskMode");
        var completionPolicyProp = stepProp.FindPropertyRelative("completionPolicy");
        var waypointIndexProp = stepProp.FindPropertyRelative("waypointIndex");
        var waitProp = stepProp.FindPropertyRelative("waitForTrigger");
        var secondsProp = stepProp.FindPropertyRelative("seconds");

        var type = (SnippetsFlowController.StepType)(typeProp != null ? typeProp.enumValueIndex : 0);
        var newType = type;

        var ctrl = target as SnippetsFlowController;
        var registry = ctrl != null ? ctrl.registry : null;

        string title = ctrl != null ? ctrl.GetStepDisplayLabel(index) : $"Step {index + 1}: {type}";
        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, line), title, EditorStyles.boldLabel);
        rect.y += line + gap;

        EditorGUI.BeginChangeCheck();
        newType = (SnippetsFlowController.StepType)EditorGUI.EnumPopup(
            new Rect(rect.x, rect.y, rect.width, line),
            GC_Action,
            type
        );
        if (EditorGUI.EndChangeCheck() && typeProp != null)
            typeProp.enumValueIndex = (int)newType;

        rect.y += line + gap;

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
                    if (customAnimationIndexProp != null) customAnimationIndexProp.intValue = 0;
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

        switch (newType)
        {
            case SnippetsFlowController.StepType.Snippet:
                DrawSnippetField(rect, actorProp, snippetIndexProp, registry);
                break;

            case SnippetsFlowController.StepType.CustomAnim:
                DrawCustomAnimationField(rect, actorProp, customAnimationIndexProp, registry);
                break;

            case SnippetsFlowController.StepType.SnippetWithCustomAnim:
                DrawSnippetField(rect, actorProp, snippetIndexProp, registry);
                rect.y += line + gap;
                DrawCustomAnimationField(rect, actorProp, customAnimationIndexProp, registry);
                rect.y += line + gap;
                if (snippetMaskModeProp != null)
                {
                    EditorGUI.PropertyField(
                        new Rect(rect.x, rect.y, rect.width, line),
                        snippetMaskModeProp,
                        GC_SnippetMask
                    );
                    rect.y += line + gap;
                }

                if (completionPolicyProp != null)
                {
                    EditorGUI.PropertyField(
                        new Rect(rect.x, rect.y, rect.width, line),
                        completionPolicyProp,
                        GC_Completion
                    );
                }
                break;

            case SnippetsFlowController.StepType.Walk:
                DrawWaypointField(rect, actorProp, waypointIndexProp, registry);
                break;

            case SnippetsFlowController.StepType.Pause:
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

    void DrawSnippetField(Rect rect, SerializedProperty actorProp, SerializedProperty snippetIndexProp, SnippetsActorRegistry registry)
    {
        if (snippetIndexProp == null)
            return;

        const float line = 18f;
        int actorIndex = actorProp != null ? actorProp.intValue : 0;
        int snIdx = snippetIndexProp.intValue;

        if (registry != null)
        {
            var snippets = registry.GetSnippets(actorIndex);
            int snCount = snippets != null ? snippets.Count : 0;

            if (snCount > 0)
            {
                string[] snippetNames = new string[snCount];
                for (int i = 0; i < snCount; i++)
                    snippetNames[i] = registry.GetSnippetDisplayName(actorIndex, i);

                snIdx = Mathf.Clamp(snIdx, 0, snCount - 1);

                int newSnIdx = EditorGUI.Popup(
                    new Rect(rect.x, rect.y, rect.width, line),
                    $"{GC_Snippet.text} [{snIdx}]",
                    snIdx,
                    snippetNames
                );

                if (newSnIdx != snIdx)
                    snippetIndexProp.intValue = newSnIdx;

                return;
            }
        }

        snippetIndexProp.intValue = EditorGUI.IntField(
            new Rect(rect.x, rect.y, rect.width, line),
            new GUIContent($"{GC_Snippet.text} [{snIdx}]"),
            snIdx
        );
    }

    void DrawCustomAnimationField(Rect rect, SerializedProperty actorProp, SerializedProperty customAnimationIndexProp, SnippetsActorRegistry registry)
    {
        if (customAnimationIndexProp == null)
            return;

        const float line = 18f;
        int actorIndex = actorProp != null ? actorProp.intValue : 0;
        int customIndex = customAnimationIndexProp.intValue;

        if (TryGetCustomAnimationNames(registry, actorIndex, out var names) && names.Length > 0)
        {
            customIndex = Mathf.Clamp(customIndex, 0, names.Length - 1);

            int newIndex = EditorGUI.Popup(
                new Rect(rect.x, rect.y, rect.width, line),
                $"{GC_CustomAnimation.text} [{customIndex}]",
                customIndex,
                names
            );

            if (newIndex != customIndex)
                customAnimationIndexProp.intValue = newIndex;

            return;
        }

        customAnimationIndexProp.intValue = EditorGUI.IntField(
            new Rect(rect.x, rect.y, rect.width, line),
            new GUIContent($"{GC_CustomAnimation.text} [{customIndex}]"),
            customIndex
        );
    }

    void DrawWaypointField(Rect rect, SerializedProperty actorProp, SerializedProperty waypointIndexProp, SnippetsActorRegistry registry)
    {
        if (waypointIndexProp == null)
            return;

        const float line = 18f;
        int actorIndex = actorProp != null ? actorProp.intValue : 0;
        int waypointIndex = waypointIndexProp.intValue;

        if (registry != null)
        {
            var walker = registry.GetWalker(actorIndex);
            if (walker != null && walker.waypoints != null && walker.waypoints.Length > 0)
            {
                int wpCount = walker.waypoints.Length;
                string[] wpNames = new string[wpCount];
                for (int i = 0; i < wpCount; i++)
                    wpNames[i] = walker.waypoints[i] != null ? walker.waypoints[i].name : $"Waypoint {i}";

                waypointIndex = Mathf.Clamp(waypointIndex, 0, wpCount - 1);

                int newWaypointIndex = EditorGUI.Popup(
                    new Rect(rect.x, rect.y, rect.width, line),
                    $"{GC_Waypoint.text} [{waypointIndex}]",
                    waypointIndex,
                    wpNames
                );

                if (newWaypointIndex != waypointIndex)
                    waypointIndexProp.intValue = newWaypointIndex;

                return;
            }
        }

        waypointIndexProp.intValue = EditorGUI.IntField(
            new Rect(rect.x, rect.y, rect.width, line),
            new GUIContent($"{GC_Waypoint.text} [{waypointIndex}]"),
            waypointIndex
        );
    }

    bool TryGetCustomAnimationNames(SnippetsActorRegistry registry, int actorIndex, out string[] names)
    {
        names = Array.Empty<string>();
        if (registry == null)
            return false;

        object customAnimations = InvokeRegistryMethod(
            registry,
            new[]
            {
                "GetCustomAnimations",
                "GetCustomAnimationEntries",
                "GetCustomAnimEntries",
                "GetCustomAnimationClips",
                "GetCustomAnimClips"
            },
            actorIndex
        );

        if (TryGetNamesFromList(customAnimations, out names))
            return true;

        var actor = registry.GetActor(actorIndex);
        if (actor == null)
            return false;

        if (TryGetNamesFromActorMember(actor, "customAnimations", out names))
            return true;

        if (TryGetNamesFromActorMember(actor, "customAnimationEntries", out names))
            return true;

        if (TryGetNamesFromActorMember(actor, "customAnimationClips", out names))
            return true;

        if (TryGetNamesFromActorMember(actor, "customClips", out names))
            return true;

        return false;
    }

    static bool TryGetNamesFromActorMember(object actor, string memberName, out string[] names)
    {
        names = Array.Empty<string>();
        if (actor == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type actorType = actor.GetType();

        FieldInfo field = actorType.GetField(memberName, flags);
        if (field != null && TryGetNamesFromList(field.GetValue(actor), out names))
            return true;

        PropertyInfo property = actorType.GetProperty(memberName, flags);
        if (property != null && property.CanRead && TryGetNamesFromList(property.GetValue(actor), out names))
            return true;

        return false;
    }

    static bool TryGetNamesFromList(object value, out string[] names)
    {
        names = Array.Empty<string>();
        if (value is not IList list || list.Count == 0)
            return false;

        names = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            names[i] = GetDisplayNameFromObject(list[i], i);

        return true;
    }

    static string GetDisplayNameFromObject(object value, int index)
    {
        if (value == null)
            return $"Custom Animation {index}";

        if (value is AnimationClip clip && !string.IsNullOrWhiteSpace(clip.name))
            return clip.name;

        if (value is UnityEngine.Object unityObject && !string.IsNullOrWhiteSpace(unityObject.name))
            return unityObject.name;

        Type valueType = value.GetType();
        if (TryReadNamedMember(value, valueType, "name", out string displayName))
            return displayName;

        if (TryReadClipMember(value, valueType, "clip", out string clipName))
            return clipName;

        if (TryReadClipMember(value, valueType, "animationClip", out clipName))
            return clipName;

        return value.ToString();
    }

    static bool TryReadNamedMember(object value, Type valueType, string memberName, out string result)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        result = null;

        FieldInfo field = valueType.GetField(memberName, flags);
        if (field != null && field.GetValue(value) is string fieldValue && !string.IsNullOrWhiteSpace(fieldValue))
        {
            result = fieldValue;
            return true;
        }

        PropertyInfo property = valueType.GetProperty(memberName, flags);
        if (property != null && property.CanRead && property.GetValue(value) is string propertyValue && !string.IsNullOrWhiteSpace(propertyValue))
        {
            result = propertyValue;
            return true;
        }

        return false;
    }

    static bool TryReadClipMember(object value, Type valueType, string memberName, out string result)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        result = null;

        FieldInfo field = valueType.GetField(memberName, flags);
        if (field != null && field.GetValue(value) is AnimationClip fieldClip && !string.IsNullOrWhiteSpace(fieldClip.name))
        {
            result = fieldClip.name;
            return true;
        }

        PropertyInfo property = valueType.GetProperty(memberName, flags);
        if (property != null && property.CanRead && property.GetValue(value) is AnimationClip propertyClip && !string.IsNullOrWhiteSpace(propertyClip.name))
        {
            result = propertyClip.name;
            return true;
        }

        return false;
    }

    static object InvokeRegistryMethod(SnippetsActorRegistry registry, string[] methodNames, params object[] args)
    {
        MethodInfo method = ResolveRegistryMethod(registry, methodNames, args.Length);
        if (method == null || registry == null)
            return null;

        try
        {
            return method.Invoke(registry, args);
        }
        catch (TargetInvocationException ex)
        {
            Debug.LogException(ex.InnerException ?? ex);
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return null;
        }
    }

    static MethodInfo ResolveRegistryMethod(SnippetsActorRegistry registry, string[] methodNames, int parameterCount)
    {
        if (registry == null || methodNames == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type registryType = registry.GetType();
        MethodInfo[] methods = registryType.GetMethods(flags);

        for (int i = 0; i < methodNames.Length; i++)
        {
            string methodName = methodNames[i];
            if (string.IsNullOrEmpty(methodName))
                continue;

            for (int m = 0; m < methods.Length; m++)
            {
                MethodInfo method = methods[m];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    continue;

                if (method.GetParameters().Length == parameterCount)
                    return method;
            }
        }

        return null;
    }
}
#endif
