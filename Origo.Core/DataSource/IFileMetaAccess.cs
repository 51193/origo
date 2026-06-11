using System.Collections.Generic;

namespace Origo.Core.DataSource;

/// <summary>
///     文件元数据访问接口：提供文件/目录存在性检查、枚举、创建、删除、复制、重命名等
///     文件系统级别的元操作。与 <see cref="IDataSourceIoGateway" /> 并行使用，
///     前者负责内容读写（含 codec 路由），本接口负责元数据和文件系统结构操作。
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
