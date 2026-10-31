using System;

namespace Origo.Core.Utility;

/// <summary>
///     Platform-agnostic path utilities: normalization, combining with
///     traversal guard, suffix extraction, and parent directory lookup.
/// </summary>
public static class PathUtility
{
    /// <summary>
    ///     Normalizes a directory path by trimming trailing separators, while
    ///     preserving root forms: a scheme root (<c>user://</c>), a plain
    ///     filesystem root (<c>/</c>), and a drive root (<c>C:\</c>) are
    ///     returned unchanged instead of degrading to an empty or bare-drive
    ///     string.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="path" /> is null, matching
    ///     <see cref="Combine" />.
    /// </exception>
    public static string NormalizeDirectoryPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        // A scheme root ("user://", "user:///", "res:////") must normalize to
        // the scheme marker; trimming would destroy it into the invalid
        // "user:" prefix.
        if (TryGetSchemeRoot(path, out var schemeRoot))
            return schemeRoot;

        var trimmed = path.TrimEnd('/', '\\');
        if (trimmed.Length == 0)
        {
            // "/" and "\" are filesystem roots: keep the separator instead of
            // returning an empty string that Combine would treat as "no base".
            return path.Length == 0 ? path : path[..1];
        }

        // "C:\" trims to the drive-relative "C:"; keep the separator so the
        // result stays an absolute root.
        if (trimmed.Length == 2 && trimmed[1] == ':' && char.IsLetter(trimmed[0]))
            return path;

        return trimmed;
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="basePath" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativePath" /> contains a traversal sequence.</exception>
    public static string Combine(string basePath, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        if (string.IsNullOrEmpty(relativePath))
            return basePath;

        // The traversal guard applies to every branch, including an empty
        // base (where Combine would otherwise pass the relative path
        // through untouched) and scheme roots.
        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            var normalized = relativePath.Replace('\\', '/');
            if (normalized.Contains("../", StringComparison.Ordinal)
                || normalized.EndsWith("..", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Relative path must not contain path traversal sequences: '{relativePath}'",
                    nameof(relativePath));
        }

        if (basePath.Length == 0)
            return relativePath;

        // A scheme root ("user://", "user:///") must keep its double slash;
        // trimming it would produce the invalid "user:/" prefix.
        if (TryGetSchemeRoot(basePath, out var schemeRoot))
            return $"{schemeRoot}{relativePath.TrimStart('/')}";
        return $"{basePath.TrimEnd('/', '\\')}/{relativePath.TrimStart('/')}";
    }

    /// <summary>
    ///     Recognizes a scheme root: a non-empty scheme, "://", and only
    ///     separators after it. Extra separators are normalized away.
    /// </summary>
    private static bool TryGetSchemeRoot(string path, out string root)
    {
        root = string.Empty;
        var schemeIndex = path.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex <= 0)
            return false;
        for (var index = schemeIndex + 3; index < path.Length; index++)
        {
            if (path[index] is not ('/' or '\\'))
                return false;
        }
        root = path[..(schemeIndex + 3)];
        return true;
    }

    /// <summary>Gets the parent directory of the given path.</summary>
    public static string GetParentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        // Scheme paths ("user://dir/file"): the scheme root "user://" is the
        // top of the hierarchy; splitting must not truncate its double slash.
        var schemeIndex = path.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            var schemeRoot = path[..(schemeIndex + 3)];
            var rest = path[(schemeIndex + 3)..].TrimEnd('/', '\\');
            if (rest.Length == 0)
                throw new InvalidOperationException(
                    $"Path '{path}' is at root and has no parent directory.");

            var lastSlash = Math.Max(rest.LastIndexOf('/'), rest.LastIndexOf('\\'));
            if (lastSlash < 0)
                return schemeRoot;
            var parent = rest[..lastSlash];
            return parent.Length == 0 ? schemeRoot : $"{schemeRoot}{parent}";
        }

        var trimmed = path.TrimEnd('/', '\\');
        var lastSeparator = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        if (lastSeparator < 0)
        {
            if (trimmed.Length == 0 || trimmed.IndexOf(':') == trimmed.Length - 1)
                throw new InvalidOperationException(
                    $"Path '{path}' is at root and has no parent directory.");

            return string.Empty;
        }

        var parentDir = trimmed[..lastSeparator];
        if (parentDir.Length == 0)
            throw new InvalidOperationException(
                $"Path '{path}' is at root and has no parent directory.");

        return parentDir;
    }
}
