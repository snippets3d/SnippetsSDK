#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SnippetsGazeFlowController))]
public class SnippetsGazeFlowControllerEditor : Editor
{
    SerializedProperty flow;
    SerializedProperty registry;
    SerializedProperty gazeSteps;
    SerializedProperty unspecifiedActors;
    SerializedProperty autoSyncToFlowSteps;
    SerializedProperty autoLabelFromFlow;

    ReorderableList _stepsList;

    readonly Dictionary<int, ReorderableList> _simpleOverrideLists = new();
    readonly Dictionary<int, ReorderableList> _cueLists = new();
    readonly Dictionary<string, ReorderableList> _cueOverrideLists = new();

    GUIStyle _headerStyle;
    GUIStyle _miniLabel;
    GUIStyle _miniBoldLabel;

    const float kPad = 6f;

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
        registry = serializedObject.FindProperty("registry");
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

        EditorGUILayout.LabelField("Links", HeaderStyle);
        EditorGUILayout.PropertyField(flow);
        EditorGUILayout.PropertyField(registry);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Defaults", HeaderStyle);
        EditorGUILayout.PropertyField(unspecifiedActors);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Authoring Helpers", HeaderStyle);
        EditorGUILayout.PropertyField(autoSyncToFlowSteps);
        EditorGUILayout.PropertyField(autoLabelFromFlow);

        EditorGUILayout.Space(8);
        DrawSyncBlock();

        EditorGUILayout.Space(10);
        _stepsList ??= BuildStepsList();
        _stepsList?.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawSyncBlock()
    {
        var controller = (SnippetsGazeFlowController)target;
        var flowObj = controller.flow;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Step Sync", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Gaze Steps are indexed exactly like Flow steps.", MiniLabel);

        using (new EditorGUI.DisabledScope(flowObj == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sync Now (Resize + Relabel)"))
            {
                controller.SyncNow();
                EditorUtility.SetDirty(controller);
                if (controller.flow != null) EditorUtility.SetDirty(controller.flow);
                serializedObject.Update();

                _simpleOverrideLists.Clear();
                _cueLists.Clear();
                _cueOverrideLists.Clear();

                BuildStepsList();
            }

            if (GUILayout.Button("Relabel Only"))
            {
                controller.RelabelFromFlow();

                EditorUtility.SetDirty(controller);
                serializedObject.Update();

                BuildStepsList();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    ReorderableList BuildStepsList()
    {
        if (gazeSteps == null)
            return _stepsList;

        _stepsList = new ReorderableList(serializedObject, gazeSteps, false, true, false, false);

        _stepsList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Gaze Steps (mirrors Flow step index)");
        };

        _stepsList.elementHeightCallback = stepIndex =>
        {
            var stepEl = gazeSteps.GetArrayElementAtIndex(stepIndex);
            if (stepEl == null) return EditorGUIUtility.singleLineHeight * 2f;

            var modeProp = stepEl.FindPropertyRelative("mode");
            var overridesProp = stepEl.FindPropertyRelative("overrides");
            var cuesProp = stepEl.FindPropertyRelative("cues");

            float h = 0f;
            h += EditorGUIUtility.singleLineHeight + kPad; // header
            h += EditorGUIUtility.singleLineHeight + kPad; // mode row

            if (modeProp == null)
                return h + EditorGUIUtility.singleLineHeight * 2f;

            var mode = (SnippetsGazeFlowController.StepGazeMode)modeProp.enumValueIndex;

            if (mode == SnippetsGazeFlowController.StepGazeMode.Simple)
            {
                var list = GetOrCreateSimpleOverridesList(stepIndex, overridesProp);
                h += list.GetHeight() + kPad;
            }
            else
            {
                // help box + segments preview
                h += 54f + 4f;
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

            rect.y += 2f;

            var rHeader = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(
                rHeader,
                labelProp != null ? labelProp.stringValue : $"Step {stepIndex + 1}",
                EditorStyles.boldLabel
            );
            rect.y += EditorGUIUtility.singleLineHeight + kPad;

            if (modeProp == null)
            {
                EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, 36f),
                    "Runtime script missing GazeStep.mode (Simple/Granular). Update SnippetsGazeFlowController.cs.",
                    MessageType.Error);
                return;
            }

            // Mode dropdown (manual so always visible)
            var rMode = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            Rect modeCtrl = EditorGUI.PrefixLabel(rMode, new GUIContent("Mode"));
            var cur = (SnippetsGazeFlowController.StepGazeMode)modeProp.enumValueIndex;
            var nxt = (SnippetsGazeFlowController.StepGazeMode)EditorGUI.EnumPopup(modeCtrl, cur);
            modeProp.enumValueIndex = (int)nxt;
            rect.y += EditorGUIUtility.singleLineHeight + kPad;

            if (nxt == SnippetsGazeFlowController.StepGazeMode.Simple)
            {
                var list = GetOrCreateSimpleOverridesList(stepIndex, overridesProp);
                var listRect = new Rect(rect.x, rect.y, rect.width, list.GetHeight());
                list.DoList(listRect);
            }
            else
            {
                // (1) How cues work
                var rHelp = new Rect(rect.x, rect.y, rect.width, 54f);
                EditorGUI.HelpBox(
                    rHelp,
                    "Cues are change points (like keyframes). At each % the listed overrides are applied and stay active until the next cue changes them.\n" +
                    "To create segments: add cues at boundaries (e.g. 0%, 30%, 50%). Use 'Unspecified Actors = Keep Previous' if you only change one actor per cue.",
                    MessageType.Info
                );
                rect.y += 54f + 4f;

                // (3) Segments preview
                var rSeg = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(rSeg, BuildSegmentsPreviewSafe(cuesProp), MiniLabel);
                rect.y += EditorGUIUtility.singleLineHeight + 4f;

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
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Simple Overrides (applied at step start)");

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

    // ---------------- Granular cues ----------------

    ReorderableList GetOrCreateCueList(int stepIndex, SerializedProperty cuesProp)
    {
        cuesProp ??= DummyListProp();

        if (_cueLists.TryGetValue(stepIndex, out var list) && list != null)
            return list;

        list = new ReorderableList(serializedObject, cuesProp, true, true, true, true);
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Cues (percent of snippet)");

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
            h += EditorGUIUtility.standardVerticalSpacing + 2f;
            h += EditorGUIUtility.singleLineHeight; // blend

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
            var blendProp = cueEl.FindPropertyRelative("blendSeconds");
            var overridesProp = cueEl.FindPropertyRelative("overrides");

            rect.y += 2f;

            float myP = 0f;
            if (percentProp != null) myP = Mathf.Clamp01(percentProp.floatValue);

            float nextP = FindNextHigherPercentSafe(cuesProp, myP);

            string holdText = (nextP < 0f)
                ? $"Cue @ {(myP * 100f):0.#}% — holds until end"
                : $"Cue @ {(myP * 100f):0.#}% — holds until {(nextP * 100f):0.#}%";

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
                percentProp.floatValue = EditorGUI.Slider(r1, "Percent", myP, 0f, 1f);
            else
                EditorGUI.LabelField(r1, "Percent (missing field on runtime cue)");
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 2f;

            var r2 = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            if (blendProp != null) EditorGUI.PropertyField(r2, blendProp, new GUIContent("Blend (s)"));
            else EditorGUI.LabelField(r2, "Blend (missing field on runtime cue)");
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
                cueEl.FindPropertyRelative("blendSeconds")?.SetFloatSafe(0.25f);

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
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Cue Overrides");

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
        var regObj = controller.registry;

        var actorIndexProp = actorGazeEl.FindPropertyRelative("actorIndex");
        var targetTypeProp = actorGazeEl.FindPropertyRelative("targetType");

        var targetTransformProp = actorGazeEl.FindPropertyRelative("targetTransform");
        var targetActorIndexProp = actorGazeEl.FindPropertyRelative("targetActorIndex");
        var preferHeadProp = actorGazeEl.FindPropertyRelative("preferTargetActorHeadBone");

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
            tt = (SnippetsGazeFlowController.TargetType)EditorGUI.EnumPopup(rTT, "Target Type", tt);
            targetTypeProp.enumValueIndex = (int)tt;
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 2f;

            if (tt == SnippetsGazeFlowController.TargetType.Transform)
            {
                var rT = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                if (targetTransformProp != null)
                    targetTransformProp.objectReferenceValue = EditorGUI.ObjectField(rT, "Target", targetTransformProp.objectReferenceValue, typeof(Transform), true);
            }
            else if (tt == SnippetsGazeFlowController.TargetType.Actor)
            {
                var rTA = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                DrawActorPopupNoOverlap(rTA, regObj, targetActorIndexProp, "Target Actor");
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 2f;

                var rPref = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                if (preferHeadProp != null) preferHeadProp.boolValue = EditorGUI.ToggleLeft(rPref, "Prefer Target Actor Head Bone", preferHeadProp.boolValue);
            }
            else if (tt == SnippetsGazeFlowController.TargetType.Forward)
            {
                var rO = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                if (forwardOverrideProp != null)
                    forwardOverrideProp.objectReferenceValue = EditorGUI.ObjectField(rO, "Forward Target", forwardOverrideProp.objectReferenceValue, typeof(Transform), true);
            }
            else if (tt == SnippetsGazeFlowController.TargetType.MainCamera)
            {
                var rInfo = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(rInfo, "Uses Camera.main (FollowTarget).", EditorStyles.miniLabel);
            }
            else
            {
                var rInfo = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(rInfo, "Disables gaze (sets HeadTurn mode Off).", EditorStyles.miniLabel);
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

        int count = reg.ActorCount;
        current = Mathf.Clamp(current, 0, count - 1);

        string[] options = new string[count];
        for (int i = 0; i < count; i++)
            options[i] = $"{i}: {reg.GetActorDisplayName(i)}";

        int next = EditorGUI.Popup(controlRect, current, options);
        actorIndexProp.intValue = next;
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
