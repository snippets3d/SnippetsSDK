
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
            UnityEditor.TextureImporter textureImporter = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;

            if (textureImporter != null)
            {
                textureImporter.textureType = importerType;
                textureImporter.SaveAndReimport();
            }
        }
    }
}