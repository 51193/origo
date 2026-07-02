using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd;

internal sealed class SndContextArchiveFileAccess(
    IDataSourceIoGateway dataSourceIo,
    IFileMetaAccess metaAccess,
    DataSourceConverterRegistry converterRegistry,
    IPathResolver pathResolver,
    string saveRootPath,
    ISavePathPolicy savePathPolicy) : ISndArchiveFileAccess
{
    public DataSourceNode ReadFile(string relativePath)
    {
        RejectPathTraversal(relativePath);
        return dataSourceIo.ReadTree(ResolveExtraPath(relativePath));
    }

    public void WriteFile(string relativePath, DataSourceNode node, bool overwrite)
    {
        RejectPathTraversal(relativePath);
        dataSourceIo.WriteTree(ResolveExtraPath(relativePath), node, overwrite);
    }

    public bool FileExists(string relativePath)
    {
        RejectPathTraversal(relativePath);
        return metaAccess.FileExists(ResolveExtraPath(relativePath));
    }

    public T ReadObject<T>(string relativePath)
    {
        RejectPathTraversal(relativePath);
        var node = dataSourceIo.ReadTree(ResolveExtraPath(relativePath));
        return converterRegistry.Read<T>(node);
    }

    public void WriteObject<T>(string relativePath, T value, bool overwrite)
    {
        RejectPathTraversal(relativePath);
        var node = converterRegistry.Write(value);
        dataSourceIo.WriteTree(ResolveExtraPath(relativePath), node, overwrite);
    }

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
        if (path.Contains(".."))
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
