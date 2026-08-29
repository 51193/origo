using System.Collections.Generic;
using System.IO;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.TestSupport;

/// <summary>
///     In-memory <see cref="IFileSystem" /> for tests, delegating all
///     storage semantics to <see cref="MemoryFileSystem" /> and adding
///     test conveniences: <see cref="SeedFile" /> and read-call counting.
/// </summary>
public sealed class TestMemoryFileSystem : IFileSystem
{
    private readonly MemoryFileSystem _inner = new();

    /// <summary>Number of times <see cref="ReadAllText" /> has been called.</summary>
    public int ReadAllTextCallCount { get; private set; }

    /// <inheritdoc/>
    public bool Exists(string path) => _inner.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    /// <inheritdoc/>
    public string ReadAllText(string path)
    {
        ReadAllTextCallCount++;
        return _inner.ReadAllText(path);
    }

    /// <inheritdoc/>
    public void WriteAllText(string path, string content, bool overwrite) =>
        _inner.WriteAllText(path, content, overwrite);

    /// <inheritdoc/>
    public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
        _inner.Copy(sourcePath, destinationPath, overwrite);

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
        _inner.EnumerateFiles(directoryPath, searchPattern, recursive);

    /// <inheritdoc/>
    public void CreateDirectory(string directoryPath) => _inner.CreateDirectory(directoryPath);

    /// <inheritdoc/>
    public void Delete(string path) => _inner.Delete(path);

    /// <inheritdoc/>
    public string CombinePath(string basePath, string relativePath) =>
        _inner.CombinePath(basePath, relativePath);

    /// <inheritdoc/>
    public string GetParentDirectory(string path) => _inner.GetParentDirectory(path);

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
        _inner.EnumerateDirectories(directoryPath);

    /// <inheritdoc/>
    public void Rename(string sourcePath, string destinationPath) =>
        _inner.Rename(sourcePath, destinationPath);

    /// <inheritdoc/>
    public void DeleteDirectory(string directoryPath) => _inner.DeleteDirectory(directoryPath);

    /// <summary>
    ///     Writes a file, creating any missing parent directories.
    ///     Equivalent to <c>WriteAllText(path, content, overwrite: true)</c>.
    /// </summary>
    public void SeedFile(string path, string content) =>
        _inner.WriteAllText(path, content, overwrite: true);
}
