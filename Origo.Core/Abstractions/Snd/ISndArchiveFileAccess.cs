using Origo.Core.DataSource;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供存档内文件读写接口。所有文件内容读写统一通过
///     <see cref="IDataSourceIoGateway" /> 边界，路径相对于存档活动目录下的 extra/ 子目录。
///     策略写入的文件随存档生命周期管理：写入后纳入 save snapshot，load 时自动恢复，dispose 时清理。
///     后缀路由（.json / .map 等）由 Gateway 自动识别。
/// </summary>
public interface ISndArchiveFileAccess
{
    /// <summary>从存档活动目录的 extra/ 子目录中读取文件并解析为 DataSourceNode 树。</summary>
    DataSourceNode ReadFile(string relativePath);

    /// <summary>将 DataSourceNode 树序列化写入存档活动目录的 extra/ 子目录。</summary>
    void WriteFile(string relativePath, DataSourceNode node, bool overwrite = true);

    /// <summary>检查存档活动目录的 extra/ 子目录中文件是否存在。</summary>
    bool FileExists(string relativePath);

    /// <summary>从存档内读取文件并通过已注册的 Converter 反序列化为强类型对象。</summary>
    T ReadObject<T>(string relativePath);

    /// <summary>将强类型对象序列化后写入存档内文件。</summary>
    void WriteObject<T>(string relativePath, T value, bool overwrite = true);

    /// <summary>删除存档活动目录的 extra/ 子目录中的文件。</summary>
    void DeleteFile(string relativePath);
}
