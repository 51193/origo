using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource.Converters;
using Origo.Core.DataSource.Codec;
using Origo.Core.Serialization;

namespace Origo.Core.DataSource;

/// <summary>
///     Factory that creates pre-configured <see cref="DataSourceConverterRegistry" /> and codec instances.
/// </summary>
public static class DataSourceFactory
{
    /// <summary>
    ///     Creates a converter registry pre-loaded with the default primitive,
    ///     array, and domain converters (TypedData, metadata, blackboard, etc.).
    /// </summary>
    public static DataSourceConverterRegistry CreateDefaultRegistry(TypeStringMapping typeMapping)
    {
        var registry = new DataSourceConverterRegistry();
        RegisterPrimitives(registry);
        RegisterPrimitiveArrays(registry);
        RegisterDomainConverters(registry, typeMapping);
        return registry;
    }

    private static void RegisterPrimitives(DataSourceConverterRegistry registry)
    {
        registry.Register(new StringDataSourceConverter());
        registry.Register(new ByteDataSourceConverter());
        registry.Register(new SByteDataSourceConverter());
        registry.Register(new Int16DataSourceConverter());
        registry.Register(new UInt16DataSourceConverter());
        registry.Register(new Int32DataSourceConverter());
        registry.Register(new UInt32DataSourceConverter());
        registry.Register(new Int64DataSourceConverter());
        registry.Register(new UInt64DataSourceConverter());
        registry.Register(new SingleDataSourceConverter());
        registry.Register(new DoubleDataSourceConverter());
        registry.Register(new DecimalDataSourceConverter());
        registry.Register(new CharDataSourceConverter());
        registry.Register(new BooleanDataSourceConverter());
    }

    private static void RegisterPrimitiveArrays(DataSourceConverterRegistry registry)
    {
        registry.Register(new ByteArrayDataSourceConverter());
        registry.Register(new SByteArrayDataSourceConverter());
        registry.Register(new Int16ArrayDataSourceConverter());
        registry.Register(new UInt16ArrayDataSourceConverter());
        registry.Register(new Int32ArrayDataSourceConverter());
        registry.Register(new UInt32ArrayDataSourceConverter());
        registry.Register(new Int64ArrayDataSourceConverter());
        registry.Register(new UInt64ArrayDataSourceConverter());
        registry.Register(new SingleArrayDataSourceConverter());
        registry.Register(new DoubleArrayDataSourceConverter());
        registry.Register(new DecimalArrayDataSourceConverter());
        registry.Register(new BooleanArrayDataSourceConverter());
        registry.Register(new CharArrayDataSourceConverter());
        registry.Register(new StringArrayDataSourceConverter());
    }

    private static void RegisterDomainConverters(DataSourceConverterRegistry registry, TypeStringMapping typeMapping)
    {
        var typedDataConverter = new TypedDataConverter(typeMapping, registry);
        registry.Register(typedDataConverter);

        var nodeMetaConverter = new NodeMetaDataConverter();
        var strategyMetaConverter = new StrategyMetaDataConverter();
        var dataMetaConverter = new DataMetaDataConverter(typedDataConverter);
        var sndMetaConverter = new SndMetaDataConverter(
            nodeMetaConverter, strategyMetaConverter, dataMetaConverter);

        registry.Register(nodeMetaConverter);
        registry.Register(strategyMetaConverter);
        registry.Register(dataMetaConverter);
        registry.Register(sndMetaConverter);

        registry.Register(new SndMetaDataListConverter(sndMetaConverter));
        registry.Register(new BlackboardDataConverter(typedDataConverter));
        registry.Register(new StringDictionaryConverter());
        registry.Register(new StateMachineContainerPayloadConverter());
    }

    private static DataSourceIoOptions BuildDefaultIoOptions()
    {
        return new DataSourceIoOptions()
            .RegisterSuffix(".json", DataSourceCodecKind.Json)
            .RegisterSuffix(".map", DataSourceCodecKind.Map)
            .RegisterSuffix(".sha", DataSourceCodecKind.RawString)
            .RegisterSuffix(".write_in_progress", DataSourceCodecKind.RawString);
    }

    internal static IReadOnlyDictionary<DataSourceCodecKind, IDataSourceCodec> BuildDefaultCodecs(bool writeIndented = true)
    {
        return new Dictionary<DataSourceCodecKind, IDataSourceCodec>
        {
            [DataSourceCodecKind.Json] = new JsonDataSourceCodec(writeIndented),
            [DataSourceCodecKind.Map] = new MapDataSourceCodec(),
            [DataSourceCodecKind.RawString] = new RawStringDataSourceCodec()
        };
    }

    internal static IDataSourceIoGateway CreateIoGateway(
        IFileSystem fileSystem,
        DataSourceIoOptions options,
        IReadOnlyDictionary<DataSourceCodecKind, IDataSourceCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(codecs);
        return new DataSourceIoGateway(fileSystem, options, codecs);
    }

    /// <summary>Creates an I/O gateway with the default codecs and options.</summary>
    public static IDataSourceIoGateway CreateDefaultIoGateway(IFileSystem fileSystem, bool writeIndented = true)
        => new DataSourceIoGateway(fileSystem, BuildDefaultIoOptions(), BuildDefaultCodecs(writeIndented));

    /// <summary>Creates a file metadata access facade over the given file system.</summary>
    public static IFileMetaAccess CreateFileMetaAccess(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return new FileMetaAccess(fileSystem);
    }

    /// <summary>Creates a path resolver that combines and normalizes paths on the given file system.</summary>
    public static IPathResolver CreatePathResolver(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return new PathResolver(fileSystem);
    }
}
