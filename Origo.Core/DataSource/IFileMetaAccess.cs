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
    bool FileExists(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive);

    IEnumerable<string> EnumerateDirectories(string directoryPath);

    void CreateDirectory(string directoryPath);

    void Delete(string path);

    void DeleteDirectory(string directoryPath);

    void Copy(string sourcePath, string destinationPath, bool overwrite);

    void Rename(string sourcePath, string destinationPath);
}
