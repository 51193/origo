using System.Collections.Generic;
using System.IO;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.TestSupport;

/// <summary>
///     In-memory <see cref="IFileSystem" /> for tests, delegating all
///     storage semantics to <see cref="MemoryFileSystem" /> and adding
///     test conveniences: <see cref="SeedFile" /> and read-call counting.
/// </summary>
public sealed class TestMemoryFileSystem : IFileSystem
{
    private readonly MemoryFileSystem _inner = new();

    public int ReadAllTextCallCount { get; private set; }

    public bool Exists(string path) => _inner.Exists(path);

    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    public string ReadAllText(string path)
    {
        ReadAllTextCallCount++;
        return _inner.ReadAllText(path);
    }

    public void WriteAllText(string path, string content, bool overwrite) =>
        _inner.WriteAllText(path, content, overwrite);

    public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
        _inner.Copy(sourcePath, destinationPath, overwrite);

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
        _inner.EnumerateFiles(directoryPath, searchPattern, recursive);

    public void CreateDirectory(string directoryPath) => _inner.CreateDirectory(directoryPath);

    public void Delete(string path) => _inner.Delete(path);

    public string CombinePath(string basePath, string relativePath) =>
        _inner.CombinePath(basePath, relativePath);

    public string GetParentDirectory(string path) => _inner.GetParentDirectory(path);

    public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
        _inner.EnumerateDirectories(directoryPath);

    public void Rename(string sourcePath, string destinationPath) =>
        _inner.Rename(sourcePath, destinationPath);

    public void DeleteDirectory(string directoryPath) => _inner.DeleteDirectory(directoryPath);

    /// <summary>
    ///     Writes a file, creating any missing parent directories.
    ///     Equivalent to <c>WriteAllText(path, content, overwrite: true)</c>.
    /// </summary>
    public void SeedFile(string path, string content) =>
        _inner.WriteAllText(path, content, overwrite: true);
}
