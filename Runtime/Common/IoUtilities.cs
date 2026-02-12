using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Utilities for IO operations.
    /// </summary>
    public static class IoUtilities
    {
        /// <summary>
        /// Combines two paths by merging overlapping segments to avoid repetition.
        /// For example:
        /// Path1 = "\A\B\C\D"
        /// Path2 = "C\D\E\F"
        /// Result = "\A\B\C\D\E\F"
        /// </summary>
        /// <param name="path1">The first path.</param>
        /// <param name="path2">The second path.</param>
        /// <returns>The combined path without repeating overlapping segments.</returns>
        public static string MergePaths(string path1, string path2)
        {
            //this method has been AI generated

            if (string.IsNullOrEmpty(path1))
                return path2;
            if (string.IsNullOrEmpty(path2))
                return path1;

            // Normalize directory separators
            char separator = Path.DirectorySeparatorChar;
            path1 = path1.Replace(Path.AltDirectorySeparatorChar, separator);
            path2 = path2.Replace(Path.AltDirectorySeparatorChar, separator);

            // Split paths into segments
            var path1Segments = path1.TrimEnd(separator).Split(separator);
            var path2Segments = path2.TrimStart(separator).Split(separator);

            // Find the maximum number of overlapping segments
            int maxOverlap = Mathf.Min(path1Segments.Length, path2Segments.Length);
            int overlap = 0;

            for (int i = 1; i <= maxOverlap; i++)
            {
                bool isMatch = true;
                for (int j = 0; j < i; j++)
                {
                    if (!string.Equals(path1Segments[path1Segments.Length - i + j], path2Segments[j], StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = false;
                        break;
                    }
                }
                if (isMatch)
                    overlap = i;
            }

            // Combine paths without repeating overlapping segments
            var combinedSegments = path1Segments.Concat(path2Segments.Skip(overlap)).ToArray();
            string combinedPath = string.Join(separator.ToString(), combinedSegments);

            // If the first path was rooted, ensure the combined path is also rooted
            if (Path.IsPathRooted(path1) && path1[0] == Path.DirectorySeparatorChar)
            {
                combinedPath = Path.DirectorySeparatorChar + combinedPath;
            }

            return combinedPath;
        }

        /// <summary>
        /// Gets the project-relative path of a file or directory that are in the project,
        /// adjusting the slashes in the process
        /// </summary>
        /// <param name="fullPath">The full absolute path of a file of interest</param>
        /// <returns>Project-relative path of the file, or string empty in case the file is not in the project</returns>
        public static string GetProjectRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;

            //reverse slashes in full path to match the project path
            fullPath = fullPath.Replace('\\', '/');

            //if already starts with "Assets" return the path
            if (fullPath.StartsWith("Assets/"))
                return fullPath;

            //check the path starts with the project path
            string projectPath = Application.dataPath;

            if (!fullPath.StartsWith(projectPath))
                return string.Empty;

            //replace the project path with "Assets"
            string projectRelativePath = fullPath.Replace(projectPath, "Assets");

            //return the project relative path
            return projectRelativePath;
        }
    }
}
