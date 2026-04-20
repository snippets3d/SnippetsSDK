using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SnippetsGazeFlowController : MonoBehaviour
{
    public enum UnspecifiedActorBehavior
    {
        [InspectorName("Keep Current Gaze")]
        KeepPrevious,
        [InspectorName("Turn Gaze Off")]
        SetOff,
        [InspectorName("Look At Camera")]
        LookAtMainCamera
    }

    public enum TargetType
    {
        [InspectorName("Turn Off")]
        None,
        [InspectorName("Object")]
        Transform,
        [InspectorName("Actor")]
        Actor,
        [InspectorName("Main Camera")]
        MainCamera,
        [InspectorName("Look Forward")]
        Forward // uses SnippetsGazeDriver.LookInFront (fixedTargetOverride)
    }

    public enum StepGazeMode
    {
        [InspectorName("Whole Step")]
        Simple,
        [InspectorName("Granular")]
        Granular
    }

    [Serializable]
    public class ActorGaze
    {
        [Tooltip("Which actor this gaze instruction applies to.")]
        public int actorIndex = 0;

        [Header("Target")]
        [Tooltip("How this actor's gaze target is resolved for this override.")]
        public TargetType targetType = TargetType.Transform;

        [Tooltip("Object to look at when Look At is set to Object.")]
        public Transform targetTransform;

        [Tooltip("Actor to look at when Look At is set to Actor.")]
        public int targetActorIndex = 0;

        [Tooltip("When looking at another actor, prefer that actor's head or eye anchor instead of their root when available.")]
        public bool preferTargetActorHeadBone = true;

        [Header("Look Forward")]
        [Tooltip("Legacy per-step override for Look Forward. If left empty, the actor's Gaze Driver settings are used.")]
        public Transform forwardTargetOverride;
    }

    [Serializable]
    public class GazeCue
    {
        [Tooltip("Optional label for readability in the Inspector.")]
        public string label;

        [Tooltip("When this timed change starts within the snippet (0 = start, 1 = end).")]
        [Range(0f, 1f)]
        public float percent = 0f;

        [Tooltip("Gaze changes applied when this timed change starts.")]
        public List<ActorGaze> overrides = new();
    }

    [Serializable]
    public class GazeStep
    {
        [Tooltip("Optional label for readability in Inspector.")]
        public string label;

        [HideInInspector]
        public string flowStepGuid;

        [Header("Timing")]
        [Tooltip("Choose between one gaze setup for the whole step or timed changes across the snippet.")]
        public StepGazeMode mode = StepGazeMode.Simple;

        [Header("Whole Step")]
        [Tooltip("A single set of gaze changes applied for the full step.")]
        public List<ActorGaze> overrides = new();

        [Header("Granular")]
        [Tooltip("More detailed gaze changes spread across the snippet duration.")]
        public List<GazeCue> cues = new();
    }

    [Tooltip("Flow controller whose steps this gaze plan follows.")]
    public SnippetsFlowController flow;

    [HideInInspector]
    [Tooltip("Actor registry used to resolve gaze drivers, actors, and anchors.")]
    public SnippetsActorRegistry registry;

    [Header("Gaze Per Step")]
    [Tooltip("Per-step gaze instructions aligned to the linked flow.")]
    public List<GazeStep> gazeSteps = new();

    [Header("Actor Eye Switching")]
    [Tooltip("When looking at another actor with dynamic eye follow enabled, periodically switches between that actor's eye bones.")]
    public bool periodicallySwitchTargetActorEyes = true;

    [Tooltip("Random interval range in seconds between eye-target switches when following another actor's eyes.")]
    public Vector2 targetActorEyeSwitchIntervalRange = new Vector2(1.4f, 3.2f);

    [Tooltip("What to do for actors that do not receive an override in the active step.")]
    public UnspecifiedActorBehavior unspecifiedActors = UnspecifiedActorBehavior.KeepPrevious;

    [Tooltip("Automatically resize and remap gaze steps when the linked flow step list changes in the Inspector.")]
    public bool autoSyncToFlowSteps = true;

    [Tooltip("Automatically copy readable labels from the linked flow controller.")]
    public bool autoLabelFromFlow = true;

    Coroutine _cueCo;
    int _activeStepIndex = -1;

    class ActiveEyeSwitchState
    {
        public SnippetsGazeDriver sourceHeadTurn;
        public SnippetsGazeDriver targetHeadTurn;
        public Transform currentEyeTarget;
        public float nextSwitchTime;
    }

    readonly Dictionary<int, ActiveEyeSwitchState> _actorEyeSwitchStates = new();
    readonly Dictionary<int, Transform> _targetActorMidpointAnchors = new();

    [NonSerialized] bool _hasValidatedFlowSignature;
    [NonSerialized] int _lastValidatedFlowSignature;

    void OnEnable()
    {
        SyncRegistryFromFlow();
        TryWire();
    }
    void OnDisable()
    {
        _actorEyeSwitchStates.Clear();
        _targetActorMidpointAnchors.Clear();
        Unwire();
    }

    void Update() => UpdateActorEyeSwitchStates();

    void OnValidate()
    {
        SyncRegistryFromFlow();

        if (!autoSyncToFlowSteps || flow == null)
        {
            _hasValidatedFlowSignature = false;
            return;
        }

        int flowSignature = ComputeEditorFlowSyncSignature();
        if (_hasValidatedFlowSignature && flowSignature == _lastValidatedFlowSignature)
            return;

        SyncToFlow(resize: true, relabel: autoLabelFromFlow, remap: true);
        _lastValidatedFlowSignature = flowSignature;
        _hasValidatedFlowSignature = true;
    }

    public SnippetsActorRegistry GetResolvedRegistry()
    {
        if (flow != null)
            return flow.registry;

        return registry;
    }

    void SyncRegistryFromFlow()
    {
        if (flow != null && registry != flow.registry)
            registry = flow.registry;
    }

    public void SyncNow() => SyncToFlow(resize: true, relabel: true, remap: true);

    public void MatchStepsFromFlow() => SyncToFlow(resize: true, relabel: false, remap: true);

    public void RelabelFromFlow()
    {
        if (flow == null || gazeSteps == null) return;
        for (int i = 0; i < gazeSteps.Count; i++)
            gazeSteps[i].label = flow.GetStepDisplayLabel(i);
    }

    void SyncToFlow(bool resize, bool relabel, bool remap)
    {
        if (flow == null) return;

        flow.EnsureStepGuids();

        int desired = flow.steps != null ? flow.steps.Count : 0;
        if (gazeSteps == null) gazeSteps = new List<GazeStep>();

        if (remap)
        {
            var oldSteps = new List<GazeStep>(gazeSteps);

            var mapByGuid = new Dictionary<string, GazeStep>();
            var legacyMapByNormLabel = new Dictionary<string, List<GazeStep>>();
            for (int i = 0; i < oldSteps.Count; i++)
            {
                var gs = oldSteps[i];
                if (gs == null) continue;

                if (!string.IsNullOrEmpty(gs.flowStepGuid) && !mapByGuid.ContainsKey(gs.flowStepGuid))
                    mapByGuid[gs.flowStepGuid] = gs;

                // Only use label fallback for legacy gaze steps that predate flowStepGuid linking.
                if (string.IsNullOrEmpty(gs.flowStepGuid))
                {
                    string n = NormalizeLabel(gs.label);
                    if (!string.IsNullOrEmpty(n))
                    {
                        if (!legacyMapByNormLabel.TryGetValue(n, out var list))
                            list = legacyMapByNormLabel[n] = new List<GazeStep>();
                        list.Add(gs);
                    }
                }
            }

            var usedSteps = new HashSet<GazeStep>();

            var newList = new List<GazeStep>(desired);
            for (int i = 0; i < desired; i++)
            {
                var step = flow.steps[i];
                string guid = step != null ? step.guid : null;

                GazeStep gs = null;

                if (!string.IsNullOrEmpty(guid) && mapByGuid.TryGetValue(guid, out var byGuid) && !usedSteps.Contains(byGuid))
                    gs = byGuid;

                if (gs == null)
                {
                    string flowLabel = flow.GetStepDisplayLabel(i);
                    string norm = NormalizeLabel(flowLabel);
                    if (!string.IsNullOrEmpty(norm) && legacyMapByNormLabel.TryGetValue(norm, out var list))
                    {
                        for (int k = 0; k < list.Count; k++)
                        {
                            var cand = list[k];
                            if (cand != null && !usedSteps.Contains(cand))
                            {
                                gs = cand;
                                break;
                            }
                        }
                    }
                }

                if (gs == null && i < oldSteps.Count)
                {
                    var cand = oldSteps[i];
                    if (cand != null && !usedSteps.Contains(cand) && string.IsNullOrEmpty(cand.flowStepGuid))
                        gs = cand; // legacy index-based mapping for pre-guid data only
                }

                gs ??= new GazeStep();

                if (step != null)
                {
                    gs.flowStepGuid = step.guid;
                }
                else
                {
                    gs.flowStepGuid = null;
                }

                newList.Add(gs);
                usedSteps.Add(gs);
            }

            gazeSteps = newList;
        }
        else if (resize)
        {
            while (gazeSteps.Count < desired) gazeSteps.Add(new GazeStep());
            while (gazeSteps.Count > desired) gazeSteps.RemoveAt(gazeSteps.Count - 1);
        }

        if (relabel)
        {
            for (int i = 0; i < gazeSteps.Count; i++)
                gazeSteps[i].label = flow.GetStepDisplayLabel(i);
        }
    }

    static string NormalizeLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return string.Empty;
        int colon = label.IndexOf(':');
        if (colon >= 0 && colon + 1 < label.Length)
            return label.Substring(colon + 1).Trim();
        return label.Trim();
    }

    int ComputeEditorFlowSyncSignature()
    {
        if (flow == null)
            return 0;

        flow.EnsureStepGuids();

        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + flow.GetInstanceID();
            hash = (hash * 31) + autoLabelFromFlow.GetHashCode();

            var flowSteps = flow.steps;
            int count = flowSteps != null ? flowSteps.Count : 0;
            hash = (hash * 31) + count;

            for (int i = 0; i < count; i++)
            {
                var step = flowSteps[i];
                hash = (hash * 31) + (step?.guid?.GetHashCode() ?? 0);

                if (autoLabelFromFlow)
                    hash = (hash * 31) + (flow.GetStepDisplayLabel(i)?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    void TryWire()
    {
        if (flow == null) return;
        Unwire();
        flow.StepStarted += OnFlowStepStarted;
        flow.StepFinished += OnFlowStepFinished;
    }

    void Unwire()
    {
        if (flow == null) return;
        flow.StepStarted -= OnFlowStepStarted;
        flow.StepFinished -= OnFlowStepFinished;
    }

    void OnFlowStepStarted(int stepIndex, SnippetsFlowController.Step step)
    {
        var actorRegistry = GetResolvedRegistry();
        if (actorRegistry == null) return;
        if (stepIndex < 0) return;
        if (gazeSteps == null || stepIndex >= gazeSteps.Count) return;

        _activeStepIndex = stepIndex;
        StopCueCoroutine();

        var gs = gazeSteps[stepIndex];
        if (gs == null) return;

        if (gs.mode == StepGazeMode.Simple)
            ApplyOverridesMap(gs.overrides);
        else
            _cueCo = StartCoroutine(RunPercentCues(stepIndex, step, gs));
    }

    void OnFlowStepFinished(int stepIndex, SnippetsFlowController.Step step)
    {
        if (_activeStepIndex == stepIndex)
        {
            StopCueCoroutine();
            _activeStepIndex = -1;
        }
    }

    void StopCueCoroutine()
    {
        if (_cueCo != null)
        {
            StopCoroutine(_cueCo);
            _cueCo = null;
        }
    }

    bool IsRunningStep(int stepIndex) => flow != null && _activeStepIndex == stepIndex;

    IEnumerator RunPercentCues(int stepIndex, SnippetsFlowController.Step step, GazeStep gs)
    {
        if (gs.cues == null || gs.cues.Count == 0)
        {
            ApplyOverridesMap(null);
            yield break;
        }

        float duration = EstimateSnippetDurationSeconds(step);

        var cues = new List<GazeCue>(gs.cues);
        cues.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.percent.CompareTo(b.percent);
        });

        float t0 = Time.time;

        foreach (var cue in cues)
        {
            if (!IsRunningStep(stepIndex)) yield break;
            if (cue == null) continue;

            float targetTime = t0 + Mathf.Clamp01(cue.percent) * duration;

            while (IsRunningStep(stepIndex) && Time.time < targetTime)
                yield return null;

            if (!IsRunningStep(stepIndex)) yield break;

            ApplyOverridesMap(cue.overrides);
            yield return null;
        }
    }

    float EstimateSnippetDurationSeconds(SnippetsFlowController.Step step)
    {
        if (flow != null)
            return flow.GetTimedStepDurationSeconds(step);

        var actorRegistry = GetResolvedRegistry();
        if (actorRegistry == null || step == null)
            return 0f;

        if (step.type != SnippetsFlowController.StepType.Snippet &&
            step.type != SnippetsFlowController.StepType.SnippetWithCustomAnim)
            return 0f;

        var actor = actorRegistry.GetActor(step.actorIndex);
        if (actor == null || actor.snippets == null || actor.snippets.Count == 0)
            return 0f;

        int snippetIndex = Mathf.Clamp(step.snippetIndex, 0, actor.snippets.Count - 1);
        var snippet = actor.snippets[snippetIndex];
        if (snippet == null || snippet.Value == null)
            return 0f;

        if (snippet.Value.Sound != null && snippet.Value.Sound.length > 0.001f)
            return snippet.Value.Sound.length;

        var clip = snippet.Value.Animation;
        if (clip != null && clip.length > 0.001f)
            return clip.length;

        return 0f;
    }

    // ================= APPLY =================

    void ApplyOverridesMap(List<ActorGaze> overrides)
    {
        var actorRegistry = GetResolvedRegistry();
        if (actorRegistry == null) return;

        int actorCount = actorRegistry.ActorCount;

        Dictionary<int, ActorGaze> map = null;
        if (overrides != null && overrides.Count > 0)
        {
            map = new Dictionary<int, ActorGaze>();
            for (int i = 0; i < overrides.Count; i++)
            {
                var og = overrides[i];
                if (og == null) continue;
                map[og.actorIndex] = og; // last wins per actor
            }
        }

        for (int actorIndex = 0; actorIndex < actorCount; actorIndex++)
        {
            var gazeDriver = actorRegistry.GetGazeDriver(actorIndex);
            if (gazeDriver == null) continue;

            if (map != null && map.TryGetValue(actorIndex, out var og) && og != null)
                ApplyOverride(actorIndex, gazeDriver, og);
            else
                ApplyUnspecifiedPolicy(actorIndex, gazeDriver);
        }
    }

    void ApplyOverride(int actorIndex, SnippetsGazeDriver headTurn, ActorGaze og)
    {
        if (headTurn == null) return;

        // We rely on SnippetsGazeDriver's own smoothing for blending.
        switch (og.targetType)
        {
            case TargetType.Forward:
                ClearActorEyeSwitchState(actorIndex);
                headTurn.mode = SnippetsGazeDriver.GazeMode.LookInFront;
                headTurn.targetType = SnippetsGazeDriver.TargetType.Forward;
                headTurn.target = null;
                headTurn.targetActor = null;
                headTurn.eyeTargetOverride = null;
                headTurn.autoFindTarget = false;
                if (og.forwardTargetOverride != null)
                    headTurn.fixedTargetOverride = og.forwardTargetOverride;
                return;

            case TargetType.None:
                ClearActorEyeSwitchState(actorIndex);
                headTurn.mode = SnippetsGazeDriver.GazeMode.Off;
                headTurn.target = null;
                headTurn.targetActor = null;
                headTurn.eyeTargetOverride = null;
                headTurn.autoFindTarget = false;
                return;

            default:
                ClearActorEyeSwitchState(actorIndex);
                headTurn.mode = SnippetsGazeDriver.GazeMode.FollowTarget;
                headTurn.targetType = ResolveDriverTargetType(og);
                headTurn.target = og.targetType == TargetType.Transform ? og.targetTransform : null;
                headTurn.targetActor = og.targetType == TargetType.Actor ? ResolveTargetActorRootTransform(og) : null;
                headTurn.preferTargetActorHeadBone = og.preferTargetActorHeadBone;
                headTurn.periodicallySwitchTargetActorEyes = periodicallySwitchTargetActorEyes;
                headTurn.targetActorEyeSwitchIntervalRange = targetActorEyeSwitchIntervalRange;
                headTurn.eyeTargetOverride = null;
                headTurn.autoFindTarget = (og.targetType == TargetType.MainCamera);
                return;
        }
    }

    void ApplyUnspecifiedPolicy(int actorIndex, SnippetsGazeDriver headTurn)
    {
        switch (unspecifiedActors)
        {
            case UnspecifiedActorBehavior.KeepPrevious:
                return;

            case UnspecifiedActorBehavior.SetOff:
                ClearActorEyeSwitchState(actorIndex);
                headTurn.mode = SnippetsGazeDriver.GazeMode.Off;
                headTurn.target = null;
                headTurn.targetActor = null;
                headTurn.eyeTargetOverride = null;
                headTurn.autoFindTarget = false;
                return;

            case UnspecifiedActorBehavior.LookAtMainCamera:
                ClearActorEyeSwitchState(actorIndex);
                headTurn.mode = SnippetsGazeDriver.GazeMode.FollowTarget;
                headTurn.targetType = SnippetsGazeDriver.TargetType.MainCamera;
                headTurn.target = Camera.main ? Camera.main.transform : null;
                headTurn.targetActor = null;
                headTurn.eyeTargetOverride = null;
                headTurn.autoFindTarget = true;
                return;
        }
    }

    SnippetsGazeDriver.TargetType ResolveDriverTargetType(ActorGaze og)
    {
        if (og == null)
            return SnippetsGazeDriver.TargetType.Transform;

        return og.targetType switch
        {
            TargetType.MainCamera => SnippetsGazeDriver.TargetType.MainCamera,
            TargetType.Actor => SnippetsGazeDriver.TargetType.Actor,
            TargetType.Forward => SnippetsGazeDriver.TargetType.Forward,
            _ => SnippetsGazeDriver.TargetType.Transform,
        };
    }

    Transform ResolveTargetActorRootTransform(ActorGaze og)
    {
        var actorRegistry = GetResolvedRegistry();
        if (og == null || og.targetType != TargetType.Actor || actorRegistry == null)
            return null;

        var targetActor = actorRegistry.GetActor(og.targetActorIndex);
        if (targetActor == null)
            return null;

        if (targetActor.player != null)
            return targetActor.player.transform;

        if (targetActor.gazeDriver != null)
            return targetActor.gazeDriver.transform;

        if (targetActor.walker != null)
            return targetActor.walker.transform;

        return null;
    }

    void ConfigureActorEyeSwitching(int actorIndex, SnippetsGazeDriver sourceHeadTurn, ActorGaze og)
    {
        if (sourceHeadTurn == null || og == null || og.targetType != TargetType.Actor)
            return;

        var targetHeadTurn = ResolveTargetActorHeadTurn(og);
        if (targetHeadTurn == null || targetHeadTurn.leftEyeBone == null || targetHeadTurn.rightEyeBone == null)
            return;

        var initialEyeTarget = ResolvePreferredEyeTarget(targetHeadTurn, null);
        if (initialEyeTarget == null)
            return;

        sourceHeadTurn.eyeTargetOverride = initialEyeTarget;
        _actorEyeSwitchStates[actorIndex] = new ActiveEyeSwitchState
        {
            sourceHeadTurn = sourceHeadTurn,
            targetHeadTurn = targetHeadTurn,
            currentEyeTarget = initialEyeTarget,
            nextSwitchTime = Time.time + RandomEyeSwitchInterval()
        };
    }

    void UpdateActorEyeSwitchStates()
    {
        if (_actorEyeSwitchStates.Count == 0) return;

        targetActorEyeSwitchIntervalRange.x = Mathf.Max(0.1f, targetActorEyeSwitchIntervalRange.x);
        targetActorEyeSwitchIntervalRange.y = Mathf.Max(targetActorEyeSwitchIntervalRange.x, targetActorEyeSwitchIntervalRange.y);

        List<int> toRemove = null;

        foreach (var kvp in _actorEyeSwitchStates)
        {
            int actorIndex = kvp.Key;
            var state = kvp.Value;

            if (state == null ||
                state.sourceHeadTurn == null ||
                state.targetHeadTurn == null ||
                state.sourceHeadTurn.mode != SnippetsGazeDriver.GazeMode.FollowTarget ||
                state.targetHeadTurn.leftEyeBone == null ||
                state.targetHeadTurn.rightEyeBone == null)
            {
                toRemove ??= new List<int>();
                toRemove.Add(actorIndex);
                continue;
            }

            if (Time.time < state.nextSwitchTime)
                continue;

            var nextEye = ResolvePreferredEyeTarget(state.targetHeadTurn, state.currentEyeTarget);
            if (nextEye == null)
            {
                toRemove ??= new List<int>();
                toRemove.Add(actorIndex);
                continue;
            }

            state.currentEyeTarget = nextEye;
            state.sourceHeadTurn.eyeTargetOverride = nextEye;
            state.nextSwitchTime = Time.time + RandomEyeSwitchInterval();
        }

        if (toRemove == null) return;
        for (int i = 0; i < toRemove.Count; i++)
            _actorEyeSwitchStates.Remove(toRemove[i]);
    }

    void ClearActorEyeSwitchState(int actorIndex)
    {
        if (_actorEyeSwitchStates.TryGetValue(actorIndex, out var state) && state != null && state.sourceHeadTurn != null)
            state.sourceHeadTurn.eyeTargetOverride = null;

        _actorEyeSwitchStates.Remove(actorIndex);
    }

    float RandomEyeSwitchInterval()
    {
        return UnityEngine.Random.Range(targetActorEyeSwitchIntervalRange.x, targetActorEyeSwitchIntervalRange.y);
    }

    Transform ResolveFollowTargetTransform(ActorGaze og)
    {
        var actorRegistry = GetResolvedRegistry();

        switch (og.targetType)
        {
            case TargetType.Transform:
                return og.targetTransform;

            case TargetType.MainCamera:
                return Camera.main ? Camera.main.transform : null;

            case TargetType.Actor:
            {
                var targetHeadTurn = ResolveTargetActorHeadTurn(og);
                var targetActor = actorRegistry != null ? actorRegistry.GetActor(og.targetActorIndex) : null;
                if (targetActor == null) return null;

                var targetAnchor = ResolveTargetActorAnchor(og, targetHeadTurn);
                if (targetAnchor != null)
                    return targetAnchor;

                if (targetActor.player != null)
                    return targetActor.player.transform;

                if (targetActor.walker != null)
                    return targetActor.walker.transform;

                return null;
            }
        }
        return null;
    }

    SnippetsGazeDriver ResolveTargetActorHeadTurn(ActorGaze og)
    {
        var actorRegistry = GetResolvedRegistry();
        if (og == null || og.targetType != TargetType.Actor || actorRegistry == null)
            return null;

        var targetActor = actorRegistry.GetActor(og.targetActorIndex);
        return targetActor != null ? targetActor.gazeDriver : null;
    }

    Transform ResolveTargetActorAnchor(ActorGaze og, SnippetsGazeDriver targetHeadTurn)
    {
        if (og == null || GetResolvedRegistry() == null)
            return null;

        if (targetHeadTurn == null)
            return null;

        if (targetHeadTurn.headBone != null)
            return targetHeadTurn.headBone;

        if (targetHeadTurn.leftEyeBone != null && targetHeadTurn.rightEyeBone != null)
            return ResolveOrUpdateTargetActorMidpointAnchor(og.targetActorIndex, targetHeadTurn);

        return null;
    }

    Transform ResolveOrUpdateTargetActorMidpointAnchor(int actorIndex, SnippetsGazeDriver targetHeadTurn)
    {
        if (targetHeadTurn == null || targetHeadTurn.leftEyeBone == null || targetHeadTurn.rightEyeBone == null)
            return null;

        Transform anchor;
        if (!_targetActorMidpointAnchors.TryGetValue(actorIndex, out anchor) || anchor == null)
        {
            var go = new GameObject("ActorGazeMidpointAnchor");
            go.hideFlags = HideFlags.HideInHierarchy;
            anchor = go.transform;
            _targetActorMidpointAnchors[actorIndex] = anchor;
        }

        Transform parent = targetHeadTurn.headBone != null ? targetHeadTurn.headBone : targetHeadTurn.transform;
        if (anchor.parent != parent)
            anchor.SetParent(parent, false);

        Vector3 worldMidpoint = (targetHeadTurn.leftEyeBone.position + targetHeadTurn.rightEyeBone.position) * 0.5f;
        anchor.localPosition = parent.InverseTransformPoint(worldMidpoint);
        anchor.localRotation = Quaternion.identity;

        return anchor;
    }

    Transform ResolvePreferredEyeTarget(SnippetsGazeDriver targetHeadTurn, Transform currentEyeTarget)
    {
        if (targetHeadTurn == null)
            return null;

        bool hasLeft = targetHeadTurn.leftEyeBone != null;
        bool hasRight = targetHeadTurn.rightEyeBone != null;

        if (hasLeft && hasRight)
        {
            if (currentEyeTarget == targetHeadTurn.leftEyeBone) return targetHeadTurn.rightEyeBone;
            if (currentEyeTarget == targetHeadTurn.rightEyeBone) return targetHeadTurn.leftEyeBone;
            return UnityEngine.Random.value < 0.5f ? targetHeadTurn.leftEyeBone : targetHeadTurn.rightEyeBone;
        }

        if (hasLeft) return targetHeadTurn.leftEyeBone;
        if (hasRight) return targetHeadTurn.rightEyeBone;

        return null;
    }
}
