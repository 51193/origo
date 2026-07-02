using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;

namespace Origo.Core.Snd;

internal sealed class SndContextFileAccess(
    IDataSourceIoGateway dataSourceIo,
    IFileMetaAccess metaAccess,
    DataSourceConverterRegistry converterRegistry) : ISndFileAccess
{
    public DataSourceNode ReadFile(string path) => dataSourceIo.ReadTree(path);

    public void WriteFile(string path, DataSourceNode node, bool overwrite) =>
        dataSourceIo.WriteTree(path, node, overwrite);

    public bool FileExists(string path) => metaAccess.FileExists(path);

    public T ReadObject<T>(string path)
    {
        var node = dataSourceIo.ReadTree(path);
        return converterRegistry.Read<T>(node);
    }

    public void WriteObject<T>(string path, T value, bool overwrite)
    {
        var node = converterRegistry.Write(value);
        dataSourceIo.WriteTree(path, node, overwrite);
    }
}
