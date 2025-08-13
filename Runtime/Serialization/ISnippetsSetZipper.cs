
namespace Snippets.Sdk
{
    /// <summary>
    /// Interface for classes that can compress and decompress Snippets Set data into a zip file.
    /// </summary>
    public interface ISnippetsSetZipper
    {
#if UNITY_EDITOR
        /// <summary>
        /// Compresses a snippet set into a zip file.
        /// </summary>
        /// <param name="snippetsSet">The Snippets Set with the data of the snippets set</param>
        /// <param name="outputFolderPath">The folder where the zip file will be saved</param>
        /// <returns>The full path of the created zip file</returns>
        string CompressSnippetsSet(SnippetsSetData snippetsSet, string outputFolderPath);
#endif

        /// <summary>
        /// Compresses a snippet set DTO into a zip file.
        /// </summary>
        /// <param name="snippetsSet">The Snippets Set DTO with the data of the snippets set</param>
        /// <param name="outputFolderPath">The folder where the zip file will be saved</param>
        /// <returns>The full path of the created zip file</returns>
        string CompressSnippetsSet(SnippetsSetDataDto snippetsSet, string outputFolderPath);

#if UNITY_EDITOR
        /// <summary>
        /// Decompresses a zip file containing the data of a Snippets Set into a folder 
        /// and then uses its data to create a <see cref="SnippetsSetData"/> object.
        /// </summary>
        /// <remarks>This method is only available in the Unity Editor and only 
        /// if the zip file is unzipped inside the project, that is in a subfolder
        /// of the Assets folder</remarks>
        /// <param name="zipFilePath">The path of the zip file to decompress.</param>
        /// <param name="outputFolderPath">The folder where the files will be extracted.</param>
        /// <returns>The decompressed SnippetsSetData object.</returns>
        SnippetsSetData DecompressSnippetsSet(string zipFilePath, string outputFolderPath);
#endif

        /// <summary>
        /// Decompress a zip file containing the data of a Snippets Set into a folder
        /// and then uses its data to create a <see cref="SnippetsSetDataDto"/> object.
        /// </summary>
        /// <param name="zipFilePath">The path of the zip file to decompress.</param>
        /// <param name="outputFolderPath">The folder where the files will be extracted.</param>
        /// <returns>The decompressed SnippetsSetDataDto object.</returns>
        SnippetsSetDataDto DecompressSnippetsSetDto(string zipFilePath, string outputFolderPath);
    }
}