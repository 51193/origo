using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Godot;
using Origo.Core;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Logging;
using Origo.GodotAdapter.Serialization;
using Origo.GodotAdapter.Snd;
using Origo.GodotAdapter;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     The sole startup entry point node for self-built Runtime and SndManager.
/// </summary>
[GlobalClass]
public partial class OrigoAutoHost : Node
{
    private const string _logTag = nameof(OrigoAutoHost);
    private bool _readyFailed;

    [Export] public string SystemBlackboardSaveRoot { get; set; } = "user://origo_saves";
    public GodotSndManager SndManager { get; private set; } = null!;

    /// <summary>
    ///     Console command input queue; the UI delivers submitted lines via <see cref="IConsoleInputSource.Enqueue" />.
    /// </summary>
    public IConsoleInputSource? ConsoleInput { get; private set; }

    /// <summary>
    ///     Console output publishing channel; external consumers (ConsoleBridge, UI)
    ///     subscribe via Subscribe to receive output.
    /// </summary>
    public IConsoleOutputChannel? ConsoleOutputChannel { get; private set; }

    /// <summary>
    ///     File metadata access interface (same source as the current runtime).
    /// </summary>
    protected IFileMetaAccess SharedMetaAccess { get; private set; } = null!;

    /// <summary>
    ///     Path resolution interface (same source as the current runtime).
    /// </summary>
    protected IPathResolver SharedPathResolver { get; private set; } = null!;

    /// <summary>
    ///     DataSource I/O gateway (same source as the current runtime).
    /// </summary>
    protected IDataSourceIoGateway SharedDataSourceIo { get; private set; } = null!;

    public OrigoRuntime Runtime { get; private set; } = null!;

    public override void _Ready()
    {
        var readyWatch = Stopwatch.StartNew();
        var bootstrapLogger = CreateBootstrapLogger();
        bootstrapLogger.Log(LogLevel.Info, _logTag, new LogMessageBuilder().Build("_Ready begin."));
        try
        {
            Runtime = CreateRuntime();
            readyWatch.Stop();
            Runtime.Logger.Log(LogLevel.Info, _logTag,
                new LogMessageBuilder()
                    .SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .Build("_Ready completed."));
        }
        catch (Exception ex)
        {
            readyWatch.Stop();
            bootstrapLogger.Log(LogLevel.Error, _logTag,
                new LogMessageBuilder().SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .Build($"_Ready failed: {ex.Message}"));
            _readyFailed = true;
            throw;
        }
    }

    public override void _Process(double delta)
    {
        // A failed _Ready leaves the node alive in the scene tree; drive
        // frames explicitly fail instead of silently running without a
        // runtime (fail-fast).
        if (_readyFailed)
            throw new InvalidOperationException(
                "OrigoAutoHost bootstrap failed in _Ready; frame driving is disabled. " +
                "Fix the bootstrap error before running the scene.");
        ((IOrigoFrameDriver?)Runtime)?.DriveFrame(delta);
    }

    [MemberNotNull(nameof(SndManager), nameof(SharedMetaAccess), nameof(SharedPathResolver), nameof(SharedDataSourceIo))]
    private OrigoRuntime CreateRuntime()
    {
        var createWatch = Stopwatch.StartNew();
        var logger = CreateBootstrapLogger();
        logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder().Build("CreateRuntime begin."));

        var fileSystem = new GodotFileSystem();
        SharedDataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem);
        SharedMetaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        SharedPathResolver = DataSourceFactory.CreatePathResolver(fileSystem);

        var sndManager = CreateAndSetupSndManager(out var sharedTypeMapping,
            out var converterRegistry, out var persistentBb,
            out var consoleInput, out var consoleOutputChannel);

        var runtime = new OrigoRuntime(
            ResolveOrigoMeta(),
            logger,
            sndManager,
            sharedTypeMapping,
            converterRegistry,
            SharedDataSourceIo,
            persistentBb,
            consoleInput,
            consoleOutputChannel
        );
        sndManager.BindRuntimeDependencies(runtime.SndWorld, runtime.Logger);

        ConsoleInput = consoleInput;
        ConsoleOutputChannel = consoleOutputChannel;

        var systemBbPath = SharedPathResolver.CombinePath(SystemBlackboardSaveRoot, "system.json");
        createWatch.Stop();
        logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder()
                .SetElapsedMs(createWatch.Elapsed.TotalMilliseconds)
                .AddContext("filePath", systemBbPath)
                .Build("CreateRuntime completed."));
        return runtime;
    }

    [MemberNotNull(nameof(SndManager))]
    private GodotSndManager CreateAndSetupSndManager(
        out TypeStringMapping sharedTypeMapping,
        out DataSourceConverterRegistry converterRegistry,
        out PersistentBlackboard persistentBb,
        out IConsoleInputSource consoleInput,
        out ConsoleOutputChannel consoleOutputChannel)
    {
        var sndManager = new GodotSndManager();
        AddChild(sndManager);
        SndManager = sndManager;

        var systemBbPath = SharedPathResolver.CombinePath(SystemBlackboardSaveRoot, "system.json");
        sharedTypeMapping = new TypeStringMapping();
        GodotJsonConverterRegistry.RegisterTypeMappings(sharedTypeMapping);

        converterRegistry = DataSourceFactory.CreateDefaultRegistry(sharedTypeMapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(converterRegistry);

        persistentBb = new PersistentBlackboard(SharedMetaAccess, SharedPathResolver, systemBbPath, SharedDataSourceIo, converterRegistry,
            new Blackboard());
        persistentBb.LoadFromDisk();

        consoleInput = new ConsoleInputBuffer();
        consoleOutputChannel = new ConsoleOutputChannel();

        return sndManager;
    }

    private static GodotLogger CreateBootstrapLogger()
    {
        return new GodotLogger(static (level, tag, message) =>
        {
            switch (level)
            {
                case LogLevel.Warning:
                    GD.PushWarning($"[{tag}] {message}");
                    break;
                case LogLevel.Error:
                    GD.PushError($"[{tag}] {message}");
                    break;
                default:
                    GD.Print($"[{tag}] {message}");
                    break;
            }
        });
    }

    private static OrigoMeta ResolveOrigoMeta()
    {
        var version = typeof(OrigoRuntime).Assembly.GetName().Version?.ToString()
                      ?? "unknown";
        return new OrigoMeta("Origo", version, OrigoMeta.DefaultBanner);
    }
}
