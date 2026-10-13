using System;
using Origo.Core;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Kernel.Ports;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Pins the option projection both host ports share. Every context
///     parameter must come from its own option member, and every projected
///     value is distinct, so a transposed argument in the shared factory fails
///     here instead of surfacing as a misconfigured host at runtime.
/// </summary>
public class HostContextParametersTests
{
    [Fact]
    public void Create_ForOrigoHostOptions_MapsEveryContextParameter()
    {
        var (runtime, dataSourceIo, metaAccess, pathResolver) = CreateInfrastructure();
        var skipPrefixes = new[] { "Core.Skip", "Origo.Skip" };
        Action<DataSourceConverterRegistry> configureConverters = _ => { };
        var options = new OrigoHostOptions
        {
            Meta = new OrigoMeta("Tests", "1.0.0", "test"),
            SaveRootPath = "core_saves",
            InitialSaveRootPath = "core_initial",
            EntryConfigPath = "core_entry.json",
            AutoDiscoverStrategies = false,
            DiscoverySkipPrefixes = skipPrefixes,
            SceneAliasMapPath = "core_scene_alias.json",
            SndTemplateMapPath = "core_templates.json",
            ConfigureConverters = configureConverters,
        };

        var parameters = HostContextParameters.Create(runtime, dataSourceIo, metaAccess, pathResolver, options);

        Assert.Same(runtime, parameters.Runtime);
        Assert.Same(dataSourceIo, parameters.DataSourceIo);
        Assert.Same(metaAccess, parameters.MetaAccess);
        Assert.Same(pathResolver, parameters.PathResolver);
        Assert.Equal("core_saves", parameters.SaveRootPath);
        Assert.Equal("core_initial", parameters.InitialSaveRootPath);
        Assert.Equal("core_entry.json", parameters.EntryConfigPath);
        Assert.False(parameters.AutoDiscoverStrategies);
        Assert.Same(skipPrefixes, parameters.DiscoverySkipPrefixes);
        Assert.Equal("core_scene_alias.json", parameters.SceneAliasMapPath);
        Assert.Equal("core_templates.json", parameters.SndTemplateMapPath);
        Assert.Same(configureConverters, parameters.ConfigureConverters);
    }

    [Fact]
    public void Create_ForAdapterContextOptions_MapsEveryContextParameter()
    {
        var (runtime, dataSourceIo, metaAccess, pathResolver) = CreateInfrastructure();
        var skipPrefixes = new[] { "Godot", "GodotSharp" };
        Action<DataSourceConverterRegistry> configureConverters = _ => { };
        var options = new AdapterContextOptions(
            SaveRootPath: "adapter_saves",
            InitialSaveRootPath: "adapter_initial",
            EntryConfigPath: "adapter_entry.json",
            AutoDiscoverStrategies: false,
            DiscoverySkipPrefixes: skipPrefixes,
            SceneAliasMapPath: "adapter_scene_alias.json",
            SndTemplateMapPath: "adapter_templates.json",
            ConfigureConverters: configureConverters);

        var parameters = HostContextParameters.Create(runtime, dataSourceIo, metaAccess, pathResolver, options);

        Assert.Same(runtime, parameters.Runtime);
        Assert.Same(dataSourceIo, parameters.DataSourceIo);
        Assert.Same(metaAccess, parameters.MetaAccess);
        Assert.Same(pathResolver, parameters.PathResolver);
        Assert.Equal("adapter_saves", parameters.SaveRootPath);
        Assert.Equal("adapter_initial", parameters.InitialSaveRootPath);
        Assert.Equal("adapter_entry.json", parameters.EntryConfigPath);
        Assert.False(parameters.AutoDiscoverStrategies);
        Assert.Same(skipPrefixes, parameters.DiscoverySkipPrefixes);
        Assert.Equal("adapter_scene_alias.json", parameters.SceneAliasMapPath);
        Assert.Equal("adapter_templates.json", parameters.SndTemplateMapPath);
        Assert.Same(configureConverters, parameters.ConfigureConverters);
    }

    [Fact]
    public void Create_WithoutOptions_Throws()
    {
        var (runtime, dataSourceIo, metaAccess, pathResolver) = CreateInfrastructure();

        Assert.Throws<ArgumentNullException>(() =>
            HostContextParameters.Create(runtime, dataSourceIo, metaAccess, pathResolver, (OrigoHostOptions)null!));
        Assert.Throws<ArgumentNullException>(() =>
            HostContextParameters.Create(runtime, dataSourceIo, metaAccess, pathResolver, (AdapterContextOptions)null!));
    }

    private static (OrigoRuntime Runtime, IDataSourceIoGateway DataSourceIo, IFileMetaAccess MetaAccess,
        IPathResolver PathResolver) CreateInfrastructure()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fileSystem = new MemoryFileSystem();
        return (
            runtime,
            DataSourceFactory.CreateDefaultIoGateway(fileSystem, logger: logger),
            DataSourceFactory.CreateFileMetaAccess(fileSystem),
            DataSourceFactory.CreatePathResolver(fileSystem));
    }
}
