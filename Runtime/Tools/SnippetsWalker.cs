using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SnippetsWalker : MonoBehaviour
{
    // ============================================================
    // Waypoints (unchanged API)
    // ============================================================

    [Header("Waypoints")]
    public Transform[] waypoints;
    public int startIndex = 0;

    // ============================================================
    // Movement (shared)
    // ============================================================

    [Header("Movement")]
    [Tooltip("Units per second")]
    public float moveSpeed = 1.4f;

    [Tooltip("Arrival distance")]
    public float arriveDistance = 0.15f;

    [Tooltip("Degrees per second")]
    public float turnSpeed = 540f;

    // ============================================================
    // Navigation Mode
    // ============================================================

    [Header("Navigation Mode")]
    [Tooltip("OFF = simple straight-line waypoint movement (legacy)\nON = NavMeshAgent pathfinding using same waypoints")]
    public bool useNavMesh = false;

    // ============================================================
    // NavMesh settings (only used if useNavMesh = true)
    // ============================================================

    [Header("NavMesh Settings")]
    public NavMeshAgent navAgent;

    [Tooltip("Higher = recovers faster after avoidance")]
    public float agentAcceleration = 40f;

    [Tooltip("Only used if agent updateRotation is enabled")]
    public float agentAngularSpeed = 720f;

    [Tooltip("Recommended OFF for guided tours")]
    public bool agentAutoBraking = false;

    public ObstacleAvoidanceType agentAvoidanceQuality =
        ObstacleAvoidanceType.LowQualityObstacleAvoidance;

    [Range(0, 99)]
    public int agentAvoidancePriority = 50;

    [Header("NavMesh Rotation")]
    [Tooltip("Manually rotate towards desired velocity (recommended for legacy rigs)")]
    public bool manualAgentRotation = true;

    public float rotateMinSpeed = 0.05f;

    // ============================================================
    // Anti-drift (legacy animation safety)
    // ============================================================

    [Header("Anti-Drift")]
    public Transform rootMotionBoneToPin;
    public bool pinBoneTranslation = true;

    // ============================================================
    // Runtime
    // ============================================================

    public event Action Arrived;

    int _index;
    bool _moving;
    bool _aligning;

    Vector3 _pinnedLocalPos;
    bool _pinReady;

    public int CurrentIndex => _index;
    public bool IsBusy => _moving || _aligning;

    // ============================================================
    // Unity
    // ============================================================

    void Awake()
    {
        if (waypoints != null && waypoints.Length > 0)
            _index = Mathf.Clamp(startIndex, 0, waypoints.Length - 1);
        else
            _index = 0;

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        CachePinPose();
    }

    void Update()
    {
        if (_moving)
        {
            if (useNavMesh && CanUseAgent())
                TickNavMesh();
            else
                TickSimple();
        }

        if (_aligning)
            TickAlign();
    }

    void LateUpdate()
    {
        if (pinBoneTranslation && _pinReady && rootMotionBoneToPin != null)
            rootMotionBoneToPin.localPosition = _pinnedLocalPos;
    }

    // ============================================================
    // Public API (unchanged)
    // ============================================================

    public void MoveToIndex(int index)
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        _index = Mathf.Clamp(index, 0, waypoints.Length - 1);
        _moving = true;
        _aligning = false;

        if (useNavMesh && CanUseAgent())
        {
            ApplyAgentSettings();

            navAgent.isStopped = false;
            navAgent.updateRotation = !manualAgentRotation;

            var wp = CurrentWaypoint;
            if (wp != null)
                navAgent.SetDestination(wp.position);
        }
    }

    public void MoveNext()
    {
        MoveToIndex(_index + 1);
    }

    public void StopMovement()
    {
        _moving = false;
        _aligning = false;

        if (navAgent != null && navAgent.enabled)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
        }
    }

    // ============================================================
    // Simple (legacy) movement
    // ============================================================

    void TickSimple()
    {
        var wp = CurrentWaypoint;
        if (wp == null)
        {
            Arrive();
            return;
        }

        Vector3 to = wp.position - transform.position;
        to.y = 0f;

        if (to.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, rot, turnSpeed * Time.deltaTime);
        }

        Vector3 target = wp.position;
        target.y = transform.position.y;

        transform.position = Vector3.MoveTowards(
            transform.position, target, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(wp.position.x, 0, wp.position.z)) <= arriveDistance)
        {
            transform.position =
                new Vector3(wp.position.x, transform.position.y, wp.position.z);
            Arrive();
        }
    }

    // ============================================================
    // NavMesh movement
    // ============================================================

    bool CanUseAgent()
    {
        return navAgent != null && navAgent.enabled && navAgent.isOnNavMesh;
    }

    void ApplyAgentSettings()
    {
        navAgent.speed = moveSpeed;
        navAgent.stoppingDistance = arriveDistance;
        navAgent.acceleration = agentAcceleration;
        navAgent.angularSpeed = agentAngularSpeed;
        navAgent.autoBraking = agentAutoBraking;
        navAgent.obstacleAvoidanceType = agentAvoidanceQuality;
        navAgent.avoidancePriority = agentAvoidancePriority;
    }

    void TickNavMesh()
    {
        var wp = CurrentWaypoint;
        if (wp == null)
        {
            Arrive();
            return;
        }

        if (manualAgentRotation)
        {
            Vector3 v = navAgent.desiredVelocity;
            v.y = 0f;

            if (v.magnitude > rotateMinSpeed)
            {
                Quaternion rot = Quaternion.LookRotation(v.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, rot, turnSpeed * Time.deltaTime);
            }
        }

        if (!navAgent.pathPending &&
            navAgent.remainingDistance <= navAgent.stoppingDistance + 0.01f &&
            (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.01f))
        {
            Arrive();
        }
    }

    // ============================================================
    // Arrival + alignment
    // ============================================================

    void Arrive()
    {
        _moving = false;

        if (useNavMesh && navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        _aligning = true;
    }

    void TickAlign()
    {
        var wp = CurrentWaypoint;
        if (wp == null)
        {
            FinishArrival();
            return;
        }

        Quaternion target =
            Quaternion.Euler(0f, wp.rotation.eulerAngles.y, 0f);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, turnSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, target) <= 2f)
        {
            transform.rotation = target;
            FinishArrival();
        }
    }

    void FinishArrival()
    {
        _aligning = false;
        Arrived?.Invoke();
    }

    // ============================================================
    // Helpers
    // ============================================================

    Transform CurrentWaypoint =>
        (waypoints != null && waypoints.Length > 0)
            ? waypoints[Mathf.Clamp(_index, 0, waypoints.Length - 1)]
            : null;

    void CachePinPose()
    {
        if (!pinBoneTranslation || rootMotionBoneToPin == null)
            return;

        _pinnedLocalPos = rootMotionBoneToPin.localPosition;
        _pinReady = true;
    }
}
