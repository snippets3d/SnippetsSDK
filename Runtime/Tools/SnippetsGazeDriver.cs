using System.Collections.Generic;
using UnityEngine;
using Snippets.Sdk;

[DefaultExecutionOrder(10000)]
public class SnippetsGazeDriver : MonoBehaviour
{
    public enum GazeMode { FollowTarget, LookInFront, Off }
    public enum TargetType { Transform, MainCamera, Actor, Forward }
    public enum EyeForwardAxis { ZPlus, ZMinus, XPlus, XMinus, YPlus, YMinus }
    public enum RpmCrossEyePreset { Male, Female }
    const float kVirtualLookInFrontHeight = 1.70f;
    const float kVirtualLookInFrontDistance = 2f;

    public GazeMode mode = GazeMode.FollowTarget;
    [Tooltip("How the main gaze target is resolved while gaze is active. Look Forward mirrors the Flow Controller's option and uses the driver's forward-target logic.")]
    public TargetType targetType = TargetType.Transform;

    // ================= TARGET =================
    public Transform target = null;
    public bool autoFindTarget = false;

    [Tooltip("Actor root or any transform under that actor. If this transform belongs to a character with a SnippetsGazeDriver, that rig is used to find a head or eye anchor.")]
    public Transform targetActor;

    [Tooltip("When following another actor, prefer their head bone or eye midpoint instead of their root when possible.")]
    public bool preferTargetActorHeadBone = true;

    [Tooltip("When following another actor with dynamic eye follow enabled, periodically alternates the eye fixation between that actor's eyes.")]
    public bool periodicallySwitchTargetActorEyes = false;

    [Tooltip("Random interval range in seconds between eye-target switches while following another actor.")]
    public Vector2 targetActorEyeSwitchIntervalRange = new Vector2(1.4f, 3.2f);

    [Tooltip("Optional runtime-only eye target override. If assigned, the head still uses Target while the eyes follow this override instead.")]
    public Transform eyeTargetOverride = null;

    // ================= TARGET SMOOTHING =================
    [Tooltip("Aim at a smoothed proxy that follows the target position over time (prevents snapping when target changes).")]
    public bool smoothTarget = true;

    [Tooltip("How fast the proxy follows the target. Higher = snappier, lower = floatier.")]
    public float targetFollowSpeed = 14f;

    [Tooltip("If true, when target changes, proxy snaps to new target once (avoids proxy flying across scene).")]
    public bool snapProxyOnTargetChange = true;

    // ================= RIG =================
    [Tooltip("Actual head bone (animated by legacy Animation). No driver objects will be inserted.")]
    public Transform headBone;

    [Tooltip("Bone that represents the waist/root of upper-body yaw (often Spine/Hips depending on rig).")]
    public Transform waistBone;

    [Tooltip("Direction reference for yaw (can be animated hips to avoid feedback). If null, uses a stable fallback captured at runtime.")]
    public Transform waistDirectionSource;

    // ================= EYES =================
    [Tooltip("Optional eye-bone tracking layered on top of the current head target. Disabled by default for backward compatibility.")]
    public bool enableDynamicEyeFollow = false;

    [Tooltip("Left eye bone used for procedural fixation.")]
    public Transform leftEyeBone;

    [Tooltip("Right eye bone used for procedural fixation.")]
    public Transform rightEyeBone;

    [Tooltip("Which local axis of the eye bones should be treated as forward when aiming.")]
    public EyeForwardAxis eyeForwardAxis = EyeForwardAxis.ZPlus;

    [Tooltip("Applies the built-in RPM eye correction profile for this avatar.")]
    public bool rpmCrossEyeCorrection = false;

    [Tooltip("Choose which built-in RPM eye correction profile to use.")]
    public RpmCrossEyePreset rpmCrossEyePreset = RpmCrossEyePreset.Male;

    [Tooltip("Pushes both eyes outward all the time to make the character read as less cross-eyed.")]
    public float antiCrossEyeOutwardYawOffset = 0f;

    [HideInInspector]
    public Vector3 leftEyeCorrectionOriginOffset = new Vector3(-0.0012f, 0f, 0.02f);

    [HideInInspector]
    public Vector3 rightEyeCorrectionOriginOffset = new Vector3(0.002f, 0f, 0.02f);

    [HideInInspector]
    public Vector2 leftEyeCorrectionAngleOffset = new Vector2(0f, 2f);

    [HideInInspector]
    public Vector2 rightEyeCorrectionAngleOffset = new Vector2(0f, -2f);

    [Range(0f, 1f)] public float eyeWeight = 1f;
    public float eyeBlendSpeed = 12f;
    public float eyeRotationSpeed = 24f;
    public float eyeMaxYaw = 30f;
    [Tooltip("Maximum amount the eyes can turn inward toward the nose.")]
    public float eyeMaxInwardYaw = 5f;
    public float eyeMaxPitch = 15f;

    [Tooltip("Adds small natural eye movements while the character is fixating on a target.")]
    public bool enableEyeSaccades = true;

    [Tooltip("Controls how strong eye saccades are. 0 disables the offset, 1 uses the default strength, 10 is a strong visible setting, and higher manual values still work.")]
    public float eyeSaccadeIntensity = 3f;

    [HideInInspector]
    [Tooltip("How often the eyes perform small fixation jumps while tracking.")]
    public Vector2 eyeMicroSaccadeIntervalRange = new Vector2(0.6f, 1.8f);

    [HideInInspector]
    [Tooltip("Angular size of the shared micro-saccade offsets in degrees.")]
    public Vector2 eyeMicroSaccadeAngleRange = new Vector2(0.15f, 0.8f);

    [HideInInspector]
    [Tooltip("Low-amplitude continuous fixation drift in degrees so the eyes never feel fully static.")]
    public float eyeFixationDriftAmplitude = 0.12f;

    [HideInInspector]
    [Tooltip("Speed of the continuous fixation drift.")]
    public float eyeFixationDriftFrequency = 0.45f;

    [Tooltip("Adds a subtle extra eyelid close when the eyes look downward, layered on top of the current animated blink or expression blendshape.")]
    public bool enableLookDownEyelidFollow = false;

    [Tooltip("Face mesh that contains the eyelid or blink blendshape.")]
    public SkinnedMeshRenderer faceMesh;

    [Tooltip("Shared blink or upper-eyelid-close blendshape index for both eyes. If assigned, this is used instead of separate left/right indices.")]
    public int eyelidBlendshape = -1;

    [Tooltip("When enabled, the look-down eyelid follow drives every configured shared blendshape below instead of just one.")]
    public bool useMultipleSharedEyelidBlendshapes = false;

    [Tooltip("Shared blink or upper-eyelid-close blendshape indices for both eyes. Used when Multiple Shared Eyelid Blendshapes is enabled.")]
    public List<int> sharedEyelidBlendshapes = new();

    [Tooltip("Left-eye eyelid-close blendshape index. Only used when Shared Eyelid Blendshape is -1.")]
    public int leftEyelidBlendshape = -1;

    [Tooltip("Right-eye eyelid-close blendshape index. Only used when Shared Eyelid Blendshape is -1.")]
    public int rightEyelidBlendshape = -1;

    [Tooltip("Eye-down angle in degrees at which the extra eyelid close starts.")]
    public float eyelidLookDownStartAngle = 0f;

    [Tooltip("Eye-down angle in degrees at which the extra eyelid close reaches full strength.")]
    public float eyelidLookDownFullAngle = 30f;

    [Tooltip("Maximum extra eyelid-close amount added by look-down follow, expressed as a percentage of the eyelid blendshape's authored max. Legacy values up to 1 are still treated as normalized fractions.")]
    [Range(0f, 100f)] public float eyelidLookDownMaxAdd = 30f;

    [Tooltip("How quickly the extra eyelid close responds as the gaze moves down or returns to neutral.")]
    public float eyelidLookDownFollowSpeed = 12f;

    [Tooltip("Draws Scene view gizmos for the head ray, eye rays, and their current head/eye targets when this object is selected.")]
    public bool debugDrawEyeGizmos = false;
    public float debugEyeGizmoLength = 0.35f;

    // ================= LOOK =================
    [Range(0f, 1f)] public float lookWeight = 1f;
    [Tooltip("Balances between preserving the animated head pose and aiming more precisely at the target. Lower values keep more animation influence. Higher values make the head track the target more exactly.")]
    [Range(0f, 1f)] public float headAimPrecision = 0.5f;
    public float blendSpeed = 10f;
    public float rotationSpeed = 6f;
    public float maxYaw = 50f;
    public float maxPitch = 30f;

    [Tooltip("Applied after any height normalization. Usually small values (e.g. Y=0.0..0.1).")]
    public Vector3 lookOffset = new Vector3(0f, -0.05f, 0f);

    [Tooltip("If true, aim point Y is normalized to THIS character's head driver height (reduces up/down looking).")]
    public bool normalizeTargetHeight = false;

    // ================= LOOK IN FRONT =================
    public bool useVirtualLookInFrontTarget = true;

    public float lookInFrontDistance = 2f;

    public Vector3 lookInFrontOffset = Vector3.zero;

    [Tooltip("Only used if fixedTargetOverride is not assigned. Prefer assigning the override to avoid GameObject.Find.")]
    public string fixedTargetName = "HeadTarget";
    public Transform fixedTargetOverride;

    [Tooltip("If enabled, refreshes cached named targets at an interval (avoids per-frame GameObject.Find).")]
    public bool autoRefreshTargets = true;
    public float refreshTargetsInterval = 0.5f;

    // ================= WAIST =================
    [Range(0f, 1f)] public float waistYawWeight = 0.8f;
    public float waistHeadYawThreshold = 15f;
    [Tooltip("Prevents the waist from rapidly toggling on/off when head yaw hovers near the engage threshold.")]
    public float waistYawThresholdHysteresis = 3f;
    [Tooltip("Keeps the waist from rapidly flipping left/right when the target is near directly behind the character.")]
    public float waistRearFlipGuardDegrees = 12f;
    public float waistDelay = 0f;
    public float waistEngageSpeed = 3.5f;
    public float waistMaxYaw = 25f;
    public float waistRotationSpeed = 6f;

    // ================= INTERNAL =================
    class EyeRuntime
    {
        public Transform bone;
        public Transform parent;
        public Vector3 restLocalPos;
        public Quaternion restLocalRot;
        public Quaternion localRot;
        public Quaternion animBaselineLocalRot;
        public Quaternion lastAppliedLocalRot;
        public bool hasLastAppliedLocalRot;
    }

    // Virtual head driver (NO hierarchy changes)
    Transform _headParent;
    Vector3 _headDriverRestLocalPos;
    Quaternion _headDriverRestLocalRot;
    Quaternion _headDriverLocalRot; // smoothed "driver" local rot
    Quaternion _headAnimBaselineLocalRot;
    Quaternion _lastAppliedHeadLocalRot;
    bool _hasLastAppliedHeadLocalRot;
    float _headWeight;

    // Target proxy
    Transform _targetProxy;
    Transform _lastResolvedTarget;
    bool _hasInitializedTargetProxyPosition;

    // Look-in-front cache
    Transform _cachedFixedTargetByName;
    Transform _virtualLookInFrontTarget;
    Transform _targetActorMidpointAnchor;
    Transform _currentTargetActorEye;
    float _nextRefreshTime;
    float _nextTargetActorEyeSwitchTime;

    // Waist reference fallback (stable ref like driver version)
    Transform _waistDirectionFallback;
    float _waistGateStart = -1f;
    float _waistEngage;
    float _waistYawSmoothed;
    bool _waistTrackingActive;
    Quaternion _waistAnimBaselineLocalRot;
    Quaternion _lastAppliedWaistLocalRot;
    bool _hasLastAppliedWaistLocalRot;

    // Eye follow
    EyeRuntime _leftEyeRuntime;
    EyeRuntime _rightEyeRuntime;
    float _eyeSystemWeight;
    Vector2 _eyeMicroOffsetDeg;
    Vector2 _eyeMicroTargetDeg;
    Vector2 _eyeMicroStartDeg;
    float _eyeMicroSaccadeTimer;
    float _eyeMicroSaccadeDuration;
    float _eyeMicroSaccadeElapsed;
    Vector2 _eyeNoiseSeed;
    Transform _lastEyeMotionTarget;
    float _sharedEyelidAddCurrent;
    float _leftEyelidAddCurrent;
    float _rightEyelidAddCurrent;
    float _lastAppliedSharedEyelidAdd;
    float _lastAppliedLeftEyelidAdd;
    float _lastAppliedRightEyelidAdd;
    readonly List<float> _sharedEyelidAddCurrents = new();
    readonly List<float> _lastAppliedSharedEyelidAdds = new();

    void Awake()
    {
        if (!headBone)
        {
            Debug.LogError("[SnippetsGazeDriver] headBone not assigned");
            enabled = false;
            return;
        }

        if (autoFindTarget) target = FindActiveCamera();

        _headParent = headBone.parent;
        if (!_headParent)
        {
            Debug.LogError("[SnippetsGazeDriver] headBone has no parent (cannot emulate driver space)");
            enabled = false;
            return;
        }

        // Cache what the REAL driver would have captured:
        // driver.localPosition = headBone.localPosition
        // driver.localRotation = headBone.localRotation
        _headDriverRestLocalPos = headBone.localPosition;
        _headDriverRestLocalRot = headBone.localRotation;
        _headDriverLocalRot = _headDriverRestLocalRot;
        _headAnimBaselineLocalRot = headBone.localRotation;
        UpgradeLegacyTargetSettings();

        // Waist direction fallback (stable reference like driver version)
        if (waistBone)
        {
            _waistDirectionFallback = waistBone.parent;
            _waistAnimBaselineLocalRot = waistBone.localRotation;
        }

        EnsureTargetProxy();
        RefreshFixedTargetByName();
        InitializeEyeRuntime();
        ResetEyeMotionState();
        _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshTargetsInterval);
    }

    void Update()
    {
        EnsureEyeRuntime();

        if (mode == GazeMode.FollowTarget && (autoFindTarget || targetType == TargetType.MainCamera))
        {
            if (!target || !target.gameObject.activeInHierarchy)
                target = FindActiveCamera();
        }

        if (autoRefreshTargets && Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshTargetsInterval);
            RefreshFixedTargetByName();
        }

        UpdateVirtualLookInFrontTargetLifecycle();
        UpdateActorEyeTargetLifecycle();
        UpdateEyeMotion();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        UpgradeLegacyTargetSettings();
        RefreshFixedTargetByName();
        DestroyTargetActorMidpointAnchor();
        DestroyVirtualLookInFrontTarget();
    }

    void OnDisable()
    {
        ClearLookDownEyelidWeights();
        DestroyTargetActorMidpointAnchor();
        DestroyVirtualLookInFrontTarget();
    }

    void LateUpdate()
    {
        CaptureAnimatedBaselines();

        Transform resolvedTarget = GetResolvedTarget();
        Transform aimTarget = ResolveAimTargetProxy(resolvedTarget);
        Transform resolvedEyeTarget = GetResolvedEyeTarget(aimTarget);

        float desiredW = (mode == GazeMode.Off || !aimTarget) ? 0f : lookWeight;
        _headWeight = Mathf.MoveTowards(_headWeight, desiredW, Time.deltaTime * blendSpeed);

        Quaternion animWaistWorld = GetAnimatedWaistBaselineWorld();

        if (_headWeight <= 0f || !aimTarget)
        {
            UpdateWaistFromAimPoint(animWaistWorld, Vector3.zero, 0f, hasTarget: false);
            StoreLastAppliedLocalRotations();
            UpdateDynamicEyeFollow(resolvedEyeTarget);
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
        {
            StoreLastAppliedLocalRotations();
            UpdateDynamicEyeFollow(resolvedEyeTarget);
            return;
        }

        // Apply waist BEFORE head so the head solve uses the final parent orientation (prevents overshoot).
        UpdateWaistFromAimPoint(animWaistWorld, aimPoint, clampedYawForWaist, hasTarget: true);

        Quaternion animHeadWorld = GetAnimatedHeadBaselineWorld();

        // Second pass: compute head rotation using updated parent orientation.
        float clampedYaw;
        float clampedPitch;
        Quaternion clampedLocal;
        if (!TryComputeClampedLocal(aimTarget, out aimPoint, out driverWorldPos,
            out clampedYaw, out clampedPitch, out clampedLocal))
        {
            StoreLastAppliedLocalRotations();
            UpdateDynamicEyeFollow(resolvedEyeTarget);
            return;
        }

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
        StoreLastAppliedLocalRotations();
        UpdateDynamicEyeFollow(resolvedEyeTarget);
    }


    // ================= WAIST FOLLOW (NO DRIVER OBJECTS) =================
    // Smoothly returns to animation when inside threshold (no snap).
    void UpdateWaistFromAimPoint(Quaternion animWaistWorld, Vector3 targetPoint, float headYawDeg, bool hasTarget)
    {
        if (!waistBone || waistYawWeight <= 0f) return;

        Transform src = waistDirectionSource ? waistDirectionSource :
            (_waistDirectionFallback ? _waistDirectionFallback :
            (waistBone.parent ? waistBone.parent : waistBone));

        float rawYawWorld = 0f;
        bool hasValidTargetYaw = false;
        if (hasTarget)
        {
            Vector3 fwd = src.forward; fwd.y = 0f;
            Vector3 toT = targetPoint - src.position; toT.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f && toT.sqrMagnitude > 0.0001f)
            {
                rawYawWorld = Vector3.SignedAngle(fwd.normalized, toT.normalized, Vector3.up);
                rawYawWorld = StabilizeRearHemisphereYaw(rawYawWorld);
                hasValidTargetYaw = true;
            }
        }

        float absYaw = hasValidTargetYaw ? Mathf.Abs(rawYawWorld) : 0f;
        float engageThreshold = Mathf.Max(0f, waistHeadYawThreshold);
        float disengageThreshold = Mathf.Max(0f, engageThreshold - Mathf.Max(0f, waistYawThresholdHysteresis));

        if (!hasValidTargetYaw)
        {
            _waistTrackingActive = false;
        }
        else if (_waistTrackingActive)
        {
            if (absYaw <= disengageThreshold)
                _waistTrackingActive = false;
        }
        else if (absYaw >= engageThreshold)
        {
            _waistTrackingActive = true;
            _waistGateStart = Time.time;
        }

        // If no target (or not enough head yaw), smoothly return to animation (no snap).
        if (!_waistTrackingActive)
        {
            _waistGateStart = -1f;
            _waistEngage = Mathf.MoveTowards(_waistEngage, 0f, Time.deltaTime * waistEngageSpeed);

            // Smooth the applied yaw back toward 0
            _waistYawSmoothed = Mathf.Lerp(_waistYawSmoothed, 0f, Time.deltaTime * waistRotationSpeed);

            // Still apply the fading yaw on top of this frame's animation baseline
            ApplyWaistYaw(animWaistWorld, _waistYawSmoothed);
            return;
        }

        if (_waistGateStart < 0f) _waistGateStart = Time.time;

        // During delay, keep current yaw applied (don't snap back to baseline)
        if (Time.time - _waistGateStart < waistDelay)
        {
            ApplyWaistYaw(animWaistWorld, _waistYawSmoothed);
            return;
        }

        _waistEngage = Mathf.MoveTowards(_waistEngage, 1f, Time.deltaTime * waistEngageSpeed);
        float yawWorld = Mathf.Clamp(rawYawWorld, -waistMaxYaw, waistMaxYaw);

        float desiredYaw = yawWorld * waistYawWeight * _waistEngage;
        _waistYawSmoothed = Mathf.Lerp(_waistYawSmoothed, desiredYaw, Time.deltaTime * waistRotationSpeed);

        // Apply absolute yaw onto this frame's animated baseline (no accumulation)
        ApplyWaistYaw(animWaistWorld, _waistYawSmoothed);
    }

    // ================= TARGET RESOLUTION =================
    void UpgradeLegacyTargetSettings()
    {
        if (mode == GazeMode.FollowTarget && autoFindTarget && targetType == TargetType.Transform && targetActor == null)
            targetType = TargetType.MainCamera;

        if (mode == GazeMode.LookInFront)
            targetType = TargetType.Forward;
    }

    Transform GetResolvedTarget()
    {
        if (mode == GazeMode.Off)
            return null;

        if (mode == GazeMode.LookInFront || targetType == TargetType.Forward)
            return ResolveLookInFrontTarget();

        if (mode != GazeMode.FollowTarget)
            return null;

        switch (mode)
        {
            case GazeMode.FollowTarget:
                switch (targetType)
                {
                    case TargetType.MainCamera:
                        return FindActiveCamera();
                    case TargetType.Actor:
                        return ResolveActorHeadTarget();
                    case TargetType.Forward:
                        return ResolveLookInFrontTarget();
                    case TargetType.Transform:
                    default:
                        return autoFindTarget ? FindActiveCamera() : target;
                }
            case GazeMode.Off:
            default: return null;
        }
    }

    Transform ResolveActorHeadTarget()
    {
        if (!targetActor)
            return null;

        var actorDriver = ResolveTargetActorDriver();
        if (!preferTargetActorHeadBone)
            return targetActor;

        if (actorDriver == null)
            return targetActor;

        if (actorDriver.headBone != null)
            return actorDriver.headBone;

        if (actorDriver.leftEyeBone != null && actorDriver.rightEyeBone != null)
            return ResolveOrUpdateTargetActorMidpointAnchor(actorDriver);

        return targetActor;
    }

    Transform ResolveLookInFrontTarget()
    {
        Transform legacyTarget = fixedTargetOverride ? fixedTargetOverride : _cachedFixedTargetByName;
        if (legacyTarget)
        {
            DestroyVirtualLookInFrontTarget();
            return legacyTarget;
        }

        EnsureVirtualLookInFrontTarget();
        UpdateVirtualLookInFrontTarget();
        return _virtualLookInFrontTarget;
    }

    void RefreshFixedTargetByName()
    {
        if (fixedTargetOverride) { _cachedFixedTargetByName = null; return; }
        if (string.IsNullOrEmpty(fixedTargetName)) { _cachedFixedTargetByName = null; return; }

        var go = GameObject.Find(fixedTargetName);
        _cachedFixedTargetByName = go ? go.transform : null;
    }

    void EnsureVirtualLookInFrontTarget()
    {
        Transform parent = GetVirtualLookInFrontParent();
        if (_virtualLookInFrontTarget && _virtualLookInFrontTarget.name == "VirtualLookInFrontTarget")
        {
            if (_virtualLookInFrontTarget.parent != parent)
                _virtualLookInFrontTarget.SetParent(parent, false);
            return;
        }

        var existing = parent.Find("VirtualLookInFrontTarget");
        if (existing)
        {
            _virtualLookInFrontTarget = existing;
            return;
        }

        var go = new GameObject("VirtualLookInFrontTarget");
        _virtualLookInFrontTarget = go.transform;
        _virtualLookInFrontTarget.SetParent(parent, false);
        _virtualLookInFrontTarget.localPosition = new Vector3(0f, kVirtualLookInFrontHeight, 0f);
        _virtualLookInFrontTarget.localRotation = Quaternion.identity;
    }

    void UpdateVirtualLookInFrontTarget()
    {
        if (!_virtualLookInFrontTarget)
            return;

        Vector3 localOffset = new Vector3(0f, kVirtualLookInFrontHeight, kVirtualLookInFrontDistance);
        _virtualLookInFrontTarget.localPosition = localOffset;
        _virtualLookInFrontTarget.localRotation = Quaternion.identity;
    }

    void UpdateVirtualLookInFrontTargetLifecycle()
    {
        if (!Application.isPlaying)
        {
            DestroyVirtualLookInFrontTarget();
            return;
        }

        bool shouldUseVirtualTarget =
            (mode == GazeMode.LookInFront || targetType == TargetType.Forward) &&
            fixedTargetOverride == null &&
            _cachedFixedTargetByName == null;

        if (!shouldUseVirtualTarget)
        {
            DestroyVirtualLookInFrontTarget();
            return;
        }

        EnsureVirtualLookInFrontTarget();
        UpdateVirtualLookInFrontTarget();
    }

    Transform GetVirtualLookInFrontParent()
    {
        var avatarPlayer = GetComponentInParent<SnippetAvatarPlayer>(true);
        return avatarPlayer != null ? avatarPlayer.transform : transform;
    }

    void DestroyVirtualLookInFrontTarget()
    {
        if (_virtualLookInFrontTarget == null)
            return;

        if (Application.isPlaying)
            Destroy(_virtualLookInFrontTarget.gameObject);
        else
            DestroyImmediate(_virtualLookInFrontTarget.gameObject);

        _virtualLookInFrontTarget = null;
    }

    void DestroyTargetActorMidpointAnchor()
    {
        if (_targetActorMidpointAnchor == null)
            return;

        if (Application.isPlaying)
            Destroy(_targetActorMidpointAnchor.gameObject);
        else
            DestroyImmediate(_targetActorMidpointAnchor.gameObject);

        _targetActorMidpointAnchor = null;
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
        if (!resolvedTarget) { _lastResolvedTarget = null; _hasInitializedTargetProxyPosition = false; return null; }

        if (!_hasInitializedTargetProxyPosition)
        {
            _targetProxy.position = resolvedTarget.position;
            _hasInitializedTargetProxyPosition = true;
            _lastResolvedTarget = resolvedTarget;
        }
        else if (resolvedTarget != _lastResolvedTarget)
        {
            if (snapProxyOnTargetChange) _targetProxy.position = resolvedTarget.position;
            _lastResolvedTarget = resolvedTarget;
        }

        float speed = Mathf.Max(0.01f, targetFollowSpeed);
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
        _targetProxy.position = Vector3.Lerp(_targetProxy.position, resolvedTarget.position, t);
        return _targetProxy;
    }

    void CaptureAnimatedBaselines()
    {
        // Capture the current animation pose every frame before we layer procedural gaze on top.
        // During clip crossfades and loop boundaries the animated pose can change subtly while still
        // remaining numerically close to the rotation we applied last frame, so gating this capture
        // behind an "approximately different" check can preserve a stale baseline and create pops.
        if (headBone)
        {
            Quaternion currentHeadLocal = headBone.localRotation;
            if (_hasLastAppliedHeadLocalRot &&
                Quaternion.Angle(currentHeadLocal, _lastAppliedHeadLocalRot) <= 0.01f)
            {
                _headAnimBaselineLocalRot =
                    _headDriverRestLocalRot *
                    Quaternion.Inverse(_headDriverLocalRot) *
                    currentHeadLocal;
            }
            else
            {
                _headAnimBaselineLocalRot = currentHeadLocal;
            }
        }

        if (waistBone)
        {
            Quaternion currentWaistLocal = waistBone.localRotation;
            if (_hasLastAppliedWaistLocalRot &&
                Quaternion.Angle(currentWaistLocal, _lastAppliedWaistLocalRot) <= 0.01f)
            {
                _waistAnimBaselineLocalRot =
                    currentWaistLocal *
                    Quaternion.Inverse(Quaternion.Euler(0f, _waistYawSmoothed, 0f));
            }
            else
            {
                _waistAnimBaselineLocalRot = currentWaistLocal;
            }
        }

        CaptureEyeAnimatedBaseline(_leftEyeRuntime);
        CaptureEyeAnimatedBaseline(_rightEyeRuntime);
    }

    void CaptureEyeAnimatedBaseline(EyeRuntime eyeRuntime)
    {
        if (eyeRuntime == null || !eyeRuntime.bone) return;

        Quaternion currentLocal = eyeRuntime.bone.localRotation;

        // If no animation system rewrote the eye this frame, the current local rotation
        // may still be the driver's previously applied pose. In that case, recover the
        // underlying animated baseline instead of recapturing our own output and compounding.
        if (eyeRuntime.hasLastAppliedLocalRot &&
            Quaternion.Angle(currentLocal, eyeRuntime.lastAppliedLocalRot) <= 0.01f)
        {
            Quaternion recoveredBaseline =
                eyeRuntime.restLocalRot *
                Quaternion.Inverse(eyeRuntime.localRot) *
                currentLocal;

            eyeRuntime.animBaselineLocalRot = recoveredBaseline;
            return;
        }

        eyeRuntime.animBaselineLocalRot = currentLocal;
    }

    Quaternion GetAnimatedWaistBaselineWorld()
    {
        if (!waistBone) return Quaternion.identity;
        if (!waistBone.parent) return _waistAnimBaselineLocalRot;
        return waistBone.parent.rotation * _waistAnimBaselineLocalRot;
    }

    Quaternion GetAnimatedHeadBaselineWorld()
    {
        if (headAimPrecision <= 0.0001f)
            return _headParent.rotation * _headAnimBaselineLocalRot;

        Quaternion relativeFromRest = Quaternion.Inverse(_headDriverRestLocalRot) * _headAnimBaselineLocalRot;
        Vector3 relativeEuler = ToSignedEuler(relativeFromRest.eulerAngles);

        // Preserve animated roll while letting procedural gaze own more of pitch/yaw.
        Quaternion rollOnlyLocal = _headDriverRestLocalRot * Quaternion.Euler(0f, 0f, relativeEuler.z);
        Quaternion precisionBaselineLocal = Quaternion.Slerp(_headAnimBaselineLocalRot, rollOnlyLocal, headAimPrecision);
        return _headParent.rotation * precisionBaselineLocal;
    }

    void ApplyWaistYaw(Quaternion animWaistWorld, float yawDegrees)
    {
        if (!waistBone) return;

        // Apply waist yaw in the waist bone's local animated space instead of world-up space.
        waistBone.rotation = animWaistWorld * Quaternion.Euler(0f, yawDegrees, 0f);
    }

    float StabilizeRearHemisphereYaw(float yawDegrees)
    {
        float guard = Mathf.Clamp(waistRearFlipGuardDegrees, 0f, 45f);
        if (guard <= 0.0001f) return yawDegrees;

        if (Mathf.Abs(Mathf.Abs(yawDegrees) - 180f) > guard)
            return yawDegrees;

        float preferredSign = Mathf.Sign(_waistYawSmoothed);
        if (Mathf.Approximately(preferredSign, 0f))
            preferredSign = Mathf.Sign(yawDegrees);
        if (Mathf.Approximately(preferredSign, 0f))
            preferredSign = 1f;

        return Mathf.Abs(yawDegrees) * preferredSign;
    }

    void StoreLastAppliedLocalRotations()
    {
        if (waistBone)
        {
            _lastAppliedWaistLocalRot = waistBone.localRotation;
            _hasLastAppliedWaistLocalRot = true;
        }

        if (headBone)
        {
            _lastAppliedHeadLocalRot = headBone.localRotation;
            _hasLastAppliedHeadLocalRot = true;
        }
    }

    void StoreLastAppliedEyeLocalRotation(EyeRuntime eyeRuntime)
    {
        if (eyeRuntime == null || !eyeRuntime.bone) return;

        eyeRuntime.lastAppliedLocalRot = eyeRuntime.bone.localRotation;
        eyeRuntime.hasLastAppliedLocalRot = true;
    }

    // ================= EYE FOLLOW =================
    void InitializeEyeRuntime()
    {
        _leftEyeRuntime = CreateEyeRuntime(leftEyeBone);
        _rightEyeRuntime = CreateEyeRuntime(rightEyeBone);
    }

    EyeRuntime CreateEyeRuntime(Transform eyeBone)
    {
        if (!eyeBone || !eyeBone.parent) return null;

        return new EyeRuntime
        {
            bone = eyeBone,
            parent = eyeBone.parent,
            restLocalPos = eyeBone.localPosition,
            restLocalRot = eyeBone.localRotation,
            localRot = eyeBone.localRotation,
            animBaselineLocalRot = eyeBone.localRotation
        };
    }

    void EnsureEyeRuntime()
    {
        bool leftMismatch = (_leftEyeRuntime == null && leftEyeBone) || (_leftEyeRuntime != null && _leftEyeRuntime.bone != leftEyeBone);
        bool rightMismatch = (_rightEyeRuntime == null && rightEyeBone) || (_rightEyeRuntime != null && _rightEyeRuntime.bone != rightEyeBone);

        if (leftMismatch || rightMismatch)
            InitializeEyeRuntime();
    }

    void ResetEyeMotionState()
    {
        _eyeMicroOffsetDeg = Vector2.zero;
        _eyeMicroTargetDeg = Vector2.zero;
        _eyeMicroStartDeg = Vector2.zero;
        _eyeNoiseSeed = new Vector2(Random.value * 10f, Random.value * 10f + 19.37f);
        _eyeMicroSaccadeTimer = 0f;
        _eyeMicroSaccadeDuration = 0f;
        _eyeMicroSaccadeElapsed = 0f;
        _lastEyeMotionTarget = null;
    }

    void UpdateEyeMotion()
    {
        eyeMicroSaccadeIntervalRange = GetSanitizedEyeMicroSaccadeIntervalRange();
        eyeMicroSaccadeAngleRange = GetSanitizedEyeMicroSaccadeAngleRange();
        eyeFixationDriftAmplitude = SanitizeFiniteNonNegative(eyeFixationDriftAmplitude, 0f);
        eyeFixationDriftFrequency = SanitizeFiniteMin(eyeFixationDriftFrequency, 0.01f, 0.45f);

        bool hasEyeRig = _leftEyeRuntime != null || _rightEyeRuntime != null;
        Transform resolvedEyeTarget = GetResolvedEyeTargetForMotionState();
        bool hasTarget = mode != GazeMode.Off && resolvedEyeTarget != null;
        bool shouldAnimateSaccades = enableDynamicEyeFollow && enableEyeSaccades && hasEyeRig && hasTarget;

        if (resolvedEyeTarget != _lastEyeMotionTarget)
        {
            _lastEyeMotionTarget = resolvedEyeTarget;
            _eyeMicroOffsetDeg = Vector2.zero;
            _eyeMicroTargetDeg = Vector2.zero;
            _eyeMicroStartDeg = Vector2.zero;
            _eyeMicroSaccadeTimer = 0f;
            _eyeMicroSaccadeDuration = 0f;
            _eyeMicroSaccadeElapsed = 0f;

            if (debugDrawEyeGizmos)
                Debug.Log($"[SnippetsGazeDriver] {name} eye target changed, resetting saccade state to follow {resolvedEyeTarget?.name ?? "null"}.");
        }

        if (!IsFinite(_eyeMicroOffsetDeg))
            _eyeMicroOffsetDeg = Vector2.zero;
        if (!IsFinite(_eyeMicroTargetDeg))
            _eyeMicroTargetDeg = Vector2.zero;

        if (!shouldAnimateSaccades)
        {
            if (_eyeMicroTargetDeg != Vector2.zero)
            {
                _eyeMicroStartDeg = _eyeMicroOffsetDeg;
                _eyeMicroSaccadeDuration = 0.08f;
                _eyeMicroSaccadeElapsed = 0f;
            }
            _eyeMicroTargetDeg = Vector2.zero;
        }
        else
        {
            _eyeMicroSaccadeTimer -= Time.deltaTime;
            if (_eyeMicroSaccadeTimer <= 0f)
            {
                _eyeMicroStartDeg = _eyeMicroOffsetDeg;
                _eyeMicroTargetDeg = PickMicroSaccadeOffsetDeg(out _eyeMicroSaccadeDuration);
                _eyeMicroSaccadeElapsed = 0f;
                if (!IsFinite(_eyeMicroTargetDeg))
                {
                    if (debugDrawEyeGizmos)
                        Debug.LogWarning($"[SnippetsGazeDriver] {name} produced invalid saccade target {_eyeMicroTargetDeg}; resetting to zero.");
                    _eyeMicroTargetDeg = Vector2.zero;
                }

                _eyeMicroSaccadeTimer = PickMicroSaccadeIntervalSeconds();

                if (debugDrawEyeGizmos)
                    Debug.Log($"[SnippetsGazeDriver] {name} picked saccade target offset yaw={_eyeMicroTargetDeg.x:F2} pitch={_eyeMicroTargetDeg.y:F2} duration={_eyeMicroSaccadeDuration * 1000f:F0}ms next={_eyeMicroSaccadeTimer:F2}s.");
            }
        }

        _eyeMicroSaccadeElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.0001f, _eyeMicroSaccadeDuration);
        float t = Mathf.Clamp01(_eyeMicroSaccadeElapsed / duration);
        t = t * t * (3f - (2f * t));
        _eyeMicroOffsetDeg = Vector2.Lerp(_eyeMicroStartDeg, _eyeMicroTargetDeg, t);
        if (!IsFinite(_eyeMicroOffsetDeg))
            _eyeMicroOffsetDeg = Vector2.zero;
    }

    Vector2 PickMicroSaccadeOffsetDeg(out float durationSeconds)
    {
        Vector2 amplitudeRangeDeg;
        Vector2 durationRangeSeconds;
        GetMicroSaccadeProfile(out amplitudeRangeDeg, out durationRangeSeconds);

        float amplitude = Random.Range(amplitudeRangeDeg.x, amplitudeRangeDeg.y) * GetEyeSaccadeIntensityScale();
        float maxSafeAmplitude = GetSafeEyeSaccadeAmplitudeLimit();
        amplitude = Mathf.Min(amplitude, maxSafeAmplitude);
        durationSeconds = Random.Range(durationRangeSeconds.x, durationRangeSeconds.y);
        if (!float.IsFinite(amplitude) || amplitude <= 0.0001f) return Vector2.zero;
        if (!float.IsFinite(durationSeconds) || durationSeconds <= 0.0001f)
            durationSeconds = 0.03f;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 result = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * amplitude;
        return IsFinite(result) ? result : Vector2.zero;
    }

    float PickMicroSaccadeIntervalSeconds()
    {
        Vector2 intervalRange = GetSanitizedEyeMicroSaccadeIntervalRange();
        return Random.Range(intervalRange.x, intervalRange.y);
    }

    void GetMicroSaccadeProfile(out Vector2 amplitudeRangeDeg, out Vector2 durationRangeSeconds)
    {
        float sample = Random.value;
        if (sample < 0.72f)
        {
            amplitudeRangeDeg = new Vector2(0.05f, 0.20f);
            durationRangeSeconds = new Vector2(0.025f, 0.040f);
            return;
        }

        if (sample < 0.96f)
        {
            amplitudeRangeDeg = new Vector2(0.20f, 0.50f);
            durationRangeSeconds = new Vector2(0.035f, 0.055f);
            return;
        }

        // Rare corrective fixational jumps should stay near the microsaccade regime
        // rather than becoming full gaze shifts.
        amplitudeRangeDeg = new Vector2(0.50f, 1.00f);
        durationRangeSeconds = new Vector2(0.045f, 0.070f);
    }

    float GetEyeSaccadeIntensityScale()
    {
        return SanitizeFiniteNonNegative(eyeSaccadeIntensity, 1f);
    }

    float GetSafeEyeSaccadeAmplitudeLimit()
    {
        float maxYaw = SanitizeFiniteNonNegative(eyeMaxYaw, 30f);
        float maxPitch = SanitizeFiniteNonNegative(eyeMaxPitch, 18f);
        return Mathf.Max(0.5f, Mathf.Min(maxYaw, maxPitch) * 0.35f);
    }

    void UpdateDynamicEyeFollow(Transform aimTarget)
    {
        float desiredWeight = (enableDynamicEyeFollow && aimTarget && (_leftEyeRuntime != null || _rightEyeRuntime != null))
            ? eyeWeight
            : 0f;

        _eyeSystemWeight = Mathf.MoveTowards(
            _eyeSystemWeight,
            desiredWeight,
            Time.deltaTime * Mathf.Max(0.01f, eyeBlendSpeed)
        );

        ApplyEyeFollow(_leftEyeRuntime, aimTarget);
        ApplyEyeFollow(_rightEyeRuntime, aimTarget);
        StoreLastAppliedEyeLocalRotation(_leftEyeRuntime);
        StoreLastAppliedEyeLocalRotation(_rightEyeRuntime);
        UpdateLookDownEyelids();
    }

    Transform GetResolvedEyeTarget()
    {
        return GetResolvedEyeTarget(GetResolvedTarget());
    }

    Transform GetResolvedEyeTargetForMotionState()
    {
        Transform resolvedTarget = GetResolvedTarget();
        if (eyeTargetOverride != null)
            return eyeTargetOverride;

        Transform actorEyeTarget = ResolveActorEyeTarget();
        if (actorEyeTarget != null)
            return actorEyeTarget;

        if (!smoothTarget || resolvedTarget == null)
            return resolvedTarget;

        EnsureTargetProxy();
        return _targetProxy;
    }

    Transform GetResolvedEyeTarget(Transform defaultTarget)
    {
        if (mode == GazeMode.Off)
            return null;

        if (eyeTargetOverride != null)
            return eyeTargetOverride;

        Transform actorEyeTarget = ResolveActorEyeTarget();
        if (actorEyeTarget != null)
            return actorEyeTarget;

        return defaultTarget;
    }

    void UpdateActorEyeTargetLifecycle()
    {
        if (mode == GazeMode.Off || mode == GazeMode.LookInFront || targetType != TargetType.Actor || eyeTargetOverride != null || !periodicallySwitchTargetActorEyes)
        {
            _currentTargetActorEye = null;
            return;
        }

        var actorDriver = ResolveTargetActorDriver();
        if (actorDriver == null)
        {
            _currentTargetActorEye = null;
            return;
        }

        bool hasLeft = actorDriver.leftEyeBone != null;
        bool hasRight = actorDriver.rightEyeBone != null;
        if (!hasLeft && !hasRight)
        {
            _currentTargetActorEye = null;
            return;
        }

        targetActorEyeSwitchIntervalRange.x = Mathf.Max(0.1f, targetActorEyeSwitchIntervalRange.x);
        targetActorEyeSwitchIntervalRange.y = Mathf.Max(targetActorEyeSwitchIntervalRange.x, targetActorEyeSwitchIntervalRange.y);

        if (!hasLeft || !hasRight)
        {
            _currentTargetActorEye = null;
            return;
        }

        if (_currentTargetActorEye == null ||
            (_currentTargetActorEye != actorDriver.leftEyeBone && _currentTargetActorEye != actorDriver.rightEyeBone))
        {
            _currentTargetActorEye = ResolvePreferredEyeTarget(actorDriver, null);
            _nextTargetActorEyeSwitchTime = Time.time + Random.Range(targetActorEyeSwitchIntervalRange.x, targetActorEyeSwitchIntervalRange.y);
            return;
        }

        if (Time.time < _nextTargetActorEyeSwitchTime)
            return;

        _currentTargetActorEye = ResolvePreferredEyeTarget(actorDriver, _currentTargetActorEye);
        _nextTargetActorEyeSwitchTime = Time.time + Random.Range(targetActorEyeSwitchIntervalRange.x, targetActorEyeSwitchIntervalRange.y);
    }

    Transform ResolveActorEyeTarget()
    {
        if (mode == GazeMode.Off || mode == GazeMode.LookInFront || targetType != TargetType.Actor)
            return null;

        if (!periodicallySwitchTargetActorEyes)
            return null;

        var actorDriver = ResolveTargetActorDriver();
        if (actorDriver == null)
            return null;

        bool hasLeft = actorDriver.leftEyeBone != null;
        bool hasRight = actorDriver.rightEyeBone != null;
        if (!hasLeft && !hasRight)
            return null;

        if (!hasLeft) return actorDriver.rightEyeBone;
        if (!hasRight) return actorDriver.leftEyeBone;

        return _currentTargetActorEye != null ? _currentTargetActorEye : ResolvePreferredEyeTarget(actorDriver, null);
    }

    SnippetsGazeDriver ResolveTargetActorDriver()
    {
        if (!targetActor)
            return null;

        return targetActor.GetComponentInParent<SnippetsGazeDriver>() ??
            targetActor.GetComponentInChildren<SnippetsGazeDriver>(true);
    }

    Transform ResolveOrUpdateTargetActorMidpointAnchor(SnippetsGazeDriver actorDriver)
    {
        if (actorDriver == null || actorDriver.leftEyeBone == null || actorDriver.rightEyeBone == null)
            return null;

        if (_targetActorMidpointAnchor == null)
        {
            var go = new GameObject("TargetActorGazeMidpointAnchor");
            go.hideFlags = HideFlags.HideInHierarchy;
            _targetActorMidpointAnchor = go.transform;
        }

        Transform parent = actorDriver.headBone != null ? actorDriver.headBone : actorDriver.transform;
        if (_targetActorMidpointAnchor.parent != parent)
            _targetActorMidpointAnchor.SetParent(parent, false);

        Vector3 worldMidpoint = (actorDriver.leftEyeBone.position + actorDriver.rightEyeBone.position) * 0.5f;
        _targetActorMidpointAnchor.localPosition = parent.InverseTransformPoint(worldMidpoint);
        _targetActorMidpointAnchor.localRotation = Quaternion.identity;
        return _targetActorMidpointAnchor;
    }

    Transform ResolvePreferredEyeTarget(SnippetsGazeDriver actorDriver, Transform currentEyeTarget)
    {
        if (actorDriver == null)
            return null;

        bool hasLeft = actorDriver.leftEyeBone != null;
        bool hasRight = actorDriver.rightEyeBone != null;

        if (hasLeft && hasRight)
        {
            if (currentEyeTarget == actorDriver.leftEyeBone) return actorDriver.rightEyeBone;
            if (currentEyeTarget == actorDriver.rightEyeBone) return actorDriver.leftEyeBone;
            return Random.value < 0.5f ? actorDriver.leftEyeBone : actorDriver.rightEyeBone;
        }

        if (hasLeft) return actorDriver.leftEyeBone;
        if (hasRight) return actorDriver.rightEyeBone;
        return null;
    }

    void ApplyEyeFollow(EyeRuntime eyeRuntime, Transform aimTarget)
    {
        if (eyeRuntime == null || !eyeRuntime.bone || !eyeRuntime.parent) return;

        Quaternion animEyeWorld = eyeRuntime.parent.rotation * eyeRuntime.animBaselineLocalRot;
        Quaternion desiredLocal = eyeRuntime.restLocalRot;

        if (_eyeSystemWeight > 0f && aimTarget && TryComputeEyeClampedLocal(eyeRuntime, aimTarget.position, out Quaternion clampedLocal))
            desiredLocal = clampedLocal;

        Quaternion weightedLocal = Quaternion.Slerp(eyeRuntime.restLocalRot, desiredLocal, _eyeSystemWeight);
        eyeRuntime.localRot = Quaternion.Slerp(
            eyeRuntime.localRot,
            weightedLocal,
            Time.deltaTime * Mathf.Max(0.01f, eyeRotationSpeed)
        );

        Quaternion driverDeltaWorld =
            eyeRuntime.parent.rotation *
            eyeRuntime.localRot *
            Quaternion.Inverse(eyeRuntime.restLocalRot) *
            Quaternion.Inverse(eyeRuntime.parent.rotation);

        eyeRuntime.bone.rotation = driverDeltaWorld * animEyeWorld;
    }

    bool TryComputeEyeClampedLocal(EyeRuntime eyeRuntime, Vector3 baseTargetPoint, out Quaternion clampedLocal)
    {
        clampedLocal = eyeRuntime.restLocalRot;

        Vector3 visualOrigin = GetEyeVisualOriginWorld(eyeRuntime.bone);
        Vector3 worldDir = baseTargetPoint - visualOrigin;
        if (worldDir.sqrMagnitude < 0.000001f)
            return false;

        Vector3 localDir = Quaternion.Inverse(eyeRuntime.parent.rotation) * worldDir.normalized;
        Vector3 restSpaceDir = Quaternion.Inverse(eyeRuntime.restLocalRot) * localDir;
        if (restSpaceDir.sqrMagnitude < 0.000001f)
            return false;

        Quaternion eyeAxisToForwardSpace = GetEyeAxisAdjustment();

        Vector3 alignedDir = eyeAxisToForwardSpace * restSpaceDir.normalized;
        if (alignedDir.sqrMagnitude < 0.000001f)
            return false;

        alignedDir.Normalize();
        float yaw = Mathf.Atan2(alignedDir.x, alignedDir.z) * Mathf.Rad2Deg;
        float horiz = Mathf.Sqrt(alignedDir.x * alignedDir.x + alignedDir.z * alignedDir.z);
        float pitch = -Mathf.Atan2(alignedDir.y, horiz) * Mathf.Rad2Deg;

        Vector2 correctionOffset = GetEyeCorrectionAngleOffset(eyeRuntime.bone);
        if (IsFinite(correctionOffset))
        {
            pitch += correctionOffset.x;
            yaw += correctionOffset.y;
        }

        // Keep pure target following as the baseline, then layer small fixation
        // offsets directly on top of the solved yaw/pitch.
        Vector2 offsetDeg = GetEyeFixationOffsetDeg();
        yaw += offsetDeg.x;
        pitch -= offsetDeg.y;

        if (!float.IsFinite(yaw) || !float.IsFinite(pitch))
        {
            if (debugDrawEyeGizmos)
                Debug.LogWarning($"[SnippetsGazeDriver] {name} computed invalid eye yaw/pitch ({yaw}, {pitch}); falling back to rest pose.");
            return false;
        }

        float maxYaw = SanitizeFiniteNonNegative(eyeMaxYaw, 30f);
        float maxPitch = SanitizeFiniteNonNegative(eyeMaxPitch, 18f);
        float clampedYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        float clampedPitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        float maxInwardYaw = Mathf.Min(
            maxYaw,
            SanitizeFiniteNonNegative(eyeMaxInwardYaw, 5f)
        );

        if (eyeRuntime.bone == leftEyeBone)
            clampedYaw = Mathf.Min(clampedYaw, maxInwardYaw);
        else if (eyeRuntime.bone == rightEyeBone)
            clampedYaw = Mathf.Max(clampedYaw, -maxInwardYaw);

        if (!float.IsFinite(clampedYaw) || !float.IsFinite(clampedPitch))
        {
            if (debugDrawEyeGizmos)
                Debug.LogWarning($"[SnippetsGazeDriver] {name} computed invalid clamped eye yaw/pitch ({clampedYaw}, {clampedPitch}) from raw ({yaw}, {pitch}) and limits ({maxYaw}, {maxPitch}); falling back to rest pose.");
            return false;
        }

        Quaternion clampedAligned = Quaternion.Euler(clampedPitch, clampedYaw, 0f);
        Quaternion clampedRelative =
            Quaternion.Inverse(eyeAxisToForwardSpace) *
            clampedAligned *
            eyeAxisToForwardSpace;

        clampedLocal = eyeRuntime.restLocalRot * clampedRelative;
        return true;
    }

    Vector3 GetEyeAimPoint(Vector3 baseTargetPoint, Vector3 eyeWorldPos)
    {
        Vector3 toTarget = baseTargetPoint - eyeWorldPos;
        float distance = toTarget.magnitude;
        if (distance < 0.0001f) return baseTargetPoint;

        Transform reference = headBone ? headBone : transform;
        Vector2 offsetDeg = GetEyeFixationOffsetDeg();
        Quaternion offsetRot =
            Quaternion.AngleAxis(offsetDeg.x, reference.up) *
            Quaternion.AngleAxis(-offsetDeg.y, reference.right);

        Vector3 offsetDir = offsetRot * toTarget.normalized;
        return eyeWorldPos + offsetDir * distance;
    }

    Vector2 GetEyeFixationOffsetDeg()
    {
        if (!enableDynamicEyeFollow || !enableEyeSaccades)
            return Vector2.zero;

        float intensity = GetEyeSaccadeIntensityScale();
        if (intensity <= 0.0001f)
            return Vector2.zero;

        float driftAmplitude = Mathf.Clamp(SanitizeFiniteNonNegative(eyeFixationDriftAmplitude, 0.04f), 0f, 0.08f);
        float driftFrequency = Mathf.Clamp(SanitizeFiniteMin(eyeFixationDriftFrequency, 0.01f, 0.45f), 0.05f, 1.5f);

        float driftX = (Mathf.PerlinNoise(_eyeNoiseSeed.x, Time.time * driftFrequency) - 0.5f) * 2f * driftAmplitude * intensity;
        float driftY = (Mathf.PerlinNoise(_eyeNoiseSeed.y, Time.time * (driftFrequency * 1.17f)) - 0.5f) * 2f * driftAmplitude * intensity;

        Vector2 result = _eyeMicroOffsetDeg + new Vector2(driftX, driftY);
        if (!IsFinite(result))
            return Vector2.zero;

        float maxFixationOffset = GetSafeEyeSaccadeAmplitudeLimit();
        if (result.sqrMagnitude > maxFixationOffset * maxFixationOffset)
            result = result.normalized * maxFixationOffset;

        return result;
    }

    Vector2 GetSanitizedEyeMicroSaccadeIntervalRange()
    {
        float minInterval = Mathf.Clamp(
            SanitizeFiniteNonNegative(eyeMicroSaccadeIntervalRange.x, 0.45f),
            0.35f,
            1.2f
        );

        float rawMaxInterval = SanitizeFiniteNonNegative(eyeMicroSaccadeIntervalRange.y, 1.1f);
        float maxInterval = Mathf.Max(rawMaxInterval, minInterval + 0.15f, 1.1f);
        maxInterval = Mathf.Clamp(maxInterval, minInterval, 2f);
        return new Vector2(minInterval, maxInterval);
    }

    Vector2 GetSanitizedEyeMicroSaccadeAngleRange()
    {
        float minAngle = Mathf.Clamp(SanitizeFiniteNonNegative(eyeMicroSaccadeAngleRange.x, 0f), 0f, 0.75f);
        float baseMaxAngle = Mathf.Clamp(SanitizeFiniteNonNegative(eyeMicroSaccadeAngleRange.y, minAngle), minAngle, 1.25f);
        float maxAngle = Mathf.Clamp(baseMaxAngle, minAngle, 1.25f);
        return new Vector2(minAngle, maxAngle);
    }

    static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y);
    }

    static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    static float SanitizeFiniteNonNegative(float value, float fallback)
    {
        return float.IsFinite(value) ? Mathf.Max(0f, value) : fallback;
    }

    static float SanitizeFiniteMin(float value, float min, float fallback)
    {
        return float.IsFinite(value) ? Mathf.Max(min, value) : fallback;
    }

    static float SanitizeFiniteRange(float value, float min, float max, float fallback)
    {
        return float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }

    Quaternion GetEyeAxisAdjustment()
    {
        switch (eyeForwardAxis)
        {
            case EyeForwardAxis.ZMinus: return Quaternion.Euler(0f, 180f, 0f);
            case EyeForwardAxis.XPlus: return Quaternion.Euler(0f, -90f, 0f);
            case EyeForwardAxis.XMinus: return Quaternion.Euler(0f, 90f, 0f);
            case EyeForwardAxis.YPlus: return Quaternion.Euler(90f, 0f, 0f);
            case EyeForwardAxis.YMinus: return Quaternion.Euler(-90f, 0f, 0f);
            case EyeForwardAxis.ZPlus:
            default:
                return Quaternion.identity;
        }
    }

    Vector3 GetEyeForwardLocalAxis()
    {
        switch (eyeForwardAxis)
        {
            case EyeForwardAxis.ZMinus: return Vector3.back;
            case EyeForwardAxis.XPlus: return Vector3.right;
            case EyeForwardAxis.XMinus: return Vector3.left;
            case EyeForwardAxis.YPlus: return Vector3.up;
            case EyeForwardAxis.YMinus: return Vector3.down;
            case EyeForwardAxis.ZPlus:
            default:
                return Vector3.forward;
        }
    }

    Vector3 GetEyeVisualOriginWorld(Transform eyeBone)
    {
        if (!eyeBone)
            return Vector3.zero;

        return eyeBone.TransformPoint(GetEyeCorrectionOriginLocalOffset(eyeBone));
    }

    public bool TryGetEyeVisualOrigin(bool useLeftEye, out Vector3 worldPosition)
    {
        Transform eyeBone = useLeftEye ? leftEyeBone : rightEyeBone;
        if (eyeBone == null)
        {
            worldPosition = Vector3.zero;
            return false;
        }

        worldPosition = GetEyeVisualOriginWorld(eyeBone);
        return true;
    }

    public bool TryGetVisualEyeMidpoint(out Vector3 worldMidpoint)
    {
        bool hasLeft = TryGetEyeVisualOrigin(true, out Vector3 leftOrigin);
        bool hasRight = TryGetEyeVisualOrigin(false, out Vector3 rightOrigin);

        if (hasLeft && hasRight)
        {
            worldMidpoint = (leftOrigin + rightOrigin) * 0.5f;
            return true;
        }

        if (hasLeft)
        {
            worldMidpoint = leftOrigin;
            return true;
        }

        if (hasRight)
        {
            worldMidpoint = rightOrigin;
            return true;
        }

        worldMidpoint = Vector3.zero;
        return false;
    }

    void UpdateLookDownEyelids()
    {
        if (!HasValidLookDownEyelidSetup())
        {
            ClearLookDownEyelidWeights();
            return;
        }

        float followSpeed = Mathf.Max(0.01f, eyelidLookDownFollowSpeed);

        if (UsesSharedEyelidBlendshape())
        {
            RemoveAppliedBlendshape(faceMesh, leftEyelidBlendshape, ref _lastAppliedLeftEyelidAdd);
            RemoveAppliedBlendshape(faceMesh, rightEyelidBlendshape, ref _lastAppliedRightEyelidAdd);
            float sharedLookDown01 = Mathf.Max(ComputeEyeLookDown01(_leftEyeRuntime), ComputeEyeLookDown01(_rightEyeRuntime));

            if (UsesMultipleSharedEyelidBlendshapes())
            {
                RemoveAppliedBlendshape(faceMesh, eyelidBlendshape, ref _lastAppliedSharedEyelidAdd);
                ResetSharedEyelidState();
                ApplySharedAdditiveBlendshapes(sharedLookDown01, followSpeed);
            }
            else
            {
                ClearSharedAdditiveBlendshapeList();
                float sharedMaxAdd = GetConfiguredEyelidMaxAdd(faceMesh, eyelidBlendshape);
                float sharedTargetAdd = sharedLookDown01 * sharedMaxAdd;
                _sharedEyelidAddCurrent = Mathf.MoveTowards(_sharedEyelidAddCurrent, sharedTargetAdd, Time.deltaTime * followSpeed * Mathf.Max(0.01f, sharedMaxAdd));
                ApplyAdditiveBlendshape(faceMesh, eyelidBlendshape, _sharedEyelidAddCurrent, ref _lastAppliedSharedEyelidAdd);
            }

            ResetSplitEyelidState();
            return;
        }

        ClearSharedAdditiveBlendshapeList();
        RemoveAppliedBlendshape(faceMesh, eyelidBlendshape, ref _lastAppliedSharedEyelidAdd);
        float leftMaxAdd = GetConfiguredEyelidMaxAdd(faceMesh, leftEyelidBlendshape);
        float rightMaxAdd = GetConfiguredEyelidMaxAdd(faceMesh, rightEyelidBlendshape);
        float leftTargetAdd = ComputeEyeLookDown01(_leftEyeRuntime) * leftMaxAdd;
        float rightTargetAdd = ComputeEyeLookDown01(_rightEyeRuntime) * rightMaxAdd;

        _leftEyelidAddCurrent = Mathf.MoveTowards(_leftEyelidAddCurrent, leftTargetAdd, Time.deltaTime * followSpeed * Mathf.Max(0.01f, leftMaxAdd));
        _rightEyelidAddCurrent = Mathf.MoveTowards(_rightEyelidAddCurrent, rightTargetAdd, Time.deltaTime * followSpeed * Mathf.Max(0.01f, rightMaxAdd));

        ApplyAdditiveBlendshape(faceMesh, leftEyelidBlendshape, _leftEyelidAddCurrent, ref _lastAppliedLeftEyelidAdd);
        ApplyAdditiveBlendshape(faceMesh, rightEyelidBlendshape, _rightEyelidAddCurrent, ref _lastAppliedRightEyelidAdd);
        ResetSharedEyelidState();
    }

    bool HasValidLookDownEyelidSetup()
    {
        if (!enableLookDownEyelidFollow || faceMesh == null)
            return false;

        if (_leftEyeRuntime == null && _rightEyeRuntime == null)
            return false;

        if (eyelidLookDownMaxAdd <= 0f)
            return false;

        return UsesSharedEyelidBlendshape()
            ? HasValidSharedEyelidBlendshape()
            : IsValidBlendshapeIndex(faceMesh, leftEyelidBlendshape) || IsValidBlendshapeIndex(faceMesh, rightEyelidBlendshape);
    }

    bool UsesSharedEyelidBlendshape() => UsesMultipleSharedEyelidBlendshapes() || eyelidBlendshape >= 0;

    bool UsesMultipleSharedEyelidBlendshapes() =>
        useMultipleSharedEyelidBlendshapes &&
        sharedEyelidBlendshapes != null &&
        sharedEyelidBlendshapes.Count > 0;

    bool HasValidSharedEyelidBlendshape()
    {
        if (UsesMultipleSharedEyelidBlendshapes())
        {
            for (int i = 0; i < sharedEyelidBlendshapes.Count; i++)
            {
                if (IsValidBlendshapeIndex(faceMesh, sharedEyelidBlendshapes[i]))
                    return true;
            }

            return false;
        }

        return IsValidBlendshapeIndex(faceMesh, eyelidBlendshape);
    }

    float ComputeEyeLookDown01(EyeRuntime eyeRuntime)
    {
        if (eyeRuntime == null || !eyeRuntime.bone)
            return 0f;

        float fullAngle = Mathf.Max(eyelidLookDownStartAngle + 0.01f, eyelidLookDownFullAngle);
        float downAngle = ComputeEyeLookDownAngle(eyeRuntime);
        return Mathf.InverseLerp(eyelidLookDownStartAngle, fullAngle, downAngle);
    }

    float ComputeEyeLookDownAngle(EyeRuntime eyeRuntime)
    {
        if (eyeRuntime == null || !eyeRuntime.bone)
            return 0f;

        // Remove the current animation baseline first, then measure pitch in the
        // same corrected "eye forward" space that the gaze solver uses. Reading
        // raw local Euler X directly can misclassify convergence as looking down.
        Quaternion relativeLocal =
            Quaternion.Inverse(eyeRuntime.restLocalRot) *
            eyeRuntime.bone.localRotation *
            Quaternion.Inverse(eyeRuntime.animBaselineLocalRot) *
            eyeRuntime.restLocalRot;

        Quaternion eyeAxisToForwardSpace =
            GetEyeAxisAdjustment() *
            Quaternion.Inverse(GetRpmCrossEyeVisualAxisCorrection(eyeRuntime.bone));

        Vector3 alignedDir =
            (eyeAxisToForwardSpace * relativeLocal * Quaternion.Inverse(eyeAxisToForwardSpace)) *
            Vector3.forward;

        if (alignedDir.sqrMagnitude < 0.000001f)
            return 0f;

        alignedDir.Normalize();
        Vector3 neutralAlignedDir = GetEyeNeutralAlignedForward(eyeRuntime);
        float currentPitch = GetAlignedEyePitchDeg(alignedDir);
        float neutralPitch = GetAlignedEyePitchDeg(neutralAlignedDir);
        return Mathf.Max(0f, currentPitch - neutralPitch);
    }

    float GetAlignedEyePitchDeg(Vector3 alignedDir)
    {
        if (alignedDir.sqrMagnitude < 0.000001f)
            return 0f;

        alignedDir.Normalize();
        float horiz = Mathf.Sqrt(alignedDir.x * alignedDir.x + alignedDir.z * alignedDir.z);
        return -Mathf.Atan2(alignedDir.y, horiz) * Mathf.Rad2Deg;
    }

    Vector3 GetEyeNeutralAlignedForward(EyeRuntime eyeRuntime)
    {
        if (eyeRuntime == null || !eyeRuntime.parent)
            return Vector3.forward;

        Vector3 referenceWorldDir = GetNeutralEyeReferenceWorldDirection();
        if (referenceWorldDir.sqrMagnitude < 0.000001f)
            return Vector3.forward;

        Vector3 localReferenceDir = Quaternion.Inverse(eyeRuntime.parent.rotation) * referenceWorldDir.normalized;
        Vector3 restSpaceReferenceDir = Quaternion.Inverse(eyeRuntime.restLocalRot) * localReferenceDir;
        if (restSpaceReferenceDir.sqrMagnitude < 0.000001f)
            return Vector3.forward;

        Quaternion eyeAxisToForwardSpace =
            GetEyeAxisAdjustment() *
            Quaternion.Inverse(GetRpmCrossEyeVisualAxisCorrection(eyeRuntime.bone));

        Vector3 alignedNeutralDir = eyeAxisToForwardSpace * restSpaceReferenceDir.normalized;
        return alignedNeutralDir.sqrMagnitude > 0.000001f ? alignedNeutralDir.normalized : Vector3.forward;
    }

    Vector3 GetNeutralEyeReferenceWorldDirection()
    {
        if (headBone != null && _headParent != null)
            return (_headParent.rotation * _headDriverRestLocalRot) * Vector3.forward;

        if (headBone != null)
            return headBone.forward;

        return transform.forward;
    }

    void ApplyAdditiveBlendshape(SkinnedMeshRenderer smr, int blendshapeIndex, float desiredAdd, ref float lastAppliedAdd)
    {
        if (!IsValidBlendshapeIndex(smr, blendshapeIndex))
        {
            lastAppliedAdd = 0f;
            return;
        }

        float maxWeight = GetBlendshapeMaxWeight(smr, blendshapeIndex);
        float currentWeight = smr.GetBlendShapeWeight(blendshapeIndex);
        float baseWeight = Mathf.Max(0f, currentWeight - lastAppliedAdd);
        float finalWeight = Mathf.Clamp(baseWeight + Mathf.Max(0f, desiredAdd), 0f, maxWeight);
        smr.SetBlendShapeWeight(blendshapeIndex, finalWeight);
        lastAppliedAdd = finalWeight - baseWeight;
    }

    void ClearLookDownEyelidWeights()
    {
        if (faceMesh != null)
        {
            ClearSharedAdditiveBlendshapeList();
            RemoveAppliedBlendshape(faceMesh, eyelidBlendshape, ref _lastAppliedSharedEyelidAdd);
            RemoveAppliedBlendshape(faceMesh, leftEyelidBlendshape, ref _lastAppliedLeftEyelidAdd);
            RemoveAppliedBlendshape(faceMesh, rightEyelidBlendshape, ref _lastAppliedRightEyelidAdd);
        }

        ResetSharedEyelidState();
        ResetSharedAdditiveBlendshapeListState();
        ResetSplitEyelidState();
    }

    void ApplySharedAdditiveBlendshapes(float sharedLookDown01, float followSpeed)
    {
        if (!UsesMultipleSharedEyelidBlendshapes())
            return;

        EnsureSharedAdditiveBlendshapeState();

        for (int i = 0; i < sharedEyelidBlendshapes.Count; i++)
        {
            int blendshapeIndex = sharedEyelidBlendshapes[i];
            if (!IsValidBlendshapeIndex(faceMesh, blendshapeIndex))
            {
                _sharedEyelidAddCurrents[i] = 0f;
                _lastAppliedSharedEyelidAdds[i] = 0f;
                continue;
            }

            float sharedMaxAdd = GetConfiguredEyelidMaxAdd(faceMesh, blendshapeIndex);
            float sharedTargetAdd = sharedLookDown01 * sharedMaxAdd;
            float nextAdd = Mathf.MoveTowards(
                _sharedEyelidAddCurrents[i],
                sharedTargetAdd,
                Time.deltaTime * followSpeed * Mathf.Max(0.01f, sharedMaxAdd));

            _sharedEyelidAddCurrents[i] = nextAdd;
            float lastAppliedAdd = _lastAppliedSharedEyelidAdds[i];
            ApplyAdditiveBlendshape(faceMesh, blendshapeIndex, nextAdd, ref lastAppliedAdd);
            _lastAppliedSharedEyelidAdds[i] = lastAppliedAdd;
        }
    }

    void EnsureSharedAdditiveBlendshapeState()
    {
        int targetCount = sharedEyelidBlendshapes != null ? sharedEyelidBlendshapes.Count : 0;
        while (_sharedEyelidAddCurrents.Count < targetCount)
            _sharedEyelidAddCurrents.Add(0f);

        while (_lastAppliedSharedEyelidAdds.Count < targetCount)
            _lastAppliedSharedEyelidAdds.Add(0f);

        if (_sharedEyelidAddCurrents.Count > targetCount)
            _sharedEyelidAddCurrents.RemoveRange(targetCount, _sharedEyelidAddCurrents.Count - targetCount);

        if (_lastAppliedSharedEyelidAdds.Count > targetCount)
            _lastAppliedSharedEyelidAdds.RemoveRange(targetCount, _lastAppliedSharedEyelidAdds.Count - targetCount);
    }

    void ClearSharedAdditiveBlendshapeList()
    {
        if (faceMesh == null || sharedEyelidBlendshapes == null)
        {
            ResetSharedAdditiveBlendshapeListState();
            return;
        }

        int count = Mathf.Min(sharedEyelidBlendshapes.Count, _lastAppliedSharedEyelidAdds.Count);
        for (int i = 0; i < count; i++)
        {
            int blendshapeIndex = sharedEyelidBlendshapes[i];
            float lastAppliedAdd = _lastAppliedSharedEyelidAdds[i];
            RemoveAppliedBlendshape(faceMesh, blendshapeIndex, ref lastAppliedAdd);
            _lastAppliedSharedEyelidAdds[i] = lastAppliedAdd;
        }

        ResetSharedAdditiveBlendshapeListState();
    }

    void RemoveAppliedBlendshape(SkinnedMeshRenderer smr, int blendshapeIndex, ref float lastAppliedAdd)
    {
        if (!IsValidBlendshapeIndex(smr, blendshapeIndex) || lastAppliedAdd <= 0f)
        {
            lastAppliedAdd = 0f;
            return;
        }

        float currentWeight = smr.GetBlendShapeWeight(blendshapeIndex);
        smr.SetBlendShapeWeight(blendshapeIndex, Mathf.Max(0f, currentWeight - lastAppliedAdd));
        lastAppliedAdd = 0f;
    }

    bool IsValidBlendshapeIndex(SkinnedMeshRenderer smr, int blendshapeIndex)
    {
        if (smr == null || blendshapeIndex < 0)
            return false;

        Mesh mesh = smr.sharedMesh;
        return mesh != null && blendshapeIndex < mesh.blendShapeCount;
    }

    float GetConfiguredEyelidMaxAdd(SkinnedMeshRenderer smr, int blendshapeIndex)
    {
        float blendshapeMax = GetBlendshapeMaxWeight(smr, blendshapeIndex);
        float normalizedMaxAdd = eyelidLookDownMaxAdd <= 1f
            ? Mathf.Clamp01(eyelidLookDownMaxAdd)
            : Mathf.Clamp01(eyelidLookDownMaxAdd * 0.01f);
        return blendshapeMax * normalizedMaxAdd;
    }

    float GetBlendshapeMaxWeight(SkinnedMeshRenderer smr, int blendshapeIndex)
    {
        if (!IsValidBlendshapeIndex(smr, blendshapeIndex))
            return 100f;

        Mesh mesh = smr.sharedMesh;
        int frameCount = mesh.GetBlendShapeFrameCount(blendshapeIndex);
        if (frameCount > 0)
        {
            float frameWeight = mesh.GetBlendShapeFrameWeight(blendshapeIndex, frameCount - 1);
            if (frameWeight > 0.0001f)
                return frameWeight;
        }

        float currentWeight = smr.GetBlendShapeWeight(blendshapeIndex);
        return currentWeight > 1f ? Mathf.Max(1f, currentWeight) : 1f;
    }

    void ResetSharedEyelidState()
    {
        _sharedEyelidAddCurrent = 0f;
        _lastAppliedSharedEyelidAdd = 0f;
    }

    void ResetSharedAdditiveBlendshapeListState()
    {
        for (int i = 0; i < _sharedEyelidAddCurrents.Count; i++)
            _sharedEyelidAddCurrents[i] = 0f;

        for (int i = 0; i < _lastAppliedSharedEyelidAdds.Count; i++)
            _lastAppliedSharedEyelidAdds[i] = 0f;
    }

    void ResetSplitEyelidState()
    {
        _leftEyelidAddCurrent = 0f;
        _rightEyelidAddCurrent = 0f;
        _lastAppliedLeftEyelidAdd = 0f;
        _lastAppliedRightEyelidAdd = 0f;
    }

    Quaternion GetRpmCrossEyeVisualAxisCorrection(Transform eyeBone)
    {
        if (!rpmCrossEyeCorrection || !eyeBone)
            return Quaternion.identity;

        Vector2 angleOffset = GetEyeCorrectionAngleOffset(eyeBone);
        if (!IsFinite(angleOffset))
            return Quaternion.identity;
        return Quaternion.Euler(angleOffset.x, angleOffset.y, 0f);
    }

    Vector3 GetCorrectedEyeForwardLocalAxis(Transform eyeBone)
    {
        Quaternion correction = GetRpmCrossEyeVisualAxisCorrection(eyeBone);
        return (correction * GetEyeForwardLocalAxis()).normalized;
    }

    Vector3 GetCorrectedEyeForwardWorld(Transform eyeBone)
    {
        if (!eyeBone)
            return transform.forward;

        return eyeBone.TransformDirection(GetCorrectedEyeForwardLocalAxis(eyeBone)).normalized;
    }

    Vector3 GetEyeCorrectionOriginLocalOffset(Transform eyeBone)
    {
        if (!eyeBone)
            return Vector3.zero;

        if (!rpmCrossEyeCorrection)
            return Vector3.zero;

        if (eyeBone == leftEyeBone)
            return GetRpmCrossEyeOriginOffset(true);

        if (eyeBone == rightEyeBone)
            return GetRpmCrossEyeOriginOffset(false);

        return Vector3.zero;
    }

    Vector2 GetEyeCorrectionAngleOffset(Transform eyeBone)
    {
        if (!eyeBone)
            return Vector2.zero;

        Vector2 angleOffset = Vector2.zero;

        if (eyeBone == leftEyeBone)
        {
            if (rpmCrossEyeCorrection)
                angleOffset = GetRpmCrossEyeAngleOffset(true);
            angleOffset.y -= float.IsFinite(antiCrossEyeOutwardYawOffset) ? antiCrossEyeOutwardYawOffset : 0f;
            return angleOffset;
        }

        if (eyeBone == rightEyeBone)
        {
            if (rpmCrossEyeCorrection)
                angleOffset = GetRpmCrossEyeAngleOffset(false);
            angleOffset.y += float.IsFinite(antiCrossEyeOutwardYawOffset) ? antiCrossEyeOutwardYawOffset : 0f;
            return angleOffset;
        }

        return Vector2.zero;
    }

    public void ApplyRpmCrossEyePresetValues()
    {
        leftEyeCorrectionOriginOffset = GetRpmCrossEyeOriginOffset(true);
        rightEyeCorrectionOriginOffset = GetRpmCrossEyeOriginOffset(false);
        leftEyeCorrectionAngleOffset = GetRpmCrossEyeAngleOffset(true);
        rightEyeCorrectionAngleOffset = GetRpmCrossEyeAngleOffset(false);
    }

    Vector3 GetRpmCrossEyeOriginOffset(bool isLeftEye)
    {
        switch (rpmCrossEyePreset)
        {
            case RpmCrossEyePreset.Female:
                return isLeftEye
                    ? new Vector3(-0.0012f, 0f, 0.002f)
                    : new Vector3(0.0015f, 0f, 0.002f);

            case RpmCrossEyePreset.Male:
            default:
                return isLeftEye
                    ? new Vector3(-0.0012f, 0f, 0.02f)
                    : new Vector3(0.002f, 0f, 0.02f);
        }
    }

    Vector2 GetRpmCrossEyeAngleOffset(bool isLeftEye)
    {
        switch (rpmCrossEyePreset)
        {
            case RpmCrossEyePreset.Female:
                return isLeftEye ? new Vector2(0f, 3f) : new Vector2(0f, -3f);

            case RpmCrossEyePreset.Male:
            default:
                return isLeftEye ? new Vector2(0f, 2f) : new Vector2(0f, -2f);
        }
    }

    bool TryGetEyeDebugData(out Vector3 eyeCenter, out Vector3 averagedEyeForward, out Vector3 leftEyeOrigin, out Vector3 rightEyeOrigin)
    {
        eyeCenter = Vector3.zero;
        averagedEyeForward = Vector3.zero;
        leftEyeOrigin = Vector3.zero;
        rightEyeOrigin = Vector3.zero;

        int count = 0;
        if (leftEyeBone) count++;
        if (rightEyeBone) count++;

        if (count == 0)
            return false;

        if (leftEyeBone)
        {
            leftEyeOrigin = GetEyeVisualOriginWorld(leftEyeBone);
            averagedEyeForward += GetCorrectedEyeForwardWorld(leftEyeBone);
            eyeCenter += leftEyeOrigin;
        }

        if (rightEyeBone)
        {
            rightEyeOrigin = GetEyeVisualOriginWorld(rightEyeBone);
            averagedEyeForward += GetCorrectedEyeForwardWorld(rightEyeBone);
            eyeCenter += rightEyeOrigin;
        }

        eyeCenter /= count;
        averagedEyeForward = averagedEyeForward.sqrMagnitude > 0.0001f
            ? averagedEyeForward.normalized
            : transform.forward;

        return true;
    }

    Transform GetResolvedTargetForDebug()
    {
        if (mode == GazeMode.Off)
            return null;

        if (mode == GazeMode.LookInFront || targetType == TargetType.Forward)
        {
            if (Application.isPlaying)
                return ResolveLookInFrontTarget();

            RefreshFixedTargetByName();

            Transform legacyTarget = fixedTargetOverride ? fixedTargetOverride : _cachedFixedTargetByName;
            return legacyTarget;
        }

        if (targetType == TargetType.MainCamera || autoFindTarget)
            return FindActiveCamera();

        if (targetType == TargetType.Actor)
            return ResolveActorHeadTarget();

        return target;
    }

    void OnDrawGizmosSelected()
    {
        if (!debugDrawEyeGizmos) return;
        if (!TryGetEyeDebugData(out Vector3 eyeCenter, out Vector3 averagedEyeForward, out Vector3 leftEyeOrigin, out Vector3 rightEyeOrigin)) return;

        float lineLength = Mathf.Max(0.01f, debugEyeGizmoLength);
        Transform headDebugTarget = GetResolvedTargetForDebug();
        Transform eyeDebugTarget = Application.isPlaying ? GetResolvedEyeTarget() : headDebugTarget;

        if (headBone)
        {
            Gizmos.color = new Color(1f, 0.65f, 0.2f, 0.95f);
            Gizmos.DrawLine(headBone.position, headBone.position + headBone.forward * lineLength);

            if (headDebugTarget)
                Gizmos.DrawLine(headBone.position, headDebugTarget.position);
        }

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
        if (leftEyeBone)
            Gizmos.DrawLine(leftEyeOrigin, leftEyeOrigin + GetCorrectedEyeForwardWorld(leftEyeBone) * lineLength);

        if (rightEyeBone)
            Gizmos.DrawLine(rightEyeOrigin, rightEyeOrigin + GetCorrectedEyeForwardWorld(rightEyeBone) * lineLength);

        if (!leftEyeBone || !rightEyeBone)
            Gizmos.DrawLine(eyeCenter, eyeCenter + averagedEyeForward * lineLength);

        if (!eyeDebugTarget) return;

        Vector3 toTarget = eyeDebugTarget.position - eyeCenter;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Gizmos.color = new Color(0.35f, 1f, 0.35f, 0.9f);
        if (leftEyeBone)
            Gizmos.DrawLine(leftEyeOrigin, eyeDebugTarget.position);

        if (rightEyeBone)
            Gizmos.DrawLine(rightEyeOrigin, eyeDebugTarget.position);

        if (!leftEyeBone && !rightEyeBone)
            Gizmos.DrawLine(eyeCenter, eyeDebugTarget.position);

        Gizmos.DrawWireSphere(eyeDebugTarget.position, lineLength * 0.1f);

        if (headDebugTarget && headDebugTarget != eyeDebugTarget)
        {
            Gizmos.color = new Color(1f, 0.65f, 0.2f, 0.85f);
            Gizmos.DrawWireSphere(headDebugTarget.position, lineLength * 0.08f);
        }
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

