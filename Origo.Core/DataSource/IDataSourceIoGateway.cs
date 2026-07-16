namespace Origo.Core.DataSource;

/// <summary>
///     DataSource file I/O intermediate layer: uniformly handles file content reading and writing,
///     routing codecs by file suffix. All file content I/O is forced through codec routing — no direct
///     read/write backdoors. For file metadata operations (existence checks, directory management, etc.),
///     use <see cref="IFileMetaAccess" />.
/// </summary>
public interface IDataSourceIoGateway
{
    DataSourceNode ReadTree(string filePath);
    void WriteTree(string filePath, DataSourceNode node, bool overwrite = true);
}
