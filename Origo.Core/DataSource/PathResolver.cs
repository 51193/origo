using System;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.Core.DataSource;

internal sealed class PathResolver : IPathResolver
{
    private readonly IFileSystem _fileSystem;

    public PathResolver(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string CombinePath(string basePath, string relativePath) =>
        _fileSystem.CombinePath(basePath, relativePath);

    public string GetParentDirectory(string path) =>
        _fileSystem.GetParentDirectory(path);
}
