using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif

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

            fullPath = NormalizePath(fullPath);

            if (IsAssetDatabasePath(fullPath))
                return fullPath;

            string assetsRootPath = NormalizePath(Application.dataPath);

            if (IsPathInsideRoot(fullPath, assetsRootPath))
                return "Assets" + fullPath.Substring(assetsRootPath.Length);

#if UNITY_EDITOR
            foreach (var packageInfo in PackageInfo.GetAllRegisteredPackages())
            {
                string packageRootPath = NormalizePath(packageInfo.resolvedPath);
                if (!IsPathInsideRoot(fullPath, packageRootPath))
                    continue;

                string relativePath = fullPath.Substring(packageRootPath.Length).TrimStart('/');
                return string.IsNullOrEmpty(relativePath)
                    ? $"Packages/{packageInfo.name}"
                    : $"Packages/{packageInfo.name}/{relativePath}";
            }
#endif

            return string.Empty;
        }

        public static string GetAbsoluteAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            assetPath = NormalizePath(assetPath);
            if (Path.IsPathRooted(assetPath))
                return Path.GetFullPath(assetPath);

            string projectRootPath = NormalizePath(Path.GetDirectoryName(Application.dataPath));

            if (assetPath.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(projectRootPath, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            }

#if UNITY_EDITOR
            if (assetPath.Equals("Packages", StringComparison.OrdinalIgnoreCase) ||
                assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                var packageInfo = PackageInfo.FindForAssetPath(assetPath);
                if (packageInfo != null)
                {
                    string packageAssetPathPrefix = $"Packages/{packageInfo.name}";
                    string relativePath = assetPath.Substring(packageAssetPathPrefix.Length).TrimStart('/');
                    return string.IsNullOrEmpty(relativePath)
                        ? Path.GetFullPath(packageInfo.resolvedPath)
                        : Path.GetFullPath(Path.Combine(packageInfo.resolvedPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
#endif

            return string.Empty;
        }

        private static bool IsAssetDatabasePath(string path)
        {
            return path.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                   path.Equals("Packages", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathInsideRoot(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
                return false;

            if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase))
                return true;

            return path.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
