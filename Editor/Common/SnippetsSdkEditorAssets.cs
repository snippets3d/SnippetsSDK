using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Snippets.Sdk
{
    internal static class SnippetsSdkEditorAssets
    {
        private const string SnippetTemplatePrefabGuid = "fbcc40e91526859428bab09db558e208";
        private const string MaleIdleAssetGuid = "c2f10bbac20435748b606ea5453b976f";
        private const string MaleWalkAssetGuid = "d89923ffddc395e458d8ad7c16f263f7";
        private const string FemaleIdleAssetGuid = "5fe7f3777026f19499ad29067434f612";
        private const string FemaleWalkAssetGuid = "5560c69507cb0f44f8d1e1379ec28305";

        internal static GameObject LoadSnippetTemplatePrefab()
        {
            return LoadAssetByGuid<GameObject>(SnippetTemplatePrefabGuid);
        }

        internal static AnimationClip LoadMaleIdleClip()
        {
            return LoadAnimationClip(MaleIdleAssetGuid, "M_Idle");
        }

        internal static AnimationClip LoadMaleWalkClip()
        {
            return LoadAnimationClip(MaleWalkAssetGuid, "M_Walk");
        }

        internal static AnimationClip LoadFemaleIdleClip()
        {
            return LoadAnimationClip(FemaleIdleAssetGuid, "F_Idle");
        }

        internal static AnimationClip LoadFemaleWalkClip()
        {
            return LoadAnimationClip(FemaleWalkAssetGuid, "F_Walk");
        }

        private static T LoadAssetByGuid<T>(string assetGuid) where T : UnityEngine.Object
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        private static AnimationClip LoadAnimationClip(string assetGuid, string clipName)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => string.Equals(clip.name, clipName, StringComparison.Ordinal));
        }
    }
}
