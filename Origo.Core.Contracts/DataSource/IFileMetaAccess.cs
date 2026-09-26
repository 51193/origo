using System.Collections.Generic;

namespace Origo.Core.DataSource;

/// <summary>
///     File metadata access interface: provides file/directory existence checks, enumeration, creation,
///     deletion, copy, rename, and other file-system-level meta-operations. Used alongside
///     <see cref="IDataSourceIoGateway" />, where the former handles content I/O (including codec routing)
///     and this interface handles metadata and file system structure operations.
/// </summary>
public interface IFileMetaAccess
{
    /// <summary>Checks whether a file exists at the given path.</summary>
    bool FileExists(string path);

    /// <summary>Checks whether a directory exists at the given path.</summary>
    bool DirectoryExists(string path);

    /// <summary>Enumerates files under the given directory, optionally recursive, filtered by a search pattern.</summary>
    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive);

    /// <summary>Enumerates immediate subdirectories under the given directory, returning full paths.</summary>
    IEnumerable<string> EnumerateDirectories(string directoryPath);

    /// <summary>Creates the directory (and parents) at the given path.</summary>
    void CreateDirectory(string directoryPath);

    /// <summary>Deletes the file at the given path. Ignored if the file does not exist.</summary>
    void Delete(string path);

    /// <summary>Recursively deletes the directory and all its contents. Ignored if the directory does not exist.</summary>
    void DeleteDirectory(string directoryPath);

    /// <summary>Copies a file to the destination path, honoring the overwrite flag.</summary>
    void Copy(string sourcePath, string destinationPath, bool overwrite);

    /// <summary>Renames or moves a file or directory from the source to the destination path.</summary>
    void Rename(string sourcePath, string destinationPath);
}
