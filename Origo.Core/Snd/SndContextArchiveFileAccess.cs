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
    private readonly IDataSourceIoGateway _dataSourceIo = dataSourceIo;
    private readonly IFileMetaAccess _metaAccess = metaAccess;
    private readonly DataSourceConverterRegistry _converterRegistry = converterRegistry;
    private readonly IPathResolver _pathResolver = pathResolver;
    private readonly string _saveRootPath = saveRootPath;
    private readonly ISavePathPolicy _savePathPolicy = savePathPolicy;

    public DataSourceNode ReadFile(string relativePath)
    {
        RejectPathTraversal(relativePath);
        return _dataSourceIo.ReadTree(ResolveExtraPath(relativePath));
    }

    public void WriteFile(string relativePath, DataSourceNode node, bool overwrite)
    {
        RejectPathTraversal(relativePath);
        _dataSourceIo.WriteTree(ResolveExtraPath(relativePath), node, overwrite);
    }

    public bool FileExists(string relativePath)
    {
        RejectPathTraversal(relativePath);
        return _metaAccess.FileExists(ResolveExtraPath(relativePath));
    }

    public T ReadObject<T>(string relativePath)
    {
        RejectPathTraversal(relativePath);
        var node = _dataSourceIo.ReadTree(ResolveExtraPath(relativePath));
        return _converterRegistry.Read<T>(node);
    }

    public void WriteObject<T>(string relativePath, T value, bool overwrite)
    {
        RejectPathTraversal(relativePath);
        var node = _converterRegistry.Write(value);
        _dataSourceIo.WriteTree(ResolveExtraPath(relativePath), node, overwrite);
    }

    public void DeleteFile(string relativePath)
    {
        RejectPathTraversal(relativePath);
        var absPath = ResolveExtraPath(relativePath);
        if (!_metaAccess.FileExists(absPath))
            throw new InvalidOperationException($"File not found in archive: '{relativePath}'.");
        _metaAccess.Delete(absPath);
    }

    private static void RejectPathTraversal(string path)
    {
        if (path.Contains(".."))
            throw new ArgumentException("Path traversal '..' is not allowed.", nameof(path));
    }

    private string ResolveExtraPath(string relativePath)
    {
        var currentRel = _savePathPolicy.GetCurrentDirectory();
        var extraRel = _savePathPolicy.GetExtraDirectory(currentRel);
        var fileRel = SavePathLayout.Combine(extraRel, relativePath);
        return _pathResolver.CombinePath(_saveRootPath, fileRel);
    }
}
