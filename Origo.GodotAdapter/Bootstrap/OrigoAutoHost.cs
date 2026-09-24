using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Godot;
using Origo.Core;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.DataSource;
using Origo.Core.Kernel.Ports;
using Origo.Core.Logging;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Snd;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Logging;
using Origo.GodotAdapter.Serialization;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     The sole startup entry point node for self-built Runtime and SndManager.
/// </summary>
[GlobalClass]
public partial class OrigoAutoHost : Node
{
    private const string _logTag = nameof(OrigoAutoHost);
    private bool _readyFailed;
    private AdapterRuntimeBundle? _runtimeBundle;

    /// <summary>Root directory for the system blackboard save file.</summary>
    [Export] public string SystemBlackboardSaveRoot { get; set; } = "user://origo_saves";

    /// <summary>The Godot scene host created and wired during <see cref="_Ready" />.</summary>
    internal GodotSndManager SndManager { get; private set; } = null!;

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

    /// <summary>The stable Origo runtime surface created during <see cref="_Ready" />.</summary>
    public IOrigoRuntime Runtime { get; private set; } = null!;

    /// <summary>Kernel services backing <see cref="Runtime" />, scoped to the derived entry bootstrap.</summary>
    private protected AdapterRuntimeBundle RuntimeBundle => _runtimeBundle
        ?? throw new InvalidOperationException("The adapter runtime bundle is unavailable before _Ready completes.");

    /// <summary>
    ///     Marks the bootstrap as failed so <see cref="_Process" /> fails fast
    ///     instead of driving a partially initialized runtime. Derived entry
    ///     classes call this when any step after the base runtime creation
    ///     throws.
    /// </summary>
    protected void MarkBootstrapFailed() => _readyFailed = true;

    /// <summary>Godot lifecycle entry: builds the runtime and SndManager, or records and rethrows the failure.</summary>
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
            MarkBootstrapFailed();
            throw;
        }
    }

    /// <summary>Godot frame callback: drives the Core frame pipeline, failing fast when bootstrap failed.</summary>
    public override void _Process(double delta)
    {
        // A failed _Ready leaves the node alive in the scene tree; drive
        // frames explicitly fail instead of silently running without a
        // runtime (fail-fast).
        if (_readyFailed)
            throw new InvalidOperationException(
                "OrigoAutoHost bootstrap failed in _Ready; frame driving is disabled. " +
                "Fix the bootstrap error before running the scene.");
        Runtime.DriveFrame(delta);
    }

    [MemberNotNull(nameof(SndManager), nameof(SharedMetaAccess), nameof(SharedPathResolver),
        nameof(SharedDataSourceIo), nameof(ConsoleInput), nameof(ConsoleOutputChannel))]
    private OrigoRuntime CreateRuntime()
    {
        var createWatch = Stopwatch.StartNew();
        var logger = CreateBootstrapLogger();
        logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder().Build("CreateRuntime begin."));

        var fileSystem = new GodotFileSystem();
        var systemBlackboardPath = fileSystem.CombinePath(SystemBlackboardSaveRoot, "system.json");

        var sndManager = new GodotSndManager();
        AddChild(sndManager);
        SndManager = sndManager;

        var options = new OrigoHostOptions
        {
            Meta = ResolveOrigoMeta(),
            Logger = logger,
            FileSystem = fileSystem,
        };

        var bundle = AdapterHostKernelPort.CreateRuntime(
            options,
            sndManager,
            systemBlackboardPath,
            static (typeMapping, converterRegistry) =>
            {
                GodotJsonConverterRegistry.RegisterTypeMappings(typeMapping);
                GodotJsonConverterRegistry.RegisterDataSourceConverters(converterRegistry);
            });

        _runtimeBundle = bundle;
        SharedDataSourceIo = bundle.DataSourceIo;
        SharedMetaAccess = bundle.MetaAccess;
        SharedPathResolver = bundle.PathResolver;
        ConsoleInput = bundle.ConsoleInput;
        ConsoleOutputChannel = bundle.ConsoleOutputChannel;

        createWatch.Stop();
        logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder()
                .SetElapsedMs(createWatch.Elapsed.TotalMilliseconds)
                .AddContext("filePath", systemBlackboardPath)
                .Build("CreateRuntime completed."));
        return bundle.Runtime;
    }

    /// <summary>
    ///     Creates the SND context for a derived entry through the adapter kernel
    ///     port, which binds the context to the Godot scene host.
    /// </summary>
    private protected ISndContext CreateSndContext(AdapterContextOptions options) =>
        AdapterHostKernelPort.CreateContext(RuntimeBundle, options);

    /// <summary>Registers one adapter console handler through the adapter kernel port.</summary>
    private protected void RegisterConsoleCommandHandler(IConsoleCommandHandler handler) =>
        AdapterHostKernelPort.RegisterConsoleHandler(Runtime, handler);

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
        // The informational version carries the repository <Version> value
        // (e.g. 0.0.9 or 0.0.9-nightly.20260827) plus the source commit hash.
        // Strip the hash so runtime metadata matches the release/tag version
        // instead of the four-part assembly version (0.0.9.0).
        var version = typeof(OrigoRuntime).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
            version = typeof(OrigoRuntime).Assembly.GetName().Version?.ToString() ?? "unknown";
        var commitSeparator = version!.IndexOf('+');
        if (commitSeparator >= 0)
            version = version[..commitSeparator];
        return new OrigoMeta("Origo", version, OrigoMeta.DefaultBanner);
    }
}
