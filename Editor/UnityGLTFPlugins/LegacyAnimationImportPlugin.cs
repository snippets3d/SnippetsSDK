using Snippets.Sdk;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Plugins;

namespace Snippets.Plugins
{
    /// <summary>
    /// Import plugin that sets the animation import method to Legacy for the GLTFImporter.
    /// This is necessary because Snippets currently use the legacy animation system.
    /// </summary>
    //[CreateAssetMenu(fileName = "LegacyAnimationImportPlugin", menuName = "UnityGLTF/ImportPlugin/LegacyAnimationImportPlugin", order = 1)]
    public class LegacyAnimationImportPlugin : GLTFImportPlugin
    {
        private const string UserDataInitializedToken = "SnippetsGLTF:Initialized";
        private const string UserDataUserOverrideToken = "SnippetsGLTF:UserOverride";

        /// <inheritdoc />
        public override string DisplayName { get => "Legacy Animation Import Plugin"; }

        /// <inheritdoc />
        public override string Description { get => "Imports by default all animations as Legacy and without loop"; }

        /// <inheritdoc />
        public override bool EnabledByDefault => true;

        /// <inheritdoc />
        public override bool AlwaysEnabled => false;

        /// <inheritdoc />
        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
#if UNITY_EDITOR
            var gltfImporter = context.SourceImporter as GLTFImporter;
            var assetImporter = context.SourceImporter as UnityEditor.AssetImporter;

            // if the asset is imported via GLTF Importer and it is a Snippet asset,
            // set animation import method to Legacy and loop to false by default
            if (gltfImporter != null && assetImporter != null && IsSnippetAnimationAsset(context.AssetContext.assetPath))
            {
                bool initialized = HasUserDataToken(assetImporter, UserDataInitializedToken);

                if (!initialized)
                {
                    // First import only: force defaults to Legacy and no loop.
                    SetImportAnimationParameters(gltfImporter, AnimationMethod.Legacy, false);
                    AddUserDataToken(assetImporter, UserDataInitializedToken);
                }
                else if (!HasUserOverride(assetImporter))
                {
                    AnimationMethod? currentMethod = GetImportAnimationMethod(gltfImporter);
                    bool? currentLoop = GetImportAnimationLoop(gltfImporter);

                    // If the user changed any relevant setting, record an override and never force again.
                    if ((currentMethod.HasValue && currentMethod.Value != AnimationMethod.Legacy) ||
                        (currentLoop.HasValue && currentLoop.Value != false))
                    {
                        AddUserDataToken(assetImporter, UserDataUserOverrideToken);
                    }
                }
            }
#endif
            return new LegacyAnimationImportPluginContext();
        }

        /// <summary>
        /// Checks if the filename ends with the specified suffix of the Snippet assets.
        /// </summary>
        /// <param name="assetPath">The path of the asset</param>
        /// <returns>True if the filename ends with the suffix of Snippets assets, false otherwise</returns>
        private bool IsSnippetAnimationAsset(string assetPath)
        {
            const string AnimationFileSuffix = SnippetsSetZipper.AnimationFileSuffix;
            return assetPath.EndsWith(AnimationFileSuffix + ".glb") || assetPath.EndsWith(AnimationFileSuffix + ".gltf");
        }

        /// <summary>
        /// Set the animation import method and loop for the GLTFImporter.
        /// </summary>
        /// <param name="importer">The importer of the asset</param>
        /// <param name="animationMethod">Animation method to apply during import operation</param>
        /// <param name="animationLoop">Animation loop to apply during the import operation</param>
        private void SetImportAnimationParameters(GLTFImporter importer, AnimationMethod animationMethod, bool animationLoop)
        {
            // Use reflection to set the internal field '_importAnimations', because it is internal :|
            var field = typeof(GLTFImporter).GetField("_importAnimations", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
                field.SetValue(importer, animationMethod);

            // Use reflection to set the internal field '_animationLoopTime', because it is internal, too
            field = typeof(GLTFImporter).GetField("_animationLoopTime", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
                field.SetValue(importer, animationLoop);
        }

        private AnimationMethod? GetImportAnimationMethod(GLTFImporter importer)
        {
            var field = typeof(GLTFImporter).GetField("_importAnimations", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return null;

            return field.GetValue(importer) as AnimationMethod? ?? (AnimationMethod?)field.GetValue(importer);
        }

        private bool? GetImportAnimationLoop(GLTFImporter importer)
        {
            var field = typeof(GLTFImporter).GetField("_animationLoopTime", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return null;

            return field.GetValue(importer) as bool? ?? (bool?)field.GetValue(importer);
        }

        private bool HasUserOverride(UnityEditor.AssetImporter importer)
        {
            return HasUserDataToken(importer, UserDataUserOverrideToken);
        }

        private bool HasUserDataToken(UnityEditor.AssetImporter importer, string token)
        {
            if (importer == null || string.IsNullOrEmpty(importer.userData))
                return false;

            return importer.userData.Contains(token);
        }

        private void AddUserDataToken(UnityEditor.AssetImporter importer, string token)
        {
            if (importer == null)
                return;

            if (HasUserDataToken(importer, token))
                return;

            if (string.IsNullOrEmpty(importer.userData))
                importer.userData = token;
            else
                importer.userData = importer.userData + ";" + token;
        }
    }

    /// <summary>
    /// Context for the LegacyAnimationImportPlugin. We need to create this class for 
    /// the plugin <see cref="LegacyAnimationImportPlugin"/> to work.
    /// </summary>
    public class LegacyAnimationImportPluginContext : GLTFImportPluginContext
    {
    }
}
