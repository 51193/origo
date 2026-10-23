using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Unified save I/O operation handle, encapsulating
///     <see cref="IFileMetaAccess" />, <see cref="IDataSourceIoGateway" />,
///     <see cref="IPathResolver" />, the save root path, and
///     <see cref="ISavePathPolicy" />, along with consolidated path
///     resolution helper logic.
/// </summary>
internal sealed class SaveFileHandle
{
    public IFileMetaAccess MetaAccess { get; }
    public IDataSourceIoGateway IoGateway { get; }
    public IPathResolver PathResolver { get; }
    public string SaveRootPath { get; }
    public ISavePathPolicy PathPolicy { get; }

    public SaveFileHandle(
        IFileMetaAccess metaAccess,
        IDataSourceIoGateway ioGateway,
        IPathResolver pathResolver,
        string saveRootPath,
        ISavePathPolicy? pathPolicy = null)
    {
        MetaAccess = metaAccess ?? throw new ArgumentNullException(nameof(metaAccess));
        IoGateway = ioGateway ?? throw new ArgumentNullException(nameof(ioGateway));
        PathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        SaveRootPath = saveRootPath ?? throw new ArgumentNullException(nameof(saveRootPath));
        ValidateRootPath(saveRootPath, nameof(saveRootPath), "Save root path cannot be null or whitespace.");
        PathPolicy = pathPolicy ?? new DefaultSavePathPolicy();
    }

    public string GetAbsolutePath(string relativePath) => PathResolver.CombinePath(SaveRootPath, relativePath);

    public void EnsureParentDirectory(string filePath)
    {
        var absPath = GetAbsolutePath(filePath);
        var parentDir = PathResolver.GetParentDirectory(absPath);
        if (!string.IsNullOrEmpty(parentDir) && !MetaAccess.DirectoryExists(parentDir))
            MetaAccess.CreateDirectory(parentDir);
    }

    public string GetRelativePath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return fullPath;

        // IFileSystem/IPathResolver are platform-adapter contracts and may
        // return either separator style (and may expose a scheme root such as
        // user://). Normalize both sides before the prefix check so a Windows
        // host's backslash paths are not silently treated as "not under the
        // save root" (which would embed the absolute path in a snapshot).
        var normalizedFull = NormalizeSeparators(fullPath);
        var baseDir = NormalizeRoot(SaveRootPath);
        var basePrefix = baseDir.EndsWith("://", StringComparison.Ordinal)
            ? baseDir
            : baseDir.Length == 0 ? "/" : $"{baseDir}/";
        // Drive-letter and UNC roots are Windows paths, whose file system is
        // case-insensitive; a configured root's casing need not match the
        // enumerator's casing. Other roots (including scheme roots used on
        // Linux/macOS) stay ordinal so a genuinely different path is rejected.
        var comparison = IsDriveOrUncRoot(baseDir)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (normalizedFull.StartsWith(basePrefix, comparison))
        {
            var relative = normalizedFull[basePrefix.Length..];
            RejectPathTraversal(relative);
            return relative;
        }

        if (string.Equals(normalizedFull, baseDir, comparison))
            return string.Empty;

        throw new InvalidOperationException(
            $"Path '{fullPath}' is not under save root '{SaveRootPath}'. " +
            "Snapshot enumeration must only return paths below the configured save root.");
    }

    private static bool IsDriveOrUncRoot(string path) =>
        path.StartsWith("//", StringComparison.Ordinal)
        || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':');

    private static string NormalizeRoot(string path)
    {
        var normalized = NormalizeSeparators(path);
        return normalized.EndsWith("://", StringComparison.Ordinal)
            ? normalized
            : normalized.TrimEnd('/');
    }

    private static string NormalizeSeparators(string path)
    {
        var normalized = path.Replace('\\', '/');
        var schemeIndex = normalized.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex < 0)
            return normalized;

        // Godot's directory enumerator adds a third slash when the root is an
        // exact scheme root ("user://" + "/" + file); collapse extra leading
        // slashes after the scheme so both the combine path and the enumerated
        // path compare as "user://current/...".
        var schemeRoot = normalized[..(schemeIndex + 3)];
        var rest = normalized[(schemeIndex + 3)..].TrimStart('/');
        return rest.Length == 0 ? schemeRoot : $"{schemeRoot}{rest}";
    }

    public static string GetLeafDirectoryName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var sep = SavePathLayout.PathSeparator;
        var trimmed = path.TrimEnd(sep, '\\');
        var slashIndex = trimmed.LastIndexOf(sep);
        var backslashIndex = trimmed.LastIndexOf('\\');
        var lastSeparator = Math.Max(slashIndex, backslashIndex);
        return lastSeparator < 0 ? trimmed : trimmed[(lastSeparator + 1)..];
    }

    public static void RejectPathTraversal(string pathSegment)
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

    internal static void ValidateRootPath(string path, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(message, paramName);
    }
}
