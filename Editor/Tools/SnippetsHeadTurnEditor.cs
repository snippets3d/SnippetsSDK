#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SnippetsHeadTurn))]
public class SnippetsHeadTurnEditor : Editor
{
    SerializedProperty mode;

    // Follow Target
    SerializedProperty target, autoFindTarget;

    // Target smoothing
    SerializedProperty smoothTarget, targetFollowSpeed, snapProxyOnTargetChange;

    // Rig
    SerializedProperty headBone, waistBone, waistDirectionSource;

    // Look
    SerializedProperty lookWeight, blendSpeed, rotationSpeed, maxYaw, maxPitch, lookOffset, normalizeTargetHeight;

    // Look in front
    SerializedProperty fixedTargetName, fixedTargetOverride;

    // Refresh
    SerializedProperty autoRefreshTargets, refreshTargetsInterval;

    // Waist
    SerializedProperty waistYawWeight, waistHeadYawThreshold, waistDelay;
    SerializedProperty waistEngageSpeed, waistMaxYaw, waistRotationSpeed;

    void OnEnable()
    {
        mode = serializedObject.FindProperty("mode");

        target = serializedObject.FindProperty("target");
        autoFindTarget = serializedObject.FindProperty("autoFindTarget");

        smoothTarget = serializedObject.FindProperty("smoothTarget");
        targetFollowSpeed = serializedObject.FindProperty("targetFollowSpeed");
        snapProxyOnTargetChange = serializedObject.FindProperty("snapProxyOnTargetChange");

        headBone = serializedObject.FindProperty("headBone");
        waistBone = serializedObject.FindProperty("waistBone");
        waistDirectionSource = serializedObject.FindProperty("waistDirectionSource");

        lookWeight = serializedObject.FindProperty("lookWeight");
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
        waistDelay = serializedObject.FindProperty("waistDelay");
        waistEngageSpeed = serializedObject.FindProperty("waistEngageSpeed");
        waistMaxYaw = serializedObject.FindProperty("waistMaxYaw");
        waistRotationSpeed = serializedObject.FindProperty("waistRotationSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(mode);
        var m = (SnippetsHeadTurn.GazeMode)mode.enumValueIndex;

        // ===== MODE-SPECIFIC =====

        if (m == SnippetsHeadTurn.GazeMode.FollowTarget)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Follow Target", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(target);
            EditorGUILayout.PropertyField(autoFindTarget);
        }
        else if (m == SnippetsHeadTurn.GazeMode.LookInFront)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Look In Front", EditorStyles.boldLabel);

            // Prefer override to avoid Find()-by-name
            EditorGUILayout.PropertyField(fixedTargetOverride);

            using (new EditorGUI.DisabledScope(fixedTargetOverride.objectReferenceValue != null))
            {
                EditorGUILayout.PropertyField(fixedTargetName);

                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(autoRefreshTargets);
                if (autoRefreshTargets.boolValue)
                    EditorGUILayout.PropertyField(refreshTargetsInterval);
            }
        }
        else // Off
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Gaze is Off (weight blends to 0).", MessageType.Info);
        }

        // ===== COMMON =====

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Rig", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(headBone);
        EditorGUILayout.PropertyField(waistBone);
        EditorGUILayout.PropertyField(waistDirectionSource);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Target Smoothing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(smoothTarget);
        using (new EditorGUI.DisabledScope(!smoothTarget.boolValue))
        {
            EditorGUILayout.PropertyField(targetFollowSpeed);
            EditorGUILayout.PropertyField(snapProxyOnTargetChange);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Look Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lookWeight);
        EditorGUILayout.PropertyField(blendSpeed);
        EditorGUILayout.PropertyField(rotationSpeed);
        EditorGUILayout.PropertyField(maxYaw);
        EditorGUILayout.PropertyField(maxPitch);
        EditorGUILayout.PropertyField(lookOffset);
        EditorGUILayout.PropertyField(normalizeTargetHeight);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Waist Follow", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(waistYawWeight);
        EditorGUILayout.PropertyField(waistHeadYawThreshold);
        EditorGUILayout.PropertyField(waistDelay);
        EditorGUILayout.PropertyField(waistEngageSpeed);
        EditorGUILayout.PropertyField(waistMaxYaw);
        EditorGUILayout.PropertyField(waistRotationSpeed);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
