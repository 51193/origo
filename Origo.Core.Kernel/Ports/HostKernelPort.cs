#pragma warning disable IDE0130 // Intentional: kernel ports are required to live in the Origo.Core.Kernel.Ports namespace.
using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Runtime;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Kernel.Ports;

/// <summary>
///     Kernel entry point used by the Core shell facade to create a complete
///     runtime and SND context. The port never appears in a shell public
///     signature.
/// </summary>
/// <remarks>
///     Reason: the Core shell assembly needs a construction path for the
///     concrete runtime/SND context while kernel compile assets stay out of
///     the consumer graph.
///     Removal condition: remove when the shell can construct an equivalent
///     host directly through stable contracts, or when host construction moves
///     to a separate composition package that both shell and adapter reference.
/// </remarks>
internal static class HostKernelPort
{
    internal static OrigoHostBundle Create(OrigoHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CoreHostKernelPort().Create(options);
    }
}

/// <summary>Internal kernel port contract for shell host construction.</summary>
internal interface IHostKernelPort
{
    OrigoHostBundle Create(OrigoHostOptions options);
}

/// <summary>
///     Result of host construction: stable shell interfaces plus the internal
///     SND context used by the facade.
/// </summary>
internal sealed record OrigoHostBundle(IOrigoRuntime Runtime, ISndContext Context);

/// <summary>Default host construction port.</summary>
internal sealed class CoreHostKernelPort : IHostKernelPort
{
    public OrigoHostBundle Create(OrigoHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var fileSystem = options.FileSystem ?? HostUnavailableFileSystem.Instance;
        var typeMapping = new TypeStringMapping();
        var converterRegistry = DataSourceFactory.CreateDefaultRegistry(typeMapping);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem, logger: options.Logger);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        var pathResolver = DataSourceFactory.CreatePathResolver(fileSystem);
        var sceneHost = new FullMemorySndSceneHost(options.Logger);

        var runtime = new OrigoRuntime(
            options.Meta,
            options.Logger,
            sceneHost,
            typeMapping,
            converterRegistry,
            dataSourceIo,
            new Blackboard.Blackboard(),
            options.ConsoleInput,
            options.ConsoleOutputChannel);
        sceneHost.BindWorld(runtime.SndWorld);

        var context = new SndContext(new SndContextParameters(
            runtime,
            dataSourceIo,
            metaAccess,
            pathResolver,
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
        sceneHost.BindContext(context);

        return new OrigoHostBundle(runtime, context);
    }
}

/// <summary>
///     Fail-fast placeholder used when a host is created without a file
///     system. Startup that never touches files works; any file access throws
///     with an actionable message instead of silently ignoring I/O.
/// </summary>
internal sealed class HostUnavailableFileSystem : IFileSystem
{
    internal static HostUnavailableFileSystem Instance { get; } = new();

    private HostUnavailableFileSystem()
    {
    }

    public bool Exists(string path) => throw Unavailable();

    public bool DirectoryExists(string path) => throw Unavailable();

    public string ReadAllText(string path) => throw Unavailable();

    public void WriteAllText(string path, string content, bool overwrite) => throw Unavailable();

    public void Copy(string sourcePath, string destinationPath, bool overwrite) => throw Unavailable();

    public System.Collections.Generic.IEnumerable<string> EnumerateFiles(
        string directoryPath,
        string searchPattern,
        bool recursive) => throw Unavailable();

    public void CreateDirectory(string directoryPath) => throw Unavailable();

    public void Delete(string path) => throw Unavailable();

    public string CombinePath(string basePath, string relativePath) => throw Unavailable();

    public string GetParentDirectory(string path) => throw Unavailable();

    public System.Collections.Generic.IEnumerable<string> EnumerateDirectories(string directoryPath) => throw Unavailable();

    public void Rename(string sourcePath, string destinationPath) => throw Unavailable();

    public void DeleteDirectory(string directoryPath) => throw Unavailable();

    private static InvalidOperationException Unavailable() =>
        new("OrigoHostOptions.FileSystem must be supplied before using file or save operations.");
}

#pragma warning restore IDE0130
