using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Abstractions.Runtime;

/// <summary>
///     Stable consumer access to the SND world: strategy registration,
///     type/data-source mapping, and metadata conversion. The concrete
///     world implementation remains in the kernel package.
/// </summary>
public interface ISndWorldAccess
{
    /// <summary>Converter registry used for typed data-source serialization.</summary>
    DataSourceConverterRegistry ConverterRegistry { get; }

    /// <summary>Data-source I/O gateway used by this world.</summary>
    IDataSourceIoGateway DataSourceIo { get; }

    /// <summary>Returns all registered strategy indices.</summary>
    IReadOnlyCollection<string> GetRegisteredStrategyIndices();

    /// <summary>Returns whether a strategy index is registered.</summary>
    bool IsStrategyRegistered(string index);

    /// <summary>Registers a strategy factory under its declared index.</summary>
    void RegisterStrategy<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseStrategy;

    /// <summary>Registers additional stable type-name mappings.</summary>
    void RegisterTypeMappings(Action<TypeStringMapping> registerMappings);

    /// <summary>Creates a deep clone of entity metadata.</summary>
    SndMetaData CloneMetaData(SndMetaData meta);

    /// <summary>Resolves a registered template alias into metadata.</summary>
    SndMetaData ResolveTemplate(string templateAlias);

    /// <summary>Reads a single entity metadata node.</summary>
    SndMetaData ReadMetaNode(DataSourceNode node);

    /// <summary>Reads a metadata list node.</summary>
    IReadOnlyList<SndMetaData> ReadMetaListNode(DataSourceNode node);

    /// <summary>Writes entity metadata to a data-source node.</summary>
    DataSourceNode WriteMetaNode(SndMetaData meta);

    /// <summary>Writes a metadata list to a data-source node.</summary>
    DataSourceNode WriteMetaListNode(IEnumerable<SndMetaData> metaDataList);

    /// <summary>Reads a typed-data map from a data-source node.</summary>
    IReadOnlyDictionary<string, TypedData> ReadTypedDataMap(DataSourceNode node);
}
