
namespace Snippets.Sdk
{     
    /// <summary>
    /// Utility class to perform operations on assets.
    /// </summary>
    public static class AssetsUtilities
    {
        /// <summary>
        /// Sets the import settings for a texture asset.
        /// </summary>
        /// <param name="assetPath">The path of the asset to set the import settings for.</param>
        /// <param name="importerType">The type of the texture importer.</param>
        public static void SetTextureImporterSettings(string assetPath, UnityEditor.TextureImporterType importerType)
        {
            SetTextureImporterSettingsIfNeeded(assetPath, importerType);
        }

        /// <summary>
        /// Sets the import settings for a texture asset only if needed.
        /// </summary>
        /// <param name="assetPath">The path of the asset to set the import settings for.</param>
        /// <param name="importerType">The type of the texture importer.</param>
        /// <returns>True if a reimport was triggered, false otherwise.</returns>
        public static bool SetTextureImporterSettingsIfNeeded(string assetPath, UnityEditor.TextureImporterType importerType)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            UnityEditor.TextureImporter textureImporter = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;

            if (textureImporter == null || textureImporter.textureType == importerType)
                return false;

            textureImporter.textureType = importerType;
            textureImporter.SaveAndReimport();
            return true;
        }
    }
}
