using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Storage;

/// <summary>
///     统一的存档 I/O 操作句柄，封装 <see cref="IFileMetaAccess" />、<see cref="IDataSourceIoGateway" />、
///     <see cref="IPathResolver" />、保存根路径和 <see cref="ISavePathPolicy" />，
///     并合并了路径解析的辅助逻辑。
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

    public string GetAbsolutePath(string relativePath)
    {
        return PathResolver.CombinePath(SaveRootPath, relativePath);
    }

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

        var sep = SavePathLayout.PathSeparator;
        var baseDir = SaveRootPath.TrimEnd(sep, '\\');
        var basePrefix = baseDir.Length == 0 ? $"{sep}" : $"{baseDir}{sep}";

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

        var sep = SavePathLayout.PathSeparator;
        var trimmed = path.TrimEnd(sep, '\\');
        var slashIndex = trimmed.LastIndexOf(sep);
        var backslashIndex = trimmed.LastIndexOf('\\');
        var lastSeparator = Math.Max(slashIndex, backslashIndex);
        return lastSeparator < 0 ? trimmed : trimmed.Substring(lastSeparator + 1);
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
