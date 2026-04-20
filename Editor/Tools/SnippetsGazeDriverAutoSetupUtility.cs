#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class SnippetsGazeDriverAutoSetupUtility
{
    public static void AutoSetupDriver(SnippetsGazeDriver driver)
    {
        if (driver == null)
            return;

        Transform searchRoot = GetAutoSetupRoot(driver);
        if (searchRoot == null)
            return;

        Animator animator = searchRoot.GetComponentInChildren<Animator>(true);

        Undo.RecordObject(driver, "Auto Setup Gaze Driver");

        Transform foundHead = ResolveHeadBone(searchRoot, animator);
        if (foundHead != null)
            driver.headBone = foundHead;

        Transform foundWaist = ResolveWaistBone(searchRoot, animator);
        if (foundWaist != null)
            driver.waistBone = foundWaist;

        Transform foundLeftEye = ResolveEyeBone(searchRoot, animator, true);
        Transform foundRightEye = ResolveEyeBone(searchRoot, animator, false);
        if (foundLeftEye != null)
            driver.leftEyeBone = foundLeftEye;
        if (foundRightEye != null)
            driver.rightEyeBone = foundRightEye;
        if (foundLeftEye != null || foundRightEye != null)
            driver.enableDynamicEyeFollow = true;

        SkinnedMeshRenderer foundFaceMesh = ResolveFaceMesh(searchRoot);
        if (foundFaceMesh != null)
        {
            driver.faceMesh = foundFaceMesh;

            int leftBlink = FindEyeBlinkBlendshape(foundFaceMesh, true);
            int rightBlink = FindEyeBlinkBlendshape(foundFaceMesh, false);

            if (leftBlink >= 0 || rightBlink >= 0)
            {
                driver.eyelidBlendshape = -1;
                driver.useMultipleSharedEyelidBlendshapes = true;
                if (driver.sharedEyelidBlendshapes == null)
                    driver.sharedEyelidBlendshapes = new System.Collections.Generic.List<int>();
                else
                    driver.sharedEyelidBlendshapes.Clear();

                if (leftBlink >= 0)
                    driver.sharedEyelidBlendshapes.Add(leftBlink);
                if (rightBlink >= 0 && rightBlink != leftBlink)
                    driver.sharedEyelidBlendshapes.Add(rightBlink);

                driver.leftEyelidBlendshape = -1;
                driver.rightEyelidBlendshape = -1;
                driver.enableLookDownEyelidFollow = true;
            }
            else
            {
                int sharedBlink = FindSharedBlinkBlendshape(foundFaceMesh);
                if (sharedBlink >= 0)
                {
                    driver.eyelidBlendshape = sharedBlink;
                    driver.useMultipleSharedEyelidBlendshapes = false;
                    driver.sharedEyelidBlendshapes?.Clear();
                    driver.leftEyelidBlendshape = -1;
                    driver.rightEyelidBlendshape = -1;
                    driver.enableLookDownEyelidFollow = true;
                }
            }
        }

        EditorUtility.SetDirty(driver);
    }

    public static bool NeedsAutoSetup(SnippetsGazeDriver driver)
    {
        if (driver == null)
            return false;

        return driver.headBone == null
            || driver.waistBone == null
            || (driver.leftEyeBone == null && driver.rightEyeBone == null)
            || driver.faceMesh == null;
    }

    static Transform GetAutoSetupRoot(SnippetsGazeDriver driver)
    {
        var avatarPlayer = driver.GetComponentInParent<Snippets.Sdk.SnippetAvatarPlayer>(true);
        if (avatarPlayer != null)
            return avatarPlayer.transform;

        return driver.transform.root;
    }

    static Transform ResolveHeadBone(Transform root, Animator animator)
    {
        Transform humanoidHead = GetAnimatorBone(animator, HumanBodyBones.Head);
        if (humanoidHead != null)
            return humanoidHead;

        return FindBestTransform(root, GetHeadBoneScore);
    }

    static Transform ResolveWaistBone(Transform root, Animator animator)
    {
        Transform spine = GetAnimatorBone(animator, HumanBodyBones.Spine);
        if (spine != null)
            return spine;

        Transform chest = GetAnimatorBone(animator, HumanBodyBones.Chest);
        if (chest != null)
            return chest;

        Transform upperChest = GetAnimatorBone(animator, HumanBodyBones.UpperChest);
        if (upperChest != null)
            return upperChest;

        Transform hips = GetAnimatorBone(animator, HumanBodyBones.Hips);
        if (hips != null)
            return hips;

        return FindBestTransform(root, GetWaistBoneScore);
    }

    static Transform ResolveEyeBone(Transform root, Animator animator, bool useLeftEye)
    {
        Transform humanoidEye = GetAnimatorBone(animator, useLeftEye ? HumanBodyBones.LeftEye : HumanBodyBones.RightEye);
        if (humanoidEye != null)
            return humanoidEye;

        return FindBestTransform(root, t => GetEyeBoneScore(t, useLeftEye));
    }

    static Transform GetAnimatorBone(Animator animator, HumanBodyBones bone)
    {
        if (animator == null || !animator.isHuman)
            return null;

        return animator.GetBoneTransform(bone);
    }

    static Transform FindBestTransform(Transform root, Func<Transform, int> scorer)
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

    static int GetHeadBoneScore(Transform candidate)
    {
        string name = NormalizeName(candidate.name);
        if (string.IsNullOrEmpty(name))
            return 0;

        if (name.Contains("target") || name.Contains("proxy") || name.Contains("lookat") || name.Contains("camera"))
            return 0;

        if (name == "head" || name.EndsWith("head"))
            return 120;

        if (name.Contains("head"))
            return 100;

        return 0;
    }

    static int GetWaistBoneScore(Transform candidate)
    {
        string name = NormalizeName(candidate.name);
        if (string.IsNullOrEmpty(name))
            return 0;

        if (name == "spine" || name.EndsWith("spine"))
            return 130;
        if (name.Contains("spine1"))
            return 120;
        if (name.Contains("spine"))
            return 110;
        if (name == "chest" || name.EndsWith("chest"))
            return 100;
        if (name.Contains("upperchest"))
            return 95;
        if (name.Contains("chest"))
            return 90;
        if (name.Contains("hips"))
            return 70;

        return 0;
    }

    static int GetEyeBoneScore(Transform candidate, bool useLeftEye)
    {
        string name = NormalizeName(candidate.name);
        if (string.IsNullOrEmpty(name) || !name.Contains("eye"))
            return 0;

        if (name.Contains("lid") || name.Contains("lash") || name.Contains("brow") || name.Contains("target"))
            return 0;

        int score = 40;
        bool matchesSide = useLeftEye ? IsLeftName(name) : IsRightName(name);
        bool wrongSide = useLeftEye ? IsRightName(name) : IsLeftName(name);

        if (wrongSide)
            return 0;

        if (matchesSide)
            score += 80;

        if (name == "lefteye" || name == "righteye")
            score += 40;

        return score;
    }

    static SkinnedMeshRenderer ResolveFaceMesh(Transform root)
    {
        SkinnedMeshRenderer best = null;
        int bestScore = 0;

        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0)
                continue;

            string name = NormalizeName(smr.name);
            int score = mesh.blendShapeCount;
            if (name.Contains("face"))
                score += 100;
            if (name.Contains("head"))
                score += 60;
            if (FindSharedBlinkBlendshape(smr) >= 0)
                score += 90;
            if (FindEyeBlinkBlendshape(smr, true) >= 0)
                score += 90;
            if (FindEyeBlinkBlendshape(smr, false) >= 0)
                score += 90;

            if (score > bestScore)
            {
                best = smr;
                bestScore = score;
            }
        }

        return best;
    }

    static int FindSharedBlinkBlendshape(SkinnedMeshRenderer smr)
    {
        if (smr == null || smr.sharedMesh == null)
            return -1;

        int bestIndex = -1;
        int bestScore = 0;
        Mesh mesh = smr.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = NormalizeName(mesh.GetBlendShapeName(i));
            int score = GetSharedBlinkBlendshapeScore(name);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    static int FindEyeBlinkBlendshape(SkinnedMeshRenderer smr, bool useLeftEye)
    {
        if (smr == null || smr.sharedMesh == null)
            return -1;

        int bestIndex = -1;
        int bestScore = 0;
        Mesh mesh = smr.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = NormalizeName(mesh.GetBlendShapeName(i));
            int score = GetEyeBlinkBlendshapeScore(name, useLeftEye);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    static int GetSharedBlinkBlendshapeScore(string normalizedName)
    {
        if (!LooksLikeBlinkName(normalizedName))
            return 0;

        if (IsLeftName(normalizedName) || IsRightName(normalizedName))
            return 0;

        if (normalizedName.Contains("both") || normalizedName.Contains("eyes"))
            return 120;

        return 100;
    }

    static int GetEyeBlinkBlendshapeScore(string normalizedName, bool useLeftEye)
    {
        if (!LooksLikeBlinkName(normalizedName))
            return 0;

        bool matchesSide = useLeftEye ? IsLeftName(normalizedName) : IsRightName(normalizedName);
        bool wrongSide = useLeftEye ? IsRightName(normalizedName) : IsLeftName(normalizedName);

        if (wrongSide)
            return 0;

        return matchesSide ? 120 : 0;
    }

    static bool LooksLikeBlinkName(string normalizedName)
    {
        return normalizedName.Contains("blink")
            || normalizedName.Contains("eyeclose")
            || normalizedName.Contains("eyesclosed")
            || normalizedName.Contains("eyelidclose")
            || normalizedName.Contains("upperlidclose");
    }

    static bool IsLeftName(string normalizedName)
    {
        return normalizedName.Contains("left")
            || normalizedName.Contains("eyeblinkleft")
            || normalizedName.Contains("lefteye")
            || normalizedName.Contains("eyeleft")
            || normalizedName.StartsWith("leye")
            || normalizedName.EndsWith("leye")
            || normalizedName == "eyel";
    }

    static bool IsRightName(string normalizedName)
    {
        return normalizedName.Contains("right")
            || normalizedName.Contains("eyeblinkright")
            || normalizedName.Contains("righteye")
            || normalizedName.Contains("eyeright")
            || normalizedName.StartsWith("reye")
            || normalizedName.EndsWith("reye")
            || normalizedName == "eyer";
    }

    static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(c))
                buffer[count++] = c;
        }

        return new string(buffer, 0, count);
    }
}
#endif
