using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SnippetsGazeFlowController : MonoBehaviour
{
    public enum UnspecifiedActorBehavior
    {
        KeepPrevious,
        SetOff,
        LookAtMainCamera
    }

    public enum TargetType
    {
        None,
        Transform,
        Actor,
        MainCamera,
        Forward // uses SnippetsHeadTurn.LookInFront (fixedTargetOverride)
    }

    public enum StepGazeMode
    {
        Simple,
        Granular
    }

    [Serializable]
    public class ActorGaze
    {
        [Tooltip("Which actor this gaze instruction applies to.")]
        public int actorIndex = 0;

        [Header("Target")]
        public TargetType targetType = TargetType.Transform;

        [Tooltip("Used when targetType = Transform")]
        public Transform targetTransform;

        [Tooltip("Used when targetType = Actor")]
        public int targetActorIndex = 0;

        [Tooltip("If targetType = Actor, prefer the other actor's headBone (if available).")]
        public bool preferTargetActorHeadBone = true;

        [Header("Forward (Look In Front)")]
        [Tooltip("Used when targetType = Forward")]
        public Transform forwardTargetOverride;
    }

    [Serializable]
    public class GazeCue
    {
        [Tooltip("Optional label for readability.")]
        public string label;

        [Tooltip("0..1 of the snippet duration (0=start, 1=end).")]
        [Range(0f, 1f)]
        public float percent = 0f;

        [Tooltip("Blend duration when applying this cue.")]
        [Min(0f)] public float blendSeconds = 0.25f;

        [Tooltip("Overrides applied at this cue moment.")]
        public List<ActorGaze> overrides = new();
    }

    [Serializable]
    public class GazeStep
    {
        [Tooltip("Optional label for readability in Inspector.")]
        public string label;

        [HideInInspector]
        public string flowStepGuid;

        [Header("Mode")]
        public StepGazeMode mode = StepGazeMode.Simple;

        [Header("Simple")]
        public List<ActorGaze> overrides = new();

        [Header("Granular (Percent of Snippet Duration)")]
        public List<GazeCue> cues = new();
    }

    public SnippetsFlowController flow;
    public SnippetsActorRegistry registry;

    [Header("Gaze Plan (indexed by Flow step index)")]
    public List<GazeStep> gazeSteps = new();

    public UnspecifiedActorBehavior unspecifiedActors = UnspecifiedActorBehavior.KeepPrevious;

    public bool autoSyncToFlowSteps = true;
    public bool autoLabelFromFlow = true;

    Coroutine _cueCo;
    int _activeStepIndex = -1;

    void OnEnable() => TryWire();
    void OnDisable() => Unwire();

    void OnValidate()
    {
        if (!autoSyncToFlowSteps) return;
        if (flow == null) return;

        SyncToFlow(resize: true, relabel: autoLabelFromFlow, remap: true);
    }

    public void SyncNow() => SyncToFlow(resize: true, relabel: true, remap: true);

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
            var mapByNormLabel = new Dictionary<string, List<GazeStep>>();
            for (int i = 0; i < oldSteps.Count; i++)
            {
                var gs = oldSteps[i];
                if (gs == null) continue;

                if (!string.IsNullOrEmpty(gs.flowStepGuid) && !mapByGuid.ContainsKey(gs.flowStepGuid))
                    mapByGuid[gs.flowStepGuid] = gs;

                string n = NormalizeLabel(gs.label);
                if (!string.IsNullOrEmpty(n))
                {
                    if (!mapByNormLabel.TryGetValue(n, out var list))
                        list = mapByNormLabel[n] = new List<GazeStep>();
                    list.Add(gs);
                }
            }

            var flowGuidCounts = new Dictionary<string, int>();
            if (flow.steps != null)
            {
                for (int i = 0; i < flow.steps.Count; i++)
                {
                    var step = flow.steps[i];
                    if (step == null || string.IsNullOrEmpty(step.guid)) continue;
                    flowGuidCounts.TryGetValue(step.guid, out int c);
                    flowGuidCounts[step.guid] = c + 1;
                }
            }

            var usedSteps = new HashSet<GazeStep>();
            var usedGuids = new HashSet<string>();

            var newList = new List<GazeStep>(desired);
            for (int i = 0; i < desired; i++)
            {
                var step = flow.steps[i];
                string guid = step != null ? step.guid : null;

                bool guidUnique = !string.IsNullOrEmpty(guid) &&
                                  flowGuidCounts.TryGetValue(guid, out int cnt) && cnt == 1;

                GazeStep gs = null;

                if (guidUnique && mapByGuid.TryGetValue(guid, out var byGuid) && !usedSteps.Contains(byGuid))
                    gs = byGuid;

                if (gs == null)
                {
                    string flowLabel = flow.GetStepDisplayLabel(i);
                    string norm = NormalizeLabel(flowLabel);
                    if (!string.IsNullOrEmpty(norm) && mapByNormLabel.TryGetValue(norm, out var list))
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
                    if (cand != null && !usedSteps.Contains(cand))
                        gs = cand; // legacy index-based mapping
                }

                gs ??= new GazeStep();

                if (step != null)
                {
                    string desiredGuid = null;
                    if (!string.IsNullOrEmpty(gs.flowStepGuid) && !usedGuids.Contains(gs.flowStepGuid))
                        desiredGuid = gs.flowStepGuid;
                    else if (!string.IsNullOrEmpty(step.guid) && !usedGuids.Contains(step.guid))
                        desiredGuid = step.guid;
                    else
                        desiredGuid = Guid.NewGuid().ToString("N");

                    step.guid = desiredGuid;
                    usedGuids.Add(desiredGuid);
                    gs.flowStepGuid = desiredGuid;
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
        if (registry == null) return;
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

            ApplyOverridesMap(cue.overrides, cue.blendSeconds);
            yield return null;
        }
    }

    float EstimateSnippetDurationSeconds(SnippetsFlowController.Step step)
    {
        if (registry == null || step == null) return 0f;
        if (step.type != SnippetsFlowController.StepType.Snippet) return 0f;

        var actor = registry.GetActor(step.actorIndex);
        if (actor != null && actor.snippets != null &&
            step.snippetIndex >= 0 && step.snippetIndex < actor.snippets.Count)
        {
            var sn = actor.snippets[step.snippetIndex];
            var clip = sn != null ? sn.Value.Animation : null;
            if (clip != null && clip.length > 0.001f) return clip.length;
        }

        return 0f;
    }

    // ================= APPLY =================

    void ApplyOverridesMap(List<ActorGaze> overrides, float blendSeconds = 0f)
    {
        int actorCount = registry.ActorCount;

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
            var headTurn = registry.GetHeadTurn(actorIndex);
            if (headTurn == null) continue;

            if (map != null && map.TryGetValue(actorIndex, out var og) && og != null)
                ApplyOverride(headTurn, og, blendSeconds);
            else
                ApplyUnspecifiedPolicy(headTurn);
        }
    }

    void ApplyOverride(SnippetsHeadTurn headTurn, ActorGaze og, float blendSeconds)
    {
        if (headTurn == null) return;

        // We rely on SnippetsHeadTurn's own smoothing for blending.
        switch (og.targetType)
        {
            case TargetType.Forward:
                headTurn.mode = SnippetsHeadTurn.GazeMode.LookInFront;
                headTurn.fixedTargetOverride = og.forwardTargetOverride; // transform-only
                headTurn.target = null;
                return;

            case TargetType.None:
                headTurn.mode = SnippetsHeadTurn.GazeMode.Off;
                headTurn.target = null;
                return;

            default:
                headTurn.mode = SnippetsHeadTurn.GazeMode.FollowTarget;
                headTurn.target = ResolveFollowTargetTransform(og);
                headTurn.autoFindTarget = (og.targetType == TargetType.MainCamera);
                return;
        }
    }

    void ApplyUnspecifiedPolicy(SnippetsHeadTurn headTurn)
    {
        switch (unspecifiedActors)
        {
            case UnspecifiedActorBehavior.KeepPrevious:
                return;

            case UnspecifiedActorBehavior.SetOff:
                headTurn.mode = SnippetsHeadTurn.GazeMode.Off;
                headTurn.target = null;
                return;

            case UnspecifiedActorBehavior.LookAtMainCamera:
                headTurn.mode = SnippetsHeadTurn.GazeMode.FollowTarget;
                headTurn.target = Camera.main ? Camera.main.transform : null;
                headTurn.autoFindTarget = true;
                return;
        }
    }

    Transform ResolveFollowTargetTransform(ActorGaze og)
    {
        switch (og.targetType)
        {
            case TargetType.Transform:
                return og.targetTransform;

            case TargetType.MainCamera:
                return Camera.main ? Camera.main.transform : null;

            case TargetType.Actor:
            {
                var targetActor = registry != null ? registry.GetActor(og.targetActorIndex) : null;
                if (targetActor == null) return null;

                if (og.preferTargetActorHeadBone && targetActor.headTurn != null && targetActor.headTurn.headBone != null)
                    return targetActor.headTurn.headBone;

                if (targetActor.player != null)
                    return targetActor.player.transform;

                if (targetActor.walker != null)
                    return targetActor.walker.transform;

                return null;
            }
        }
        return null;
    }
}
