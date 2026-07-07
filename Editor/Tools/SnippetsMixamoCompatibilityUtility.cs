#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SnippetsMixamoCompatibilityUtility
{
    const string MixamoPrefix = "mixamorig:";
    const string ArmatureName = "Armature";
    const string ClipSuffix = "_SnippetsCompatible";

    public static bool IsLegacyClip(AnimationClip clip)
    {
        return clip != null && clip.legacy;
    }

    public static int NormalizeClips(IEnumerable<AnimationClip> clips, List<AnimationClip> createdClips = null)
    {
        if (clips == null)
            return 0;

        var clipList = new List<AnimationClip>();
        foreach (var clip in clips)
        {
            if (clip != null && !clipList.Contains(clip))
                clipList.Add(clip);
        }

        if (clipList.Count == 0)
            return 0;

        int createdCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < clipList.Count; i++)
            {
                if (TryCreateNormalizedClip(clipList[i], out var createdClip))
                {
                    createdCount++;
                    createdClips?.Add(createdClip);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return createdCount;
    }

    [MenuItem("GameObject/Snippets/Normalize Mixamo Hierarchy For Snippets", true, 49)]
    static bool ValidateNormalizeSelectedHierarchy()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("GameObject/Snippets/Normalize Mixamo Hierarchy For Snippets", false, 49)]
    static void NormalizeSelectedHierarchy()
    {
        var roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            Debug.LogWarning("[Snippets SDK] Select one or more hierarchy roots to normalize.");
            return;
        }

        int normalizedCount = 0;

        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null)
                continue;

            if (NormalizeHierarchy(root))
            {
                normalizedCount++;
                Debug.Log($"[Snippets SDK] Normalized hierarchy for '{root.name}'.", root);
            }
        }

        if (normalizedCount == 0)
            Debug.LogWarning("[Snippets SDK] No hierarchy changes were needed.");
    }

    public static bool TryCreateNormalizedClip(AnimationClip sourceClip, out AnimationClip createdClip)
    {
        createdClip = null;

        if (sourceClip == null)
            return false;

        string sourceAssetPath = AssetDatabase.GetAssetPath(sourceClip);
        if (string.IsNullOrEmpty(sourceAssetPath))
            return false;

        string targetPath = BuildUniqueClipPath(sourceAssetPath, sourceClip.name);
        if (string.IsNullOrEmpty(targetPath))
            return false;

        var targetClip = new AnimationClip
        {
            name = Path.GetFileNameWithoutExtension(targetPath),
            frameRate = sourceClip.frameRate,
            wrapMode = sourceClip.wrapMode,
            // Snippets playback currently relies on Unity's legacy Animation path,
            // so converted clips should always be saved as legacy-compatible assets.
            legacy = true
        };

        CopyClipCurves(sourceClip, targetClip);
        AnimationUtility.SetAnimationEvents(targetClip, AnimationUtility.GetAnimationEvents(sourceClip));

        AssetDatabase.CreateAsset(targetClip, targetPath);
        createdClip = targetClip;
        return true;
    }

    static void CopyClipCurves(AnimationClip sourceClip, AnimationClip targetClip)
    {
        var curveBindings = AnimationUtility.GetCurveBindings(sourceClip);
        for (int i = 0; i < curveBindings.Length; i++)
        {
            var sourceBinding = curveBindings[i];
            var curve = AnimationUtility.GetEditorCurve(sourceClip, sourceBinding);
            if (curve == null)
                continue;

            var targetBinding = sourceBinding;
            targetBinding.path = NormalizeBindingPath(sourceBinding.path);
            AnimationUtility.SetEditorCurve(targetClip, targetBinding, new AnimationCurve(curve.keys));
        }

        var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(sourceClip);
        for (int i = 0; i < objectBindings.Length; i++)
        {
            var sourceBinding = objectBindings[i];
            var keyframes = AnimationUtility.GetObjectReferenceCurve(sourceClip, sourceBinding);

            var targetBinding = sourceBinding;
            targetBinding.path = NormalizeBindingPath(sourceBinding.path);
            AnimationUtility.SetObjectReferenceCurve(targetClip, targetBinding, keyframes);
        }
    }

    public static bool NormalizeHierarchy(GameObject root)
    {
        if (root == null)
            return false;

        bool changed = false;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Normalize Mixamo Hierarchy For Snippets");

        try
        {
            Transform rootTransform = root.transform;
            Transform armature = rootTransform.Find(ArmatureName);

            if (armature == null)
            {
                var armatureObject = new GameObject(ArmatureName);
                Undo.RegisterCreatedObjectUndo(armatureObject, "Create Armature");

                armature = armatureObject.transform;
                Undo.SetTransformParent(armature, rootTransform, "Parent Armature");
                armature.localPosition = Vector3.zero;
                armature.localRotation = Quaternion.identity;
                armature.localScale = Vector3.one;

                var childrenToMove = new List<Transform>();
                for (int i = 0; i < rootTransform.childCount; i++)
                {
                    var child = rootTransform.GetChild(i);
                    if (child != null && child != armature)
                        childrenToMove.Add(child);
                }

                for (int i = 0; i < childrenToMove.Count; i++)
                    Undo.SetTransformParent(childrenToMove[i], armature, "Move Child Under Armature");

                changed = true;
            }

            if (RenameTransformTree(armature))
                changed = true;

            if (changed)
            {
                EditorUtility.SetDirty(root);
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            }
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        return changed;
    }

    static bool RenameTransformTree(Transform root)
    {
        if (root == null)
            return false;

        bool changed = false;
        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == null)
                continue;

            string normalizedName = StripMixamoPrefix(current.name);
            if (!string.Equals(current.name, normalizedName, StringComparison.Ordinal))
            {
                Undo.RecordObject(current.gameObject, "Rename Mixamo Transform");
                current.name = normalizedName;
                changed = true;
            }

            for (int i = current.childCount - 1; i >= 0; i--)
                stack.Push(current.GetChild(i));
        }

        return changed;
    }

    static string NormalizeBindingPath(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return sourcePath;

        string[] segments = sourcePath.Split('/');
        for (int i = 0; i < segments.Length; i++)
            segments[i] = StripMixamoPrefix(segments[i]);

        string normalizedPath = string.Join("/", segments);

        if (string.IsNullOrEmpty(normalizedPath))
            return normalizedPath;

        if (string.Equals(segments[0], ArmatureName, StringComparison.Ordinal))
            return normalizedPath;

        return $"{ArmatureName}/{normalizedPath}";
    }

    static string StripMixamoPrefix(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.StartsWith(MixamoPrefix, StringComparison.Ordinal)
            ? value.Substring(MixamoPrefix.Length)
            : value;
    }

    static string BuildUniqueClipPath(string sourceAssetPath, string clipName)
    {
        string directory = Path.GetDirectoryName(sourceAssetPath);
        if (string.IsNullOrEmpty(directory))
            return null;

        directory = directory.Replace('\\', '/');
        string fileName = $"{clipName}{ClipSuffix}.anim";
        string combined = $"{directory}/{fileName}";
        return AssetDatabase.GenerateUniqueAssetPath(combined);
    }
}
#endif
