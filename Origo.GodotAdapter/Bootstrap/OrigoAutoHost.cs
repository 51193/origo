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

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     自建 Runtime 与 SndManager 的唯一启动入口节点。
/// </summary>
[GlobalClass]
public partial class OrigoAutoHost : Node
{
    private const string LogTag = nameof(OrigoAutoHost);

    [Export] public string SystemBlackboardSaveRoot { get; set; } = "user://origo_saves";
    public GodotSndManager SndManager { get; private set; } = null!;

    /// <summary>
    ///     控制台命令输入队列；UI 将提交的行通过 <see cref="IConsoleInputSource.Enqueue" /> 投递。
    /// </summary>
    public IConsoleInputSource? ConsoleInput { get; private set; }

    /// <summary>
    ///     控制台输出发布通道；外部消费者（ConsoleBridge、UI）通过 Subscribe 订阅接收输出。
    /// </summary>
    public IConsoleOutputChannel? ConsoleOutputChannel { get; private set; }

    /// <summary>
    ///     文件元数据访问接口（与当前运行时同源）。
    /// </summary>
    protected IFileMetaAccess SharedMetaAccess { get; private set; } = null!;

    /// <summary>
    ///     路径解析接口（与当前运行时同源）。
    /// </summary>
    protected IPathResolver SharedPathResolver { get; private set; } = null!;

    /// <summary>
    ///     DataSource I/O 网关（与当前运行时同源）。
    /// </summary>
    protected IDataSourceIoGateway SharedDataSourceIo { get; private set; } = null!;

    public OrigoRuntime Runtime { get; private set; } = null!;

    public override void _Ready()
    {
        var readyWatch = Stopwatch.StartNew();
        var bootstrapLogger = CreateBootstrapLogger();
        bootstrapLogger.Log(LogLevel.Info, LogTag, new LogMessageBuilder().Build("_Ready begin."));
        try
        {
            Runtime = CreateRuntime();
            readyWatch.Stop();
            Runtime.Logger.Log(LogLevel.Info, LogTag,
                new LogMessageBuilder()
                    .SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .Build("_Ready completed."));
        }
        catch (Exception ex)
        {
            readyWatch.Stop();
            bootstrapLogger.Log(LogLevel.Error, LogTag,
                new LogMessageBuilder().SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .Build($"_Ready failed: {ex.Message}"));
            throw;
        }
    }

    public override void _Process(double delta)
    {
        ((IOrigoFrameDriver?)Runtime)?.DriveFrame(delta);
    }

    [MemberNotNull(nameof(SndManager), nameof(SharedMetaAccess), nameof(SharedPathResolver), nameof(SharedDataSourceIo), nameof(Runtime))]
    private OrigoRuntime CreateRuntime()
    {
        var createWatch = Stopwatch.StartNew();
        var logger = CreateBootstrapLogger();
        logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder().Build("CreateRuntime begin."));

        var fileSystem = new GodotFileSystem();
        SharedDataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem);
        SharedMetaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        SharedPathResolver = DataSourceFactory.CreatePathResolver(fileSystem);

        var sndManager = CreateAndSetupSndManager(fileSystem, logger, out var sharedTypeMapping,
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
        sndManager.SetProcess(true);

        ConsoleInput = consoleInput;
        ConsoleOutputChannel = consoleOutputChannel;

        var systemBbPath = SharedPathResolver.CombinePath(SystemBlackboardSaveRoot, "system.json");
        createWatch.Stop();
        logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder()
                .SetElapsedMs(createWatch.Elapsed.TotalMilliseconds)
                .AddContext("filePath", systemBbPath)
                .Build("CreateRuntime completed."));
        return runtime;
    }

    private GodotSndManager CreateAndSetupSndManager(
        GodotFileSystem fileSystem,
        GodotLogger logger,
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
