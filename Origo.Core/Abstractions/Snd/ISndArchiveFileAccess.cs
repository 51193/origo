using Origo.Core.DataSource;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Save-internal file read/write access. All file I/O is unified through
///     the <see cref="IDataSourceIoGateway" /> boundary, with paths relative to
///     the save's active extra/ subdirectory. Files written by strategies follow
///     the save lifecycle: included in snapshots, restored on load, cleaned on dispose.
///     Suffix routing (.json / .map etc.) is handled automatically by the gateway.
/// </summary>
public interface ISndArchiveFileAccess
{
    /// <summary>Read a file from the save's extra/ subdirectory and parse it as a DataSourceNode tree.</summary>
    DataSourceNode ReadFile(string relativePath);

    /// <summary>Serialize a DataSourceNode tree to a file in the save's extra/ subdirectory.</summary>
    void WriteFile(string relativePath, DataSourceNode node, bool overwrite = true);

    /// <summary>Check whether a file exists in the save's extra/ subdirectory.</summary>
    bool FileExists(string relativePath);

    /// <summary>Read a file from the save and deserialize it as a strongly-typed object via registered converters.</summary>
    T ReadObject<T>(string relativePath);

    /// <summary>Serialize a strongly-typed object to a file in the save via registered converters.</summary>
    void WriteObject<T>(string relativePath, T value, bool overwrite = true);

    /// <summary>Delete a file from the save's extra/ subdirectory.</summary>
    void DeleteFile(string relativePath);
}
