#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Snippets.Sdk;

public static class SnippetsActorSetupUtility
{
    public static bool CanAcceptDraggedActors(UnityEngine.Object[] draggedObjects)
    {
        if (draggedObjects == null || draggedObjects.Length == 0)
            return false;

        for (int i = 0; i < draggedObjects.Length; i++)
        {
            if (ExtractSnippetPlayer(draggedObjects[i]) != null)
                return true;
        }

        return false;
    }

    public static SnippetPlayer ExtractSnippetPlayer(UnityEngine.Object obj)
    {
        if (obj == null)
            return null;

        if (obj is SnippetPlayer snippetPlayer)
            return EditorUtility.IsPersistent(snippetPlayer) ? null : snippetPlayer;

        if (obj is GameObject go)
        {
            if (EditorUtility.IsPersistent(go))
                return null;

            return go.GetComponent<SnippetPlayer>() ?? go.GetComponentInChildren<SnippetPlayer>(true);
        }

        if (obj is Component component)
        {
            if (EditorUtility.IsPersistent(component))
                return null;

            return component.GetComponent<SnippetPlayer>() ?? component.GetComponentInChildren<SnippetPlayer>(true);
        }

        return null;
    }

    public static bool AddActorFromPlayer(SnippetsActorRegistry registry, SnippetPlayer player)
    {
        if (registry == null || player == null)
            return false;

        registry.actors ??= new List<SnippetsActorRegistry.Actor>();
        if (registry.actors.Any(existing => existing != null && existing.player == player))
            return false;

        registry.actors.Add(new SnippetsActorRegistry.Actor
        {
            name = player.name,
            player = player,
            defaultLoopAnimations = SnippetsActorRegistry.DefaultLoopAnimationMode.Auto
        });

        RefreshActor(registry.actors[registry.actors.Count - 1]);
        return true;
    }

    public static SnippetsActorRegistry.Actor GetOrCreateActor(SnippetsActorRegistry registry, SnippetPlayer player)
    {
        if (registry == null || player == null)
            return null;

        registry.actors ??= new List<SnippetsActorRegistry.Actor>();

        var actor = registry.actors.FirstOrDefault(existing => existing != null && existing.player == player);
        if (actor != null)
            return actor;

        actor = new SnippetsActorRegistry.Actor
        {
            name = player.name,
            player = player,
            defaultLoopAnimations = SnippetsActorRegistry.DefaultLoopAnimationMode.Auto
        };

        registry.actors.Add(actor);
        return actor;
    }

    public static void AutoAssignActorComponents(SnippetsActorRegistry.Actor actor)
    {
        if (actor == null || actor.player == null)
            return;

        if (string.IsNullOrWhiteSpace(actor.name))
            actor.name = actor.player.name;

        actor.walker = actor.player.GetComponentInParent<SnippetsWalker>(true) ??
                       actor.player.GetComponentInChildren<SnippetsWalker>(true);

        actor.gazeDriver = actor.player.GetComponentInChildren<SnippetsGazeDriver>(true) ??
                           actor.player.GetComponentInParent<SnippetsGazeDriver>(true);

        actor.legacyAnimation = actor.player.GetComponentInChildren<Animation>(true) ??
                                actor.player.GetComponentInParent<Animation>(true);
    }

    public static void SetupActor(SnippetsActorRegistry.Actor actor)
    {
        AutoAssignActorComponents(actor);
        ApplyDefaultLoopClips(actor);
    }

    public static void RefreshActor(SnippetsActorRegistry.Actor actor)
    {
        SetupActor(actor);
        DiscoverSnippetsFromActorFolder(actor);
    }

    public static void ApplyDefaultLoopClips(SnippetsActorRegistry.Actor actor)
    {
        if (actor == null)
            return;

        if (actor.defaultLoopAnimations == SnippetsActorRegistry.DefaultLoopAnimationMode.None)
        {
            ClearBuiltInDefaultClips(actor);
            return;
        }

        if (!TryResolveLoopClipSet(actor, out var profile, out string reason))
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.LogWarning($"[Snippets SDK] {reason}");
            return;
        }

        actor.idleClip = profile == SnippetsGazeDriver.RpmCrossEyePreset.Female
            ? SnippetsSdkEditorAssets.LoadFemaleIdleClip()
            : SnippetsSdkEditorAssets.LoadMaleIdleClip();

        actor.walkClip = profile == SnippetsGazeDriver.RpmCrossEyePreset.Female
            ? SnippetsSdkEditorAssets.LoadFemaleWalkClip()
            : SnippetsSdkEditorAssets.LoadMaleWalkClip();
    }

    public static void DiscoverSnippetsFromActorFolder(SnippetsActorRegistry.Actor actor)
    {
        if (actor == null || actor.player == null)
        {
            Debug.LogWarning("[Snippets SDK] Auto Discover Snippets requires an assigned actor player.");
            return;
        }

        string folder = GetSnippetSetFolder(actor);
        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogWarning($"[Snippets SDK] Could not resolve a snippet set folder for actor '{actor.name}'. Make sure the actor player comes from a prefab asset in an imported snippet set folder.");
            return;
        }

        var snippetPlayers = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => string.Equals(NormalizeFolder(Path.GetDirectoryName(path)), folder, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .Where(go => go != null)
            .Select(go => go.GetComponentInChildren<SnippetPlayer>(true))
            .Where(player => player != null)
            .Distinct()
            .ToList();

        actor.snippets = snippetPlayers;
    }

    public static string GetSnippetSetFolder(SnippetsActorRegistry.Actor actor)
    {
        if (actor == null || actor.player == null)
            return null;

        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.player.gameObject);
        if (string.IsNullOrEmpty(assetPath))
            assetPath = AssetDatabase.GetAssetPath(actor.player.gameObject);

        if (string.IsNullOrEmpty(assetPath))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(actor.player.gameObject);
            if (source != null)
                assetPath = AssetDatabase.GetAssetPath(source);
        }

        if (string.IsNullOrEmpty(assetPath))
            return null;

        return NormalizeFolder(Path.GetDirectoryName(assetPath));
    }

    public static void MarkRegistryDirty(SnippetsActorRegistry registry)
    {
        if (registry == null)
            return;

        EditorUtility.SetDirty(registry);
        PrefabUtility.RecordPrefabInstancePropertyModifications(registry);
    }

    static void ClearBuiltInDefaultClips(SnippetsActorRegistry.Actor actor)
    {
        if (actor == null)
            return;

        if (IsBuiltInDefaultIdleClip(actor.idleClip))
            actor.idleClip = null;

        if (IsBuiltInDefaultWalkClip(actor.walkClip))
            actor.walkClip = null;
    }

    static bool IsBuiltInDefaultIdleClip(AnimationClip clip)
    {
        if (clip == null)
            return false;

        return ReferenceEquals(clip, SnippetsSdkEditorAssets.LoadMaleIdleClip()) ||
               ReferenceEquals(clip, SnippetsSdkEditorAssets.LoadFemaleIdleClip());
    }

    static bool IsBuiltInDefaultWalkClip(AnimationClip clip)
    {
        if (clip == null)
            return false;

        return ReferenceEquals(clip, SnippetsSdkEditorAssets.LoadMaleWalkClip()) ||
               ReferenceEquals(clip, SnippetsSdkEditorAssets.LoadFemaleWalkClip());
    }

    static bool TryResolveLoopClipSet(
        SnippetsActorRegistry.Actor actor,
        out SnippetsGazeDriver.RpmCrossEyePreset profile,
        out string reason)
    {
        profile = SnippetsGazeDriver.RpmCrossEyePreset.Male;
        reason = string.Empty;

        switch (actor.defaultLoopAnimations)
        {
            case SnippetsActorRegistry.DefaultLoopAnimationMode.RpmMale:
                profile = SnippetsGazeDriver.RpmCrossEyePreset.Male;
                return true;

            case SnippetsActorRegistry.DefaultLoopAnimationMode.RpmFemale:
                profile = SnippetsGazeDriver.RpmCrossEyePreset.Female;
                return true;

            case SnippetsActorRegistry.DefaultLoopAnimationMode.None:
                reason = $"Skipped default loop clip assignment for '{actor.name}' because Default Loop Animations is set to Custom.";
                return false;

            case SnippetsActorRegistry.DefaultLoopAnimationMode.Auto:
            default:
                if (actor.gazeDriver != null)
                {
                    profile = actor.gazeDriver.rpmCrossEyePreset;
                    return true;
                }

                reason = $"Could not auto-resolve default loop clips for '{actor.name}'. Assign a Gaze Driver with the correct preset or change Default Loop Animations to Male Default, Female Default, or Custom.";
                return false;
        }
    }

    static string NormalizeFolder(string folder)
    {
        return string.IsNullOrWhiteSpace(folder) ? null : folder.Replace('\\', '/');
    }
}
#endif
