using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Utility;

namespace Origo.GodotAdapter.FileSystem;

/// <summary>
///     Implementation of <see cref="IFileSystem" /> based on Godot <see cref="FileAccess" />
///     and <see cref="DirAccess" />, supporting <c>res://</c> and <c>user://</c> paths.
///     Path concatenation and parent directory operations are also implemented here
///     to correctly handle Godot virtual paths.
/// </summary>
public sealed class GodotFileSystem : IFileSystem
{
    public bool Exists(string path) => GodotFileOperations.Exists(path);

    public bool DirectoryExists(string path) => GodotDirectoryOperations.Exists(path);

    public string ReadAllText(string path) => GodotFileOperations.ReadAllText(path);

    public void WriteAllText(string path, string content, bool overwrite) =>
        GodotFileOperations.WriteAllText(path, content, overwrite);

    public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
        GodotFileOperations.Copy(sourcePath, destinationPath, overwrite);

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        return GodotDirectoryOperations.EnumerateFiles(directoryPath, searchPattern, recursive);
    }

    public void CreateDirectory(string directoryPath) => GodotDirectoryOperations.Create(directoryPath);

    public void Delete(string path) => GodotFileOperations.Delete(path);

    public string CombinePath(string basePath, string relativePath) =>
        PathUtility.Combine(basePath, relativePath);

    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        return GodotDirectoryOperations.EnumerateDirectories(directoryPath);
    }

    public string GetParentDirectory(string path) => PathUtility.GetParentDirectory(path);

    public void Rename(string sourcePath, string destinationPath) =>
        GodotDirectoryOperations.Rename(sourcePath, destinationPath);

    public void DeleteDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        GodotDirectoryOperations.DeleteRecursive(directoryPath);
    }
}
