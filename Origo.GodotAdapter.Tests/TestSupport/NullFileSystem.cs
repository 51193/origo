using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.GodotAdapter.Tests.TestSupport;

internal sealed class NullFileSystem : IFileSystem
{
    public bool Exists(string path) => false;

    public bool DirectoryExists(string path) => false;

    public string ReadAllText(string path) =>
        throw new NotSupportedException("NullFileSystem does not support I/O.");

    public void WriteAllText(string path, string content, bool overwrite) =>
        throw new NotSupportedException("NullFileSystem does not support I/O.");

    public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
        throw new NotSupportedException("NullFileSystem does not support I/O.");

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
        [];

    public void CreateDirectory(string directoryPath)
    {
    }

    public void Delete(string path)
    {
    }

    public string CombinePath(string basePath, string relativePath) =>
        $"{basePath.TrimEnd('/')}/{relativePath.TrimStart('/')}";

    public string GetParentDirectory(string path) => string.Empty;

    public IEnumerable<string> EnumerateDirectories(string directoryPath) => [];

    public void Rename(string sourcePath, string destinationPath)
    {
    }

    public void DeleteDirectory(string directoryPath)
    {
    }
}
