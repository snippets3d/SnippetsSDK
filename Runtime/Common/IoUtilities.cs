using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Snippets.Sdk
{
    public static class IoUtilities
    {
        public static string MergePaths(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1))
                return path2;
            if (string.IsNullOrEmpty(path2))
                return path1;

            char separator = Path.DirectorySeparatorChar;
            path1 = path1.Replace(Path.AltDirectorySeparatorChar, separator);
            path2 = path2.Replace(Path.AltDirectorySeparatorChar, separator);

            var path1Segments = path1.TrimEnd(separator).Split(separator);
            var path2Segments = path2.TrimStart(separator).Split(separator);

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

            var combinedSegments = path1Segments.Concat(path2Segments.Skip(overlap)).ToArray();
            string combinedPath = string.Join(separator.ToString(), combinedSegments);

            if (Path.IsPathRooted(path1) && path1[0] == Path.DirectorySeparatorChar)
            {
                combinedPath = Path.DirectorySeparatorChar + combinedPath;
            }

            return combinedPath;
        }

        public static string GetProjectRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;

            fullPath = fullPath.Replace('\\', '/');

            if (fullPath.StartsWith("Assets/"))
                return fullPath;

            string projectPath = Application.dataPath;

            if (!fullPath.StartsWith(projectPath))
                return string.Empty;

            string projectRelativePath = fullPath.Replace(projectPath, "Assets");

            return projectRelativePath;
        }
    }
}
