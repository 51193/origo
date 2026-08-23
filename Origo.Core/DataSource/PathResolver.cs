using System;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.Core.DataSource;

/// <summary>
///     Adapts <see cref="IFileSystem" /> to <see cref="IPathResolver" />
///     for platform-correct path operations.
/// </summary>
internal sealed class PathResolver(IFileSystem fileSystem) : IPathResolver
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <inheritdoc/>
    public string CombinePath(string basePath, string relativePath) =>
        _fileSystem.CombinePath(basePath, relativePath);

    /// <inheritdoc/>
    public string GetParentDirectory(string path) =>
        _fileSystem.GetParentDirectory(path);
}
