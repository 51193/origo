using Origo.Core.DataSource;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供带解析能力的文件访问接口。所有文件内容读写统一通过
///     <see cref="IDataSourceIoGateway" /> 边界，策略无需自行处理原始文本解析。
///     后缀路由（.json / .map 等）由 Gateway 自动识别。
/// </summary>
public interface ISndFileAccess
{
    /// <summary>读取文件并解析为 DataSourceNode 树，后缀自动路由 codec。</summary>
    DataSourceNode ReadFile(string path);

    /// <summary>将 DataSourceNode 树序列化写入文件。</summary>
    void WriteFile(string path, DataSourceNode node, bool overwrite = true);

    /// <summary>检查文件是否存在。</summary>
    bool FileExists(string path);

    /// <summary>读取文件并通过已注册的 Converter 反序列化为强类型对象。</summary>
    T ReadObject<T>(string path);

    /// <summary>将强类型对象通过已注册的 Converter 序列化后写入文件。</summary>
    void WriteObject<T>(string path, T value, bool overwrite = true);
}
