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
    /// <summary>Checks whether a file exists at the given virtual path.</summary>
    public bool Exists(string path) => GodotFileOperations.Exists(path);

    /// <summary>Checks whether a directory exists at the given virtual path.</summary>
    public bool DirectoryExists(string path) => GodotDirectoryOperations.Exists(path);

    /// <summary>Reads all text from the file at the given virtual path.</summary>
    public string ReadAllText(string path) => GodotFileOperations.ReadAllText(path);

    /// <summary>Writes text to the file at the given virtual path, honoring the overwrite flag.</summary>
    public void WriteAllText(string path, string content, bool overwrite) =>
        GodotFileOperations.WriteAllText(path, content, overwrite);

    /// <summary>Copies a file to the destination path, honoring the overwrite flag.</summary>
    public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
        GodotFileOperations.Copy(sourcePath, destinationPath, overwrite);

    /// <summary>Enumerates files under the given directory, optionally recursive.</summary>
    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        return GodotDirectoryOperations.EnumerateFiles(directoryPath, searchPattern, recursive);
    }

    /// <summary>Creates the directory (and parents) at the given virtual path.</summary>
    public void CreateDirectory(string directoryPath) => GodotDirectoryOperations.Create(directoryPath);

    /// <summary>Deletes the file at the given virtual path.</summary>
    public void Delete(string path) => GodotFileOperations.Delete(path);

    /// <summary>Combines a base path with a relative path using Godot path semantics.</summary>
    public string CombinePath(string basePath, string relativePath) =>
        PathUtility.Combine(basePath, relativePath);

    /// <summary>Enumerates immediate subdirectories of the given directory.</summary>
    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        return GodotDirectoryOperations.EnumerateDirectories(directoryPath);
    }

    /// <summary>Gets the parent directory of the given virtual path.</summary>
    public string GetParentDirectory(string path) => PathUtility.GetParentDirectory(path);

    /// <summary>Renames or moves a file/directory.</summary>
    public void Rename(string sourcePath, string destinationPath) =>
        GodotDirectoryOperations.Rename(sourcePath, destinationPath);

    /// <summary>Recursively deletes the directory at the given virtual path.</summary>
    public void DeleteDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        GodotDirectoryOperations.DeleteRecursive(directoryPath);
    }
}
