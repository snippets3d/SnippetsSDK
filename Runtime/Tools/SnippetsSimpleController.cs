using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Snippets.Sdk;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// One-off "single step" controller with the same flow as ONE step in SnippetsFlowController:
/// - Action: Snippet / Walk
/// - Actor dropdown (names) + index shown
/// - Snippet dropdown (names) + index shown
/// - Waypoint dropdown by name + index shown
///
/// Trigger semantics (restores old behavior):
/// - Trigger ALWAYS starts the selected action immediately
/// - If a snippet is currently playing, Trigger interrupts and blends into the new snippet
/// - If a walk is currently running, Trigger interrupts the walk and starts the new action
/// </summary>
[DisallowMultipleComponent]
public class SnippetsSimpleController : MonoBehaviour
{
    public enum ActionType
    {
        Snippet,
        Walk
    }

    [Header("Registry")]
    public SnippetsActorRegistry registry;

    [Header("Selection")]
    public ActionType action = ActionType.Snippet;
    public int actorIndex = 0;

    [Header("Snippet")]
    public int snippetIndex = 0;

    [Header("Walk")]
    public int waypointIndex = 0;

    [Header("Keyboard (Trigger)")]
    public bool enableKeyboard = true;
    public KeyCode key = KeyCode.Space;

    // Runtime
    Coroutine _co;
    bool _running;

    // Snippet PlaybackStopped listener bookkeeping (like FlowController)
    SnippetPlayer _currentPlayer;
    UnityAction _currentOnStopped;

    // Walk bookkeeping
    SnippetsWalker _activeWalker;
    Action _activeOnArrived;

    public bool IsRunning => _running;

    void Update()
    {
        if (!enableKeyboard) return;
        if (!Input.GetKeyDown(key)) return;

        Trigger();
    }

    // ============================================================
    // Public Controls (buttons)
    // ============================================================

    /// <summary>
    /// Starts the selected one-off action immediately.
    /// If something is already running, it gets interrupted (old behavior).
    /// </summary>
    public void Trigger()
    {
        if (!Application.isPlaying) return;
        if (registry == null) return;

        // Old behavior: triggering mid-snippet should override & blend into the new snippet.
        InterruptCurrent(keepSceneRunning: true);

        _running = true;
        _co = StartCoroutine(RunOneStep());
    }

    /// <summary>
    /// Hard stop: cancels current one-off and returns all actors to idle (matches Flow Stop semantics).
    /// </summary>
    public void Stop()
    {
        if (!Application.isPlaying) return;

        InterruptCurrent(keepSceneRunning: false);

        if (registry != null)
            registry.StopAllAndReturnToIdle();
    }

    /// <summary>
    /// Reset = Stop + Trigger.
    /// </summary>
    public void Reset()
    {
        if (!Application.isPlaying) return;
        Stop();
        Trigger();
    }

    // ============================================================
    // One-step execution
    // ============================================================

    IEnumerator RunOneStep()
    {
        actorIndex = Mathf.Clamp(actorIndex, 0, Mathf.Max(0, registry.ActorCount - 1));

        switch (action)
        {
            case ActionType.Snippet:
                yield return ExecuteSnippet();
                break;

            case ActionType.Walk:
                yield return ExecuteWalk();
                break;
        }

        _running = false;
        _co = null;
    }

    IEnumerator ExecuteSnippet()
    {
        var snippets = registry.GetSnippets(actorIndex);
        int snCount = snippets != null ? snippets.Count : 0;
        if (snCount <= 0) yield break;

        snippetIndex = Mathf.Clamp(snippetIndex, 0, snCount - 1);

        // Same semantics as a single Snippet step:
        // Start snippet NOW, then queue idle.
        registry.StartSnippetNow(actorIndex, snippetIndex);
        registry.QueueIdleAfterCurrent(actorIndex);

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

        while (_running && !done)
            yield return null;

        // Safety fallback
        if (_running && actor.legacyAnimation != null && !actor.legacyAnimation.isPlaying)
            registry.PlayIdleImmediate(actorIndex);
    }

    IEnumerator ExecuteWalk()
    {
        var walker = registry.GetWalker(actorIndex);
        if (walker == null) yield break;

        int wpCount = walker.waypoints != null ? walker.waypoints.Length : 0;
        if (wpCount <= 0) yield break;

        waypointIndex = Mathf.Clamp(waypointIndex, 0, wpCount - 1);

        registry.FadeToWalkNow(actorIndex);

        bool arrived = false;
        void OnArrived() => arrived = true;

        // Track so Trigger can interrupt a walk cleanly
        _activeWalker = walker;
        _activeOnArrived = OnArrived;

        walker.Arrived += OnArrived;
        walker.MoveToIndex(waypointIndex);

        while (_running && !arrived)
            yield return null;

        // Unsubscribe + clear
        if (_activeWalker != null && _activeOnArrived != null)
            _activeWalker.Arrived -= _activeOnArrived;

        _activeWalker = null;
        _activeOnArrived = null;

        if (!_running) yield break;

        registry.FadeToIdleNow(actorIndex);
    }

    // ============================================================
    // Interrupt / cleanup
    // ============================================================

    void InterruptCurrent(bool keepSceneRunning)
    {
        // Stop coroutine
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        // Stop waiting for snippet end
        DetachPlaybackStopped();

        // Interrupt walk movement if active
        if (_activeWalker != null)
        {
            if (_activeOnArrived != null)
                _activeWalker.Arrived -= _activeOnArrived;

            // If we are interrupting a walk, stop its movement immediately
            _activeWalker.StopMovement();

            _activeWalker = null;
            _activeOnArrived = null;
        }

        // Mark not running (caller may immediately start a new coroutine)
        _running = false;

        // If we were just switching snippets, we keep scene running; no extra work needed.
        // If this was a Stop(), the caller will return actors to idle via registry.StopAllAndReturnToIdle().
    }

    void DetachPlaybackStopped()
    {
        if (_currentPlayer != null && _currentOnStopped != null)
            _currentPlayer.PlaybackStopped.RemoveListener(_currentOnStopped);

        _currentPlayer = null;
        _currentOnStopped = null;
    }

    void OnDisable()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        DetachPlaybackStopped();

        if (_activeWalker != null && _activeOnArrived != null)
            _activeWalker.Arrived -= _activeOnArrived;

        _activeWalker = null;
        _activeOnArrived = null;

        _running = false;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SnippetsSimpleController))]
public class SnippetsSimpleControllerEditor : Editor
{
    static readonly GUIContent GC_Action = new("Action");
    static readonly GUIContent GC_Actor = new("Actor");
    static readonly GUIContent GC_Snippet = new("Snippet");
    static readonly GUIContent GC_Waypoint = new("Waypoint");

    public override void OnInspectorGUI()
    {
        if (target == null) return;

        var ctrl = (SnippetsSimpleController)target;
        if (ctrl == null) return;

        // Registry
        ctrl.registry = (SnippetsActorRegistry)EditorGUILayout.ObjectField("Registry", ctrl.registry, typeof(SnippetsActorRegistry), true);

        EditorGUILayout.Space(8);

        // Action
        ctrl.action = (SnippetsSimpleController.ActionType)EditorGUILayout.EnumPopup(GC_Action, ctrl.action);

        // Actor dropdown + [index]
        DrawActor(ctrl);

        // Action-specific (match Flow step visuals)
        if (ctrl.action == SnippetsSimpleController.ActionType.Snippet)
            DrawSnippet(ctrl);
        else
            DrawWaypoint(ctrl);

        EditorGUILayout.Space(10);

        // Keyboard
        EditorGUILayout.LabelField("Keyboard (Trigger)", EditorStyles.boldLabel);
        ctrl.enableKeyboard = EditorGUILayout.Toggle("Enable Keyboard", ctrl.enableKeyboard);
        using (new EditorGUI.DisabledScope(!ctrl.enableKeyboard))
        {
            ctrl.key = (KeyCode)EditorGUILayout.EnumPopup("Key", ctrl.key);
        }

        EditorGUILayout.Space(10);

        // Controls
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Trigger"))
                ctrl.Trigger();

            if (GUILayout.Button("Stop"))
                ctrl.Stop();

            if (GUILayout.Button("Reset"))
                ctrl.Reset();

            EditorGUILayout.EndHorizontal();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use Trigger/Stop/Reset.", MessageType.None);

        if (GUI.changed)
            EditorUtility.SetDirty(ctrl);
    }

    void DrawActor(SnippetsSimpleController ctrl)
    {
        if (ctrl.registry != null && ctrl.registry.ActorCount > 0)
        {
            int actorCount = ctrl.registry.ActorCount;
            string[] actorNames = new string[actorCount];
            for (int i = 0; i < actorCount; i++)
                actorNames[i] = ctrl.registry.GetActorDisplayName(i);

            ctrl.actorIndex = Mathf.Clamp(ctrl.actorIndex, 0, actorCount - 1);

            int newActor = EditorGUILayout.Popup(
                $"{GC_Actor.text} [{ctrl.actorIndex}]",
                ctrl.actorIndex,
                actorNames
            );

            if (newActor != ctrl.actorIndex)
            {
                Undo.RecordObject(ctrl, "Change Actor");
                ctrl.actorIndex = newActor;
                ctrl.snippetIndex = 0;
                ctrl.waypointIndex = 0;
            }
        }
        else
        {
            ctrl.actorIndex = EditorGUILayout.IntField($"{GC_Actor.text} [{ctrl.actorIndex}]", ctrl.actorIndex);
        }
    }

    void DrawSnippet(SnippetsSimpleController ctrl)
    {
        if (ctrl.registry == null)
        {
            ctrl.snippetIndex = EditorGUILayout.IntField($"{GC_Snippet.text} [{ctrl.snippetIndex}]", ctrl.snippetIndex);
            return;
        }

        var snippets = ctrl.registry.GetSnippets(ctrl.actorIndex);
        int snCount = snippets != null ? snippets.Count : 0;

        if (snCount <= 0)
        {
            EditorGUILayout.HelpBox("This actor has no snippets assigned.", MessageType.Info);
            ctrl.snippetIndex = EditorGUILayout.IntField($"{GC_Snippet.text} [{ctrl.snippetIndex}]", ctrl.snippetIndex);
            return;
        }

        string[] snippetNames = new string[snCount];
        for (int i = 0; i < snCount; i++)
            snippetNames[i] = ctrl.registry.GetSnippetDisplayName(ctrl.actorIndex, i);

        ctrl.snippetIndex = Mathf.Clamp(ctrl.snippetIndex, 0, snCount - 1);

        int newSn = EditorGUILayout.Popup(
            $"{GC_Snippet.text} [{ctrl.snippetIndex}]",
            ctrl.snippetIndex,
            snippetNames
        );

        if (newSn != ctrl.snippetIndex)
        {
            Undo.RecordObject(ctrl, "Change Snippet");
            ctrl.snippetIndex = newSn;
        }
    }

    void DrawWaypoint(SnippetsSimpleController ctrl)
    {
        if (ctrl.registry == null)
        {
            ctrl.waypointIndex = EditorGUILayout.IntField($"{GC_Waypoint.text} [{ctrl.waypointIndex}]", ctrl.waypointIndex);
            return;
        }

        var walker = ctrl.registry.GetWalker(ctrl.actorIndex);
        if (walker == null)
        {
            EditorGUILayout.HelpBox("This actor has no Walker assigned in the registry.", MessageType.Info);
            ctrl.waypointIndex = EditorGUILayout.IntField($"{GC_Waypoint.text} [{ctrl.waypointIndex}]", ctrl.waypointIndex);
            return;
        }

        int wpCount = walker.waypoints != null ? walker.waypoints.Length : 0;
        if (wpCount <= 0)
        {
            EditorGUILayout.HelpBox("Walker has no waypoints assigned.", MessageType.Info);
            ctrl.waypointIndex = EditorGUILayout.IntField($"{GC_Waypoint.text} [{ctrl.waypointIndex}]", ctrl.waypointIndex);
            return;
        }

        string[] wpNames = new string[wpCount];
        for (int i = 0; i < wpCount; i++)
            wpNames[i] = walker.waypoints[i] != null ? walker.waypoints[i].name : $"Waypoint {i}";

        ctrl.waypointIndex = Mathf.Clamp(ctrl.waypointIndex, 0, wpCount - 1);

        int newWp = EditorGUILayout.Popup(
            $"{GC_Waypoint.text} [{ctrl.waypointIndex}]",
            ctrl.waypointIndex,
            wpNames
        );

        if (newWp != ctrl.waypointIndex)
        {
            Undo.RecordObject(ctrl, "Change Waypoint");
            ctrl.waypointIndex = newWp;
        }
    }
}
#endif
