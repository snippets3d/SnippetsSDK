#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Snippets.Sdk;

public struct SnippetsSceneAutoSetupOptions
{
    public bool createActorRegistry;
    public bool createFlowController;
    public bool createGazeFlowController;
    public bool createSimpleController;
    public bool addGazeDriversToActors;
    public bool addWalkersToActors;
}

public sealed class SnippetsSceneAutoSetupResult
{
    public GameObject toolsRoot;
    public SnippetsActorRegistry actorRegistry;
    public SnippetsFlowController flowController;
    public SnippetsGazeFlowController gazeFlowController;
    public SnippetsSimpleController simpleController;
    public int actorsAdded;
    public int actorsUpdated;
    public int gazeDriversAdded;
    public int walkersAdded;
}

public static class SnippetsSceneAutoSetupUtility
{
    public const string ToolsRootName = "Snippet Tools";
    public const string ActorRegistryName = "Actor Registry";
    public const string FlowControllerName = "Flow Controller";
    public const string GazeFlowControllerName = "Gaze Flow Controller";
    public const string SimpleControllerName = "Simple Controller";

    public static SnippetsSceneAutoSetupResult Apply(IEnumerable<SnippetPlayer> actorPlayers, SnippetsSceneAutoSetupOptions options)
    {
        var result = new SnippetsSceneAutoSetupResult();
        var players = (actorPlayers ?? Enumerable.Empty<SnippetPlayer>())
            .Where(player => player != null)
            .Distinct()
            .ToList();

        bool needsRegistry = options.createActorRegistry || options.createFlowController || options.createGazeFlowController || options.createSimpleController;
        bool needsFlow = options.createFlowController || options.createGazeFlowController;
        bool needsToolsRoot = needsRegistry || options.createGazeFlowController || options.createSimpleController;
        bool sceneChanged = false;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Snippets Scene Auto Setup");

        try
        {
            if (needsToolsRoot)
                result.toolsRoot = FindOrCreateRoot(ref sceneChanged);

            foreach (var player in players)
            {
                if (options.addGazeDriversToActors)
                {
                    bool created = false;
                    var driver = EnsureActorComponent<SnippetsGazeDriver>(player, ref created);
                    if (driver != null)
                    {
                        if (created)
                        {
                            result.gazeDriversAdded++;
                            sceneChanged = true;
                        }

                        if (created || SnippetsGazeDriverAutoSetupUtility.NeedsAutoSetup(driver))
                        {
                            SnippetsGazeDriverAutoSetupUtility.AutoSetupDriver(driver);
                            sceneChanged = true;
                        }
                    }
                }

                if (options.addWalkersToActors)
                {
                    bool created = false;
                    var walker = EnsureActorComponent<SnippetsWalker>(player, ref created);
                    if (walker != null)
                    {
                        if (created)
                        {
                            result.walkersAdded++;
                            sceneChanged = true;
                        }

                        if (created || SnippetsWalkerAutoSetupUtility.NeedsPinnedRootMotionBone(walker))
                        {
                            if (SnippetsWalkerAutoSetupUtility.AutoAssignPinnedRootMotionBone(walker))
                                sceneChanged = true;
                        }
                    }
                }
            }

            if (needsRegistry)
            {
                var registryObject = FindOrCreateChild(result.toolsRoot, ActorRegistryName, ref sceneChanged);
                result.actorRegistry = GetOrAddComponent<SnippetsActorRegistry>(registryObject, ref sceneChanged);

                if (result.actorRegistry != null)
                {
                    Undo.RecordObject(result.actorRegistry, "Configure Snippets Actor Registry");

                    foreach (var player in players)
                    {
                        bool existed = result.actorRegistry.actors != null && result.actorRegistry.actors.Any(actor => actor != null && actor.player == player);
                        var actor = SnippetsActorSetupUtility.GetOrCreateActor(result.actorRegistry, player);
                        if (actor == null)
                            continue;

                        actor.player = player;
                        if (string.IsNullOrWhiteSpace(actor.name))
                            actor.name = player.name;

                        SnippetsActorSetupUtility.RefreshActor(actor);

                        if (existed)
                            result.actorsUpdated++;
                        else
                            result.actorsAdded++;
                    }

                    SnippetsActorSetupUtility.MarkRegistryDirty(result.actorRegistry);
                    sceneChanged = true;
                }
            }

            if (needsFlow)
            {
                var flowObject = FindOrCreateChild(result.toolsRoot, FlowControllerName, ref sceneChanged);
                result.flowController = GetOrAddComponent<SnippetsFlowController>(flowObject, ref sceneChanged);
                if (result.flowController != null && result.actorRegistry != null && result.flowController.registry != result.actorRegistry)
                {
                    Undo.RecordObject(result.flowController, "Configure Snippets Flow Controller");
                    result.flowController.registry = result.actorRegistry;
                    EditorUtility.SetDirty(result.flowController);
                    sceneChanged = true;
                }
            }

            if (options.createGazeFlowController)
            {
                var gazeFlowObject = FindOrCreateChild(result.toolsRoot, GazeFlowControllerName, ref sceneChanged);
                result.gazeFlowController = GetOrAddComponent<SnippetsGazeFlowController>(gazeFlowObject, ref sceneChanged);
                if (result.gazeFlowController != null)
                {
                    Undo.RecordObject(result.gazeFlowController, "Configure Snippets Gaze Flow Controller");
                    if (result.gazeFlowController.flow != result.flowController)
                        result.gazeFlowController.flow = result.flowController;
                    if (result.gazeFlowController.registry != result.actorRegistry)
                        result.gazeFlowController.registry = result.actorRegistry;
                    result.gazeFlowController.SyncNow();
                    EditorUtility.SetDirty(result.gazeFlowController);
                    sceneChanged = true;
                }
            }

            if (options.createSimpleController)
            {
                var simpleObject = FindOrCreateChild(result.toolsRoot, SimpleControllerName, ref sceneChanged);
                result.simpleController = GetOrAddComponent<SnippetsSimpleController>(simpleObject, ref sceneChanged);
                if (result.simpleController != null && result.actorRegistry != null && result.simpleController.registry != result.actorRegistry)
                {
                    Undo.RecordObject(result.simpleController, "Configure Snippets Simple Controller");
                    result.simpleController.registry = result.actorRegistry;
                    EditorUtility.SetDirty(result.simpleController);
                    sceneChanged = true;
                }
            }

            if (sceneChanged)
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid())
                    EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        return result;
    }

    static T EnsureActorComponent<T>(SnippetPlayer player, ref bool created) where T : Component
    {
        if (player == null)
            return null;

        var existing = player.GetComponent<T>()
            ?? player.GetComponentInChildren<T>(true)
            ?? player.GetComponentInParent<T>(true);

        if (existing != null)
            return existing;

        created = true;
        return Undo.AddComponent<T>(player.gameObject);
    }

    static GameObject FindOrCreateRoot(ref bool sceneChanged)
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root != null && root.name == ToolsRootName)
                    return root;
            }
        }

        var go = new GameObject(ToolsRootName);
        Undo.RegisterCreatedObjectUndo(go, "Create Snippet Tools");
        sceneChanged = true;
        return go;
    }

    static GameObject FindOrCreateChild(GameObject parent, string childName, ref bool sceneChanged)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.transform.childCount; i++)
        {
            var child = parent.transform.GetChild(i);
            if (child != null && child.name == childName)
                return child.gameObject;
        }

        var go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
        Undo.SetTransformParent(go.transform, parent.transform, $"Parent {childName}");
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        sceneChanged = true;
        return go;
    }

    static T GetOrAddComponent<T>(GameObject go, ref bool sceneChanged) where T : Component
    {
        if (go == null)
            return null;

        var existing = go.GetComponent<T>();
        if (existing != null)
            return existing;

        sceneChanged = true;
        return Undo.AddComponent<T>(go);
    }
}
#endif
