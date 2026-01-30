using System;
using System.Collections.Generic;
using UnityEngine;
using Snippets.Sdk;

/// <summary>
/// Registry holds scene setup: actors + idle/walk + snippet templates.
/// Owns Legacy Animation alias registration and low-level playback primitives for:
/// - Snippets
/// - Walk loop
/// - Idle loop
/// - Queueing idle/snippet
/// - Hard stop (stop audio + legacy anim)
///
/// PLUS:
/// - Per-actor SnippetsHeadTurn (gaze) reference for higher-level controllers.
///
/// IMPORTANT: Only ONE system should drive the actor's Legacy Animation component.
/// This registry is that owner.
/// </summary>
[DisallowMultipleComponent]
public class SnippetsActorRegistry : MonoBehaviour
{
    [Serializable]
    public class Actor
    {
        [Tooltip("Display name shown in UIs.")]
        public string name;

        [Tooltip("Runtime SnippetPlayer for this actor.")]
        public SnippetPlayer player;

        [Header("Movement")]
        [Tooltip("SnippetsWalker responsible for moving this actor's parent/root object.")]
        public SnippetsWalker walker;

        [Header("Gaze (Head Turn)")]
        [Tooltip("SnippetsHeadTurn component for this actor (drag & drop). If null, can auto-find under player.")]
        public SnippetsHeadTurn headTurn;

        [Header("Legacy Animation (on the rig)")]
        [Tooltip("Legacy Animation component driving the rig. If null, will auto-find under player.")]
        public Animation legacyAnimation;

        [Tooltip("Looping idle clip.")]
        public AnimationClip idleClip;

        [Tooltip("Looping walk clip.")]
        public AnimationClip walkClip;

        [Header("Snippet templates (assets/components)")]
        public List<SnippetPlayer> snippets = new();
    }

    [Header("Actors")]
    public List<Actor> actors = new();

    [Header("Defaults")]
    public bool forceIdleOnEnable = true;

    [Header("Blending (Legacy Animation)")]
    [Range(0f, 2f)] public float crossFadeSeconds = 0.25f;

    [Header("Auto-Find Components")]
    [Tooltip("If true, BuildRuntimeRegistry will auto-find SnippetsHeadTurn under each actor's player when missing.")]
    public bool autoFindHeadTurn = true;

    // NEW: Face reset
    [Header("Face Reset (Idle)")]
    [Tooltip("If true, when idle is triggered, all SkinnedMeshRenderer blendshapes under the actor.player will be set to 0 once.")]
    public bool resetAllBlendshapesOnIdle = true;

    [Tooltip("If true, also reset on walk start (optional).")]
    public bool resetAllBlendshapesOnWalk = false;

    class Runtime
    {
        public Dictionary<AnimationClip, string> aliasOf = new();
        public string currentAlias;
    }

    readonly Dictionary<Actor, Runtime> _rt = new();

    void Awake()
    {
        BuildRuntimeRegistry();
        if (forceIdleOnEnable && Application.isPlaying)
            ForceAllIdleImmediate();
    }

    void OnEnable()
    {
        if (_rt.Count == 0)
            BuildRuntimeRegistry();

        if (forceIdleOnEnable && Application.isPlaying)
            ForceAllIdleImmediate();
    }

    public int ActorCount => actors != null ? actors.Count : 0;

    public Actor GetActor(int actorIndex)
    {
        if (actors == null) return null;
        if (actorIndex < 0 || actorIndex >= actors.Count) return null;
        return actors[actorIndex];
    }

    public SnippetsWalker GetWalker(int actorIndex) => GetActor(actorIndex)?.walker;

    public SnippetsHeadTurn GetHeadTurn(int actorIndex) => GetActor(actorIndex)?.headTurn;

    public IReadOnlyList<SnippetPlayer> GetSnippets(int actorIndex) => GetActor(actorIndex)?.snippets;

    public string GetActorDisplayName(int actorIndex)
    {
        var a = GetActor(actorIndex);
        if (a == null) return $"Actor {actorIndex}";
        return !string.IsNullOrWhiteSpace(a.name) ? a.name : $"Actor {actorIndex}";
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

    public void BuildRuntimeRegistry()
    {
        _rt.Clear();
        if (actors == null) return;

        foreach (var a in actors)
        {
            if (a == null || a.player == null) continue;

            if (a.legacyAnimation == null)
                a.legacyAnimation = a.player.GetComponentInChildren<Animation>(true);

            if (autoFindHeadTurn && a.headTurn == null)
                a.headTurn = a.player.GetComponentInChildren<SnippetsHeadTurn>(true);

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
        }
    }

    // ============================================================
    // Global
    // ============================================================

    public void ForceAllIdleImmediate()
    {
        if (!Application.isPlaying) return;
        if (actors == null) return;

        foreach (var a in actors)
            PlayIdleImmediate(a);
    }

    public void StopAllAndReturnToIdle()
    {
        if (actors == null) return;
        for (int i = 0; i < actors.Count; i++)
            StopActorAndReturnToIdle(i);
    }

    /// <summary>
    /// HARD STOP: stops snippet playback + audio + legacy animation, then snaps to idle baseline.
    /// Used for "Stop" semantics.
    /// </summary>
    public void StopActorAndReturnToIdle(int actorIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null) return;

        if (actor.player != null)
        {
            // Version-tolerant "stop"
            actor.player.SendMessage("Stop", SendMessageOptions.DontRequireReceiver);
            actor.player.SendMessage("StopPlayback", SendMessageOptions.DontRequireReceiver);
            actor.player.SendMessage("StopSnippetSequence", SendMessageOptions.DontRequireReceiver);

            // Hard-stop all audio sources under the actor
            var audioSources = actor.player.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (!audioSources[i]) continue;
                audioSources[i].Stop();
                audioSources[i].time = 0f;
            }
        }

        if (actor.legacyAnimation != null)
            actor.legacyAnimation.Stop();

        PlayIdleImmediate(actor);
    }

    // ============================================================
    // Playback primitives used by controllers
    // ============================================================

    public void PlayIdleImmediate(int actorIndex) => PlayIdleImmediate(GetActor(actorIndex));
    public void FadeToIdleNow(int actorIndex) => FadeToLoopNow(actorIndex, isWalk: false);
    public void FadeToWalkNow(int actorIndex) => FadeToLoopNow(actorIndex, isWalk: true);

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

        // NEW: reset face on idle/walk trigger (once)
        if (!isWalk && resetAllBlendshapesOnIdle)
            ResetAllBlendshapesOnce(actor);
        else if (isWalk && resetAllBlendshapesOnWalk)
            ResetAllBlendshapesOnce(actor);
    }

    public void StartSnippetNow(int actorIndex, int snippetIndex)
    {
        var actor = GetActor(actorIndex);
        if (actor == null || actor.player == null || actor.legacyAnimation == null) return;

        var list = actor.snippets;
        if (list == null || snippetIndex < 0 || snippetIndex >= list.Count) return;

        var snippetTemplate = list[snippetIndex];
        if (snippetTemplate == null) return;

        if (!_rt.TryGetValue(actor, out var rt)) return;

        var snippetValue = snippetTemplate.Value;
        var snippetClip = snippetValue.Animation;
        if (snippetClip == null) return;

        actor.player.Value = snippetValue;
        actor.player.Play();

        CrossFadeOrPlay(actor, rt, snippetClip);
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

    public void PlaySnippetOnce(int actorIndex, int snippetIndex)
    {
        StartSnippetNow(actorIndex, snippetIndex);
        QueueIdleAfterCurrent(actorIndex);
    }

    // ============================================================
    // Legacy internals
    // ============================================================

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

        // NEW: reset face on idle trigger (once)
        if (resetAllBlendshapesOnIdle)
            ResetAllBlendshapesOnce(actor);
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

        // NOTE: We intentionally DO NOT reset here, because this is "queue", not "idle triggered now".
        // Reset happens when idle is actually started (PlayIdleImmediate / FadeToIdleNow), or via Stop->PlayIdleImmediate.
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

        var st = actor.legacyAnimation[alias];
        if (st != null)
        {
            st.wrapMode = WrapMode.Once;
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
    }

    void EnsureRegistered(Actor actor, Runtime rt, AnimationClip clip)
    {
        if (actor == null || actor.legacyAnimation == null || clip == null) return;
        if (rt.aliasOf.ContainsKey(clip)) return;

        RegisterClip(actor, rt, clip);
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

    // ============================================================
    // NEW: Blendshape reset helpers
    // ============================================================

    void ResetAllBlendshapesOnce(Actor actor)
    {
        if (actor == null) return;
        if (actor.player == null) return;

        var smrs = actor.player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0) return;

        for (int r = 0; r < smrs.Length; r++)
        {
            var smr = smrs[r];
            if (!smr) continue;

            var mesh = smr.sharedMesh;
            if (!mesh) continue;

            int bsCount = mesh.blendShapeCount;
            for (int i = 0; i < bsCount; i++)
            {
                // 0 = neutral
                smr.SetBlendShapeWeight(i, 0f);
            }
        }
    }
}
