using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd;

/// <summary>
///     Save-archive-scoped file access companion for <see cref="SndContext" />.
///     Resolves relative paths into the current save's <c>extra/</c> subdirectory
///     and rejects path traversal attempts.
/// </summary>
internal sealed class SndContextArchiveFileAccess(
    IDataSourceIoGateway dataSourceIo,
    IFileMetaAccess metaAccess,
    DataSourceConverterRegistry converterRegistry,
    IPathResolver pathResolver,
    string saveRootPath,
    ISavePathPolicy savePathPolicy) : ISndArchiveFileAccess
{
    /// <inheritdoc/>
    public DataSourceNode ReadFile(string relativePath)
    {
        RejectPathTraversal(relativePath);
        return dataSourceIo.ReadTree(ResolveExtraPath(relativePath));
    }

    /// <inheritdoc/>
    public void WriteFile(string relativePath, DataSourceNode node, bool overwrite)
    {
        RejectPathTraversal(relativePath);
        dataSourceIo.WriteTree(ResolveExtraPath(relativePath), node, overwrite);
    }

    /// <inheritdoc/>
    public bool FileExists(string relativePath)
    {
        RejectPathTraversal(relativePath);
        return metaAccess.FileExists(ResolveExtraPath(relativePath));
    }

    /// <inheritdoc/>
    public T ReadObject<T>(string relativePath)
    {
        RejectPathTraversal(relativePath);
        using var node = dataSourceIo.ReadTree(ResolveExtraPath(relativePath));
        return converterRegistry.Read<T>(node);
    }

    /// <inheritdoc/>
    public void WriteObject<T>(string relativePath, T value, bool overwrite)
    {
        RejectPathTraversal(relativePath);
        var node = converterRegistry.Write(value);
        dataSourceIo.WriteTree(ResolveExtraPath(relativePath), node, overwrite);
    }

    /// <inheritdoc/>
    public void DeleteFile(string relativePath)
    {
        RejectPathTraversal(relativePath);
        var absPath = ResolveExtraPath(relativePath);
        if (!metaAccess.FileExists(absPath))
            throw new InvalidOperationException($"File not found in archive: '{relativePath}'.");
        metaAccess.Delete(absPath);
    }

    private static void RejectPathTraversal(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Segment-level check (consistent with PathUtility.Combine): only a
        // ".." segment (in either separator style) escapes the archive — a
        // plain substring like "my..file" is a legal file name.
        var normalized = path.Replace('\\', '/');
        foreach (var segment in normalized.Split('/'))
            if (segment == "..")
                throw new ArgumentException("Path traversal '..' is not allowed.", nameof(path));
    }

    private string ResolveExtraPath(string relativePath)
    {
        var currentRel = savePathPolicy.GetCurrentDirectory();
        var extraRel = savePathPolicy.GetExtraDirectory(currentRel);
        var fileRel = SavePathLayout.Combine(extraRel, relativePath);
        return pathResolver.CombinePath(saveRootPath, fileRel);
    }
}
