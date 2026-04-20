#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SnippetsGazeFlowController))]
public class SnippetsGazeFlowControllerEditor : Editor
{
    static readonly string[] StepModeOptions = { "Whole Step", "Granular" };
    static readonly string[] UnspecifiedActorOptions = { "Keep Current Gaze", "Turn Gaze Off", "Look At Camera" };
    static readonly string[] TargetTypeOptions = { "Turn Off", "Object", "Actor", "Main Camera", "Look Forward" };

    SerializedProperty flow;
    SerializedProperty gazeSteps;
    SerializedProperty unspecifiedActors;
    SerializedProperty autoSyncToFlowSteps;
    SerializedProperty autoLabelFromFlow;
    ReorderableList _stepsList;
    bool _showSettings;

    readonly Dictionary<int, ReorderableList> _simpleOverrideLists = new();
    readonly Dictionary<int, ReorderableList> _cueLists = new();
    readonly Dictionary<string, ReorderableList> _cueOverrideLists = new();
    readonly Dictionary<int, string[]> _actorOptionsByRegistryId = new();

    GUIStyle _headerStyle;
    GUIStyle _miniLabel;
    GUIStyle _miniBoldLabel;

    const float kPad = 6f;
    const float kStepCardPad = 8f;
    const float kStepCardGap = 6f;

    // ---------------- Safe styles (never touch EditorStyles in OnEnable) ----------------

    GUIStyle HeaderStyle
    {
        get
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
            }
            return _headerStyle;
        }
    }

    GUIStyle MiniLabel
    {
        get
        {
            if (_miniLabel == null)
            {
                _miniLabel = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = 10
                };
            }
            return _miniLabel;
        }
    }

    GUIStyle MiniBoldLabel
    {
        get
        {
            if (_miniBoldLabel == null)
            {
                _miniBoldLabel = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 10,
                    wordWrap = false
                };
            }
            return _miniBoldLabel;
        }
    }

    void OnEnable()
    {
        // Serialized properties
        flow = serializedObject.FindProperty("flow");
        gazeSteps = serializedObject.FindProperty("gazeSteps");

        unspecifiedActors = serializedObject.FindProperty("unspecifiedActors");
        autoSyncToFlowSteps = serializedObject.FindProperty("autoSyncToFlowSteps");
        autoLabelFromFlow = serializedObject.FindProperty("autoLabelFromFlow");
        // DO NOT allocate GUIStyles from EditorStyles here (can nullref in some Unity states).
        // Styles are created lazily from GUI.skin in properties above.

        BuildStepsList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var controller = (SnippetsGazeFlowController)target;
        EditorGUILayout.PropertyField(flow, new GUIContent("Flow"));

        if (controller.flow != null && controller.GetResolvedRegistry() == null)
            EditorGUILayout.HelpBox("The linked Flow does not have an Actor Registry assigned yet.", MessageType.Warning);

        EditorGUILayout.Space(8f);
        DrawSettings(controller);

        EditorGUILayout.Space(10);
        _stepsList ??= BuildStepsList();
        _stepsList?.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawSettings(SnippetsGazeFlowController controller)
    {
        _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings", true);
        if (!_showSettings)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawUnspecifiedActorsPopup();

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(autoSyncToFlowSteps, new GUIContent("Auto-Match Steps"));
        EditorGUILayout.PropertyField(autoLabelFromFlow, new GUIContent("Auto-Match Names"));

        DrawSyncBlock();
        EditorGUILayout.EndVertical();
    }

    void DrawSyncBlock()
    {
        var controller = (SnippetsGazeFlowController)target;
        var flowObj = controller.flow;
        bool keepStepsMatched = autoSyncToFlowSteps != null && autoSyncToFlowSteps.boolValue;
        bool keepNamesMatched = autoLabelFromFlow != null && autoLabelFromFlow.boolValue;

        if (keepStepsMatched && keepNamesMatched)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Apply Now", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(flowObj == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (!keepStepsMatched && GUILayout.Button("Match Steps Now"))
            {
                controller.MatchStepsFromFlow();
                EditorUtility.SetDirty(controller);
                if (controller.flow != null) EditorUtility.SetDirty(controller.flow);
                serializedObject.Update();

                _simpleOverrideLists.Clear();
                _cueLists.Clear();
                _cueOverrideLists.Clear();

                BuildStepsList();
            }

            if (!keepNamesMatched && GUILayout.Button("Copy Names Now"))
            {
                controller.RelabelFromFlow();

                EditorUtility.SetDirty(controller);
                serializedObject.Update();

                BuildStepsList();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    ReorderableList BuildStepsList()
    {
        if (gazeSteps == null)
            return _stepsList;

        _stepsList = new ReorderableList(serializedObject, gazeSteps, false, true, false, false);

        _stepsList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Gaze Steps");
        };

        _stepsList.elementHeightCallback = stepIndex =>
        {
            var stepEl = gazeSteps.GetArrayElementAtIndex(stepIndex);
            if (stepEl == null) return EditorGUIUtility.singleLineHeight * 2f;

            var modeProp = stepEl.FindPropertyRelative("mode");
            var overridesProp = stepEl.FindPropertyRelative("overrides");
            var cuesProp = stepEl.FindPropertyRelative("cues");

            float h = 0f;
            h += (kStepCardPad * 2f) + kStepCardGap;
            h += EditorGUIUtility.singleLineHeight + kPad; // header
            h += EditorGUIUtility.singleLineHeight + kPad; // mode row

            if (modeProp == null)
                return h + EditorGUIUtility.singleLineHeight * 2f;

            var mode = (SnippetsGazeFlowController.StepGazeMode)modeProp.enumValueIndex;

            if (mode == SnippetsGazeFlowController.StepGazeMode.Simple)
            {
                h += 34f + 4f;
                var list = GetOrCreateSimpleOverridesList(stepIndex, overridesProp);
                h += list.GetHeight() + kPad;
            }
            else
            {
                h += 34f + 4f;
                if (cuesProp != null && cuesProp.arraySize > 0)
                    h += EditorGUIUtility.singleLineHeight + 4f;

                var cueList = GetOrCreateCueList(stepIndex, cuesProp);
                h += cueList.GetHeight() + kPad;
            }

            return h;
        };

        _stepsList.drawElementCallback = (rect, stepIndex, isActive, isFocused) =>
        {
            var stepEl = gazeSteps.GetArrayElementAtIndex(stepIndex);
            if (stepEl == null) return;

            var labelProp = stepEl.FindPropertyRelative("label");
            var modeProp = stepEl.FindPropertyRelative("mode");
            var overridesProp = stepEl.FindPropertyRelative("overrides");
            var cuesProp = stepEl.FindPropertyRelative("cues");

            var cardRect = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - kStepCardGap);
            GUI.Box(cardRect, GUIContent.none, EditorStyles.helpBox);

            var bgRect = new Rect(cardRect.x + 1f, cardRect.y + 1f, cardRect.width - 2f, cardRect.height - 2f);
            Color bgColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, isActive ? 0.05f : 0.025f)
                : new Color(0f, 0f, 0f, isActive ? 0.05f : 0.025f);
            EditorGUI.DrawRect(bgRect, bgColor);

            rect = new Rect(
                cardRect.x + kStepCardPad,
                cardRect.y + kStepCardPad,
                cardRect.width - (kStepCardPad * 2f),
                cardRect.height - (kStepCardPad * 2f)
            );

            var rHeader = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(
                rHeader,
                labelProp != null ? labelProp.stringValue : $"Step {stepIndex + 1}",
                EditorStyles.boldLabel
            );
            var dividerRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 2f, rect.width, 1f);
            Color dividerColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.08f)
                : new Color(0f, 0f, 0f, 0.08f);
            EditorGUI.DrawRect(dividerRect, dividerColor);
            rect.y += EditorGUIUtility.singleLineHeight + kPad;

            if (modeProp == null)
            {
                EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, 36f),
                    "Runtime script missing GazeStep.mode. Update SnippetsGazeFlowController.cs.",
                    MessageType.Error);
                return;
            }

            // Timing dropdown
            var rMode = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            Rect modeCtrl = EditorGUI.PrefixLabel(rMode, new GUIContent("Style"));
            var cur = (SnippetsGazeFlowController.StepGazeMode)modeProp.enumValueIndex;
            int curIndex = Mathf.Clamp((int)cur, 0, StepModeOptions.Length - 1);
            int nextIndex = EditorGUI.Popup(modeCtrl, curIndex, StepModeOptions);
            var nxt = (SnippetsGazeFlowController.StepGazeMode)nextIndex;
            modeProp.enumValueIndex = (int)nxt;
            rect.y += EditorGUIUtility.singleLineHeight + kPad;

            if (nxt == SnippetsGazeFlowController.StepGazeMode.Simple)
            {
                var rHelp = new Rect(rect.x, rect.y, rect.width, 34f);
                EditorGUI.HelpBox(rHelp, "Whole Step keeps the same gaze setup for the full flow step.", MessageType.None);
                rect.y += 34f + 4f;

                var list = GetOrCreateSimpleOverridesList(stepIndex, overridesProp);
                var listRect = new Rect(rect.x, rect.y, rect.width, list.GetHeight());
                list.DoList(listRect);
            }
            else
            {
                var rHelp = new Rect(rect.x, rect.y, rect.width, 34f);
                EditorGUI.HelpBox(rHelp, "Granular lets you change gaze at specific times within the step.", MessageType.None);
                rect.y += 34f + 4f;

                if (cuesProp != null && cuesProp.arraySize > 0)
                {
                    var rSeg = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(rSeg, BuildActiveRangesPreview(cuesProp), MiniLabel);
                    rect.y += EditorGUIUtility.singleLineHeight + 4f;
                }

                var cueList = GetOrCreateCueList(stepIndex, cuesProp);
                var cueRect = new Rect(rect.x, rect.y, rect.width, cueList.GetHeight());
                cueList.DoList(cueRect);
            }
        };

        return _stepsList;
    }

    // ---------------- Simple overrides ----------------

    ReorderableList GetOrCreateSimpleOverridesList(int stepIndex, SerializedProperty overridesProp)
    {
        overridesProp ??= DummyListProp();

        if (_simpleOverrideLists.TryGetValue(stepIndex, out var list) && list != null)
            return list;

        list = new ReorderableList(serializedObject, overridesProp, true, true, true, true);
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Gaze For Entire Step");

        list.elementHeightCallback = idx => CalcActorGazeHeightSafe(overridesProp, idx);
        list.drawElementCallback = (r, idx, a, f) => DrawActorGazeSafe(r, overridesProp, idx);

        list.onAddCallback = l =>
        {
            int idx = overridesProp.arraySize;
            overridesProp.InsertArrayElementAtIndex(idx);
            InitActorGazeDefaults(overridesProp.GetArrayElementAtIndex(idx));
            serializedObject.ApplyModifiedProperties();
        };

        _simpleOverrideLists[stepIndex] = list;
        return list;
    }

    // ---------------- Timed cues ----------------

    ReorderableList GetOrCreateCueList(int stepIndex, SerializedProperty cuesProp)
    {
        cuesProp ??= DummyListProp();

        if (_cueLists.TryGetValue(stepIndex, out var list) && list != null)
            return list;

        list = new ReorderableList(serializedObject, cuesProp, true, true, true, true);
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Granular Changes");

        list.elementHeightCallback = cueIndex =>
        {
            var cueEl = cuesProp.GetArrayElementAtIndex(cueIndex);
            if (cueEl == null) return EditorGUIUtility.singleLineHeight * 2f;

            var overridesProp = cueEl.FindPropertyRelative("overrides");

            float h = 0f;

            // Cue header line
            h += EditorGUIUtility.singleLineHeight + 2f;

            h += EditorGUIUtility.singleLineHeight; // label
            h += EditorGUIUtility.standardVerticalSpacing + 2f;
            h += EditorGUIUtility.singleLineHeight; // percent

            var ovList = GetOrCreateCueOverridesList(stepIndex, cueIndex, overridesProp);
            h += 6f + ovList.GetHeight() + 6f;
            return h;
        };

        list.drawElementCallback = (rect, cueIndex, isActive, isFocused) =>
        {
            var cueEl = cuesProp.GetArrayElementAtIndex(cueIndex);
            if (cueEl == null) return;

            var labelProp = cueEl.FindPropertyRelative("label");
            var percentProp = cueEl.FindPropertyRelative("percent");
            var overridesProp = cueEl.FindPropertyRelative("overrides");

            rect.y += 2f;

            float myP = 0f;
            if (percentProp != null) myP = Mathf.Clamp01(percentProp.floatValue);

            float nextP = FindNextHigherPercentSafe(cuesProp, myP);

            string holdText = (nextP < 0f)
                ? $"Cue @ {(myP * 100f):0.#}% — holds until end"
                : $"Cue @ {(myP * 100f):0.#}% — holds until {(nextP * 100f):0.#}%";

            holdText = BuildCueDurationText(myP, nextP);

            // IMPORTANT: use our safe MiniBoldLabel (no EditorStyles access required)
            var rH = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rH, holdText, MiniBoldLabel);
            rect.y += EditorGUIUtility.singleLineHeight + 2f;

            var r0 = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            if (labelProp != null) EditorGUI.PropertyField(r0, labelProp, new GUIContent("Label"));
            else EditorGUI.LabelField(r0, "Label");
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 2f;

            var r1 = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            if (percentProp != null)
                percentProp.floatValue = EditorGUI.Slider(r1, "Start Time (%)", myP, 0f, 1f);
            else
                EditorGUI.LabelField(r1, "Start Time (%)");
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 4f;

            var ovList = GetOrCreateCueOverridesList(stepIndex, cueIndex, overridesProp);
            var ovRect = new Rect(rect.x, rect.y, rect.width, ovList.GetHeight());
            ovList.DoList(ovRect);
        };

        list.onAddCallback = l =>
        {
            int idx = cuesProp.arraySize;
            cuesProp.InsertArrayElementAtIndex(idx);

            var cueEl = cuesProp.GetArrayElementAtIndex(idx);
            if (cueEl != null)
            {
                cueEl.FindPropertyRelative("label")?.SetStringSafe("");
                cueEl.FindPropertyRelative("percent")?.SetFloatSafe(0f);

                var ov = cueEl.FindPropertyRelative("overrides");
                if (ov != null) ov.arraySize = 0;
            }

            _cueOverrideLists.Clear();
            serializedObject.ApplyModifiedProperties();
        };

        _cueLists[stepIndex] = list;
        return list;
    }

    ReorderableList GetOrCreateCueOverridesList(int stepIndex, int cueIndex, SerializedProperty overridesProp)
    {
        overridesProp ??= DummyListProp();

        string key = $"{stepIndex}:{cueIndex}";
        if (_cueOverrideLists.TryGetValue(key, out var list) && list != null)
            return list;

        list = new ReorderableList(serializedObject, overridesProp, true, true, true, true);
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Changes Here");

        list.elementHeightCallback = idx => CalcActorGazeHeightSafe(overridesProp, idx);
        list.drawElementCallback = (r, idx, a, f) => DrawActorGazeSafe(r, overridesProp, idx);

        list.onAddCallback = l =>
        {
            int idx = overridesProp.arraySize;
            overridesProp.InsertArrayElementAtIndex(idx);
            InitActorGazeDefaults(overridesProp.GetArrayElementAtIndex(idx));
            serializedObject.ApplyModifiedProperties();
        };

        _cueOverrideLists[key] = list;
        return list;
    }

    // ---------------- ActorGaze drawer ----------------

    float CalcActorGazeHeightSafe(SerializedProperty listProp, int idx)
    {
        if (listProp == null || idx < 0 || idx >= listProp.arraySize) return EditorGUIUtility.singleLineHeight;
        var el = listProp.GetArrayElementAtIndex(idx);
        if (el == null) return EditorGUIUtility.singleLineHeight;

        var targetTypeProp = el.FindPropertyRelative("targetType");
        float h = 0f;

        h += EditorGUIUtility.singleLineHeight;
        h += EditorGUIUtility.standardVerticalSpacing + 2f;
        h += EditorGUIUtility.singleLineHeight;

        if (targetTypeProp == null)
            return h + 6f;

        var tt = (SnippetsGazeFlowController.TargetType)targetTypeProp.enumValueIndex;

        if (tt == SnippetsGazeFlowController.TargetType.Transform)
        {
            h += EditorGUIUtility.standardVerticalSpacing + 2f;
            h += EditorGUIUtility.singleLineHeight;
        }
        else if (tt == SnippetsGazeFlowController.TargetType.Actor)
        {
            h += EditorGUIUtility.standardVerticalSpacing + 2f;
            h += EditorGUIUtility.singleLineHeight;
        }
        else if (tt == SnippetsGazeFlowController.TargetType.Forward)
        {
            h += EditorGUIUtility.standardVerticalSpacing + 2f;
            h += EditorGUIUtility.singleLineHeight;
        }
        else
        {
            h += EditorGUIUtility.standardVerticalSpacing + 2f;
            h += EditorGUIUtility.singleLineHeight;
        }

        h += 6f;
        return h;
    }

    void DrawActorGazeSafe(Rect rect, SerializedProperty listProp, int idx)
    {
        if (listProp == null || idx < 0 || idx >= listProp.arraySize) return;
        var el = listProp.GetArrayElementAtIndex(idx);
        if (el == null) return;

        DrawActorGaze(rect, el);
    }

    void DrawActorGaze(Rect rect, SerializedProperty actorGazeEl)
    {
        var controller = (SnippetsGazeFlowController)target;
        var regObj = controller.GetResolvedRegistry();

        var actorIndexProp = actorGazeEl.FindPropertyRelative("actorIndex");
        var targetTypeProp = actorGazeEl.FindPropertyRelative("targetType");

        var targetTransformProp = actorGazeEl.FindPropertyRelative("targetTransform");
        var targetActorIndexProp = actorGazeEl.FindPropertyRelative("targetActorIndex");

        var forwardOverrideProp = actorGazeEl.FindPropertyRelative("forwardTargetOverride");

        rect.y += 2f;

        float prevLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = Mathf.Min(155f, rect.width * 0.42f);

        var rActor = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
        DrawActorPopupNoOverlap(rActor, regObj, actorIndexProp, "Actor");
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 2f;

        var rTT = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
        if (targetTypeProp != null)
        {
            var tt = (SnippetsGazeFlowController.TargetType)targetTypeProp.enumValueIndex;
            int ttIndex = Mathf.Clamp((int)tt, 0, TargetTypeOptions.Length - 1);
            ttIndex = EditorGUI.Popup(rTT, "Look At", ttIndex, TargetTypeOptions);
            tt = (SnippetsGazeFlowController.TargetType)ttIndex;
            targetTypeProp.enumValueIndex = (int)tt;
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 2f;

            if (tt == SnippetsGazeFlowController.TargetType.Transform)
            {
                var rT = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                if (targetTransformProp != null)
                    targetTransformProp.objectReferenceValue = EditorGUI.ObjectField(rT, "Object", targetTransformProp.objectReferenceValue, typeof(Transform), true);
            }
            else if (tt == SnippetsGazeFlowController.TargetType.Actor)
            {
                var rTA = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                DrawActorPopupNoOverlap(rTA, regObj, targetActorIndexProp, "Actor");
            }
            else if (tt == SnippetsGazeFlowController.TargetType.Forward)
            {
                var rInfo = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(rInfo, "Uses this actor's Gaze Driver look-forward settings.", MiniLabel);
            }
            else if (tt == SnippetsGazeFlowController.TargetType.MainCamera)
            {
                var rInfo = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(rInfo, "Uses the main camera as the target.", MiniLabel);
            }
            else
            {
                var rInfo = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(rInfo, "Turns gaze off for this actor.", MiniLabel);
            }
        }
        else
        {
            EditorGUI.LabelField(rTT, "Target Type (missing field on runtime ActorGaze)");
        }

        EditorGUIUtility.labelWidth = prevLabelWidth;
    }

    void InitActorGazeDefaults(SerializedProperty actorGazeEl)
    {
        actorGazeEl.FindPropertyRelative("actorIndex")?.SetIntSafe(0);
        actorGazeEl.FindPropertyRelative("targetType")?.SetEnumSafe((int)SnippetsGazeFlowController.TargetType.Transform);

        var tt = actorGazeEl.FindPropertyRelative("targetTransform");
        if (tt != null) tt.objectReferenceValue = null;

        actorGazeEl.FindPropertyRelative("targetActorIndex")?.SetIntSafe(0);
        actorGazeEl.FindPropertyRelative("preferTargetActorHeadBone")?.SetBoolSafe(true);

        var fo = actorGazeEl.FindPropertyRelative("forwardTargetOverride");
        if (fo != null) fo.objectReferenceValue = null;
    }

    void DrawActorPopupNoOverlap(Rect rect, SnippetsActorRegistry reg, SerializedProperty actorIndexProp, string label)
    {
        int current = actorIndexProp != null ? actorIndexProp.intValue : 0;
        Rect controlRect = EditorGUI.PrefixLabel(rect, new GUIContent(label));

        if (actorIndexProp == null)
        {
            EditorGUI.IntField(controlRect, current);
            return;
        }

        if (reg == null || reg.ActorCount <= 0)
        {
            actorIndexProp.intValue = EditorGUI.IntField(controlRect, current);
            return;
        }

        string[] options = GetActorOptions(reg);
        int count = options.Length;
        current = Mathf.Clamp(current, 0, count - 1);

        int next = EditorGUI.Popup(controlRect, current, options);
        actorIndexProp.intValue = next;
    }

    string[] GetActorOptions(SnippetsActorRegistry reg)
    {
        if (reg == null || reg.ActorCount <= 0)
            return Array.Empty<string>();

        int registryId = reg.GetInstanceID();
        int count = reg.ActorCount;

        if (_actorOptionsByRegistryId.TryGetValue(registryId, out var options) && options != null && options.Length == count)
            return options;

        options = new string[count];
        for (int i = 0; i < count; i++)
            options[i] = $"{i}: {reg.GetActorDisplayName(i)}";

        _actorOptionsByRegistryId[registryId] = options;
        return options;
    }

    void DrawUnspecifiedActorsPopup()
    {
        int current = unspecifiedActors != null ? unspecifiedActors.enumValueIndex : 0;
        current = Mathf.Clamp(current, 0, UnspecifiedActorOptions.Length - 1);
        int next = EditorGUILayout.Popup(
            new GUIContent("Default Behavior", "What to do when an actor does not receive a new gaze instruction in the current step or granular change."),
            current,
            UnspecifiedActorOptions
        );
        if (unspecifiedActors != null)
            unspecifiedActors.enumValueIndex = next;
    }

    // ---------------- Helpers for cue UX ----------------

    static float FindNextHigherPercentSafe(SerializedProperty cuesProp, float currentPercent)
    {
        if (cuesProp == null) return -1f;

        float next = float.PositiveInfinity;
        for (int i = 0; i < cuesProp.arraySize; i++)
        {
            var cueEl = cuesProp.GetArrayElementAtIndex(i);
            if (cueEl == null) continue;

            var pProp = cueEl.FindPropertyRelative("percent");
            if (pProp == null) continue;

            float p = Mathf.Clamp01(pProp.floatValue);
            if (p > currentPercent && p < next)
                next = p;
        }
        return float.IsPositiveInfinity(next) ? -1f : next;
    }

    static string BuildCueDurationText(float startPercent, float nextPercent)
    {
        float start = Mathf.Clamp01(startPercent) * 100f;
        if (nextPercent < 0f)
            return $"Starts at {start:0.#}% and lasts until the end";

        float end = Mathf.Clamp01(nextPercent) * 100f;
        return $"Starts at {start:0.#}% and lasts until {end:0.#}%";
    }

    static string BuildActiveRangesPreview(SerializedProperty cuesProp)
    {
        if (cuesProp == null || cuesProp.arraySize == 0)
            return "Active ranges: 0-100 (one range)";

        var bounds = new List<float> { 0f, 1f };

        for (int i = 0; i < cuesProp.arraySize; i++)
        {
            var cueEl = cuesProp.GetArrayElementAtIndex(i);
            if (cueEl == null) continue;

            var pProp = cueEl.FindPropertyRelative("percent");
            if (pProp == null) continue;

            bounds.Add(Mathf.Clamp01(pProp.floatValue));
        }

        bounds.Sort();

        var uniq = new List<float>();
        const float eps = 0.0001f;
        for (int i = 0; i < bounds.Count; i++)
        {
            if (uniq.Count == 0 || Mathf.Abs(bounds[i] - uniq[uniq.Count - 1]) > eps)
                uniq.Add(bounds[i]);
        }

        var ranges = new List<string>();
        for (int i = 0; i < uniq.Count - 1; i++)
        {
            float a = uniq[i];
            float b = uniq[i + 1];
            if (b - a <= eps) continue;
            ranges.Add($"{(a * 100f):0.#}-{(b * 100f):0.#}");
        }

        if (ranges.Count == 0)
            return "Active ranges: none";

        return "Active ranges: " + string.Join(" | ", ranges);
    }

    static string BuildSegmentsPreviewSafe(SerializedProperty cuesProp)
    {
        if (cuesProp == null || cuesProp.arraySize == 0)
            return "Segments: 0–100 (single segment)";

        var bounds = new List<float> { 0f, 1f };

        for (int i = 0; i < cuesProp.arraySize; i++)
        {
            var cueEl = cuesProp.GetArrayElementAtIndex(i);
            if (cueEl == null) continue;

            var pProp = cueEl.FindPropertyRelative("percent");
            if (pProp == null) continue;

            bounds.Add(Mathf.Clamp01(pProp.floatValue));
        }

        bounds.Sort();

        var uniq = new List<float>();
        const float eps = 0.0001f;
        for (int i = 0; i < bounds.Count; i++)
        {
            if (uniq.Count == 0 || Mathf.Abs(bounds[i] - uniq[uniq.Count - 1]) > eps)
                uniq.Add(bounds[i]);
        }

        var segs = new List<string>();
        for (int i = 0; i < uniq.Count - 1; i++)
        {
            float a = uniq[i];
            float b = uniq[i + 1];
            if (b - a <= eps) continue;
            segs.Add($"{(a * 100f):0.#}–{(b * 100f):0.#}");
        }

        if (segs.Count == 0)
            return "Segments: (none)";

        return "Segments: " + string.Join(" | ", segs);
    }

    // ------------- dummy list property helper (safety) -------------

    SerializedProperty DummyListProp()
    {
        // We never actually use this if runtime matches; this avoids null crashes.
        return serializedObject.FindProperty("gazeSteps");
    }
}

// Small extension helpers to avoid repeated null checks
static class SerializedPropertySafeSetExtensions
{
    public static void SetStringSafe(this SerializedProperty p, string v) { if (p != null) p.stringValue = v; }
    public static void SetFloatSafe(this SerializedProperty p, float v) { if (p != null) p.floatValue = v; }
    public static void SetIntSafe(this SerializedProperty p, int v) { if (p != null) p.intValue = v; }
    public static void SetBoolSafe(this SerializedProperty p, bool v) { if (p != null) p.boolValue = v; }
    public static void SetEnumSafe(this SerializedProperty p, int v) { if (p != null) p.enumValueIndex = v; }
}
#endif
