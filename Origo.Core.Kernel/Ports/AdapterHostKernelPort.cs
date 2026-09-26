#pragma warning disable IDE0130 // Intentional: kernel ports are required to live in the Origo.Core.Kernel.Ports namespace.
using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Kernel.Ports;

/// <summary>
///     Kernel entry point used by the Godot adapter shell to create its runtime,
///     scene-host observer topology, and SND context over a Godot scene host.
///     The port never appears in an adapter shell public signature.
/// </summary>
/// <remarks>
///     Reason: the adapter shell owns real Godot <c>Node</c> entry types but must
///     not construct kernel orchestration types itself, or it would become a
///     second startup path that drifts from <see cref="HostKernelPort" />.
///     Removal condition: remove when scene-host creation can move behind stable
///     contracts without an adapter-specific construction port.
/// </remarks>
internal static class AdapterHostKernelPort
{
    /// <summary>
    ///     Creates the adapter runtime over <paramref name="sceneHost" /> and binds
    ///     the per-scene observer topology through the existing kernel wiring path.
    /// </summary>
    internal static AdapterRuntimeBundle CreateRuntime(
        OrigoHostOptions options,
        ISndSceneHost sceneHost,
        string systemBlackboardPath,
        Action<TypeStringMapping, DataSourceConverterRegistry>? configureRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sceneHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemBlackboardPath);

        var fileSystem = options.FileSystem
            ?? throw new InvalidOperationException(
                $"{nameof(OrigoHostOptions)}.{nameof(OrigoHostOptions.FileSystem)} must be supplied for the Godot adapter host.");

        var typeMapping = new TypeStringMapping();
        var converterRegistry = DataSourceFactory.CreateDefaultRegistry(typeMapping);
        configureRegistry?.Invoke(typeMapping, converterRegistry);

        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem, logger: options.Logger);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        var pathResolver = DataSourceFactory.CreatePathResolver(fileSystem);

        var systemBlackboard = new PersistentBlackboard(
            metaAccess,
            pathResolver,
            systemBlackboardPath,
            dataSourceIo,
            converterRegistry,
            new Blackboard.Blackboard());
        systemBlackboard.LoadFromDisk();

        var consoleInput = options.ConsoleInput ?? new ConsoleInputBuffer();
        var consoleOutputChannel = options.ConsoleOutputChannel ?? new ConsoleOutputChannel();

        var runtime = new OrigoRuntime(
            options.Meta,
            options.Logger,
            sceneHost,
            typeMapping,
            converterRegistry,
            dataSourceIo,
            systemBlackboard,
            consoleInput,
            consoleOutputChannel);

        if (sceneHost is not ISndSceneHostRuntimeBinder runtimeBinder)
        {
            throw new InvalidOperationException(
                $"Adapter scene host '{sceneHost.GetType().FullName}' must implement " +
                $"{nameof(ISndSceneHostRuntimeBinder)} so the kernel can bind the runtime and observer topology.");
        }

        runtimeBinder.BindRuntimeDependencies(runtime.SndWorld, runtime.Logger);

        return new AdapterRuntimeBundle(
            runtime,
            sceneHost,
            dataSourceIo,
            metaAccess,
            pathResolver,
            typeMapping,
            converterRegistry,
            consoleInput,
            consoleOutputChannel);
    }

    /// <summary>
    ///     Creates the SND context for an adapter runtime and binds it to the scene
    ///     host through the existing kernel context-binding contract.
    /// </summary>
    internal static ISndContext CreateContext(AdapterRuntimeBundle bundle, AdapterContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(options);

        var context = new SndContext(new SndContextParameters(
            bundle.Runtime,
            bundle.DataSourceIo,
            bundle.MetaAccess,
            bundle.PathResolver,
            options.SaveRootPath,
            options.InitialSaveRootPath,
            options.EntryConfigPath)
        {
            AutoDiscoverStrategies = options.AutoDiscoverStrategies,
            DiscoverySkipPrefixes = options.DiscoverySkipPrefixes,
            SceneAliasMapPath = options.SceneAliasMapPath,
            SndTemplateMapPath = options.SndTemplateMapPath,
            ConfigureConverters = options.ConfigureConverters,
        });

        if (bundle.SceneHost is not ISndContextAttachableSceneHost attachableSceneHost)
        {
            throw new InvalidOperationException(
                $"Adapter scene host '{bundle.SceneHost.GetType().FullName}' must implement " +
                $"{nameof(ISndContextAttachableSceneHost)} so the kernel can bind the SND context.");
        }

        attachableSceneHost.BindContext(context);
        return context;
    }
}

/// <summary>
///     Scene-host contract used by the adapter host port to bind the runtime and
///     create the per-scene observer topology.
/// </summary>
internal interface ISndSceneHostRuntimeBinder
{
    /// <summary>Binds the runtime world and logger and creates the observer topology.</summary>
    void BindRuntimeDependencies(SndWorld world, ILogger logger);
}

/// <summary>Kernel services created for one adapter host instance.</summary>
internal sealed record AdapterRuntimeBundle(
    OrigoRuntime Runtime,
    ISndSceneHost SceneHost,
    IDataSourceIoGateway DataSourceIo,
    IFileMetaAccess MetaAccess,
    IPathResolver PathResolver,
    TypeStringMapping TypeMapping,
    DataSourceConverterRegistry ConverterRegistry,
    IConsoleInputSource ConsoleInput,
    IConsoleOutputChannel ConsoleOutputChannel);

/// <summary>Stable shell inputs needed to create an adapter SND context.</summary>
internal sealed record AdapterContextOptions(
    string SaveRootPath,
    string InitialSaveRootPath,
    string EntryConfigPath,
    bool AutoDiscoverStrategies = true,
    IReadOnlyList<string>? DiscoverySkipPrefixes = null,
    string? SceneAliasMapPath = null,
    string? SndTemplateMapPath = null,
    Action<DataSourceConverterRegistry>? ConfigureConverters = null);

#pragma warning restore IDE0130
