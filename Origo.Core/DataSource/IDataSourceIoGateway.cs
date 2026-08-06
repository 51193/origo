namespace Origo.Core.DataSource;

/// <summary>
///     DataSource file I/O intermediate layer: uniformly handles file content reading and writing,
///     routing codecs by file suffix. All file content I/O is forced through codec routing — no direct
///     read/write backdoors. For file metadata operations (existence checks, directory management, etc.),
///     use <see cref="IFileMetaAccess" />.
/// </summary>
public interface IDataSourceIoGateway
{
    /// <summary>Reads a file and decodes its content into a <see cref="DataSourceNode" /> using the codec matched by the file suffix.</summary>
    DataSourceNode ReadTree(string filePath);

    /// <summary>Encodes a <see cref="DataSourceNode" /> and writes it to the file using the codec matched by the file suffix.</summary>
    void WriteTree(string filePath, DataSourceNode node, bool overwrite = true);
}
