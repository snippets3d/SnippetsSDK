using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Snippets.Sdk;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Keyboard (Start)")]
    public bool enableKeyboard = true;
    public KeyCode key = KeyCode.Space;

    Coroutine _co;
    bool _running;

    SnippetPlayer _currentPlayer;
    UnityAction _currentOnStopped;

    SnippetsWalker _activeWalker;
    Action _activeOnArrived;

    public bool IsRunning => _running;

    void Update()
    {
        if (!enableKeyboard) return;
        if (!Input.GetKeyDown(key)) return;
        Play();
    }

    // Public API (matches Flow verb set)
    public void Play()
    {
        if (!Application.isPlaying) return;
        if (registry == null) return;

        InterruptCurrent();

        _running = true;
        _co = StartCoroutine(RunOneStep());
    }

    public void Stop(SnippetsActorRegistry.StopMode mode = SnippetsActorRegistry.StopMode.Soft)
    {
        if (!Application.isPlaying) return;

        InterruptCurrent();

        if (registry != null)
            registry.StopAllToIdle(mode);
    }

    public void Reset()
    {
        if (!Application.isPlaying) return;
        Stop(SnippetsActorRegistry.StopMode.Soft);
        Play();
    }

    // Back-compat name (if anything still calls Trigger)
    [Obsolete("Use Play() instead")]
    public void Trigger() => Play();

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

        _activeWalker = walker;
        _activeOnArrived = OnArrived;

        walker.Arrived += OnArrived;
        walker.MoveToIndex(waypointIndex);

        while (_running && !arrived)
            yield return null;

        if (_activeWalker != null && _activeOnArrived != null)
            _activeWalker.Arrived -= _activeOnArrived;

        _activeWalker = null;
        _activeOnArrived = null;

        if (!_running) yield break;

        registry.FadeToIdleNow(actorIndex);
    }

    void InterruptCurrent()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        DetachPlaybackStopped();

        if (_activeWalker != null)
        {
            if (_activeOnArrived != null)
                _activeWalker.Arrived -= _activeOnArrived;

            _activeWalker.StopMovement();
            _activeWalker = null;
            _activeOnArrived = null;
        }

        _running = false;
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
        InterruptCurrent();
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
        var ctrl = (SnippetsSimpleController)target;
        if (ctrl == null) return;

        ctrl.registry = (SnippetsActorRegistry)EditorGUILayout.ObjectField("Registry", ctrl.registry, typeof(SnippetsActorRegistry), true);

        EditorGUILayout.Space(8);

        ctrl.action = (SnippetsSimpleController.ActionType)EditorGUILayout.EnumPopup(GC_Action, ctrl.action);

        DrawActor(ctrl);

        if (ctrl.action == SnippetsSimpleController.ActionType.Snippet)
            DrawSnippet(ctrl);
        else
            DrawWaypoint(ctrl);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Keyboard (Start)", EditorStyles.boldLabel);
        ctrl.enableKeyboard = EditorGUILayout.Toggle("Enable Keyboard", ctrl.enableKeyboard);
        using (new EditorGUI.DisabledScope(!ctrl.enableKeyboard))
        {
            ctrl.key = (KeyCode)EditorGUILayout.EnumPopup("Key", ctrl.key);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Start"))
                ctrl.Play();

            if (GUILayout.Button("Stop"))
                ctrl.Stop(); // Soft by default (blend + mouth close via Broadcast stop)

            if (GUILayout.Button("Reset"))
                ctrl.Reset();

            EditorGUILayout.EndHorizontal();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use Start/Stop/Reset.", MessageType.None);

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
