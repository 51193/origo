#pragma warning disable IDE0130 // Intentional: kernel ports are required to live in the Origo.Core.Kernel.Ports namespace.
using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Snd;

namespace Origo.Core.Kernel.Ports;

/// <summary>
///     Single construction path for the SND context parameters both host ports
///     build. The Core shell host and the Godot adapter host own different
///     option records but project them onto the same context values, so the
///     projection lives here: a context option is wired once, and the adapter
///     path cannot silently drift from the Core path.
/// </summary>
/// <remarks>
///     The two shells keep separate option records on purpose: the adapter
///     record carries Godot-specific startup configuration. This factory is the
///     single point where either record becomes the context values the kernel
///     consumes.
/// </remarks>
internal static class HostContextParameters
{
    /// <summary>Creates the parameters for the Core shell host.</summary>
    /// <param name="runtime">Runtime the context drives.</param>
    /// <param name="dataSourceIo">Data-source I/O gateway for the context.</param>
    /// <param name="metaAccess">File metadata access for the save system.</param>
    /// <param name="pathResolver">Path resolver for save paths.</param>
    /// <param name="options">Core host options to project.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is null.</exception>
    internal static SndContextParameters Create(
        OrigoRuntime runtime,
        IDataSourceIoGateway dataSourceIo,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        OrigoHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Create(
            runtime,
            dataSourceIo,
            metaAccess,
            pathResolver,
            options.SaveRootPath,
            options.InitialSaveRootPath,
            options.EntryConfigPath,
            options.AutoDiscoverStrategies,
            options.DiscoverySkipPrefixes,
            options.SceneAliasMapPath,
            options.SndTemplateMapPath,
            options.ConfigureConverters);
    }

    /// <summary>Creates the parameters for the Godot adapter shell host.</summary>
    /// <param name="runtime">Runtime the context drives.</param>
    /// <param name="dataSourceIo">Data-source I/O gateway for the context.</param>
    /// <param name="metaAccess">File metadata access for the save system.</param>
    /// <param name="pathResolver">Path resolver for save paths.</param>
    /// <param name="options">Adapter context options to project.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is null.</exception>
    internal static SndContextParameters Create(
        OrigoRuntime runtime,
        IDataSourceIoGateway dataSourceIo,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        AdapterContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Create(
            runtime,
            dataSourceIo,
            metaAccess,
            pathResolver,
            options.SaveRootPath,
            options.InitialSaveRootPath,
            options.EntryConfigPath,
            options.AutoDiscoverStrategies,
            options.DiscoverySkipPrefixes,
            options.SceneAliasMapPath,
            options.SndTemplateMapPath,
            options.ConfigureConverters);
    }

    private static SndContextParameters Create(
        OrigoRuntime runtime,
        IDataSourceIoGateway dataSourceIo,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        string saveRootPath,
        string initialSaveRootPath,
        string entryConfigPath,
        bool autoDiscoverStrategies,
        IReadOnlyList<string>? discoverySkipPrefixes,
        string? sceneAliasMapPath,
        string? sndTemplateMapPath,
        Action<DataSourceConverterRegistry>? configureConverters) =>
        new(runtime, dataSourceIo, metaAccess, pathResolver, saveRootPath, initialSaveRootPath, entryConfigPath)
        {
            AutoDiscoverStrategies = autoDiscoverStrategies,
            DiscoverySkipPrefixes = discoverySkipPrefixes,
            SceneAliasMapPath = sceneAliasMapPath,
            SndTemplateMapPath = sndTemplateMapPath,
            ConfigureConverters = configureConverters,
        };
}
