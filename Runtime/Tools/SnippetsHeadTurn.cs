using UnityEngine;

[DefaultExecutionOrder(10000)]
public class SnippetsHeadTurn : MonoBehaviour
{
    public enum GazeMode { FollowTarget, LookInFront, Off }

    [Header("Mode")]
    public GazeMode mode = GazeMode.FollowTarget;

    // ================= TARGET =================
    [Header("Target (Follow Target)")]
    public Transform target = null;
    public bool autoFindTarget = false;

    // ================= TARGET SMOOTHING =================
    [Header("Target Smoothing")]
    [Tooltip("Aim at a smoothed proxy that follows the target position over time (prevents snapping when target changes).")]
    public bool smoothTarget = true;

    [Tooltip("How fast the proxy follows the target. Higher = snappier, lower = floatier.")]
    public float targetFollowSpeed = 14f;

    [Tooltip("If true, when target changes, proxy snaps to new target once (avoids proxy flying across scene).")]
    public bool snapProxyOnTargetChange = true;

    // ================= RIG =================
    [Header("Rig")]
    [Tooltip("Actual head bone (animated by legacy Animation). No driver objects will be inserted.")]
    public Transform headBone;

    [Tooltip("Bone that represents the waist/root of upper-body yaw (often Spine/Hips depending on rig).")]
    public Transform waistBone;

    [Tooltip("Direction reference for yaw (can be animated hips to avoid feedback). If null, uses a stable fallback captured at runtime.")]
    public Transform waistDirectionSource;

    // ================= LOOK =================
    [Header("Look Settings")]
    [Range(0f, 1f)] public float lookWeight = 1f;
    public float blendSpeed = 10f;
    public float rotationSpeed = 18f;
    public float maxYaw = 50f;
    public float maxPitch = 30f;

    [Tooltip("Applied after any height normalization. Usually small values (e.g. Y=0.0..0.1).")]
    public Vector3 lookOffset = Vector3.zero;

    [Tooltip("If true, aim point Y is normalized to THIS character's head driver height (reduces up/down looking).")]
    public bool normalizeTargetHeight = false;

    // ================= LOOK IN FRONT =================
    [Header("Look In Front")]
    [Tooltip("Only used if fixedTargetOverride is not assigned. Prefer assigning the override to avoid GameObject.Find.")]
    public string fixedTargetName = "HeadTarget";
    public Transform fixedTargetOverride;

    [Header("Runtime Target Refresh")]
    [Tooltip("If enabled, refreshes cached named targets at an interval (avoids per-frame GameObject.Find).")]
    public bool autoRefreshTargets = true;
    public float refreshTargetsInterval = 0.5f;

    // ================= WAIST =================
    [Header("Waist Follow")]
    [Range(0f, 1f)] public float waistYawWeight = 0.8f;
    public float waistHeadYawThreshold = 15f;
    public float waistDelay = 0f;
    public float waistEngageSpeed = 3.5f;
    public float waistMaxYaw = 25f;
    public float waistRotationSpeed = 6f;

    // ================= INTERNAL =================
    // Virtual head driver (NO hierarchy changes)
    Transform _headParent;
    Vector3 _headDriverRestLocalPos;
    Quaternion _headDriverRestLocalRot;
    Quaternion _headDriverLocalRot; // smoothed "driver" local rot
    float _headWeight;

    // Target proxy
    Transform _targetProxy;
    Transform _lastResolvedTarget;

    // Look-in-front cache
    Transform _cachedFixedTargetByName;
    float _nextRefreshTime;

    // Waist reference fallback (stable ref like driver version)
    Transform _waistDirectionFallback;
    float _waistGateStart = -1f;
    float _waistEngage;
    float _waistYawSmoothed;

    void Awake()
    {
        if (!headBone)
        {
            Debug.LogError("[SnippetsHeadTurn] headBone not assigned");
            enabled = false;
            return;
        }

        if (autoFindTarget) target = FindActiveCamera();

        _headParent = headBone.parent;
        if (!_headParent)
        {
            Debug.LogError("[SnippetsHeadTurn] headBone has no parent (cannot emulate driver space)");
            enabled = false;
            return;
        }

        // Cache what the REAL driver would have captured:
        // driver.localPosition = headBone.localPosition
        // driver.localRotation = headBone.localRotation
        _headDriverRestLocalPos = headBone.localPosition;
        _headDriverRestLocalRot = headBone.localRotation;
        _headDriverLocalRot = _headDriverRestLocalRot;

        // Waist direction fallback (stable reference like driver version)
        if (waistBone) _waistDirectionFallback = waistBone.parent;

        EnsureTargetProxy();
        RefreshFixedTargetByName();
        _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshTargetsInterval);
    }

    void Update()
    {
        if (mode == GazeMode.FollowTarget && autoFindTarget)
        {
            if (!target || !target.gameObject.activeInHierarchy)
                target = FindActiveCamera();
        }

        if (autoRefreshTargets && Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshTargetsInterval);
            RefreshFixedTargetByName();
        }
    }

    void LateUpdate()
    {
        Transform resolvedTarget = GetResolvedTarget();
        Transform aimTarget = ResolveAimTargetProxy(resolvedTarget);

        float desiredW = (mode == GazeMode.Off || !aimTarget) ? 0f : lookWeight;
        _headWeight = Mathf.MoveTowards(_headWeight, desiredW, Time.deltaTime * blendSpeed);

        // Capture animated baselines (legacy Animation has written by now if your order is correct)
        Quaternion animWaistWorld = waistBone ? waistBone.rotation : Quaternion.identity;

        if (_headWeight <= 0f || !aimTarget)
        {
            UpdateWaistFromAimPoint(animWaistWorld, Vector3.zero, 0f, hasTarget: false);
            return;
        }

        // First pass: compute yaw for waist gating using current parent orientation.
        Vector3 aimPoint;
        Vector3 driverWorldPos;
        float clampedYawForWaist;
        float clampedPitchUnused;
        Quaternion clampedLocalUnused;
        if (!TryComputeClampedLocal(aimTarget, out aimPoint, out driverWorldPos,
            out clampedYawForWaist, out clampedPitchUnused, out clampedLocalUnused))
            return;

        // Apply waist BEFORE head so the head solve uses the final parent orientation (prevents overshoot).
        UpdateWaistFromAimPoint(animWaistWorld, aimPoint, clampedYawForWaist, hasTarget: true);

        // Re-sample animated head baseline after waist updated.
        Quaternion animHeadWorld = headBone.rotation;

        // Second pass: compute head rotation using updated parent orientation.
        float clampedYaw;
        float clampedPitch;
        Quaternion clampedLocal;
        if (!TryComputeClampedLocal(aimTarget, out aimPoint, out driverWorldPos,
            out clampedYaw, out clampedPitch, out clampedLocal))
            return;

        Quaternion weightedLocal = Quaternion.Slerp(_headDriverRestLocalRot, clampedLocal, _headWeight);

        // Smooth the virtual driver like the real one
        _headDriverLocalRot = Quaternion.Slerp(_headDriverLocalRot, weightedLocal, Time.deltaTime * rotationSpeed);

        // Apply to the animated baseline (no stacking)
        Quaternion driverDeltaWorld =
            _headParent.rotation *
            _headDriverLocalRot *
            Quaternion.Inverse(_headDriverRestLocalRot) *
            Quaternion.Inverse(_headParent.rotation);

        headBone.rotation = driverDeltaWorld * animHeadWorld;
    }


    // ================= WAIST FOLLOW (NO DRIVER OBJECTS) =================
    // Smoothly returns to animation when inside threshold (no snap).
    void UpdateWaistFromAimPoint(Quaternion animWaistWorld, Vector3 targetPoint, float headYawDeg, bool hasTarget)
    {
        if (!waistBone || waistYawWeight <= 0f) return;

        float absYaw = hasTarget ? Mathf.Abs(headYawDeg) : 0f;

        // If no target (or not enough head yaw), smoothly return to animation (no snap).
        if (!hasTarget || absYaw < waistHeadYawThreshold)
        {
            _waistGateStart = -1f;
            _waistEngage = Mathf.MoveTowards(_waistEngage, 0f, Time.deltaTime * waistEngageSpeed);

            // Smooth the applied yaw back toward 0
            _waistYawSmoothed = Mathf.Lerp(_waistYawSmoothed, 0f, Time.deltaTime * waistRotationSpeed);

            // Still apply the fading yaw on top of this frame's animation baseline
            waistBone.rotation = Quaternion.AngleAxis(_waistYawSmoothed, Vector3.up) * animWaistWorld;
            return;
        }

        if (_waistGateStart < 0f) _waistGateStart = Time.time;

        // During delay, keep current yaw applied (don't snap back to baseline)
        if (Time.time - _waistGateStart < waistDelay)
        {
            waistBone.rotation = Quaternion.AngleAxis(_waistYawSmoothed, Vector3.up) * animWaistWorld;
            return;
        }

        _waistEngage = Mathf.MoveTowards(_waistEngage, 1f, Time.deltaTime * waistEngageSpeed);

        Transform src = waistDirectionSource ? waistDirectionSource :
            (_waistDirectionFallback ? _waistDirectionFallback :
            (waistBone.parent ? waistBone.parent : waistBone));

        Vector3 fwd = src.forward; fwd.y = 0f;
        Vector3 toT = targetPoint - src.position; toT.y = 0f;
        if (toT.sqrMagnitude < 0.0001f)
        {
            waistBone.rotation = Quaternion.AngleAxis(_waistYawSmoothed, Vector3.up) * animWaistWorld;
            return;
        }

        float yawWorld = Mathf.Clamp(
            Vector3.SignedAngle(fwd.normalized, toT.normalized, Vector3.up),
            -waistMaxYaw, waistMaxYaw
        );

        float desiredYaw = yawWorld * waistYawWeight * _waistEngage;
        _waistYawSmoothed = Mathf.Lerp(_waistYawSmoothed, desiredYaw, Time.deltaTime * waistRotationSpeed);

        // Apply absolute yaw onto this frame’s animated baseline (no accumulation)
        waistBone.rotation = Quaternion.AngleAxis(_waistYawSmoothed, Vector3.up) * animWaistWorld;
    }

    // ================= TARGET RESOLUTION =================
    Transform GetResolvedTarget()
    {
        switch (mode)
        {
            case GazeMode.FollowTarget: return target;
            case GazeMode.LookInFront:
                if (fixedTargetOverride) return fixedTargetOverride;
                return _cachedFixedTargetByName;
            case GazeMode.Off:
            default: return null;
        }
    }

    void RefreshFixedTargetByName()
    {
        if (fixedTargetOverride) { _cachedFixedTargetByName = null; return; }
        if (string.IsNullOrEmpty(fixedTargetName)) { _cachedFixedTargetByName = null; return; }

        var go = GameObject.Find(fixedTargetName);
        _cachedFixedTargetByName = go ? go.transform : null;
    }

    // ================= TARGET PROXY =================
    void EnsureTargetProxy()
    {
        if (_targetProxy && _targetProxy.name == "HeadAimTargetProxy") return;

        var existing = transform.Find("HeadAimTargetProxy");
        if (existing) { _targetProxy = existing; return; }

        var go = new GameObject("HeadAimTargetProxy");
        _targetProxy = go.transform;
        _targetProxy.SetParent(transform, false);
        _targetProxy.localPosition = Vector3.zero;
        _targetProxy.localRotation = Quaternion.identity;
    }

    Transform ResolveAimTargetProxy(Transform resolvedTarget)
    {
        if (!smoothTarget) { _lastResolvedTarget = resolvedTarget; return resolvedTarget; }

        EnsureTargetProxy();
        if (!resolvedTarget) { _lastResolvedTarget = null; return null; }

        if (resolvedTarget != _lastResolvedTarget)
        {
            if (snapProxyOnTargetChange) _targetProxy.position = resolvedTarget.position;
            _lastResolvedTarget = resolvedTarget;
        }

        float speed = Mathf.Max(0.01f, targetFollowSpeed);
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
        _targetProxy.position = Vector3.Lerp(_targetProxy.position, resolvedTarget.position, t);
        return _targetProxy;
    }

    // ================= HELPERS =================
    static Vector3 ToSignedEuler(Vector3 euler)
    {
        if (euler.x > 180f) euler.x -= 360f;
        if (euler.y > 180f) euler.y -= 360f;
        if (euler.z > 180f) euler.z -= 360f;
        return euler;
    }
    bool TryComputeClampedLocal(Transform aimTarget, out Vector3 aimPoint, out Vector3 driverWorldPos,
        out float clampedYaw, out float clampedPitch, out Quaternion clampedLocal)
    {
        driverWorldPos = _headParent.TransformPoint(_headDriverRestLocalPos);

        aimPoint = aimTarget.position;
        if (normalizeTargetHeight) aimPoint.y = driverWorldPos.y;
        aimPoint += lookOffset;

        Vector3 worldDir = aimPoint - driverWorldPos;
        if (worldDir.sqrMagnitude < 0.000001f)
        {
            clampedYaw = 0f;
            clampedPitch = 0f;
            clampedLocal = _headDriverRestLocalRot;
            return false;
        }

        // Compute direction in driver-rest space to avoid Euler-induced overshoot.
        Vector3 localDir = Quaternion.Inverse(_headParent.rotation) * worldDir;
        Vector3 restSpaceDir = Quaternion.Inverse(_headDriverRestLocalRot) * localDir;

        if (restSpaceDir.sqrMagnitude < 0.000001f)
        {
            clampedYaw = 0f;
            clampedPitch = 0f;
            clampedLocal = _headDriverRestLocalRot;
            return false;
        }

        restSpaceDir.Normalize();

        float yaw = Mathf.Atan2(restSpaceDir.x, restSpaceDir.z) * Mathf.Rad2Deg;
        float horiz = Mathf.Sqrt(restSpaceDir.x * restSpaceDir.x + restSpaceDir.z * restSpaceDir.z);
        float pitch = -Mathf.Atan2(restSpaceDir.y, horiz) * Mathf.Rad2Deg;

        clampedYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        clampedPitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        clampedLocal = _headDriverRestLocalRot * Quaternion.Euler(clampedPitch, clampedYaw, 0f);
        return true;
    }

    Transform FindActiveCamera()
    {
        if (Camera.main && Camera.main.isActiveAndEnabled) return Camera.main.transform;

        Camera best = null;
        float bestDepth = float.MinValue;
        foreach (var c in Camera.allCameras)
        {
            if (c.isActiveAndEnabled && c.depth > bestDepth)
            {
                best = c; bestDepth = c.depth;
            }
        }
        return best ? best.transform : null;
    }
}

