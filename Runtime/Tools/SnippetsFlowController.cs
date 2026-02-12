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
        Pause
    }

    [Serializable]
    public class Step
    {
        public StepType type = StepType.Snippet;

        [HideInInspector]
        public string guid;

        [Header("Common")]
        [InspectorName("Actor")]
        public int actorIndex = 0;

        [Header("Snippet")]
        [InspectorName("Snippet")]
        public int snippetIndex = 0;

        [Header("Walk")]
        [InspectorName("Waypoint")]
        public int waypointIndex = 0;

        [Header("Pause")]
        [Tooltip("If true, pauses until Play (or keyboard key) is pressed again.")]
        [InspectorName("Wait For Trigger")]
        public bool waitForTrigger = false;

        [Tooltip("If waitForTrigger is false, waits this many seconds.")]
        [Min(0f)]
        [InspectorName("Seconds")]
        public float seconds = 0f;
    }

    [InspectorName("Actor Registry")]
    public SnippetsActorRegistry registry;

    [Header("Steps")]
    public List<Step> steps = new();

    [InspectorName("Play On Start")]
    public bool playOnStart = true;

    [InspectorName("Loop Sequence")]
    public bool loopSequence = false;

    [Tooltip("If false, after every NON-Pause step it will wait for trigger (like manual stepping).")]
    [InspectorName("Auto Progress")]
    public bool autoProgress = true;

    [InspectorName("Enable Keyboard")]
    public bool enableKeyboard = true;

    [InspectorName("Key")]
    public KeyCode key = KeyCode.Space;

    // Step events (so other systems can react without polling)
    public event Action<int, Step> StepStarted;
    public event Action<int, Step> StepFinished;

    Coroutine _co;
    int _index = 0;
    bool _running = false;

    bool _waitingForTrigger = false;
    bool _resumeRequested = false;

    SnippetPlayer _currentPlayer;
    UnityAction _currentOnStopped;

    public bool IsRunning => _running;

    void OnValidate() => EnsureStepGuids();

    public void EnsureStepGuids()
    {
        if (steps == null) return;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step == null) continue;
            if (string.IsNullOrEmpty(step.guid))
                step.guid = Guid.NewGuid().ToString("N");
        }
    }

    // Unity lifecycle Start (do not rename; Unity calls this automatically)
    void Start()
    {
        if (registry == null) return;

        registry.BuildRuntimeRegistry();
        registry.ForceAllIdleImmediate();

        if (playOnStart)
            Play();
    }

    void Update()
    {
        if (!enableKeyboard) return;

        if (Input.GetKeyDown(key))
        {
            if (!_running)
            {
                Play();
                return;
            }

            if (_waitingForTrigger)
            {
                RequestResume();
                return;
            }

            if (!autoProgress && !_waitingForTrigger)
            {
                RequestResume();
            }
        }
    }

    // ============================================================
    // Public API (dev-facing)
    // ============================================================

    public void Play()
    {
        if (!Application.isPlaying) return;
        if (registry == null) return;
        if (steps == null || steps.Count == 0) return;

        // If already running, and we're paused waiting for trigger, resume
        if (_running && _co != null)
        {
            if (_waitingForTrigger) RequestResume();
            return;
        }

        _running = true;
        _resumeRequested = false;
        _waitingForTrigger = false;

        if (_index < 0) _index = 0;
        if (_index >= steps.Count) _index = Mathf.Max(0, steps.Count - 1);

        _co = StartCoroutine(Run());
    }

    /// <summary>
    /// Stop flow and return actors to idle.
    /// Default = Soft (BLEND) for nicer UX. Use Hard when you want snap/kill.
    /// </summary>
    public void Stop(SnippetsActorRegistry.StopMode mode = SnippetsActorRegistry.StopMode.Soft)
    {
        if (!Application.isPlaying) return;

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
        if (!Application.isPlaying) return;
        Stop(SnippetsActorRegistry.StopMode.Soft);
        _index = 0;
        Play();
    }

    // ============================================================
    // Back-compat wrappers (keep older integrations working)
    // ============================================================

    public void StartFlow() => Play();

    /// <summary>
    /// CHANGED: StopFlow now blends (Soft) so it matches your desired behavior.
    /// </summary>
    public void StopFlow() => Stop(SnippetsActorRegistry.StopMode.Soft);

    /// <summary>
    /// Explicit kill switch: snap/hard stop.
    /// </summary>
    public void StopFlowHard() => Stop(SnippetsActorRegistry.StopMode.Hard);

    public void ResetFlow() => Reset();

    // ============================================================
    // Internals
    // ============================================================

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

            if (_index < 0) _index = 0;

            if (_index >= steps.Count)
            {
                if (loopSequence) _index = 0;
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

            if (!_running) yield break;

            if (step.type != StepType.Pause && autoProgress == false)
            {
                yield return WaitForTriggerInternal();
                if (!_running) yield break;
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
        }
    }

    IEnumerator ExecuteSnippet(Step step, int stepIndex)
    {
        if (registry == null) yield break;

        Step next = (stepIndex + 1 < steps.Count) ? steps[stepIndex + 1] : null;
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

        var actor = registry.GetActor(step.actorIndex);
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

        while (_running && !done)
            yield return null;

        // If we are not chaining another snippet on the same actor, ensure the mouth closes
        // after the queued idle crossfade completes.
        if (_running && !canChain && registry != null)
            registry.RequestPostFadeFaceReset(step.actorIndex);

        // Safety fallback (should be rare)
        if (_running && actor.legacyAnimation != null && !actor.legacyAnimation.isPlaying)
            registry.PlayIdleImmediate(step.actorIndex);
    }

    IEnumerator ExecuteWalk(Step step)
    {
        if (registry == null) yield break;

        var walker = registry.GetWalker(step.actorIndex);
        if (walker == null) yield break;

        registry.FadeToWalkNow(step.actorIndex);

        bool arrived = false;
        void OnArrived() => arrived = true;

        walker.Arrived += OnArrived;
        walker.MoveToIndex(step.waypointIndex);

        while (_running && !arrived)
            yield return null;

        walker.Arrived -= OnArrived;

        if (!_running) yield break;

        registry.FadeToIdleNow(step.actorIndex);
    }

    IEnumerator ExecutePause(Step step)
    {
        if (step.waitForTrigger)
        {
            yield return WaitForTriggerInternal();
        }
        else
        {
            float s = Mathf.Max(0f, step.seconds);
            if (s > 0f)
                yield return WaitSecondsCancellable(s);
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
        float t = 0f;
        while (_running && t < seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    void DetachPlaybackStopped()
    {
        if (_currentPlayer != null && _currentOnStopped != null)
            _currentPlayer.PlaybackStopped.RemoveListener(_currentOnStopped);

        _currentPlayer = null;
        _currentOnStopped = null;
    }

    // ============================================================
    // Shared, human-friendly labels (GazeFlowController depends on this)
    // ============================================================

    public string GetStepDisplayLabel(int stepIndex)
    {
        if (steps == null || stepIndex < 0 || stepIndex >= steps.Count) return $"Step {stepIndex + 1}";
        return GetStepDisplayLabel(stepIndex, steps[stepIndex]);
    }

    public string GetStepDisplayLabel(int stepIndex, Step step)
    {
        if (step == null) return $"Step {stepIndex + 1}: (missing step)";

        string actorName = GetActorNameSafe(step.actorIndex);

        switch (step.type)
        {
            case StepType.Walk:
                return $"Step {stepIndex + 1}: {actorName} (walks) → {GetWaypointNameSafe(step.actorIndex, step.waypointIndex)}";

            case StepType.Snippet:
                return $"Step {stepIndex + 1}: {actorName} (performs) → {GetSnippetNameSafe(step.actorIndex, step.snippetIndex)}";

            case StepType.Pause:
                return step.waitForTrigger
                    ? $"Step {stepIndex + 1}: {actorName} (waits) → until trigger"
                    : $"Step {stepIndex + 1}: {actorName} (waits) → {Mathf.Max(0f, step.seconds):0.##}s";

            default:
                return $"Step {stepIndex + 1}: {actorName} ({step.type})";
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
        if (registry == null) return $"Snippet {snippetIndex}";

        var list = registry.GetSnippets(actorIndex);
        int count = list != null ? list.Count : 0;
        if (count <= 0) return $"Snippet {snippetIndex}";

        int i = Mathf.Clamp(snippetIndex, 0, count - 1);
        return registry.GetSnippetDisplayName(actorIndex, i);
    }

    string GetWaypointNameSafe(int actorIndex, int waypointIndex)
    {
        if (registry == null) return $"Waypoint {waypointIndex}";

        var walker = registry.GetWalker(actorIndex);
        if (walker == null || walker.waypoints == null || walker.waypoints.Length == 0)
            return $"Waypoint {waypointIndex}";

        int i = Mathf.Clamp(waypointIndex, 0, walker.waypoints.Length - 1);
        var tr = walker.waypoints[i];
        return tr != null ? tr.name : $"Waypoint {i}";
    }
}
