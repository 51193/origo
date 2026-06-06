using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd;

/// <summary>
///     面向上层游戏的 SND 统一入口，封装策略池、序列化配置与映射。
/// </summary>
public sealed class SndWorld
{
    private readonly ILogger _logger;

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
    ///     策略对象池，管理所有已注册策略的创建、共享与引用计数。
    /// </summary>
    internal SndStrategyPool StrategyPool { get; }

    /// <summary>
    ///     类型名与 .NET 类型之间的双向映射，用于 TypedData 的序列化。
    ///     适配层可在启动时注册引擎特有类型。
    /// </summary>
    internal TypeStringMapping TypeMapping { get; }

    /// <summary>
    ///     数据源转换器注册表，负责 DataSourceNode 与强类型 C# 对象之间的双向转换。
    /// </summary>
    public DataSourceConverterRegistry ConverterRegistry { get; }

    public IDataSourceIoGateway DataSourceIo { get; }

    /// <summary>
    ///     SND 映射管理器，维护场景别名与模板别名的映射关系。
    /// </summary>
    internal SndMappings Mappings { get; }

    public void RegisterStrategy<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseStrategy =>
        StrategyPool.Register(factory);

    /// <summary>
    ///     检查指定索引的策略是否已注册。
    /// </summary>
    public bool IsStrategyRegistered(string index)
    {
        if (string.IsNullOrWhiteSpace(index))
            throw new ArgumentException("Strategy index cannot be null or whitespace.", nameof(index));
        return StrategyPool.IsRegistered(index);
    }

    /// <summary>
    ///     获取所有已注册的策略索引集合（只读）。
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredStrategyIndices() =>
        StrategyPool.EnumerateRegisteredIndices();

    public void RegisterTypeMappings(Action<TypeStringMapping> registerMappings)
    {
        ArgumentNullException.ThrowIfNull(registerMappings);
        registerMappings(TypeMapping);
    }

    public SndMetaData ResolveTemplate(string alias) => Mappings.ResolveTemplate(alias);

    /// <summary>
    ///     克隆 SND 元数据（与模板解析路径一致，便于将来统一替换实现）。
    /// </summary>
    public static SndMetaData CloneMetaData(SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        return meta.DeepClone();
    }

    public void LoadSceneAliases(IFileSystem fileSystem, string mapFilePath, ILogger logger) =>
        Mappings.LoadSceneAliases(fileSystem, mapFilePath, logger);

    public void LoadTemplates(IFileSystem fileSystem, string mapFilePath, ILogger logger)
    {
        Mappings.LoadTemplates(
            fileSystem,
            mapFilePath,
            DataSourceFactory.CreateDefaultIoGateway(fileSystem),
            ConverterRegistry,
            logger);
    }

    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(DataSourceNode root) =>
        Mappings.ResolveMetaListFromJsonArray(root, ConverterRegistry);

    public IReadOnlyDictionary<string, TypedData> ReadTypedDataMap(DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ConverterRegistry.Read<IReadOnlyDictionary<string, TypedData>>(node);
    }

    public SndEntity CreateEntity(
        INodeFactory nodeFactory,
        ISndContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return new SndEntity(nodeFactory, StrategyPool, Mappings.ResolveSceneAlias, context, logger);
    }

    public DataSourceNode WriteMetaNode(SndMetaData metaData) => ConverterRegistry.Write(metaData);

    public SndMetaData ReadMetaNode(DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ConverterRegistry.Read<SndMetaData>(node);
    }

    public DataSourceNode WriteMetaListNode(IEnumerable<SndMetaData> metaDataList)
    {
        var list = metaDataList as IReadOnlyList<SndMetaData> ?? metaDataList.ToList();
        return ConverterRegistry.Write(list);
    }

    public IReadOnlyList<SndMetaData> ReadMetaListNode(DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ConverterRegistry.Read<IReadOnlyList<SndMetaData>>(node);
    }
}
