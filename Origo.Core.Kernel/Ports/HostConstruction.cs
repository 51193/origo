#pragma warning disable IDE0130 // Intentional: kernel ports are required to live in the Origo.Core.Kernel.Ports namespace.
using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Origo.Core.Snd;

namespace Origo.Core.Kernel.Ports;

/// <summary>
///     Shared kernel construction steps used by the Core shell port and the
///     Godot adapter port. Each port keeps its own entry point and failure
///     contract; this type only removes duplicate service, blackboard, and
///     runtime/context construction so startup wiring cannot drift.
/// </summary>
internal static class HostConstruction
{
    /// <summary>
    ///     Creates the type mapping, converter registry, I/O gateway, metadata
    ///     access, and path resolver from the host options and file system.
    ///     The startup registry callback runs after the default registry is
    ///     created and before any persisted data is read, matching both ports.
    /// </summary>
    internal static HostConstructionServices CreateServices(
        OrigoHostOptions options,
        IFileSystem fileSystem,
        Action<TypeStringMapping, DataSourceConverterRegistry>? configureRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);

        var typeMapping = new TypeStringMapping();
        var converterRegistry = DataSourceFactory.CreateDefaultRegistry(typeMapping);
        configureRegistry?.Invoke(typeMapping, converterRegistry);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem, logger: options.Logger);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        var pathResolver = DataSourceFactory.CreatePathResolver(fileSystem);
        return new HostConstructionServices(
            typeMapping,
            converterRegistry,
            dataSourceIo,
            metaAccess,
            pathResolver);
    }

    /// <summary>
    ///     Creates the system blackboard used by the runtime. When
    ///     <paramref name="persistenceFileSystem" /> is null the Core host's
    ///     in-memory fallback is used; otherwise the blackboard is loaded from
    ///     <paramref name="systemBlackboardPath" /> exactly once before runtime
    ///     construction.
    /// </summary>
    internal static IBlackboard CreateSystemBlackboard(
        IFileSystem? persistenceFileSystem,
        HostConstructionServices services,
        string? systemBlackboardPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (persistenceFileSystem is null)
            return new Origo.Core.Blackboard.Blackboard();

        var persistentBlackboard = new PersistentBlackboard(
            services.MetaAccess,
            services.PathResolver,
            systemBlackboardPath!,
            services.DataSourceIo,
            services.ConverterRegistry,
            new Origo.Core.Blackboard.Blackboard());
        persistentBlackboard.LoadFromDisk();
        return persistentBlackboard;
    }

    /// <summary>Creates the runtime over the port-specific scene host and services.</summary>
    internal static OrigoRuntime CreateRuntime(
        OrigoHostOptions options,
        ISndSceneHost sceneHost,
        HostConstructionServices services,
        IBlackboard systemBlackboard,
        IConsoleInputSource? consoleInput,
        IConsoleOutputChannel? consoleOutputChannel)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sceneHost);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(systemBlackboard);

        return new OrigoRuntime(
            options.Meta,
            options.Logger,
            sceneHost,
            services.TypeMapping,
            services.ConverterRegistry,
            services.DataSourceIo,
            systemBlackboard,
            consoleInput,
            consoleOutputChannel);
    }

    /// <summary>Creates the SND context over services constructed by this helper.</summary>
    internal static SndContext CreateContext(
        OrigoRuntime runtime,
        SndContextParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(parameters);
        return new SndContext(parameters);
    }
}

/// <summary>Kernel services created once per host construction.</summary>
internal sealed record HostConstructionServices(
    TypeStringMapping TypeMapping,
    DataSourceConverterRegistry ConverterRegistry,
    IDataSourceIoGateway DataSourceIo,
    IFileMetaAccess MetaAccess,
    IPathResolver PathResolver);

#pragma warning restore IDE0130
