#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SnippetsWalker))]
[CanEditMultipleObjects]
public class SnippetsWalkerEditor : Editor
{
    static readonly GUIContent GC_Waypoints = new("Waypoints", "Waypoint transforms visited by the walker in index order.");
    static readonly GUIContent GC_StartIndex = new("Starting Waypoint", "Waypoint index this walker treats as current when it wakes up.");
    static readonly GUIContent GC_MoveSpeed = new("Move Speed", "Movement speed in world units per second.");
    static readonly GUIContent GC_ArriveDistance = new("Arrival Distance", "Horizontal distance from the waypoint at which arrival is considered complete.");
    static readonly GUIContent GC_TurnSpeed = new("Turn Speed", "Rotation speed in degrees per second while turning toward movement or final alignment.");
    static readonly GUIContent GC_UseNavMesh = new("Use NavMesh Pathfinding", "OFF = simple straight-line waypoint movement (legacy).\nON = NavMeshAgent pathfinding using the same waypoints.");
    static readonly GUIContent GC_NavAgent = new("NavMesh Agent", "Optional NavMeshAgent used when NavMesh pathfinding is enabled. If left empty, one is auto-fetched from this GameObject.");
    static readonly GUIContent GC_AgentAcceleration = new("Acceleration", "NavMeshAgent acceleration. Higher values let the actor recover speed faster after slowing for turns or avoidance.");
    static readonly GUIContent GC_AgentAngularSpeed = new("Agent Angular Speed", "NavMeshAgent angular speed in degrees per second. Only used when the agent handles rotation itself.");
    static readonly GUIContent GC_AgentAutoBraking = new("Auto Braking", "If enabled, the NavMeshAgent brakes as it approaches the destination. Turning this off usually gives smoother guided-tour movement.");
    static readonly GUIContent GC_AgentAvoidanceQuality = new("Avoidance Quality", "Obstacle avoidance quality used by the NavMeshAgent while pathfinding.");
    static readonly GUIContent GC_AgentAvoidancePriority = new("Avoidance Priority", "NavMeshAgent avoidance priority. Lower values make other agents yield more often to this actor.");
    static readonly GUIContent GC_ManualAgentRotation = new("Manual Facing", "If enabled, this script rotates the actor toward desired velocity instead of letting the NavMeshAgent rotate it. Recommended for legacy rigs.");
    static readonly GUIContent GC_RotateMinSpeed = new("Min Velocity To Turn", "Minimum desired velocity before manual NavMesh rotation starts turning the actor.");
    static readonly GUIContent GC_RootMotionBoneToPin = new("Pinned Root-Motion Bone", "Optional bone whose local position is pinned every frame to prevent legacy animation root drift.");
    static readonly GUIContent GC_PinBoneTranslation = new("Pin Bone Translation", "Keeps the pinned bone's local translation fixed at its startup pose.");

    SerializedProperty _waypoints;
    SerializedProperty _startIndex;
    SerializedProperty _moveSpeed;
    SerializedProperty _arriveDistance;
    SerializedProperty _turnSpeed;
    SerializedProperty _useNavMesh;
    SerializedProperty _navAgent;
    SerializedProperty _agentAcceleration;
    SerializedProperty _agentAngularSpeed;
    SerializedProperty _agentAutoBraking;
    SerializedProperty _agentAvoidanceQuality;
    SerializedProperty _agentAvoidancePriority;
    SerializedProperty _manualAgentRotation;
    SerializedProperty _rotateMinSpeed;
    SerializedProperty _rootMotionBoneToPin;
    SerializedProperty _pinBoneTranslation;
    ReorderableList _waypointsList;

    void OnEnable()
    {
        _waypoints = serializedObject.FindProperty(nameof(SnippetsWalker.waypoints));
        _startIndex = serializedObject.FindProperty(nameof(SnippetsWalker.startIndex));
        _moveSpeed = serializedObject.FindProperty(nameof(SnippetsWalker.moveSpeed));
        _arriveDistance = serializedObject.FindProperty(nameof(SnippetsWalker.arriveDistance));
        _turnSpeed = serializedObject.FindProperty(nameof(SnippetsWalker.turnSpeed));
        _useNavMesh = serializedObject.FindProperty(nameof(SnippetsWalker.useNavMesh));
        _navAgent = serializedObject.FindProperty(nameof(SnippetsWalker.navAgent));
        _agentAcceleration = serializedObject.FindProperty(nameof(SnippetsWalker.agentAcceleration));
        _agentAngularSpeed = serializedObject.FindProperty(nameof(SnippetsWalker.agentAngularSpeed));
        _agentAutoBraking = serializedObject.FindProperty(nameof(SnippetsWalker.agentAutoBraking));
        _agentAvoidanceQuality = serializedObject.FindProperty(nameof(SnippetsWalker.agentAvoidanceQuality));
        _agentAvoidancePriority = serializedObject.FindProperty(nameof(SnippetsWalker.agentAvoidancePriority));
        _manualAgentRotation = serializedObject.FindProperty(nameof(SnippetsWalker.manualAgentRotation));
        _rotateMinSpeed = serializedObject.FindProperty(nameof(SnippetsWalker.rotateMinSpeed));
        _rootMotionBoneToPin = serializedObject.FindProperty(nameof(SnippetsWalker.rootMotionBoneToPin));
        _pinBoneTranslation = serializedObject.FindProperty(nameof(SnippetsWalker.pinBoneTranslation));

        BuildWaypointsList();
        AutoAssignPinnedRootMotionBoneIfNeeded();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BeginSection("Route Setup");
        DrawRouteSection();
        EndSection();
        EditorGUILayout.Space(10);

        BeginSection("Movement");
        DrawMovementSection();
        EndSection();
        EditorGUILayout.Space(10);

        BeginSection("Navigation");
        DrawNavigationSection();
        EndSection();
        EditorGUILayout.Space(10);

        BeginSection("Anti-Drift");
        DrawAntiDriftSection();
        EndSection();

        if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.Space(10);
            BeginSection("Runtime");
            DrawRuntimeSection((SnippetsWalker)target);
            EndSection();
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawRouteSection()
    {
        EditorGUILayout.HelpBox("Add every waypoint this character may need to walk to here. Simple Controller and Flow Controller can only send this walker to waypoints listed in this array.", MessageType.None);
        EditorGUILayout.Space(4f);
        _waypointsList?.DoLayoutList();
        DrawStartingWaypointField();
    }

    void DrawMovementSection()
    {
        EditorGUILayout.PropertyField(_moveSpeed, GC_MoveSpeed);
        EditorGUILayout.PropertyField(_arriveDistance, GC_ArriveDistance);
        EditorGUILayout.PropertyField(_turnSpeed, GC_TurnSpeed);
    }

    void DrawNavigationSection()
    {
        EditorGUILayout.PropertyField(_useNavMesh, GC_UseNavMesh);

        if (!_useNavMesh.boolValue)
        {
            EditorGUILayout.HelpBox("Using simple straight-line waypoint movement.", MessageType.None);
            return;
        }

        EditorGUILayout.HelpBox("Using NavMeshAgent pathfinding with the same waypoint list.", MessageType.None);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Pathfinding", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(_navAgent, GC_NavAgent);
        EditorGUILayout.PropertyField(_agentAcceleration, GC_AgentAcceleration);
        EditorGUILayout.PropertyField(_agentAutoBraking, GC_AgentAutoBraking);
        EditorGUILayout.PropertyField(_agentAvoidanceQuality, GC_AgentAvoidanceQuality);
        EditorGUILayout.PropertyField(_agentAvoidancePriority, GC_AgentAvoidancePriority);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Facing", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(_manualAgentRotation, GC_ManualAgentRotation);

        if (_manualAgentRotation.boolValue)
        {
            EditorGUILayout.HelpBox("The walker uses Turn Speed above for facing while the NavMeshAgent handles pathfinding.", MessageType.None);
            EditorGUILayout.PropertyField(_rotateMinSpeed, GC_RotateMinSpeed);
        }
        else
        {
            EditorGUILayout.HelpBox("The NavMeshAgent handles facing.", MessageType.None);
            EditorGUILayout.PropertyField(_agentAngularSpeed, GC_AgentAngularSpeed);
        }
    }

    void DrawAntiDriftSection()
    {
        EditorGUILayout.PropertyField(_rootMotionBoneToPin, GC_RootMotionBoneToPin);

        using (new EditorGUI.DisabledScope(_rootMotionBoneToPin.objectReferenceValue == null))
        {
            EditorGUILayout.PropertyField(_pinBoneTranslation, GC_PinBoneTranslation);
        }
    }

    void AutoAssignPinnedRootMotionBoneIfNeeded()
    {
        bool changed = false;

        foreach (Object targetObject in targets)
        {
            if (targetObject is not SnippetsWalker walker)
                continue;

            if (SnippetsWalkerAutoSetupUtility.AutoAssignPinnedRootMotionBone(walker))
                changed = true;
        }

        if (changed)
            serializedObject.Update();
    }

    static void DrawRuntimeSection(SnippetsWalker walker)
    {
        if (walker == null) return;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Current Waypoint", walker.CurrentIndex);
            EditorGUILayout.Toggle("Is Busy", walker.IsBusy);
        }
    }

    static void BeginSection(string title)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(4);
    }

    static void EndSection()
    {
        EditorGUILayout.EndVertical();
    }

    void BuildWaypointsList()
    {
        _waypointsList = new ReorderableList(serializedObject, _waypoints, true, true, true, true);

        _waypointsList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, GC_Waypoints);
        };

        _waypointsList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;

        _waypointsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = _waypoints.GetArrayElementAtIndex(index);
            rect.y += 2f;
            EditorGUI.PropertyField(
                rect,
                element,
                new GUIContent($"Waypoint {index + 1}", $"Waypoint transform at route index {index}."),
                true
            );
        };
    }

    void DrawStartingWaypointField()
    {
        if (serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.PropertyField(_startIndex, GC_StartIndex);
            return;
        }

        int waypointCount = _waypoints != null ? _waypoints.arraySize : 0;
        if (waypointCount <= 0)
        {
            EditorGUILayout.PropertyField(_startIndex, GC_StartIndex);
            return;
        }

        string[] options = new string[waypointCount];
        for (int i = 0; i < waypointCount; i++)
        {
            var element = _waypoints.GetArrayElementAtIndex(i);
            var tr = element != null ? element.objectReferenceValue as Transform : null;
            options[i] = tr != null ? $"{i}: {tr.name}" : $"{i}: Waypoint {i + 1}";
        }

        _startIndex.intValue = Mathf.Clamp(_startIndex.intValue, 0, waypointCount - 1);

        int nextIndex = EditorGUILayout.Popup(
            GC_StartIndex,
            _startIndex.intValue,
            options
        );

        if (nextIndex != _startIndex.intValue)
            _startIndex.intValue = nextIndex;
    }
}
#endif
