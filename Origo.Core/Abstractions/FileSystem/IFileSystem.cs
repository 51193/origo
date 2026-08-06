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
    /// <summary>Checks whether a file exists at the given path.</summary>
    bool Exists(string path);

    /// <summary>Checks whether a directory exists at the given path.</summary>
    bool DirectoryExists(string path);

    /// <summary>Reads all text from the file at the given path.</summary>
    string ReadAllText(string path);

    /// <summary>Writes text to the file at the given path, honoring the overwrite flag.</summary>
    void WriteAllText(string path, string content, bool overwrite);

    /// <summary>Copies a file to the destination path, honoring the overwrite flag.</summary>
    void Copy(string sourcePath, string destinationPath, bool overwrite);

    /// <summary>Enumerates files under the given directory, optionally recursive, filtered by a search pattern.</summary>
    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive);

    /// <summary>Creates the directory (and parents) at the given path.</summary>
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
