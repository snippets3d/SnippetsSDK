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
        Walk,
        [InspectorName("Custom Animation")]
        CustomAnim,
        [InspectorName("Snippet + Custom Animation")]
        SnippetWithCustomAnim
    }

    [Header("Registry")]
    [Tooltip("Actor registry used to resolve actors, snippets, and walkers.")]
    public SnippetsActorRegistry registry;

    [Header("Selection")]
    [Tooltip("Whether this controller triggers a snippet or moves an actor to a waypoint.")]
    public ActionType action = ActionType.Snippet;

    [Tooltip("Actor index inside the registry to control.")]
    public int actorIndex = 0;

    [Header("Snippet")]
    [Tooltip("Snippet index to play when Action is Snippet.")]
    public int snippetIndex = 0;

    [Header("Custom Animation")]
    [Tooltip("Custom animation index to play when Action is Custom Animation or Snippet + Custom Animation.")]
    public int customAnimationIndex = 0;

    [Tooltip("Which playback completion this controller waits for when Action is Snippet + Custom Animation.")]
    public SnippetsFlowController.CompletionPolicy completionPolicy = SnippetsFlowController.CompletionPolicy.WaitForBoth;

    [Tooltip("Optional legacy overlay mask applied to the snippet animation when Action is Snippet + Custom Animation.")]
    public SnippetsActorRegistry.SnippetMaskMode snippetMaskMode = SnippetsActorRegistry.SnippetMaskMode.None;

    [Header("Walk")]
    [Tooltip("Waypoint index to move to when Action is Walk.")]
    public int waypointIndex = 0;

    [Header("Keyboard (Start)")]
    [Tooltip("Lets the configured keyboard key trigger Play during Play Mode.")]
    public bool enableKeyboard = false;

    [Tooltip("Keyboard key that starts the configured action in Play Mode.")]
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

            case ActionType.CustomAnim:
                yield return ExecuteCustomAnimation();
                break;

            case ActionType.SnippetWithCustomAnim:
                yield return ExecuteSnippetWithCustomAnimation();
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

    IEnumerator ExecuteCustomAnimation()
    {
        if (!HasPlayableCustomAnimation())
            yield break;

        customAnimationIndex = ClampCustomAnimationIndex();

        registry.PlayCustomAnimationOnce(actorIndex, customAnimationIndex);
        yield return WaitForCustomAnimationCompletion();

        if (_running && !registry.IsCustomAnimationPlaying(actorIndex))
            registry.FadeToIdleNow(actorIndex);
    }

    IEnumerator ExecuteSnippetWithCustomAnimation()
    {
        if (!HasPlayableSnippet() || !HasPlayableCustomAnimation())
            yield break;

        snippetIndex = ClampSnippetIndex();
        customAnimationIndex = ClampCustomAnimationIndex();

        registry.PlaySnippetSpeechWithCustomAnimationOnce(
            actorIndex,
            snippetIndex,
            customAnimationIndex,
            snippetMaskMode);

        bool waitForSnippet =
            completionPolicy == SnippetsFlowController.CompletionPolicy.WaitForSnippet ||
            completionPolicy == SnippetsFlowController.CompletionPolicy.WaitForBoth;
        bool waitForCustom =
            completionPolicy == SnippetsFlowController.CompletionPolicy.WaitForCustomAnim ||
            completionPolicy == SnippetsFlowController.CompletionPolicy.WaitForBoth;

        if (waitForSnippet)
            yield return WaitForSnippetCompletion();

        if (!_running)
            yield break;

        if (waitForCustom)
            yield return WaitForCustomAnimationCompletion();

        if (_running && !registry.IsCustomAnimationPlaying(actorIndex))
        {
            registry.FadeActorBlendshapesToZeroAfterSnippet(actorIndex);

            var actor = registry.GetActor(actorIndex);
            if (actor != null && actor.legacyAnimation != null && !actor.legacyAnimation.isPlaying)
                registry.PlayIdleImmediate(actorIndex);
        }
    }

    IEnumerator WaitForSnippetCompletion()
    {
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

    IEnumerator WaitForCustomAnimationCompletion()
    {
        while (_running && registry != null && registry.IsCustomAnimationPlaying(actorIndex))
            yield return null;
    }

    int ClampSnippetIndex()
    {
        var snippets = registry.GetSnippets(actorIndex);
        int count = snippets != null ? snippets.Count : 0;
        return count <= 0 ? 0 : Mathf.Clamp(snippetIndex, 0, count - 1);
    }

    int ClampCustomAnimationIndex()
    {
        var customAnimations = registry.GetCustomAnimations(actorIndex);
        int count = customAnimations != null ? customAnimations.Count : 0;
        return count <= 0 ? 0 : Mathf.Clamp(customAnimationIndex, 0, count - 1);
    }

    bool HasPlayableSnippet()
    {
        var snippets = registry.GetSnippets(actorIndex);
        if (snippets == null || snippetIndex < 0 || snippetIndex >= snippets.Count)
            return false;

        var snippet = snippets[snippetIndex];
        return snippet != null && snippet.Value != null && snippet.Value.IsValid;
    }

    bool HasPlayableCustomAnimation()
    {
        var customAnimations = registry.GetCustomAnimations(actorIndex);
        if (customAnimations == null || customAnimationIndex < 0 || customAnimationIndex >= customAnimations.Count)
            return false;

        var customAnimation = customAnimations[customAnimationIndex];
        return customAnimation != null && customAnimation.clip != null;
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
    static readonly GUIContent GC_Registry = new("Registry", "Actor registry used to resolve actors, snippets, and walkers.");
    static readonly GUIContent GC_Action = new("Action", "Whether this controller triggers a snippet, custom animation, combined snippet/custom animation, or moves an actor to a waypoint.");
    static readonly GUIContent GC_Actor = new("Actor", "Actor index inside the registry to control.");
    static readonly GUIContent GC_Snippet = new("Snippet", "Snippet index to play when Action is Snippet.");
    static readonly GUIContent GC_CustomAnimation = new("Custom Animation", "Custom animation index to play when Action is Custom Animation or Snippet + Custom Animation.");
    static readonly GUIContent GC_Completion = new("Completion", "Which playback completion this controller waits for when Action is Snippet + Custom Animation.");
    static readonly GUIContent GC_SnippetMask = new("Snippet Mask", "Optional legacy overlay mask applied to the snippet animation when Action is Snippet + Custom Animation.");
    static readonly GUIContent GC_Waypoint = new("Waypoint", "Waypoint index to move to when Action is Walk.");
    static readonly GUIContent GC_EnableKeyboard = new("Enable Keyboard", "Lets the configured keyboard key trigger Play during Play Mode.");
    static readonly GUIContent GC_Key = new("Key", "Keyboard key that starts the configured action in Play Mode.");

    public override void OnInspectorGUI()
    {
        var ctrl = (SnippetsSimpleController)target;
        if (ctrl == null) return;

        ctrl.registry = (SnippetsActorRegistry)EditorGUILayout.ObjectField(GC_Registry, ctrl.registry, typeof(SnippetsActorRegistry), true);

        EditorGUILayout.Space(8);

        ctrl.action = (SnippetsSimpleController.ActionType)EditorGUILayout.EnumPopup(GC_Action, ctrl.action);

        DrawActor(ctrl);

        switch (ctrl.action)
        {
            case SnippetsSimpleController.ActionType.Snippet:
                DrawSnippet(ctrl);
                break;

            case SnippetsSimpleController.ActionType.Walk:
                DrawWaypoint(ctrl);
                break;

            case SnippetsSimpleController.ActionType.CustomAnim:
                DrawCustomAnimation(ctrl);
                break;

            case SnippetsSimpleController.ActionType.SnippetWithCustomAnim:
                DrawSnippet(ctrl);
                DrawCustomAnimation(ctrl);
                ctrl.snippetMaskMode = (SnippetsActorRegistry.SnippetMaskMode)EditorGUILayout.EnumPopup(GC_SnippetMask, ctrl.snippetMaskMode);
                ctrl.completionPolicy = (SnippetsFlowController.CompletionPolicy)EditorGUILayout.EnumPopup(GC_Completion, ctrl.completionPolicy);
                break;
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Keyboard (Start)", EditorStyles.boldLabel);
        ctrl.enableKeyboard = EditorGUILayout.Toggle(GC_EnableKeyboard, ctrl.enableKeyboard);
        using (new EditorGUI.DisabledScope(!ctrl.enableKeyboard))
        {
            ctrl.key = (KeyCode)EditorGUILayout.EnumPopup(GC_Key, ctrl.key);
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
        var actorLabel = new GUIContent($"{GC_Actor.text} [{ctrl.actorIndex}]", GC_Actor.tooltip);

        if (ctrl.registry != null && ctrl.registry.ActorCount > 0)
        {
            int actorCount = ctrl.registry.ActorCount;
            string[] actorNames = new string[actorCount];
            for (int i = 0; i < actorCount; i++)
                actorNames[i] = ctrl.registry.GetActorDisplayName(i);

            ctrl.actorIndex = Mathf.Clamp(ctrl.actorIndex, 0, actorCount - 1);

            int newActor = EditorGUILayout.Popup(
                actorLabel,
                ctrl.actorIndex,
                actorNames
            );

            if (newActor != ctrl.actorIndex)
            {
                Undo.RecordObject(ctrl, "Change Actor");
                ctrl.actorIndex = newActor;
                ctrl.snippetIndex = 0;
                ctrl.customAnimationIndex = 0;
                ctrl.waypointIndex = 0;
            }
        }
        else
        {
            ctrl.actorIndex = EditorGUILayout.IntField(actorLabel, ctrl.actorIndex);
        }
    }

    void DrawSnippet(SnippetsSimpleController ctrl)
    {
        var snippetLabel = new GUIContent($"{GC_Snippet.text} [{ctrl.snippetIndex}]", GC_Snippet.tooltip);

        if (ctrl.registry == null)
        {
            ctrl.snippetIndex = EditorGUILayout.IntField(snippetLabel, ctrl.snippetIndex);
            return;
        }

        var snippets = ctrl.registry.GetSnippets(ctrl.actorIndex);
        int snCount = snippets != null ? snippets.Count : 0;

        if (snCount <= 0)
        {
            EditorGUILayout.HelpBox("This actor has no snippets assigned.", MessageType.Info);
            ctrl.snippetIndex = EditorGUILayout.IntField(snippetLabel, ctrl.snippetIndex);
            return;
        }

        string[] snippetNames = new string[snCount];
        for (int i = 0; i < snCount; i++)
            snippetNames[i] = ctrl.registry.GetSnippetDisplayName(ctrl.actorIndex, i);

        ctrl.snippetIndex = Mathf.Clamp(ctrl.snippetIndex, 0, snCount - 1);

        int newSn = EditorGUILayout.Popup(
            snippetLabel,
            ctrl.snippetIndex,
            snippetNames
        );

        if (newSn != ctrl.snippetIndex)
        {
            Undo.RecordObject(ctrl, "Change Snippet");
            ctrl.snippetIndex = newSn;
        }
    }

    void DrawCustomAnimation(SnippetsSimpleController ctrl)
    {
        var customAnimationLabel = new GUIContent($"{GC_CustomAnimation.text} [{ctrl.customAnimationIndex}]", GC_CustomAnimation.tooltip);

        if (ctrl.registry == null)
        {
            ctrl.customAnimationIndex = EditorGUILayout.IntField(customAnimationLabel, ctrl.customAnimationIndex);
            return;
        }

        var customAnimations = ctrl.registry.GetCustomAnimations(ctrl.actorIndex);
        int customAnimationCount = customAnimations != null ? customAnimations.Count : 0;

        if (customAnimationCount <= 0)
        {
            EditorGUILayout.HelpBox("This actor has no custom animations assigned.", MessageType.Info);
            ctrl.customAnimationIndex = EditorGUILayout.IntField(customAnimationLabel, ctrl.customAnimationIndex);
            return;
        }

        string[] customAnimationNames = new string[customAnimationCount];
        for (int i = 0; i < customAnimationCount; i++)
            customAnimationNames[i] = ctrl.registry.GetCustomAnimationDisplayName(ctrl.actorIndex, i);

        ctrl.customAnimationIndex = Mathf.Clamp(ctrl.customAnimationIndex, 0, customAnimationCount - 1);

        int newCustomAnimation = EditorGUILayout.Popup(
            customAnimationLabel,
            ctrl.customAnimationIndex,
            customAnimationNames
        );

        if (newCustomAnimation != ctrl.customAnimationIndex)
        {
            Undo.RecordObject(ctrl, "Change Custom Animation");
            ctrl.customAnimationIndex = newCustomAnimation;
        }
    }

    void DrawWaypoint(SnippetsSimpleController ctrl)
    {
        var waypointLabel = new GUIContent($"{GC_Waypoint.text} [{ctrl.waypointIndex}]", GC_Waypoint.tooltip);

        if (ctrl.registry == null)
        {
            ctrl.waypointIndex = EditorGUILayout.IntField(waypointLabel, ctrl.waypointIndex);
            return;
        }

        var walker = ctrl.registry.GetWalker(ctrl.actorIndex);
        if (walker == null)
        {
            EditorGUILayout.HelpBox("This actor has no Walker assigned in the registry.", MessageType.Info);
            ctrl.waypointIndex = EditorGUILayout.IntField(waypointLabel, ctrl.waypointIndex);
            return;
        }

        int wpCount = walker.waypoints != null ? walker.waypoints.Length : 0;
        if (wpCount <= 0)
        {
            EditorGUILayout.HelpBox("Walker has no waypoints assigned.", MessageType.Info);
            ctrl.waypointIndex = EditorGUILayout.IntField(waypointLabel, ctrl.waypointIndex);
            return;
        }

        string[] wpNames = new string[wpCount];
        for (int i = 0; i < wpCount; i++)
            wpNames[i] = walker.waypoints[i] != null ? walker.waypoints[i].name : $"Waypoint {i}";

        ctrl.waypointIndex = Mathf.Clamp(ctrl.waypointIndex, 0, wpCount - 1);

        int newWp = EditorGUILayout.Popup(
            waypointLabel,
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
