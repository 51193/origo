using System.Collections.Generic;

namespace Origo.Core.Abstractions.FileSystem;

/// <summary>
///     Abstract file system access interface that abstracts away platform-
///     and engine-specific APIs. Path operations (combining, parent directory
///     lookup, etc.) are also handled by the implementation to correctly
///     process engine virtual paths (e.g., Godot's res://, user://).
/// </summary>
public interface IFileSystem
{
    bool Exists(string path);

    bool DirectoryExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string content, bool overwrite);

    void Copy(string sourcePath, string destinationPath, bool overwrite);

    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive);

    void CreateDirectory(string directoryPath);

    /// <summary>
    ///     Delete the file at the specified path. Ignored if the file
    ///     does not exist.
    /// </summary>
    void Delete(string path);

    /// <summary>
    ///     Combine a base path and a relative path using platform-appropriate
    ///     separators.
    /// </summary>
    string CombinePath(string basePath, string relativePath);

    /// <summary>
    ///     Get the parent directory path of the specified path.
    /// </summary>
    string GetParentDirectory(string path);

    /// <summary>
    ///     Enumerate immediate subdirectories under the specified directory,
    ///     returning a full path list.
    /// </summary>
    IEnumerable<string> EnumerateDirectories(string directoryPath);

    /// <summary>
    ///     Atomically rename/move a directory or file from
    ///     <paramref name="sourcePath" /> to <paramref name="destinationPath" />.
    ///     If the destination already exists, behavior is
    ///     implementation-defined (may overwrite or throw).
    /// </summary>
    void Rename(string sourcePath, string destinationPath);

    /// <summary>
    ///     Recursively delete the specified directory and all its contents.
    ///     Ignored if the directory does not exist.
    /// </summary>
    void DeleteDirectory(string directoryPath);
}
