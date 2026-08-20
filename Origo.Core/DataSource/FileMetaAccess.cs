using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.Core.DataSource;

/// <summary>
///     Adapts <see cref="IFileSystem" /> to <see cref="IFileMetaAccess" />
///     for Core-layer file metadata operations (existence, enumeration,
///     creation, deletion, copy, rename).
/// </summary>
internal sealed class FileMetaAccess(IFileSystem fileSystem) : IFileMetaAccess
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("DataSource file path cannot be null or whitespace.", nameof(path));
        return _fileSystem.Exists(path);
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("DataSource file path cannot be null or whitespace.", nameof(path));
        return _fileSystem.DirectoryExists(path);
    }

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
        _fileSystem.EnumerateFiles(directoryPath, searchPattern, recursive);

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
        _fileSystem.EnumerateDirectories(directoryPath);

    /// <inheritdoc/>
    public void CreateDirectory(string directoryPath) =>
        _fileSystem.CreateDirectory(directoryPath);

    /// <inheritdoc/>
    public void Delete(string path) =>
        _fileSystem.Delete(path);

    /// <inheritdoc/>
    public void DeleteDirectory(string directoryPath) =>
        _fileSystem.DeleteDirectory(directoryPath);

    /// <inheritdoc/>
    public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
        _fileSystem.Copy(sourcePath, destinationPath, overwrite);

    /// <inheritdoc/>
    public void Rename(string sourcePath, string destinationPath) =>
        _fileSystem.Rename(sourcePath, destinationPath);
}
