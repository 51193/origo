namespace Origo.Core.DataSource;

/// <summary>
///     DataSource 文件 I/O 中间层：统一处理文件内容读写，按文件后缀路由编解码器。
///     所有文件内容 I/O 强制走 codec 路由，无直读直写后门。
///     文件元数据操作（存在性检查、目录管理等）请使用 <see cref="IFileMetaAccess" />。
/// </summary>
public interface IDataSourceIoGateway
{
    DataSourceNode ReadTree(string filePath);
    void WriteTree(string filePath, DataSourceNode node, bool overwrite = true);
}
