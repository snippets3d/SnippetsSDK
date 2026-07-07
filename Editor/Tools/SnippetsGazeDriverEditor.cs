#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SnippetsGazeDriver))]
public class SnippetsGazeDriverEditor : Editor
{
    static readonly string[] GazeTargetOptions = { "Turn Off", "Object", "Main Camera", "Actor", "Look Forward" };
    const float EyeSaccadeIntensitySliderMax = 10f;
    bool _showEyeAdvanced;
    bool _showHeadAdvanced;
    bool _showWaistAdvanced;
    bool _showDebugAdvanced;
    bool _showTargetAdvanced;

    SerializedProperty modeProp;
    SerializedProperty targetTypeProp;

    // Follow Target
    SerializedProperty targetProp;
    SerializedProperty autoFindTargetProp;
    SerializedProperty targetActorProp;
    SerializedProperty preferTargetActorHeadBoneProp;
    SerializedProperty periodicallySwitchTargetActorEyesProp;
    SerializedProperty targetActorEyeSwitchIntervalRangeProp;

    // Target smoothing
    SerializedProperty smoothTarget, targetFollowSpeed;

    // Rig
    SerializedProperty headBone, waistBone;

    // Eyes
    SerializedProperty enableDynamicEyeFollow, leftEyeBone, rightEyeBone, eyeForwardAxis;
    SerializedProperty eyeWeight, eyeBlendSpeed, eyeRotationSpeed, eyeMaxYaw, eyeMaxInwardYaw, eyeMaxPitch;
    SerializedProperty enableEyeSaccades, eyeSaccadeIntensity;
    SerializedProperty eyeMicroSaccadeIntervalRange, eyeMicroSaccadeAngleRange, eyeFixationDriftAmplitude, eyeFixationDriftFrequency;
    SerializedProperty rpmCrossEyeCorrection, rpmCrossEyePreset;
    SerializedProperty antiCrossEyeOutwardYawOffset;
    SerializedProperty leftEyeCorrectionOriginOffset, rightEyeCorrectionOriginOffset, leftEyeCorrectionAngleOffset, rightEyeCorrectionAngleOffset;
    SerializedProperty enableLookDownEyelidFollow, faceMesh, eyelidBlendshape, useMultipleSharedEyelidBlendshapes, sharedEyelidBlendshapes, leftEyelidBlendshape, rightEyelidBlendshape;
    SerializedProperty eyelidLookDownStartAngle, eyelidLookDownFullAngle, eyelidLookDownMaxAdd, eyelidLookDownFollowSpeed;
    SerializedProperty debugDrawEyeGizmos, debugEyeGizmoLength;

    // Look
    SerializedProperty lookWeight, headAimPrecision, blendSpeed, rotationSpeed, maxYaw, maxPitch, lookOffset, normalizeTargetHeight;

    // Look in front
    SerializedProperty fixedTargetName, fixedTargetOverride;

    // Refresh
    SerializedProperty autoRefreshTargets, refreshTargetsInterval;

    // Waist
    SerializedProperty waistYawWeight, waistHeadYawThreshold, waistDelay;
    SerializedProperty waistYawThresholdHysteresis, waistRearFlipGuardDegrees, waistEngageSpeed, waistMaxYaw, waistRotationSpeed;

    void OnEnable()
    {
        modeProp = serializedObject.FindProperty("mode");
        targetTypeProp = serializedObject.FindProperty("targetType");

        targetProp = serializedObject.FindProperty("target");
        autoFindTargetProp = serializedObject.FindProperty("autoFindTarget");
        targetActorProp = serializedObject.FindProperty("targetActor");
        preferTargetActorHeadBoneProp = serializedObject.FindProperty("preferTargetActorHeadBone");
        periodicallySwitchTargetActorEyesProp = serializedObject.FindProperty("periodicallySwitchTargetActorEyes");
        targetActorEyeSwitchIntervalRangeProp = serializedObject.FindProperty("targetActorEyeSwitchIntervalRange");
        smoothTarget = serializedObject.FindProperty("smoothTarget");
        targetFollowSpeed = serializedObject.FindProperty("targetFollowSpeed");

        headBone = serializedObject.FindProperty("headBone");
        waistBone = serializedObject.FindProperty("waistBone");

        enableDynamicEyeFollow = serializedObject.FindProperty("enableDynamicEyeFollow");
        leftEyeBone = serializedObject.FindProperty("leftEyeBone");
        rightEyeBone = serializedObject.FindProperty("rightEyeBone");
        eyeForwardAxis = serializedObject.FindProperty("eyeForwardAxis");
        eyeWeight = serializedObject.FindProperty("eyeWeight");
        eyeBlendSpeed = serializedObject.FindProperty("eyeBlendSpeed");
        eyeRotationSpeed = serializedObject.FindProperty("eyeRotationSpeed");
        eyeMaxYaw = serializedObject.FindProperty("eyeMaxYaw");
        eyeMaxInwardYaw = serializedObject.FindProperty("eyeMaxInwardYaw");
        eyeMaxPitch = serializedObject.FindProperty("eyeMaxPitch");
        enableEyeSaccades = serializedObject.FindProperty("enableEyeSaccades");
        eyeSaccadeIntensity = serializedObject.FindProperty("eyeSaccadeIntensity");
        eyeMicroSaccadeIntervalRange = serializedObject.FindProperty("eyeMicroSaccadeIntervalRange");
        eyeMicroSaccadeAngleRange = serializedObject.FindProperty("eyeMicroSaccadeAngleRange");
        eyeFixationDriftAmplitude = serializedObject.FindProperty("eyeFixationDriftAmplitude");
        eyeFixationDriftFrequency = serializedObject.FindProperty("eyeFixationDriftFrequency");
        rpmCrossEyeCorrection = serializedObject.FindProperty("rpmCrossEyeCorrection");
        rpmCrossEyePreset = serializedObject.FindProperty("rpmCrossEyePreset");
        antiCrossEyeOutwardYawOffset = serializedObject.FindProperty("antiCrossEyeOutwardYawOffset");
        leftEyeCorrectionOriginOffset = serializedObject.FindProperty("leftEyeCorrectionOriginOffset");
        rightEyeCorrectionOriginOffset = serializedObject.FindProperty("rightEyeCorrectionOriginOffset");
        leftEyeCorrectionAngleOffset = serializedObject.FindProperty("leftEyeCorrectionAngleOffset");
        rightEyeCorrectionAngleOffset = serializedObject.FindProperty("rightEyeCorrectionAngleOffset");
        enableLookDownEyelidFollow = serializedObject.FindProperty("enableLookDownEyelidFollow");
        faceMesh = serializedObject.FindProperty("faceMesh");
        eyelidBlendshape = serializedObject.FindProperty("eyelidBlendshape");
        useMultipleSharedEyelidBlendshapes = serializedObject.FindProperty("useMultipleSharedEyelidBlendshapes");
        sharedEyelidBlendshapes = serializedObject.FindProperty("sharedEyelidBlendshapes");
        leftEyelidBlendshape = serializedObject.FindProperty("leftEyelidBlendshape");
        rightEyelidBlendshape = serializedObject.FindProperty("rightEyelidBlendshape");
        eyelidLookDownStartAngle = serializedObject.FindProperty("eyelidLookDownStartAngle");
        eyelidLookDownFullAngle = serializedObject.FindProperty("eyelidLookDownFullAngle");
        eyelidLookDownMaxAdd = serializedObject.FindProperty("eyelidLookDownMaxAdd");
        eyelidLookDownFollowSpeed = serializedObject.FindProperty("eyelidLookDownFollowSpeed");
        debugDrawEyeGizmos = serializedObject.FindProperty("debugDrawEyeGizmos");
        debugEyeGizmoLength = serializedObject.FindProperty("debugEyeGizmoLength");

        lookWeight = serializedObject.FindProperty("lookWeight");
        headAimPrecision = serializedObject.FindProperty("headAimPrecision");
        blendSpeed = serializedObject.FindProperty("blendSpeed");
        rotationSpeed = serializedObject.FindProperty("rotationSpeed");
        maxYaw = serializedObject.FindProperty("maxYaw");
        maxPitch = serializedObject.FindProperty("maxPitch");
        lookOffset = serializedObject.FindProperty("lookOffset");
        normalizeTargetHeight = serializedObject.FindProperty("normalizeTargetHeight");

        fixedTargetName = serializedObject.FindProperty("fixedTargetName");
        fixedTargetOverride = serializedObject.FindProperty("fixedTargetOverride");

        autoRefreshTargets = serializedObject.FindProperty("autoRefreshTargets");
        refreshTargetsInterval = serializedObject.FindProperty("refreshTargetsInterval");

        waistYawWeight = serializedObject.FindProperty("waistYawWeight");
        waistHeadYawThreshold = serializedObject.FindProperty("waistHeadYawThreshold");
        waistYawThresholdHysteresis = serializedObject.FindProperty("waistYawThresholdHysteresis");
        waistRearFlipGuardDegrees = serializedObject.FindProperty("waistRearFlipGuardDegrees");
        waistDelay = serializedObject.FindProperty("waistDelay");
        waistEngageSpeed = serializedObject.FindProperty("waistEngageSpeed");
        waistMaxYaw = serializedObject.FindProperty("waistMaxYaw");
        waistRotationSpeed = serializedObject.FindProperty("waistRotationSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawGazeSection();
        DrawRigSection();
        DrawHeadSection();
        DrawEyeSection();
        DrawWaistSection();
        DrawDebugSection();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawGazeSection()
    {
        BeginSection("Gaze");
        int currentOption = GetCurrentGazeTargetOption();
        int nextOption = EditorGUILayout.Popup(
            new GUIContent("Mode", "Choose how this character should look: object, main camera, actor, look forward, or off."),
            currentOption,
            GazeTargetOptions
        );
        ApplyGazeTargetOption(nextOption);
        EditorGUILayout.Space(4);

        switch (nextOption)
        {
            case 1:
                DrawProperty(targetProp, "Object", "The object this character should look at.");
                break;
            case 2:
                EditorGUILayout.HelpBox("Main Camera resolves to the active camera automatically.", MessageType.None);
                break;
            case 3:
                DrawProperty(targetActorProp, "Actor", "Assign a transform on the target character. If that character has a SnippetsGazeDriver, its head and eye anchors will be used.");
                if (targetActorProp != null && targetActorProp.objectReferenceValue != null)
                {
                    DrawProperty(preferTargetActorHeadBoneProp, "Prefer Head Anchor", "When enabled, the head aims at the target actor's head or eye midpoint instead of the root transform.");
                    DrawProperty(periodicallySwitchTargetActorEyesProp, "Switch Target Eyes", "When enabled, dynamic eye follow periodically alternates fixation between the target actor's eyes.");

                    using (new EditorGUI.DisabledScope(!periodicallySwitchTargetActorEyesProp.boolValue))
                    {
                        DrawProperty(targetActorEyeSwitchIntervalRangeProp, "Switch Interval", "Random interval range in seconds between switches from one eye to the other.");
                    }

                    if (periodicallySwitchTargetActorEyesProp.boolValue && !enableDynamicEyeFollow.boolValue)
                        EditorGUILayout.HelpBox("Enable Eye Follow in the Eyes section for target-eye switching to affect the source actor's eyes.", MessageType.None);
                }
                break;
            case 4:
                EditorGUILayout.HelpBox("Look Forward uses a fixed forward target. You can optionally point it at a named or assigned helper target below.", MessageType.None);
                break;
            default:
                EditorGUILayout.HelpBox("Gaze is Off, so the character will keep its current animation without a look target.", MessageType.Info);
                break;
        }

        _showTargetAdvanced = EditorGUILayout.Foldout(_showTargetAdvanced, "Advanced Target Options", true);
        if (_showTargetAdvanced)
        {
            if (nextOption == 4)
            {
                DrawProperty(fixedTargetOverride, "Forward Target Override", "Optional explicit target for Look Forward. If left empty, the driver uses the named target or a virtual forward point.");
                DrawProperty(fixedTargetName, "Named Forward Target", "Optional scene object name used for Look Forward when no override is assigned.");
                DrawProperty(autoRefreshTargets, "Refresh Named Target", "Re-checks the scene for the named forward target at an interval.");

                using (new EditorGUI.DisabledScope(!autoRefreshTargets.boolValue))
                {
                    DrawProperty(refreshTargetsInterval, "Refresh Interval", "How often the named forward target is refreshed.");
                }
            }
            else if (nextOption != 0)
            {
                DrawProperty(smoothTarget, "Smooth Target", "Smooths sudden target movement so the gaze does not snap immediately.");

                using (new EditorGUI.DisabledScope(!smoothTarget.boolValue))
                {
                    DrawProperty(targetFollowSpeed, "Target Smooth Speed", "How quickly the gaze catches up to the target after smoothing. Higher values react faster.");
                }
            }
        }

        EndSection();
    }

    int GetCurrentGazeTargetOption()
    {
        var gazeMode = (SnippetsGazeDriver.GazeMode)modeProp.enumValueIndex;
        if (gazeMode == SnippetsGazeDriver.GazeMode.Off)
            return 0;

        if (gazeMode == SnippetsGazeDriver.GazeMode.LookInFront)
            return 4;

        if (targetTypeProp == null)
            return 1;

        if (autoFindTargetProp != null && autoFindTargetProp.boolValue)
            return 2;

        var targetType = (SnippetsGazeDriver.TargetType)targetTypeProp.enumValueIndex;
        return targetType switch
        {
            SnippetsGazeDriver.TargetType.MainCamera => 2,
            SnippetsGazeDriver.TargetType.Actor => 3,
            SnippetsGazeDriver.TargetType.Forward => 4,
            _ => 1,
        };
    }

    void ApplyGazeTargetOption(int option)
    {
        switch (option)
        {
            case 0:
                modeProp.enumValueIndex = (int)SnippetsGazeDriver.GazeMode.Off;
                if (autoFindTargetProp != null) autoFindTargetProp.boolValue = false;
                break;
            case 1:
                modeProp.enumValueIndex = (int)SnippetsGazeDriver.GazeMode.FollowTarget;
                targetTypeProp?.SetEnumSafe((int)SnippetsGazeDriver.TargetType.Transform);
                if (autoFindTargetProp != null) autoFindTargetProp.boolValue = false;
                break;
            case 2:
                modeProp.enumValueIndex = (int)SnippetsGazeDriver.GazeMode.FollowTarget;
                targetTypeProp?.SetEnumSafe((int)SnippetsGazeDriver.TargetType.MainCamera);
                if (autoFindTargetProp != null) autoFindTargetProp.boolValue = true;
                break;
            case 3:
                modeProp.enumValueIndex = (int)SnippetsGazeDriver.GazeMode.FollowTarget;
                targetTypeProp?.SetEnumSafe((int)SnippetsGazeDriver.TargetType.Actor);
                if (autoFindTargetProp != null) autoFindTargetProp.boolValue = false;
                break;
            case 4:
                modeProp.enumValueIndex = (int)SnippetsGazeDriver.GazeMode.LookInFront;
                targetTypeProp?.SetEnumSafe((int)SnippetsGazeDriver.TargetType.Forward);
                if (autoFindTargetProp != null) autoFindTargetProp.boolValue = false;
                break;
        }
    }

    void DrawRigSection()
    {
        BeginSection("Rig");
        if (GUILayout.Button(new GUIContent("Auto Setup", "Try to find the head, waist, eye bones, face mesh, and eyelid blendshapes automatically. This prefers Humanoid Animator bones first, then common naming patterns.")))
        {
            serializedObject.ApplyModifiedProperties();
            AutoSetupSelectedDrivers();
            serializedObject.Update();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.HelpBox("Auto Setup first uses Humanoid Animator bones when available, then falls back to common bone and blendshape names. That way it still works even when different characters use different naming styles.", MessageType.None);
        EditorGUILayout.Space(4);
        DrawProperty(headBone, "Head Bone", "The main head bone that should rotate when the character looks at something.");
        DrawProperty(waistBone, "Waist Bone", "Optional upper-body bone used to turn the waist slightly so it follows the head during wider looks. A spine bone is often a good choice.");
        EndSection();
    }

    void DrawEyeSection()
    {
        BeginSection("Eyes");
        DrawProperty(enableDynamicEyeFollow, "Enable Eye Follow", "Lets the eyes track the current gaze target instead of relying only on the head.");

        using (new EditorGUI.DisabledScope(!enableDynamicEyeFollow.boolValue))
        {
            DrawProperty(leftEyeBone, "Left Eye Bone", "The bone or transform that rotates the left eye.");
            DrawProperty(rightEyeBone, "Right Eye Bone", "The bone or transform that rotates the right eye.");
            DrawProperty(eyeForwardAxis, "Eye Forward Axis", "Choose which axis on the eye bones points out through the pupils.");
            DrawProperty(eyeWeight, "Eye Strength", "How strongly the eyes follow the target. Lower values keep more of the original animation.");
            DrawProperty(eyeRotationSpeed, "Eye Speed", "How quickly the eyes move toward the target.");
            DrawProperty(eyeMaxYaw, "Max Eye Yaw", "Maximum left/right eye rotation in degrees.");
            DrawProperty(eyeMaxPitch, "Max Eye Pitch", "Maximum up/down eye rotation in degrees.");
            DrawProperty(enableEyeSaccades, "Enable Eye Saccades", "Adds small natural eye movements while the eyes stay focused on a target.");

            using (new EditorGUI.DisabledScope(!enableEyeSaccades.boolValue))
            {
                DrawEyeSaccadeIntensityControl();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Cross-Eye Fixes", EditorStyles.miniBoldLabel);
            DrawProperty(rpmCrossEyeCorrection, "Use RPM Eye Fix", "Turns on the built-in RPM eye correction profile.");
            if (rpmCrossEyeCorrection.boolValue)
                DrawProperty(rpmCrossEyePreset, "RPM Profile", "Choose which RPM eye correction profile fits this avatar.");
            DrawProperty(eyeMaxInwardYaw, "Max Cross-Eye", "Limits how far the eyes can turn inward toward the nose.");
            DrawProperty(antiCrossEyeOutwardYawOffset, "Eyes Outward Offset", "Pushes both eyes outward all the time. If RPM eye fix is enabled, that profile is applied first and this offset is added on top.");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Eyelids", EditorStyles.miniBoldLabel);
            DrawProperty(enableLookDownEyelidFollow, "Look-Down Eyelids", "Adds a little extra upper-eyelid close while the eyes are looking downward, on top of existing blinks and facial animation.");

            if (enableLookDownEyelidFollow.boolValue)
            {
                DrawProperty(faceMesh, "Face Mesh", "The face mesh that contains the eyelid or blink blendshape.");
                DrawProperty(useMultipleSharedEyelidBlendshapes, "Multiple Blenshapes Control Eyelids", "Enable this when the same look-down eyelid follow should drive more than one shared blendshape at once.");

                if (useMultipleSharedEyelidBlendshapes.boolValue)
                {
                    EnsureSharedBlendshapeListSeeded();
                    DrawSharedBlendshapeList();
                }
                else
                {
                    DrawBlendshapePopup(faceMesh, eyelidBlendshape, "Shared Eyelid Blendshape", "Choose one blendshape if the same eyelid or blink shape controls both eyes.");
                }

                DrawProperty(eyelidLookDownStartAngle, "Eyelid Start Angle", "How far down the eyes must look before the extra eyelid close starts.");
                DrawProperty(eyelidLookDownFullAngle, "Eyelid Full Angle", "How far down the eyes must look before the extra eyelid close reaches its maximum.");
                DrawProperty(eyelidLookDownMaxAdd, "Max Eyelid Add", "Maximum extra eyelid close as a percentage of the blendshape's authored max. Legacy values up to 1 still work as normalized fractions.");
                DrawProperty(eyelidLookDownFollowSpeed, "Eyelid Follow Speed", "How quickly the eyelids respond when the eyes move down or back up.");
            }

            _showEyeAdvanced = EditorGUILayout.Foldout(_showEyeAdvanced, "Advanced Eye Options", true);
            if (_showEyeAdvanced)
            {
                DrawProperty(eyeBlendSpeed, "Eye Blend Speed", "How quickly eye follow turns on or off.");

                if (enableEyeSaccades.boolValue)
                {
                    DrawProperty(eyeMicroSaccadeAngleRange, "Saccade Angle Range", "Angular size of the micro-saccade jumps before intensity scaling. Increase this if the eye motion is too subtle to notice.");
                    DrawProperty(eyeMicroSaccadeIntervalRange, "Saccade Interval Range", "How often the eyes pick a new micro-saccade target. Shorter intervals feel more active.");
                    DrawProperty(eyeFixationDriftAmplitude, "Fixation Drift Amplitude", "Low-amplitude continuous drift added on top of micro-saccades while fixating.");
                    DrawProperty(eyeFixationDriftFrequency, "Fixation Drift Frequency", "Speed of the continuous fixation drift.");
                }

            }
        }
        EndSection();
    }

    void DrawHeadSection()
    {
        BeginSection("Head");
        DrawProperty(lookWeight, "Head Strength", "How much the head joins in when looking at the target. Lower values keep more of the original head animation. Higher values make the head turn more strongly toward the target.");
        DrawProperty(headAimPrecision, "Animation vs Precision", "When the head is already turning toward the target, this controls how much of the animation's original head direction is preserved. Lower values keep more of the animation. Higher values make the head aim more exactly at the target.");
        DrawProperty(rotationSpeed, "Head Speed", "How quickly the head turns toward the target.");
        DrawProperty(maxYaw, "Max Head Yaw", "Maximum left/right head rotation in degrees.");
        DrawProperty(maxPitch, "Max Head Pitch", "Maximum up/down head rotation in degrees.");

        _showHeadAdvanced = EditorGUILayout.Foldout(_showHeadAdvanced, "Advanced Head Options", true);
        if (_showHeadAdvanced)
        {
            DrawProperty(blendSpeed, "Head Blend Speed", "How quickly head gaze fades in and out when gaze starts or stops.");
            DrawProperty(lookOffset, "Target Offset", "Moves the final aim point by a fixed amount. Useful if the character seems to look slightly too high or too low.");
            DrawProperty(normalizeTargetHeight, "Flatten Vertical Tracking", "Makes the character look more level by treating the target as if it were at roughly head height.");
        }
        EndSection();
    }

    void DrawWaistSection()
    {
        BeginSection("Waist");

        using (new EditorGUI.DisabledScope(waistBone.objectReferenceValue == null))
        {
            DrawProperty(waistYawWeight, "Waist Strength", "How much the waist helps during wider looks.");
            DrawProperty(waistHeadYawThreshold, "Waist Start Angle", "How far left or right the head must turn before the waist starts helping.");
            DrawProperty(waistMaxYaw, "Max Waist Yaw", "Maximum left/right waist rotation in degrees.");

            _showWaistAdvanced = EditorGUILayout.Foldout(_showWaistAdvanced, "Advanced Waist Options", true);
            if (_showWaistAdvanced)
            {
                DrawProperty(waistEngageSpeed, "Waist Engage Speed", "How quickly the waist influence builds up once it starts helping.");
                DrawProperty(waistRotationSpeed, "Waist Rotation Speed", "How quickly the waist rotates toward its target direction.");
            }
        }

        if (waistBone.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Assign a Waist Bone if you want torso support for wider turns.", MessageType.None);
        EndSection();
    }

    void DrawDebugSection()
    {
        BeginSection("Debug");
        DrawProperty(debugDrawEyeGizmos, "Show Gaze Gizmos", "Shows head and eye rays in the Scene view while this object is selected.");

        using (new EditorGUI.DisabledScope(!debugDrawEyeGizmos.boolValue))
        {
            _showDebugAdvanced = EditorGUILayout.Foldout(_showDebugAdvanced, "Debug Options", true);
            if (_showDebugAdvanced)
                DrawProperty(debugEyeGizmoLength, "Gizmo Length", "How long the debug rays appear in the Scene view.");
        }
        EndSection();
    }

    void DrawProperty(SerializedProperty property, string label, string tooltip)
    {
        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
    }

    void BeginSection(string title)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    void EndSection()
    {
        EditorGUILayout.EndVertical();
    }

    void DrawBlendshapePopup(SerializedProperty meshProperty, SerializedProperty blendshapeProperty, string label, string tooltip)
    {
        var smr = meshProperty.objectReferenceValue as SkinnedMeshRenderer;
        var mesh = smr != null ? smr.sharedMesh : null;

        if (mesh == null || mesh.blendShapeCount == 0)
        {
            DrawProperty(blendshapeProperty, label, tooltip);
            return;
        }

        int count = mesh.blendShapeCount;
        bool hasMissingCurrent = blendshapeProperty.intValue >= count;
        int extra = hasMissingCurrent ? 2 : 1;
        string[] options = new string[count + extra];
        int[] values = new int[count + extra];
        int cursor = 0;

        if (hasMissingCurrent)
        {
            options[cursor] = $"Missing ({blendshapeProperty.intValue})";
            values[cursor] = blendshapeProperty.intValue;
            cursor++;
        }

        options[cursor] = "None";
        values[cursor] = -1;
        cursor++;

        for (int i = 0; i < count; i++, cursor++)
        {
            options[cursor] = mesh.GetBlendShapeName(i);
            values[cursor] = i;
        }

        int currentSelection = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == blendshapeProperty.intValue)
            {
                currentSelection = i;
                break;
            }
        }

        int nextSelection = EditorGUILayout.Popup(
            new GUIContent(label, tooltip),
            currentSelection,
            options
        );

        if (nextSelection >= 0 && nextSelection < values.Length)
            blendshapeProperty.intValue = values[nextSelection];
    }

    void DrawEyeSaccadeIntensityControl()
    {
        GUIContent label = new(
            "Eye Saccade Intensity",
            "Controls how strong eye saccades are. 0 disables the offset, 1 uses the default strength, 10 is a strong visible setting, and higher manual values still work.");

        Rect rect = EditorGUILayout.GetControlRect();
        rect = EditorGUI.PrefixLabel(rect, label);

        Rect sliderRect = rect;
        sliderRect.width -= 58f;
        Rect fieldRect = rect;
        fieldRect.x = sliderRect.xMax + 4f;
        fieldRect.width = 54f;

        EditorGUI.BeginChangeCheck();
        float sliderValue = GUI.HorizontalSlider(
            sliderRect,
            Mathf.Clamp(eyeSaccadeIntensity.floatValue, 0f, EyeSaccadeIntensitySliderMax),
            0f,
            EyeSaccadeIntensitySliderMax);
        if (EditorGUI.EndChangeCheck())
            eyeSaccadeIntensity.floatValue = sliderValue;

        EditorGUI.BeginChangeCheck();
        float rawValue = EditorGUI.FloatField(fieldRect, eyeSaccadeIntensity.floatValue);
        if (EditorGUI.EndChangeCheck())
            eyeSaccadeIntensity.floatValue = rawValue;
    }

    void EnsureSharedBlendshapeListSeeded()
    {
        if (sharedEyelidBlendshapes == null || !sharedEyelidBlendshapes.isArray)
            return;

        if (sharedEyelidBlendshapes.arraySize == 0)
        {
            sharedEyelidBlendshapes.arraySize = 2;
            int seededValue = eyelidBlendshape.intValue >= 0 ? eyelidBlendshape.intValue : -1;
            sharedEyelidBlendshapes.GetArrayElementAtIndex(0).intValue = seededValue;
            sharedEyelidBlendshapes.GetArrayElementAtIndex(1).intValue = -1;
            return;
        }

        if (sharedEyelidBlendshapes.arraySize == 1)
        {
            sharedEyelidBlendshapes.InsertArrayElementAtIndex(1);
            sharedEyelidBlendshapes.GetArrayElementAtIndex(1).intValue = -1;
        }
    }

    void DrawSharedBlendshapeList()
    {
        if (sharedEyelidBlendshapes == null || !sharedEyelidBlendshapes.isArray)
            return;

        int removeIndex = -1;
        for (int i = 0; i < sharedEyelidBlendshapes.arraySize; i++)
        {
            SerializedProperty element = sharedEyelidBlendshapes.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBlendshapePopup(
                    faceMesh,
                    element,
                    $"Shared Eyelid Blendshape {i + 1}",
                    "Blendshape used to close both upper eyelids. Add more entries to drive multiple shared shapes together.");

                using (new EditorGUI.DisabledScope(sharedEyelidBlendshapes.arraySize <= 2))
                {
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                        removeIndex = i;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Shared Blendshape"))
            {
                int nextIndex = sharedEyelidBlendshapes.arraySize;
                sharedEyelidBlendshapes.InsertArrayElementAtIndex(nextIndex);
                sharedEyelidBlendshapes.GetArrayElementAtIndex(nextIndex).intValue = -1;
            }

            using (new EditorGUI.DisabledScope(sharedEyelidBlendshapes.arraySize <= 2))
            {
                if (GUILayout.Button("Remove Last"))
                    removeIndex = sharedEyelidBlendshapes.arraySize - 1;
            }
        }

        if (removeIndex >= 0 && sharedEyelidBlendshapes.arraySize > 2)
            sharedEyelidBlendshapes.DeleteArrayElementAtIndex(removeIndex);
    }

    void AutoSetupSelectedDrivers()
    {
        foreach (UnityEngine.Object obj in targets)
        {
            if (obj is SnippetsGazeDriver driver)
                SnippetsGazeDriverAutoSetupUtility.AutoSetupDriver(driver);
        }
    }

    void ApplyPresetValuesToSelectedDrivers()
    {
        foreach (UnityEngine.Object obj in targets)
        {
            if (obj is not SnippetsGazeDriver driver)
                continue;

            Undo.RecordObject(driver, "Apply Gaze Driver RPM Preset");
            driver.ApplyRpmCrossEyePresetValues();
            EditorUtility.SetDirty(driver);
        }
    }
}
#endif
