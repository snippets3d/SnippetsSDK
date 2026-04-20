using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Snippets.Sdk;

[DisallowMultipleComponent]
public class SnippetsFlowController : MonoBehaviour
{
    public enum StepType
    {
        Snippet,
        Walk,
        Pause,
        [InspectorName("Custom Animation")]
        CustomAnim,
        [InspectorName("Snippet + Custom Animation")]
        SnippetWithCustomAnim
    }

    public enum CompletionPolicy
    {
        [InspectorName("Wait For Custom Animation")]
        WaitForCustomAnim,
        [InspectorName("Wait For Snippet")]
        WaitForSnippet,
        [InspectorName("Wait For Both")]
        WaitForBoth
    }

    [Serializable]
    public class Step
    {
        [Tooltip("Which kind of step this flow executes.")]
        public StepType type = StepType.Snippet;

        [HideInInspector]
        public string guid;

        [Header("Common")]
        [InspectorName("Actor")]
        [Tooltip("Actor index in the registry that this step targets.")]
        public int actorIndex = 0;

        [Header("Snippet")]
        [InspectorName("Snippet")]
        [Tooltip("Snippet index used when Step Type is Snippet or Snippet + Custom Animation.")]
        public int snippetIndex = 0;

        [Header("Custom Animation")]
        [InspectorName("Custom Animation")]
        [Tooltip("Custom animation index used when Step Type is Custom Animation or Snippet + Custom Animation.")]
        public int customAnimationIndex = 0;

        [Tooltip("Which playback completion the flow waits for when Step Type is Snippet + Custom Animation.")]
        [InspectorName("Completion")]
        public CompletionPolicy completionPolicy = CompletionPolicy.WaitForBoth;

        [Tooltip("Optional legacy overlay mask applied to the snippet animation when Step Type is Snippet + Custom Animation.")]
        [InspectorName("Snippet Mask")]
        public SnippetsActorRegistry.SnippetMaskMode snippetMaskMode = SnippetsActorRegistry.SnippetMaskMode.None;

        [Header("Walk")]
        [InspectorName("Waypoint")]
        [Tooltip("Waypoint index used when Step Type is Walk.")]
        public int waypointIndex = 0;

        [Header("Pause")]
        [Tooltip("If enabled, this pause waits until Play or the keyboard trigger is pressed again.")]
        [InspectorName("Wait For Trigger")]
        public bool waitForTrigger = false;

        [Tooltip("Pause duration in seconds when Wait For Trigger is disabled.")]
        [Min(0f)]
        [InspectorName("Seconds")]
        public float seconds = 0f;
    }

    [InspectorName("Actor Registry")]
    [Tooltip("Actor registry used to resolve actors, snippets, walkers, playback targets, and custom animations.")]
    public SnippetsActorRegistry registry;

    [Header("Steps")]
    [Tooltip("Ordered list of snippet, walk, pause, and custom animation steps that make up the sequence.")]
    public List<Step> steps = new();

    [InspectorName("Play On Start")]
    [Tooltip("Automatically starts the flow when this component enters Play Mode.")]
    public bool playOnStart = true;

    [InspectorName("Loop Sequence")]
    [Tooltip("Restarts from the first step after the last step finishes.")]
    public bool loopSequence = false;

    [Tooltip("If disabled, every non-pause step waits for another trigger before the flow continues.")]
    [InspectorName("Auto Progress")]
    public bool autoProgress = true;

    [InspectorName("Enable Keyboard")]
    [Tooltip("Allows the configured keyboard key to start the flow and resume trigger waits.")]
    public bool enableKeyboard = false;

    [InspectorName("Key")]
    [Tooltip("Keyboard key used to start the flow or advance trigger waits.")]
    public KeyCode key = KeyCode.Space;

    public event Action<int, Step> StepStarted;
    public event Action<int, Step> StepFinished;

    Coroutine _co;
    int _index;
    bool _running;

    bool _waitingForTrigger;
    bool _resumeRequested;

    SnippetPlayer _currentPlayer;
    UnityAction _currentOnStopped;

    public bool IsRunning => _running;

    void OnValidate() => EnsureStepGuids();

    public void EnsureStepGuids()
    {
        if (steps == null)
            return;

        var seenGuids = new HashSet<string>();
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step == null)
                continue;

            if (string.IsNullOrEmpty(step.guid) || !seenGuids.Add(step.guid))
            {
                step.guid = Guid.NewGuid().ToString("N");
                seenGuids.Add(step.guid);
            }
        }
    }

    void Start()
    {
        if (registry == null)
            return;

        registry.BuildRuntimeRegistry();
        registry.ForceAllIdleImmediate();

        if (playOnStart)
            Play();
    }

    void Update()
    {
        if (!enableKeyboard || !Input.GetKeyDown(key))
            return;

        if (!_running)
        {
            Play();
            return;
        }

        if (_waitingForTrigger || !autoProgress)
            RequestResume();
    }

    public void Play()
    {
        if (!Application.isPlaying || registry == null || steps == null || steps.Count == 0)
            return;

        if (_running && _co != null)
        {
            if (_waitingForTrigger)
                RequestResume();
            return;
        }

        _running = true;
        _resumeRequested = false;
        _waitingForTrigger = false;

        if (_index < 0)
            _index = 0;
        if (_index >= steps.Count)
            _index = Mathf.Max(0, steps.Count - 1);

        _co = StartCoroutine(Run());
    }

    public void Stop(SnippetsActorRegistry.StopMode mode = SnippetsActorRegistry.StopMode.Soft)
    {
        if (!Application.isPlaying)
            return;

        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        DetachPlaybackStopped();

        _running = false;
        _waitingForTrigger = false;
        _resumeRequested = false;
        _index = 0;

        if (registry != null)
            registry.StopAllToIdle(mode);
    }

    public void Reset()
    {
        if (!Application.isPlaying)
            return;

        Stop(SnippetsActorRegistry.StopMode.Soft);
        _index = 0;
        Play();
    }

    public void StartFlow() => Play();
    public void StopFlow() => Stop(SnippetsActorRegistry.StopMode.Soft);
    public void StopFlowHard() => Stop(SnippetsActorRegistry.StopMode.Hard);
    public void ResetFlow() => Reset();

    void RequestResume()
    {
        _resumeRequested = true;
        _waitingForTrigger = false;
    }

    IEnumerator Run()
    {
        while (_running)
        {
            if (registry == null || steps == null || steps.Count == 0)
            {
                Stop(SnippetsActorRegistry.StopMode.Soft);
                yield break;
            }

            if (_index < 0)
                _index = 0;

            if (_index >= steps.Count)
            {
                if (loopSequence)
                    _index = 0;
                else
                {
                    Stop(SnippetsActorRegistry.StopMode.Soft);
                    yield break;
                }
            }

            var step = steps[_index];
            if (step == null)
            {
                _index++;
                continue;
            }

            StepStarted?.Invoke(_index, step);
            yield return ExecuteStep(step, _index);
            StepFinished?.Invoke(_index, step);

            if (!_running)
                yield break;

            if (step.type != StepType.Pause && !autoProgress)
            {
                yield return WaitForTriggerInternal();
                if (!_running)
                    yield break;
            }

            _index++;
        }
    }

    IEnumerator ExecuteStep(Step step, int stepIndex)
    {
        switch (step.type)
        {
            case StepType.Snippet:
                yield return ExecuteSnippet(step, stepIndex);
                yield break;

            case StepType.Walk:
                yield return ExecuteWalk(step);
                yield break;

            case StepType.Pause:
                yield return ExecutePause(step);
                yield break;

            case StepType.CustomAnim:
                yield return ExecuteCustomAnim(step);
                yield break;

            case StepType.SnippetWithCustomAnim:
                yield return ExecuteSnippetWithCustomAnim(step);
                yield break;
        }
    }

    IEnumerator ExecuteSnippet(Step step, int stepIndex)
    {
        if (registry == null || !HasPlayableSnippet(step.actorIndex, step.snippetIndex))
            yield break;

        Step next = stepIndex + 1 < steps.Count ? steps[stepIndex + 1] : null;
        bool canChain =
            autoProgress &&
            next != null &&
            next.type == StepType.Snippet &&
            next.actorIndex == step.actorIndex;

        registry.StartSnippetNow(step.actorIndex, step.snippetIndex);

        if (canChain)
            registry.QueueSnippetAfterCurrent(step.actorIndex, next.snippetIndex);
        else
            registry.QueueIdleAfterCurrent(step.actorIndex);

        yield return WaitForSnippetCompletion(step.actorIndex);

        var actor = registry.GetActor(step.actorIndex);
        if (_running && actor != null && actor.legacyAnimation != null && !actor.legacyAnimation.isPlaying)
            registry.PlayIdleImmediate(step.actorIndex);
    }

    IEnumerator ExecuteWalk(Step step)
    {
        if (registry == null)
            yield break;

        var walker = registry.GetWalker(step.actorIndex);
        if (walker == null)
            yield break;

        registry.FadeToWalkNow(step.actorIndex);

        bool arrived = false;
        void OnArrived() => arrived = true;

        walker.Arrived += OnArrived;
        walker.MoveToIndex(step.waypointIndex);

        while (_running && !arrived)
            yield return null;

        walker.Arrived -= OnArrived;

        if (_running)
            registry.FadeToIdleNow(step.actorIndex);
    }

    IEnumerator ExecutePause(Step step)
    {
        if (step.waitForTrigger)
            yield return WaitForTriggerInternal();
        else if (step.seconds > 0f)
            yield return WaitSecondsCancellable(Mathf.Max(0f, step.seconds));
    }

    IEnumerator ExecuteCustomAnim(Step step)
    {
        if (registry == null || !HasPlayableCustomAnimation(step.actorIndex, step.customAnimationIndex))
            yield break;

        registry.PlayCustomAnimationOnce(step.actorIndex, step.customAnimationIndex);
        yield return WaitForCustomAnimationCompletion(step.actorIndex);

        if (_running && !registry.IsCustomAnimationPlaying(step.actorIndex))
            registry.FadeToIdleNow(step.actorIndex);
    }

    IEnumerator ExecuteSnippetWithCustomAnim(Step step)
    {
        if (registry == null ||
            !HasPlayableSnippet(step.actorIndex, step.snippetIndex) ||
            !HasPlayableCustomAnimation(step.actorIndex, step.customAnimationIndex))
            yield break;

        registry.PlaySnippetSpeechWithCustomAnimationOnce(
            step.actorIndex,
            step.snippetIndex,
            step.customAnimationIndex,
            step.snippetMaskMode);

        bool waitForSnippet =
            step.completionPolicy == CompletionPolicy.WaitForSnippet ||
            step.completionPolicy == CompletionPolicy.WaitForBoth;
        bool waitForCustom =
            step.completionPolicy == CompletionPolicy.WaitForCustomAnim ||
            step.completionPolicy == CompletionPolicy.WaitForBoth;

        if (waitForSnippet)
            yield return WaitForSnippetCompletion(step.actorIndex);

        if (!_running)
            yield break;

        if (waitForCustom)
            yield return WaitForCustomAnimationCompletion(step.actorIndex);

        if (_running && !registry.IsCustomAnimationPlaying(step.actorIndex))
        {
            registry.FadeActorBlendshapesToZeroAfterSnippet(step.actorIndex);

            var actor = registry.GetActor(step.actorIndex);
            if (actor != null && actor.legacyAnimation != null && !actor.legacyAnimation.isPlaying)
                registry.PlayIdleImmediate(step.actorIndex);
        }
    }

    IEnumerator WaitForTriggerInternal()
    {
        _waitingForTrigger = true;
        _resumeRequested = false;

        while (_running && !_resumeRequested)
            yield return null;

        _waitingForTrigger = false;
        _resumeRequested = false;
    }

    IEnumerator WaitSecondsCancellable(float seconds)
    {
        float elapsed = 0f;
        while (_running && elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitForSnippetCompletion(int actorIndex)
    {
        if (registry == null)
            yield break;

        var actor = registry.GetActor(actorIndex);
        if (actor == null || actor.player == null)
            yield break;

        bool done = false;
        DetachPlaybackStopped();

        _currentPlayer = actor.player;
        _currentOnStopped = () =>
        {
            DetachPlaybackStopped();
            done = true;
        };

        actor.player.PlaybackStopped.AddListener(_currentOnStopped);

        if (!actor.player.IsPlaying)
        {
            DetachPlaybackStopped();
            yield break;
        }

        while (_running && !done)
            yield return null;

        if (_running && !registry.IsCustomAnimationPlaying(actorIndex))
        {
            registry.FadeActorBlendshapesToZeroAfterSnippet(actorIndex);

            if (actor.legacyAnimation != null && !actor.legacyAnimation.isPlaying)
                registry.PlayIdleImmediate(actorIndex);
        }
    }

    IEnumerator WaitForCustomAnimationCompletion(int actorIndex)
    {
        while (_running && registry != null && registry.IsCustomAnimationPlaying(actorIndex))
            yield return null;
    }

    void DetachPlaybackStopped()
    {
        if (_currentPlayer != null && _currentOnStopped != null)
            _currentPlayer.PlaybackStopped.RemoveListener(_currentOnStopped);

        _currentPlayer = null;
        _currentOnStopped = null;
    }

    public string GetStepDisplayLabel(int stepIndex)
    {
        if (steps == null || stepIndex < 0 || stepIndex >= steps.Count)
            return $"Step {stepIndex + 1}";

        return GetStepDisplayLabel(stepIndex, steps[stepIndex]);
    }

    public string GetStepDisplayLabel(int stepIndex, Step step)
    {
        if (step == null)
            return $"Step {stepIndex + 1}: (missing step)";

        string actorName = GetActorNameSafe(step.actorIndex);

        switch (step.type)
        {
            case StepType.Walk:
                return $"Step {stepIndex + 1}: {actorName} (walks) → {GetWaypointNameSafe(step.actorIndex, step.waypointIndex)}";

            case StepType.Snippet:
                return $"Step {stepIndex + 1}: {actorName} (performs) → {GetSnippetNameSafe(step.actorIndex, step.snippetIndex)}";

            case StepType.CustomAnim:
                return $"Step {stepIndex + 1}: {actorName} (custom anim) → {GetCustomAnimationNameSafe(step.actorIndex, step.customAnimationIndex)}";

            case StepType.SnippetWithCustomAnim:
                return $"Step {stepIndex + 1}: {actorName} (snippet + custom) → {GetSnippetNameSafe(step.actorIndex, step.snippetIndex)} + {GetCustomAnimationNameSafe(step.actorIndex, step.customAnimationIndex)}";

            case StepType.Pause:
                return step.waitForTrigger
                    ? $"Step {stepIndex + 1}: {actorName} (waits) → until trigger"
                    : $"Step {stepIndex + 1}: {actorName} (waits) → {Mathf.Max(0f, step.seconds):0.##}s";

            default:
                return $"Step {stepIndex + 1}: {actorName} ({step.type})";
        }
    }

    public float GetTimedStepDurationSeconds(int stepIndex)
    {
        if (steps == null || stepIndex < 0 || stepIndex >= steps.Count)
            return 0f;

        return GetTimedStepDurationSeconds(steps[stepIndex]);
    }

    public float GetTimedStepDurationSeconds(Step step)
    {
        if (step == null)
            return 0f;

        switch (step.type)
        {
            case StepType.Snippet:
            case StepType.SnippetWithCustomAnim:
                return GetSnippetDurationSeconds(step.actorIndex, step.snippetIndex);

            case StepType.Pause:
                return step.waitForTrigger ? 0f : Mathf.Max(0f, step.seconds);

            default:
                return 0f;
        }
    }

    string GetActorNameSafe(int actorIndex)
    {
        if (registry != null && registry.ActorCount > 0)
        {
            int i = Mathf.Clamp(actorIndex, 0, registry.ActorCount - 1);
            return registry.GetActorDisplayName(i);
        }

        return $"Actor {actorIndex}";
    }

    string GetSnippetNameSafe(int actorIndex, int snippetIndex)
    {
        if (registry == null)
            return $"Snippet {snippetIndex}";

        var list = registry.GetSnippets(actorIndex);
        int count = list != null ? list.Count : 0;
        if (count <= 0)
            return $"Snippet {snippetIndex}";

        int i = Mathf.Clamp(snippetIndex, 0, count - 1);
        return registry.GetSnippetDisplayName(actorIndex, i);
    }

    float GetSnippetDurationSeconds(int actorIndex, int snippetIndex)
    {
        if (registry == null)
            return 0f;

        var actor = registry.GetActor(actorIndex);
        if (actor == null || actor.snippets == null || actor.snippets.Count == 0)
            return 0f;

        int i = Mathf.Clamp(snippetIndex, 0, actor.snippets.Count - 1);
        var snippet = actor.snippets[i];
        if (snippet == null || snippet.Value == null)
            return 0f;

        if (snippet.Value.Sound != null && snippet.Value.Sound.length > 0.001f)
            return snippet.Value.Sound.length;

        if (snippet.Value.Animation != null && snippet.Value.Animation.length > 0.001f)
            return snippet.Value.Animation.length;

        return 0f;
    }

    bool HasPlayableSnippet(int actorIndex, int snippetIndex)
    {
        if (registry == null)
            return false;

        var actor = registry.GetActor(actorIndex);
        if (actor == null || actor.snippets == null || snippetIndex < 0 || snippetIndex >= actor.snippets.Count)
            return false;

        var snippet = actor.snippets[snippetIndex];
        return snippet != null && snippet.Value != null && snippet.Value.IsValid;
    }

    bool HasPlayableCustomAnimation(int actorIndex, int customAnimationIndex)
    {
        if (registry == null)
            return false;

        var items = registry.GetCustomAnimations(actorIndex);
        if (items == null || customAnimationIndex < 0 || customAnimationIndex >= items.Count)
            return false;

        var customAnimation = items[customAnimationIndex];
        return customAnimation != null && customAnimation.clip != null;
    }

    string GetWaypointNameSafe(int actorIndex, int waypointIndex)
    {
        if (registry == null)
            return $"Waypoint {waypointIndex}";

        var walker = registry.GetWalker(actorIndex);
        if (walker == null || walker.waypoints == null || walker.waypoints.Length == 0)
            return $"Waypoint {waypointIndex}";

        int i = Mathf.Clamp(waypointIndex, 0, walker.waypoints.Length - 1);
        var tr = walker.waypoints[i];
        return tr != null ? tr.name : $"Waypoint {i}";
    }

    string GetCustomAnimationNameSafe(int actorIndex, int customAnimationIndex)
    {
        if (registry == null)
            return $"Custom Animation {customAnimationIndex}";

        var items = registry.GetCustomAnimations(actorIndex);
        int count = items != null ? items.Count : 0;
        if (count <= 0)
            return $"Custom Animation {customAnimationIndex}";

        int i = Mathf.Clamp(customAnimationIndex, 0, count - 1);
        return registry.GetCustomAnimationDisplayName(actorIndex, i);
    }
}
