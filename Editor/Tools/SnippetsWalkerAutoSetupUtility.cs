#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SnippetsWalkerAutoSetupUtility
{
    public static bool NeedsPinnedRootMotionBone(SnippetsWalker walker)
    {
        return walker != null && walker.rootMotionBoneToPin == null;
    }

    public static bool AutoAssignPinnedRootMotionBone(SnippetsWalker walker)
    {
        if (walker == null || walker.rootMotionBoneToPin != null)
            return false;

        Transform searchRoot = GetAutoSetupRoot(walker);
        if (searchRoot == null)
            return false;

        Animator animator = searchRoot.GetComponentInChildren<Animator>(true);
        Transform hips = ResolveHipsBone(searchRoot, animator);
        if (hips == null)
            return false;

        Undo.RecordObject(walker, "Auto Assign Walker Pinned Root-Motion Bone");
        walker.rootMotionBoneToPin = hips;
        EditorUtility.SetDirty(walker);
        return true;
    }

    static Transform GetAutoSetupRoot(SnippetsWalker walker)
    {
        if (walker == null)
            return null;

        var avatarPlayer = walker.GetComponentInParent<Snippets.Sdk.SnippetAvatarPlayer>(true);
        if (avatarPlayer != null)
            return avatarPlayer.transform;

        Animator ownAnimator = walker.GetComponent<Animator>();
        if (ownAnimator != null)
            return ownAnimator.transform;

        Animator childAnimator = walker.GetComponentInChildren<Animator>(true);
        if (childAnimator != null)
            return childAnimator.transform;

        Animator parentAnimator = walker.GetComponentInParent<Animator>(true);
        if (parentAnimator != null)
            return parentAnimator.transform;

        return walker.transform.root;
    }

    static Transform ResolveHipsBone(Transform root, Animator animator)
    {
        Transform humanoidHips = GetAnimatorBone(animator, HumanBodyBones.Hips);
        if (humanoidHips != null)
            return humanoidHips;

        return FindBestTransform(root, GetHipsBoneScore);
    }

    static Transform GetAnimatorBone(Animator animator, HumanBodyBones bone)
    {
        if (animator == null || !animator.isHuman)
            return null;

        return animator.GetBoneTransform(bone);
    }

    static Transform FindBestTransform(Transform root, System.Func<Transform, int> scorer)
    {
        Transform best = null;
        int bestScore = 0;

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            int score = scorer(candidate);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    static int GetHipsBoneScore(Transform candidate)
    {
        string name = NormalizeName(candidate != null ? candidate.name : string.Empty);
        if (string.IsNullOrEmpty(name))
            return 0;

        if (name.Contains("target") || name.Contains("proxy") || name.Contains("effector"))
            return 0;

        if (name == "hips" || name.EndsWith("hips"))
            return 120;
        if (name.Contains("hips"))
            return 100;
        if (name == "pelvis" || name.EndsWith("pelvis"))
            return 95;
        if (name.Contains("pelvis"))
            return 85;
        if (name == "root" || name.EndsWith("root"))
            return 40;

        return 0;
    }

    static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }
}
#endif
