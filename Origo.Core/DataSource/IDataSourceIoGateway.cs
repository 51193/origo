namespace Origo.Core.DataSource;

/// <summary>
///     DataSource 文件 I/O 中间层：统一处理文件读写、codec 路由和 DataSourceNode 树编解码。
/// </summary>
public interface IDataSourceIoGateway
{
    bool Exists(string filePath);
    DataSourceNode ReadTree(string filePath);
    void WriteTree(string filePath, DataSourceNode node, bool overwrite = true);
}
