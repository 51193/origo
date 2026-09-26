using Origo.Core.DataSource;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     File access with parsing support. All file I/O is unified through the
///     <see cref="IDataSourceIoGateway" /> boundary so strategies never handle
///     raw text parsing directly. Suffix routing (.json / .map etc.) is
///     handled automatically by the gateway.
/// </summary>
public interface ISndFileAccess
{
    /// <summary>Read a file and parse it as a DataSourceNode tree. Suffix auto-routes the codec.</summary>
    DataSourceNode ReadFile(string path);

    /// <summary>Serialize a DataSourceNode tree to a file.</summary>
    void WriteFile(string path, DataSourceNode node, bool overwrite = true);

    /// <summary>Check whether a file exists.</summary>
    bool FileExists(string path);

    /// <summary>Read a file and deserialize it as a strongly-typed object via registered converters.</summary>
    T ReadObject<T>(string path);

    /// <summary>Serialize a strongly-typed object to a file via registered converters.</summary>
    void WriteObject<T>(string path, T value, bool overwrite = true);
}
