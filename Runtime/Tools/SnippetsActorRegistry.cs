using System;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Snippets.Sdk;

[DisallowMultipleComponent]
public class SnippetsActorRegistry : MonoBehaviour
{
    public enum StopMode
    {
        Soft,
        Hard
    }

    public enum DefaultLoopAnimationMode
    {
        [InspectorName("Auto")]
        Auto,
        [InspectorName("Male Default")]
        RpmMale,
        [InspectorName("Female Default")]
        RpmFemale,
        [InspectorName("Custom")]
        None
    }

    public enum SnippetMaskMode
    {
        [InspectorName("None")]
        None,
        [InspectorName("Head Only")]
        HeadOnly,
        [InspectorName("Upper Body")]
        UpperBody,
        [InspectorName("Face Only")]
        FaceOnly
    }

    [Serializable]
    public class CustomAnimationDefinition
    {
        [Tooltip("Display name shown in UIs. Falls back to the clip name when empty.")]
        public string name;

        [Tooltip("Legacy AnimationClip that can be reused by flows and controllers for this actor.")]
        public AnimationClip clip;
    }

    [Serializable]
    public class Actor
    {
        [Tooltip("Display name shown in UIs.")]
        public string name;

        [Tooltip("Runtime SnippetPlayer used to play snippets, audio, and facial animation for this actor.")]
        public SnippetPlayer player;

        [Header("Movement")]
        [Tooltip("SnippetsWalker responsible for moving this actor's parent/root object. If left empty, it can be auto-found around the player.")]
        public SnippetsWalker walker;

        [Header("Gaze")]
        [FormerlySerializedAs("headTurn")]
        [Tooltip("SnippetsGazeDriver component for this actor. If left empty, it can be auto-found around the player.")]
        public SnippetsGazeDriver gazeDriver;

        [Header("Legacy Animation (on the rig)")]
        [Tooltip("Legacy Animation component driving the rig. If left empty, it can be auto-found around the player.")]
        public Animation legacyAnimation;

        [Tooltip("Which built-in default idle and walk clips the editor should assign when using setup helpers. Choose Custom to keep manual clip assignments.")]
        public DefaultLoopAnimationMode defaultLoopAnimations = DefaultLoopAnimationMode.Auto;

        [Tooltip("Looping idle clip used when the actor is standing.")]
        public AnimationClip idleClip;

        [Tooltip("Looping walk clip used while the actor is moving.")]
        public AnimationClip walkClip;

        [Header("Snippet templates (assets/components)")]
        [Tooltip("Snippet templates available for this actor in controller dropdowns and flow steps.")]
        public List<SnippetPlayer> snippets = new();

        [Header("Custom animations")]
        [Tooltip("Reusable custom legacy Animation clips that controllers and flows can play for this actor.")]
        public List<CustomAnimationDefinition> customAnimations = new();
    }

    [Header("Actors")]
    [Tooltip("All actors available to controllers and flow sequences.")]
    public List<Actor> actors = new();

    [Header("Defaults")]
    [Tooltip("If enabled, actors are forced into their idle loops when this registry awakens or is enabled in Play Mode.")]
    public bool forceIdleOnEnable = true;

    [Header("Blending (Legacy Animation)")]
    [Tooltip("Cross-fade duration used when transitioning between idle, walk, and snippet clips on the legacy Animation component.")]
    [Range(0f, 2f)] public float crossFadeSeconds = 0.25f;

    [Header("Auto-Find Components")]
    [FormerlySerializedAs("autoFindHeadTurn")]
    [Tooltip("Automatically find a SnippetsGazeDriver around each actor's player when Gaze Driver is not assigned.")]
    public bool autoFindGazeDriver = true;

    [Header("Face Neutralization")]
    [Tooltip("Seconds used to fade a finished actor's facial blendshapes back to neutral after a snippet completes.")]
    [Range(0.01f, 1f)] public float snippetEndBlendshapeFadeSeconds = 0.18f;

    class Runtime
    {
        public Dictionary<AnimationClip, string> aliasOf = new();
        public string currentAlias;
        public string activeCustomAlias;
        public string activeSnippetOverlayAlias;
        public readonly List<Transform> activeSnippetOverlayRoots = new();
        public UnityAction snippetOverlayCleanup;
    }

    readonly Dictionary<Actor, Runtime> _rt = new();
    readonly Dictionary<Actor, Coroutine> _blendshapeFadeCos = new();
    readonly HashSet<Actor> _activeBlendshapeNeutralizers = new();
    readonly HashSet<SnippetPlayer> _managedPlayers = new();

    void Awake()
    {
        BuildRuntimeRegistry();
        if (Application.isPlaying)
            ResetAllActorBlendshapesImmediate();
        if (forceIdleOnEnable && Application.isPlaying)
            ForceAllIdleImmediate();
    }

    void OnEnable()
    {
        BuildRuntimeRegistry();

        if (Application.isPlaying)
            ResetAllActorBlendshapesImmediate();

        if (forceIdleOnEnable && Application.isPlaying)
            ForceAllIdleImmediate();
    }

    void OnDisable()
    {
        CancelAllBlendshapeFades();
        ClearAllSnippetOverlays();
        ReleaseManagedPlayers();
    }

    void LateUpdate()
    {
        if (_activeBlendshapeNeutralizers.Count == 0)
            return;

        var toRemove = new List<Actor>();

        foreach (var actor in _activeBlendshapeNeutralizers)
        {
            if (actor == null || actor.player == null)
            {
                toRemove.Add(actor);
                continue;
            }

            NeutralizeBlendshapesStep(actor);
        }

        for (int i = 0; i < toRemove.Count; i++)
            _activeBlendshapeNeutralizers.Remove(toRemove[i]);
    }

    public int ActorCount => actors != null ? actors.Count : 0;

    public Actor GetActor(int actorIndex)
    {
        if (actors == null) return null;
        if (actorIndex < 0 || actorIndex >= actors.Count) return null;
        return actors[actorIndex];
    }

    public SnippetsWalker GetWalker(int actorIndex) => GetActor(actorIndex)?.walker;
    public SnippetsGazeDriver GetGazeDriver(int actorIndex) => GetActor(actorIndex)?.gazeDriver;
    public IReadOnlyList<SnippetPlayer> GetSnippets(int actorIndex) => GetActor(actorIndex)?.snippets;
    public IReadOnlyList<CustomAnimationDefinition> GetCustomAnimations(int actorIndex) => GetActor(actorIndex)?.customAnimations;

    [Obsolete("Use GetGazeDriver(int) instead.")]
    public SnippetsGazeDriver GetHeadTurn(int actorIndex) => GetGazeDriver(actorIndex);

    public string GetActorDisplayName(int actorIndex)
    {
        var a = GetActor(actorIndex);
        if (a == null) return $"Actor {actorIndex}";
        if (!string.IsNullOrWhiteSpace(a.name))
            return a.name;
        if (a.player != null && !string.IsNullOrWhiteSpace(a.player.name))
            return a.player.name;
        return $"Actor {actorIndex}";
    }

    public string GetSnippetDisplayName(int actorIndex, int snippetIndex)
    {
        var a = GetActor(actorIndex);
        if (a == null || a.snippets == null) return $"Snippet {snippetIndex}";
        if (snippetIndex < 0 || snippetIndex >= a.snippets.Count) return $"Snippet {snippetIndex}";

        var sn = a.snippets[snippetIndex];
        if (sn != null && !string.IsNullOrWhiteSpace(sn.name))
            return sn.name;

        var clip = sn != null ? sn.Value.Animation : null;
        if (clip != null && !string.IsNullOrWhiteSpace(clip.name))
            return clip.name;

        return $"Snippet {snippetIndex}";
    }

    public string GetCustomAnimationDisplayName(int actorIndex, int customAnimationIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.customAnimations == null)
            return $"Custom Animation {customAnimationIndex}";
        if (customAnimationIndex < 0 || customAnimationIndex >= actor.customAnimations.Count)
            return $"Custom Animation {customAnimationIndex}";

        var customAnimation = actor.customAnimations[customAnimationIndex];
        if (customAnimation == null)
            return $"Custom Animation {customAnimationIndex}";

        if (!string.IsNullOrWhiteSpace(customAnimation.name))
            return customAnimation.name;

        if (customAnimation.clip != null && !string.IsNullOrWhiteSpace(customAnimation.clip.name))
            return customAnimation.clip.name;

        return $"Custom Animation {customAnimationIndex}";
    }

    public void BuildRuntimeRegistry()
    {
        ReleaseManagedPlayers();
        _rt.Clear();
        if (actors == null) return;

        foreach (var a in actors)
        {
            if (a == null || a.player == null) continue;

            ClaimManagedPlayer(a.player);

            if (a.walker == null)
                a.walker = a.player.GetComponentInParent<SnippetsWalker>(true) ??
                           a.player.GetComponentInChildren<SnippetsWalker>(true);

            if (a.legacyAnimation == null)
                a.legacyAnimation = a.player.GetComponentInChildren<Animation>(true) ??
                                    a.player.GetComponentInParent<Animation>(true);

            if (autoFindGazeDriver && a.gazeDriver == null)
                a.gazeDriver = a.player.GetComponentInChildren<SnippetsGazeDriver>(true) ??
                               a.player.GetComponentInParent<SnippetsGazeDriver>(true);

            if (a.legacyAnimation == null) continue;

            var rt = new Runtime();
            _rt[a] = rt;

            RegisterClip(a, rt, a.idleClip);
            RegisterClip(a, rt, a.walkClip);

            if (a.snippets != null)
            {
                foreach (var sn in a.snippets)
                {
                    var clip = sn != null ? sn.Value.Animation : null;
                    RegisterClip(a, rt, clip);
                }
            }

            if (a.customAnimations != null)
            {
                foreach (var customAnimation in a.customAnimations)
                    RegisterClip(a, rt, customAnimation?.clip);
            }
        }
    }

    void ClaimManagedPlayer(SnippetPlayer player)
    {
        if (player == null || !_managedPlayers.Add(player))
            return;

        player.AcquireControllerManagement();
    }

    void ReleaseManagedPlayers()
    {
        foreach (var player in _managedPlayers)
        {
            if (player != null)
                player.ReleaseControllerManagement();
        }

        _managedPlayers.Clear();
    }

    public void ForceAllIdleImmediate()
    {
        if (!Application.isPlaying) return;
        if (actors == null) return;

        foreach (var a in actors)
            PlayIdleImmediate(a);
    }

    public void ResetAllActorBlendshapesImmediate()
    {
        if (actors == null) return;

        foreach (var actor in actors)
            ResetBlendshapesImmediate(actor);
    }

    public void StopAllToIdle(StopMode mode = StopMode.Soft)
    {
        if (actors == null) return;
        for (int i = 0; i < actors.Count; i++)
            StopToIdle(i, mode);
    }

    public void StopToIdle(int actorIndex, StopMode mode = StopMode.Soft)
    {
        if (mode == StopMode.Hard)
            StopActorAndReturnToIdle(actorIndex);
        else
            SoftStopActorToIdle(actorIndex);
    }

    public void StopAllAndReturnToIdle()
    {
        StopAllToIdle(StopMode.Hard);
    }

    void StopSnippetPlaybackAndAudio(Actor actor)
    {
        if (actor == null || actor.player == null) return;

        CancelBlendshapeFade(actor);
        ClearSnippetOverlay(actor);

        var go = actor.player.gameObject;

        go.BroadcastMessage("Stop", SendMessageOptions.DontRequireReceiver);
        go.BroadcastMessage("StopPlayback", SendMessageOptions.DontRequireReceiver);
        go.BroadcastMessage("StopSnippetSequence", SendMessageOptions.DontRequireReceiver);

        var audioSources = actor.player.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (!audioSources[i]) continue;
            audioSources[i].Stop();
            audioSources[i].time = 0f;
        }
    }

    public void StopActorAndReturnToIdle(int actorIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null) return;

        StopSnippetPlaybackAndAudio(actor);
        ClearActiveCustomAnimation(actor);

        if (actor.legacyAnimation != null)
            actor.legacyAnimation.Stop();

        PlayIdleImmediate(actor);
    }

    public void SoftStopActorToIdle(int actorIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null) return;

        StopSnippetPlaybackAndAudio(actor);
        ClearActiveCustomAnimation(actor);

        // Start the blend to idle
        FadeToIdleNow(actorIndex);
    }

    public void PlayIdleImmediate(int actorIndex) => PlayIdleImmediate(GetActor(actorIndex));
    public void FadeToIdleNow(int actorIndex)
    {
        FadeToLoopNow(actorIndex, isWalk: false);
        ScheduleBlendshapeNeutralization(GetActor(actorIndex), 0f);
    }
    public void FadeToWalkNow(int actorIndex)
    {
        FadeToLoopNow(actorIndex, isWalk: true);
    }

    void FadeToLoopNow(int actorIndex, bool isWalk)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.legacyAnimation == null) return;
        if (!_rt.TryGetValue(actor, out var rt)) return;

        var clip = isWalk ? actor.walkClip : actor.idleClip;
        if (clip == null) return;

        EnsureRegistered(actor, rt, clip);
        if (!rt.aliasOf.TryGetValue(clip, out var alias)) return;

        clip.wrapMode = WrapMode.Loop;

        var st = actor.legacyAnimation[alias];
        if (st != null)
        {
            st.wrapMode = WrapMode.Loop;
            st.layer = 0;
            st.blendMode = AnimationBlendMode.Blend;
        }

        if (string.IsNullOrEmpty(rt.currentAlias) || !actor.legacyAnimation.isPlaying)
        {
            actor.legacyAnimation.Stop();
            actor.legacyAnimation.Play(alias);
        }
        else
        {
            actor.legacyAnimation.CrossFade(alias, crossFadeSeconds);
        }

        rt.currentAlias = alias;
        rt.activeCustomAlias = null;

    }

    public void StartSnippetNow(int actorIndex, int snippetIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.player == null || actor.legacyAnimation == null) return;

        CancelBlendshapeFade(actor);
        ClearSnippetOverlay(actor);

        var list = actor.snippets;
        if (list == null || snippetIndex < 0 || snippetIndex >= list.Count) return;

        var snippetTemplate = list[snippetIndex];
        if (snippetTemplate == null) return;

        if (!_rt.TryGetValue(actor, out var rt)) return;

        var snippetValue = snippetTemplate.Value;
        var snippetClip = snippetValue.Animation;
        if (snippetClip == null) return;

        actor.player.Value = snippetValue;
        CrossFadeOrPlay(actor, rt, snippetClip);
        actor.player.Play();
        rt.activeCustomAlias = null;
    }

    public void QueueSnippetAfterCurrent(int actorIndex, int snippetIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.legacyAnimation == null) return;

        var list = actor.snippets;
        if (list == null || snippetIndex < 0 || snippetIndex >= list.Count) return;

        var snippetTemplate = list[snippetIndex];
        if (snippetTemplate == null) return;

        if (!_rt.TryGetValue(actor, out var rt)) return;

        QueueClipAfterCurrent(actor, rt, snippetTemplate.Value.Animation);
    }

    public void QueueIdleAfterCurrent(int actorIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.legacyAnimation == null) return;

        if (!_rt.TryGetValue(actor, out var rt)) return;
        QueueLoopAfterCurrent(actor, rt, actor.idleClip);
    }

    public void QueueWalkAfterCurrent(int actorIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.legacyAnimation == null) return;

        if (!_rt.TryGetValue(actor, out var rt)) return;
        QueueLoopAfterCurrent(actor, rt, actor.walkClip);
    }

    public void StartCustomAnimationNow(int actorIndex, int customAnimationIndex, bool stopSnippetPlayback = true)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.legacyAnimation == null) return;
        if (!_rt.TryGetValue(actor, out var rt)) return;

        var customClip = GetCustomAnimationClip(actor, customAnimationIndex);
        if (customClip == null) return;

        CancelBlendshapeFade(actor);

        if (stopSnippetPlayback)
        {
            StopSnippetPlaybackAndAudio(actor);
            ScheduleBlendshapeNeutralization(actor, 0f);
        }
        else
        {
            ClearSnippetOverlay(actor);
        }

        CrossFadeOrPlay(actor, rt, customClip);

        if (rt.aliasOf.TryGetValue(customClip, out var alias))
            rt.activeCustomAlias = alias;
    }

    public void QueueCustomAnimationAfterCurrent(int actorIndex, int customAnimationIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.legacyAnimation == null) return;
        if (!_rt.TryGetValue(actor, out var rt)) return;

        var customClip = GetCustomAnimationClip(actor, customAnimationIndex);
        if (customClip == null) return;

        QueueClipAfterCurrent(actor, rt, customClip);

        if (rt.aliasOf.TryGetValue(customClip, out var alias))
            rt.activeCustomAlias = alias;
    }

    public void PlayCustomAnimationOnce(int actorIndex, int customAnimationIndex, bool stopSnippetPlayback = true)
    {
        StartCustomAnimationNow(actorIndex, customAnimationIndex, stopSnippetPlayback);
        QueueIdleAfterCurrent(actorIndex);
    }

    public void StartSnippetSpeechWithCustomAnimationNow(
        int actorIndex,
        int snippetIndex,
        int customAnimationIndex,
        SnippetMaskMode snippetMaskMode = SnippetMaskMode.None)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.player == null || actor.legacyAnimation == null) return;
        if (!_rt.TryGetValue(actor, out var rt)) return;

        var list = actor.snippets;
        if (list == null || snippetIndex < 0 || snippetIndex >= list.Count) return;

        var snippetTemplate = list[snippetIndex];
        if (snippetTemplate == null) return;

        var customClip = GetCustomAnimationClip(actor, customAnimationIndex);
        if (customClip == null) return;

        CancelBlendshapeFade(actor);

        var snippetValue = snippetTemplate.Value;
        if (snippetValue == null || !snippetValue.IsValid) return;

        actor.player.Value = snippetValue;
        ClearSnippetOverlay(actor);
        CrossFadeOrPlay(actor, rt, customClip);

        if (snippetMaskMode != SnippetMaskMode.None && snippetValue.Animation != null)
            TryPlaySnippetOverlay(actor, rt, snippetValue.Animation, snippetMaskMode);

        actor.player.Play();

        if (rt.aliasOf.TryGetValue(customClip, out var alias))
            rt.activeCustomAlias = alias;
    }

    public void PlaySnippetSpeechWithCustomAnimationOnce(
        int actorIndex,
        int snippetIndex,
        int customAnimationIndex,
        SnippetMaskMode snippetMaskMode = SnippetMaskMode.None)
    {
        StartSnippetSpeechWithCustomAnimationNow(actorIndex, snippetIndex, customAnimationIndex, snippetMaskMode);
        QueueIdleAfterCurrent(actorIndex);
    }

    public bool IsCustomAnimationPlaying(int actorIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.legacyAnimation == null) return false;
        if (!_rt.TryGetValue(actor, out var rt)) return false;
        if (string.IsNullOrEmpty(rt.activeCustomAlias)) return false;

        bool isPlaying = actor.legacyAnimation.IsPlaying(rt.activeCustomAlias);
        if (!isPlaying)
            rt.activeCustomAlias = null;

        return isPlaying;
    }

    public void PlaySnippetOnce(int actorIndex, int snippetIndex)
    {
        StartSnippetNow(actorIndex, snippetIndex);
        QueueIdleAfterCurrent(actorIndex);
    }

    public void FadeActorBlendshapesToZeroAfterSnippet(int actorIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.player == null)
            return;

        ScheduleBlendshapeNeutralization(actor, 0f);
    }

    void PlayIdleImmediate(Actor actor)
    {
        if (actor == null || actor.legacyAnimation == null || actor.idleClip == null) return;
        if (!_rt.TryGetValue(actor, out var rt)) return;

        EnsureRegistered(actor, rt, actor.idleClip);
        if (!rt.aliasOf.TryGetValue(actor.idleClip, out var idleAlias)) return;

        actor.idleClip.wrapMode = WrapMode.Loop;

        var st = actor.legacyAnimation[idleAlias];
        if (st != null)
        {
            st.wrapMode = WrapMode.Loop;
            st.layer = 0;
            st.blendMode = AnimationBlendMode.Blend;
        }

        actor.legacyAnimation.Stop();
        actor.legacyAnimation.Play(idleAlias);
        rt.currentAlias = idleAlias;
        rt.activeCustomAlias = null;

        ScheduleBlendshapeNeutralization(actor, 0f);
    }

    void QueueLoopAfterCurrent(Actor actor, Runtime rt, AnimationClip loopClip)
    {
        if (actor == null || actor.legacyAnimation == null || loopClip == null) return;

        EnsureRegistered(actor, rt, loopClip);
        if (!rt.aliasOf.TryGetValue(loopClip, out var alias)) return;

        loopClip.wrapMode = WrapMode.Loop;

        var st = actor.legacyAnimation[alias];
        if (st != null)
        {
            st.wrapMode = WrapMode.Loop;
            st.layer = 0;
            st.blendMode = AnimationBlendMode.Blend;
        }

        actor.legacyAnimation.CrossFadeQueued(
            alias,
            crossFadeSeconds,
            QueueMode.CompleteOthers,
            PlayMode.StopSameLayer
        );

        rt.currentAlias = alias;
    }

    void QueueClipAfterCurrent(Actor actor, Runtime rt, AnimationClip clip)
    {
        if (actor == null || actor.legacyAnimation == null || clip == null) return;

        EnsureRegistered(actor, rt, clip);
        if (!rt.aliasOf.TryGetValue(clip, out var alias)) return;

        clip.wrapMode = WrapMode.Once;

        var st = actor.legacyAnimation[alias];
        if (st != null)
        {
            st.wrapMode = WrapMode.Once;
            st.layer = 0;
            st.blendMode = AnimationBlendMode.Blend;
        }

        actor.legacyAnimation.CrossFadeQueued(
            alias,
            crossFadeSeconds,
            QueueMode.CompleteOthers,
            PlayMode.StopSameLayer
        );
    }

    void CrossFadeOrPlay(Actor actor, Runtime rt, AnimationClip clip)
    {
        if (actor == null || actor.legacyAnimation == null || clip == null) return;

        EnsureRegistered(actor, rt, clip);
        if (!rt.aliasOf.TryGetValue(clip, out var alias)) return;

        clip.wrapMode = WrapMode.Once;

        bool shouldBlend =
            crossFadeSeconds > 0f &&
            actor.legacyAnimation.isPlaying &&
            !string.IsNullOrEmpty(rt.currentAlias) &&
            !string.Equals(rt.currentAlias, alias, StringComparison.Ordinal);

        var st = actor.legacyAnimation[alias];
        if (st != null)
        {
            st.wrapMode = WrapMode.Once;
            st.layer = 0;
            st.blendMode = AnimationBlendMode.Blend;
            st.time = 0f;
            st.speed = 1f;
            st.enabled = true;
            if (!shouldBlend)
                st.weight = 1f;
        }

        if (shouldBlend)
        {
            // Start the snippet clip at frame 0 while preserving the current loop long enough
            // for a visible transition into the speech animation.
            actor.legacyAnimation.CrossFade(alias, crossFadeSeconds, PlayMode.StopSameLayer);
        }
        else
        {
            // Fall back to an immediate start when nothing meaningful is already playing.
            actor.legacyAnimation.Stop();
            actor.legacyAnimation.Play(alias);
            actor.legacyAnimation.Sample();
        }

        rt.currentAlias = alias;
    }

    void EnsureRegistered(Actor actor, Runtime rt, AnimationClip clip)
    {
        if (actor == null || actor.legacyAnimation == null || clip == null) return;
        if (rt.aliasOf.ContainsKey(clip)) return;

        RegisterClip(actor, rt, clip);
    }

    AnimationClip GetCustomAnimationClip(Actor actor, int customAnimationIndex)
    {
        if (actor == null || actor.customAnimations == null)
            return null;
        if (customAnimationIndex < 0 || customAnimationIndex >= actor.customAnimations.Count)
            return null;

        return actor.customAnimations[customAnimationIndex]?.clip;
    }

    void ClearActiveCustomAnimation(Actor actor)
    {
        if (actor == null) return;
        if (_rt.TryGetValue(actor, out var rt))
            rt.activeCustomAlias = null;
    }

    void ClearAllSnippetOverlays()
    {
        if (actors == null)
            return;

        foreach (var actor in actors)
            ClearSnippetOverlay(actor);
    }

    void ClearSnippetOverlay(Actor actor)
    {
        if (actor == null || actor.legacyAnimation == null)
            return;
        if (!_rt.TryGetValue(actor, out var rt))
            return;
        if (string.IsNullOrEmpty(rt.activeSnippetOverlayAlias))
            return;

        DetachSnippetOverlayCleanup(actor, rt);

        var state = actor.legacyAnimation[rt.activeSnippetOverlayAlias];
        if (state != null)
        {
            for (int i = 0; i < rt.activeSnippetOverlayRoots.Count; i++)
            {
                var overlayRoot = rt.activeSnippetOverlayRoots[i];
                if (overlayRoot != null)
                    state.RemoveMixingTransform(overlayRoot);
            }

            state.enabled = false;
            state.weight = 0f;
            state.layer = 0;
        }

        actor.legacyAnimation.Stop(rt.activeSnippetOverlayAlias);
        rt.activeSnippetOverlayAlias = null;
        rt.activeSnippetOverlayRoots.Clear();
    }

    bool TryPlaySnippetOverlay(Actor actor, Runtime rt, AnimationClip snippetClip, SnippetMaskMode snippetMaskMode)
    {
        if (actor == null || actor.legacyAnimation == null || snippetClip == null || rt == null)
            return false;

        var maskRoots = ResolveSnippetMaskRoots(actor, snippetMaskMode);
        if (maskRoots == null || maskRoots.Count == 0)
            return false;

        EnsureRegistered(actor, rt, snippetClip);
        if (!rt.aliasOf.TryGetValue(snippetClip, out var alias))
            return false;
        if (string.Equals(alias, rt.activeCustomAlias, StringComparison.Ordinal))
            return false;

        var state = actor.legacyAnimation[alias];
        if (state == null)
            return false;

        for (int i = 0; i < maskRoots.Count; i++)
        {
            var maskRoot = maskRoots[i];
            if (maskRoot != null)
                state.AddMixingTransform(maskRoot, true);
        }

        state.wrapMode = WrapMode.Once;
        state.layer = 1;
        state.blendMode = AnimationBlendMode.Blend;
        state.time = 0f;
        state.speed = 1f;
        state.enabled = true;
        state.weight = 1f;

        actor.legacyAnimation.Play(alias, PlayMode.StopSameLayer);
        actor.legacyAnimation.Sample();

        rt.activeSnippetOverlayAlias = alias;
        rt.activeSnippetOverlayRoots.Clear();
        rt.activeSnippetOverlayRoots.AddRange(maskRoots);
        AttachSnippetOverlayCleanup(actor, rt);
        return true;
    }

    void AttachSnippetOverlayCleanup(Actor actor, Runtime rt)
    {
        if (actor == null || actor.player == null || rt == null)
            return;

        DetachSnippetOverlayCleanup(actor, rt);

        rt.snippetOverlayCleanup = () => ClearSnippetOverlay(actor);
        actor.player.PlaybackStopped.AddListener(rt.snippetOverlayCleanup);
    }

    void DetachSnippetOverlayCleanup(Actor actor, Runtime rt)
    {
        if (actor?.player == null || rt?.snippetOverlayCleanup == null)
            return;

        actor.player.PlaybackStopped.RemoveListener(rt.snippetOverlayCleanup);
        rt.snippetOverlayCleanup = null;
    }

    List<Transform> ResolveSnippetMaskRoots(Actor actor, SnippetMaskMode snippetMaskMode)
    {
        var maskRoots = new List<Transform>();
        if (actor == null)
            return maskRoots;

        switch (snippetMaskMode)
        {
            case SnippetMaskMode.HeadOnly:
                AddUniqueMaskRoot(
                    maskRoots,
                    actor.gazeDriver != null && actor.gazeDriver.headBone != null
                        ? actor.gazeDriver.headBone
                        : FindFirstNamedTransform(actor, "head"));
                break;

            case SnippetMaskMode.UpperBody:
                AddUniqueMaskRoot(
                    maskRoots,
                    actor.gazeDriver != null && actor.gazeDriver.waistBone != null
                        ? actor.gazeDriver.waistBone
                        : FindFirstNamedTransform(actor, "upperchest", "chest", "spine2", "spine1", "spine"));
                break;

            case SnippetMaskMode.FaceOnly:
                var headRoot = actor.gazeDriver != null && actor.gazeDriver.headBone != null
                    ? actor.gazeDriver.headBone
                    : FindFirstNamedTransform(actor, "head");

                if (headRoot != null)
                {
                    // Use the head's children as mixing roots so the custom animation and gaze
                    // retain ownership of the head bone rotation while facial descendants still animate.
                    for (int i = 0; i < headRoot.childCount; i++)
                        AddUniqueMaskRoot(maskRoots, headRoot.GetChild(i));
                }

                break;

            default:
                break;
        }

        return maskRoots;
    }

    static void AddUniqueMaskRoot(List<Transform> maskRoots, Transform candidate)
    {
        if (maskRoots == null || candidate == null || maskRoots.Contains(candidate))
            return;

        maskRoots.Add(candidate);
    }

    Transform FindFirstNamedTransform(Actor actor, params string[] candidateNames)
    {
        if (actor == null || candidateNames == null || candidateNames.Length == 0)
            return null;

        var root = actor.legacyAnimation != null
            ? actor.legacyAnimation.transform
            : actor.player != null ? actor.player.transform : null;
        if (root == null)
            return null;

        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            if (transform == null)
                continue;

            string normalizedName = NormalizeTransformName(transform.name);
            for (int c = 0; c < candidateNames.Length; c++)
            {
                if (normalizedName == candidateNames[c])
                    return transform;
            }
        }

        return null;
    }

    static string NormalizeTransformName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        string normalized = name.Trim().ToLowerInvariant();
        return normalized.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
    }

    void RegisterClip(Actor actor, Runtime rt, AnimationClip clip)
    {
        if (actor == null || actor.legacyAnimation == null || clip == null) return;
        if (rt.aliasOf.ContainsKey(clip)) return;

        string alias = $"{clip.name}__{clip.GetInstanceID()}";

        if (actor.legacyAnimation.GetClip(alias) != null)
            actor.legacyAnimation.RemoveClip(alias);

        actor.legacyAnimation.AddClip(clip, alias);
        rt.aliasOf[clip] = alias;

        var st = actor.legacyAnimation[alias];
        if (st != null)
        {
            st.layer = 0;
            st.blendMode = AnimationBlendMode.Blend;
        }
    }

    void ScheduleBlendshapeNeutralization(Actor actor, float delaySeconds)
    {
        if (actor == null || actor.player == null)
            return;

        CancelBlendshapeFade(actor);

        if (delaySeconds <= 0f)
        {
            _activeBlendshapeNeutralizers.Add(actor);
            return;
        }

        _blendshapeFadeCos[actor] = StartCoroutine(BeginBlendshapeNeutralizationAfterDelay(actor, delaySeconds));
    }

    IEnumerator BeginBlendshapeNeutralizationAfterDelay(Actor actor, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (actor != null && actor.player != null)
            _activeBlendshapeNeutralizers.Add(actor);

        ClearBlendshapeFade(actor);
    }

    void ResetBlendshapesImmediate(Actor actor)
    {
        if (actor == null || actor.player == null) return;

        var smrs = actor.player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0) return;

        for (int r = 0; r < smrs.Length; r++)
        {
            var smr = smrs[r];
            if (!smr) continue;

            var mesh = smr.sharedMesh;
            if (!mesh) continue;

            int count = mesh.blendShapeCount;
            for (int i = 0; i < count; i++)
                smr.SetBlendShapeWeight(i, 0f);
        }
    }

    void NeutralizeBlendshapesStep(Actor actor)
    {
        if (actor == null || actor.player == null) return;

        var smrs = actor.player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0) return;

        float duration = Mathf.Max(0.01f, snippetEndBlendshapeFadeSeconds);
        float lerpFactor = 1f - Mathf.Exp((-3f * Time.deltaTime) / duration);

        for (int r = 0; r < smrs.Length; r++)
        {
            var smr = smrs[r];
            if (!smr) continue;

            var mesh = smr.sharedMesh;
            if (!mesh) continue;

            int count = mesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                float current = smr.GetBlendShapeWeight(i);
                float next = Mathf.Lerp(current, 0f, lerpFactor);
                if (Mathf.Abs(next) < 0.005f)
                    next = 0f;

                if (!Mathf.Approximately(current, next))
                    smr.SetBlendShapeWeight(i, next);
            }
        }
    }

    void CancelBlendshapeFade(Actor actor)
    {
        if (actor == null) return;

        if (_blendshapeFadeCos.TryGetValue(actor, out var existing) && existing != null)
            StopCoroutine(existing);

        _blendshapeFadeCos.Remove(actor);
        _activeBlendshapeNeutralizers.Remove(actor);
    }

    void CancelAllBlendshapeFades()
    {
        foreach (var entry in _blendshapeFadeCos)
        {
            if (entry.Value != null)
                StopCoroutine(entry.Value);
        }

        _blendshapeFadeCos.Clear();
        _activeBlendshapeNeutralizers.Clear();
    }

    void ClearBlendshapeFade(Actor actor)
    {
        if (actor == null) return;
        _blendshapeFadeCos.Remove(actor);
    }

}
