using System;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.Core.Save.Storage;

internal static class SavePathResolver
{
    public static void EnsureParentDirectory(IFileSystem fileSystem, string filePath)
    {
        var parentDir = fileSystem.GetParentDirectory(filePath);
        if (!string.IsNullOrEmpty(parentDir) && !fileSystem.DirectoryExists(parentDir))
            fileSystem.CreateDirectory(parentDir);
    }

    public static string GetRelativePath(string baseDirectory, string fullPath)
    {
        if (string.IsNullOrEmpty(baseDirectory) || string.IsNullOrEmpty(fullPath))
            return fullPath;

        // Normalize to "baseDir/" to avoid false positives: "/a/b1" should not match base "/a/b".
        var baseDir = baseDirectory.TrimEnd('/');
        var basePrefix = baseDir.Length == 0 ? "/" : $"{baseDir}/";

        if (fullPath.StartsWith(basePrefix, StringComparison.Ordinal))
        {
            var relative = fullPath.Substring(basePrefix.Length);
            RejectPathTraversal(relative);
            return relative;
        }

        if (string.Equals(fullPath, baseDir, StringComparison.Ordinal))
            return string.Empty;

        return fullPath;
    }

    public static string GetLeafDirectoryName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.TrimEnd('/', '\\');
        var slashIndex = trimmed.LastIndexOf('/');
        var backslashIndex = trimmed.LastIndexOf('\\');
        var lastSeparator = Math.Max(slashIndex, backslashIndex);
        return lastSeparator < 0 ? trimmed : trimmed.Substring(lastSeparator + 1);
    }

    /// <summary>
    ///     拒绝包含 ".." 路径遍历序列的路径片段。
    /// </summary>
    internal static void RejectPathTraversal(string pathSegment)
    {
        if (string.IsNullOrEmpty(pathSegment))
            return;

        var normalized = pathSegment.Replace('\\', '/');
        if (normalized.Contains("../", StringComparison.Ordinal)
            || normalized.EndsWith("..", StringComparison.Ordinal)
            || normalized.StartsWith("../", StringComparison.Ordinal)
            || normalized == "..")
            throw new ArgumentException(
                $"Path must not contain path traversal sequences: '{pathSegment}'",
                nameof(pathSegment));
    }
}
