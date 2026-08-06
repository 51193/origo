using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd;

/// <summary>
///     Unified SND entry point for upper-layer game code,
///     encapsulating the strategy pool, serialization configuration, and mappings.
/// </summary>
public sealed class SndWorld
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Creates a world with the given type mapping, logger, converter
    ///     registry, and data-source I/O gateway.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public SndWorld(
        TypeStringMapping typeMapping,
        ILogger logger,
        DataSourceConverterRegistry registry,
        IDataSourceIoGateway dataSourceIo)
    {
        ArgumentNullException.ThrowIfNull(typeMapping);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        _logger = logger;
        StrategyPool = new SndStrategyPool(logger);
        TypeMapping = typeMapping;
        ConverterRegistry = registry;
        DataSourceIo = dataSourceIo;
        Mappings = new SndMappings();
    }

    /// <summary>
    ///     Strategy object pool that manages creation, sharing, and reference counting
    ///     of all registered strategies.
    /// </summary>
    internal SndStrategyPool StrategyPool { get; }

    /// <summary>
    ///     Bidirectional mapping between type names and .NET types, used for TypedData serialization.
    ///     The adapter layer can register engine-specific types at startup.
    /// </summary>
    internal TypeStringMapping TypeMapping { get; }

    /// <summary>
    ///     Data source converter registry responsible for bidirectional conversion
    ///     between DataSourceNode and strongly-typed C# objects.
    /// </summary>
    public DataSourceConverterRegistry ConverterRegistry { get; }

    /// <summary>Data-source I/O gateway used for all file content reads and writes.</summary>
    public IDataSourceIoGateway DataSourceIo { get; }

    /// <summary>
    ///     SND mapping manager that maintains the mapping relationships
    ///     for scene aliases and template aliases.
    /// </summary>
    internal SndMappings Mappings { get; }

    /// <summary>
    ///     Registers a strategy type with the strategy pool, applying the
    ///     statelessness validation performed at registration time.
    /// </summary>
    public void RegisterStrategy<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseStrategy =>
        StrategyPool.Register(factory);

    /// <summary>
    ///     Checks whether the strategy with the specified index is registered.
    /// </summary>
    public bool IsStrategyRegistered(string index)
    {
        if (string.IsNullOrWhiteSpace(index))
            throw new ArgumentException("Strategy index cannot be null or whitespace.", nameof(index));
        return StrategyPool.IsRegistered(index);
    }

    /// <summary>
    ///     Gets a read-only collection of all registered strategy indices.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredStrategyIndices() =>
        StrategyPool.EnumerateRegisteredIndices();

    /// <summary>
    ///     Registers type-name mappings used for TypedData serialization,
    ///     via a callback that mutates the world's <see cref="TypeMapping" />.
    /// </summary>
    public void RegisterTypeMappings(Action<TypeStringMapping> registerMappings)
    {
        ArgumentNullException.ThrowIfNull(registerMappings);
        registerMappings(TypeMapping);
    }

    /// <summary>
    ///     Resolves and loads an SndMetaData template by alias. Returns a
    ///     deep clone so callers may freely mutate the returned metadata
    ///     without polluting the template cache.
    /// </summary>
    public SndMetaData ResolveTemplate(string alias) => CloneMetaData(Mappings.ResolveTemplate(alias));

    /// <summary>
    ///     Creates a deep copy of SND metadata. Used by template resolution
    ///     so callers can mutate the returned metadata freely.
    /// </summary>
    public static SndMetaData CloneMetaData(SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        return meta.DeepClone();
    }

    /// <summary>Loads scene alias mappings from the given map file.</summary>
    public void LoadSceneAliases(string mapFilePath, ILogger logger) =>
        Mappings.LoadSceneAliases(DataSourceIo, mapFilePath, logger);

    /// <summary>Loads SND template mappings from the given map file.</summary>
    public void LoadTemplates(string mapFilePath, ILogger logger)
    {
        Mappings.LoadTemplates(
            DataSourceIo,
            mapFilePath,
            ConverterRegistry,
            logger);
    }

    /// <summary>
    ///     Resolves a JSON array node into a list of entity metadata, applying
    ///     template resolution and type conversion.
    /// </summary>
    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(DataSourceNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Mappings.ResolveMetaListFromJsonArray(root, ConverterRegistry);
    }

    /// <summary>Reads a typed data map (key → <see cref="TypedData" />) from a data source node.</summary>
    public IReadOnlyDictionary<string, TypedData> ReadTypedDataMap(DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ConverterRegistry.Read<IReadOnlyDictionary<string, TypedData>>(node);
    }

    internal SndEntity CreateEntity(
        INodeFactory nodeFactory,
        ISndContext context,
        ILogger logger,
        ObserverTopology observerTopology)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return new SndEntity(nodeFactory, StrategyPool, Mappings.ResolveSceneAlias, context, logger, observerTopology);
    }

    /// <summary>Serializes a single entity metadata into a data source node.</summary>
    public DataSourceNode WriteMetaNode(SndMetaData metaData) => ConverterRegistry.Write(metaData);

    /// <summary>Deserializes a data source node into a single entity metadata.</summary>
    public SndMetaData ReadMetaNode(DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ConverterRegistry.Read<SndMetaData>(node);
    }

    /// <summary>Serializes a list of entity metadata into a data source node.</summary>
    public DataSourceNode WriteMetaListNode(IEnumerable<SndMetaData> metaDataList)
    {
        var list = metaDataList as IReadOnlyList<SndMetaData> ?? [.. metaDataList];
        return ConverterRegistry.Write(list);
    }

    /// <summary>Deserializes a data source node into a list of entity metadata.</summary>
    public IReadOnlyList<SndMetaData> ReadMetaListNode(DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ConverterRegistry.Read<IReadOnlyList<SndMetaData>>(node);
    }
}
