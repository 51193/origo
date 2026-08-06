using System;

namespace Origo.Core.Utility;

/// <summary>
///     Platform-agnostic path utilities: normalization, combining with
///     traversal guard, suffix extraction, and parent directory lookup.
/// </summary>
public static class PathUtility
{
    /// <summary>Normalizes a directory path by trimming trailing separators.</summary>
    public static string NormalizeDirectoryPath(string path)
    {
        if (path is null)
            return string.Empty;
        return path.TrimEnd('/', '\\');
    }

    /// <summary>Extracts the suffix after a leading '*' glob; null when the pattern has no glob prefix.</summary>
    public static string? ExtractGlobSuffix(string searchPattern)
    {
        if (!string.IsNullOrEmpty(searchPattern) && searchPattern.StartsWith('*'))
            return searchPattern[1..];
        return null;
    }

    /// <summary>
    ///     Combines a base path with a relative path, rejecting traversal sequences.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativePath" /> contains a traversal sequence.</exception>
    public static string Combine(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return relativePath;
        if (string.IsNullOrEmpty(relativePath))
            return basePath;

        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            var normalized = relativePath.Replace('\\', '/');
            if (normalized.Contains("../", StringComparison.Ordinal)
                || normalized.EndsWith("..", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Relative path must not contain path traversal sequences: '{relativePath}'",
                    nameof(relativePath));
        }

        return $"{basePath.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    /// <summary>Gets the parent directory of the given path.</summary>
    public static string GetParentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var trimmed = path.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash < 0)
        {
            if (trimmed.Length == 0 || trimmed.IndexOf(':') == trimmed.Length - 1)
                throw new InvalidOperationException(
                    $"Path '{path}' is at root and has no parent directory.");

            return string.Empty;
        }

        var parent = trimmed[..lastSlash];
        if (parent.Length == 0)
            throw new InvalidOperationException(
                $"Path '{path}' is at root and has no parent directory.");

        return parent;
    }
}
