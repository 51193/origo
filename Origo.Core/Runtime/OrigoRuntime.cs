using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Scheduling;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime;

/// <summary>
///     Origo 在宿主游戏中的统一运行时入口。
///     聚合 SND 子系统与系统级黑板。
///     <para>
///         线程模型：未做跨线程同步；<see cref="EnqueueBusinessDeferred" /> 与 <see cref="EnqueueSystemDeferred" />
///         应在宿主主线程（或单线程游戏主循环）上调用，与 <see cref="FlushEndOfFrameDeferred" /> 成对使用。
///     </para>
/// </summary>
public sealed class OrigoRuntime : IOrigoFrameDriver
{
    private readonly ActionScheduler _businessDeferredScheduler;
    private readonly ActionScheduler _systemDeferredScheduler;
    private readonly ISndSceneHost _foregroundSceneHost;
    private Func<ISessionManager> _sessionManagerProvider = static () => EmptySessionManager.Instance;

    public OrigoRuntime(
        OrigoMeta meta,
        ILogger logger,
        ISndSceneHost sndSceneHost,
        TypeStringMapping typeStringMapping,
        DataSourceConverterRegistry converterRegistry,
        IDataSourceIoGateway dataSourceIo,
        IBlackboard? systemBlackboard = null,
        IConsoleInputSource? consoleInput = null,
        IConsoleOutputChannel? consoleOutputChannel = null)
    {
        ArgumentNullException.ThrowIfNull(meta);
        Meta = meta;
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
        Logger.Log(LogLevel.Info, nameof(OrigoRuntime), new LogMessageBuilder()
            .AddContext("version", meta.Version)
            .Build($"{meta.Name} runtime constructed."));
        Logger.Log(LogLevel.Debug, nameof(OrigoRuntime), meta.Banner);
        ArgumentNullException.ThrowIfNull(sndSceneHost);
        ArgumentNullException.ThrowIfNull(typeStringMapping);
        ArgumentNullException.ThrowIfNull(converterRegistry);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        SndWorld = new SndWorld(typeStringMapping, Logger, converterRegistry, dataSourceIo);
        _foregroundSceneHost = sndSceneHost;
        _businessDeferredScheduler = new ActionScheduler(Logger);
        _systemDeferredScheduler = new ActionScheduler(Logger);

        ArgumentNullException.ThrowIfNull(systemBlackboard);
        SystemBlackboard = systemBlackboard;

        ConsoleInput = consoleInput;
        ConsoleOutputChannel = consoleOutputChannel;
        if (consoleInput is not null && consoleOutputChannel is not null)
            Console = new OrigoConsole(consoleInput, consoleOutputChannel, this);
    }

    /// <summary>
    ///     日志服务实例，贯穿整个运行时，供所有子系统记录日志。
    /// </summary>
    public ILogger Logger { get; }

    public OrigoMeta Meta { get; }

    /// <summary>
    ///     SND 世界实例，管理策略池、类型映射、编解码器和模板配置。
    ///     是 SND 子系统的核心数据层。注意：Snd.World 与此属性指向同一实例。
    /// </summary>
    public SndWorld SndWorld { get; }

    /// <summary>
    ///     前台场景宿主，在构造时注入。
    ///     用于框架内部在拆前台会话后清理场景实体（ResetForeground）。
    /// </summary>
    internal ISndSceneHost ForegroundSceneHost => _foregroundSceneHost;

    /// <summary>
    ///     系统级黑板，生命周期跨越整个应用运行期。
    ///     存储全局状态（如 continue slot ID、active save ID）。与 SndContext.SystemBlackboard 指向同一实例。
    /// </summary>
    public IBlackboard SystemBlackboard { get; }

    /// <summary>
    ///     控制台输入队列，若启动时未注入则为 null。线程安全。
    ///     适配层通过 Enqueue 投递命令行，Core 通过 Console.ProcessPending() 消费。
    /// </summary>
    public IConsoleInputSource? ConsoleInput { get; }

    /// <summary>
    ///     控制台输出发布通道，若启动时未注入则为 null。
    ///     Core 发布消息，适配层/策略订阅接收。
    /// </summary>
    public IConsoleOutputChannel? ConsoleOutputChannel { get; }

    /// <summary>
    ///     控制台门面实例，仅在同时注入输入队列和输出通道时创建。
    ///     内部持有 ConsoleInput 和 ConsoleOutputChannel 的引用。
    /// </summary>
    public OrigoConsole? Console { get; }

    /// <summary>
    ///     将一个业务逻辑延迟动作加入队列，在下次 FlushEndOfFrameDeferred() 时执行。
    ///     适用于需要延迟到帧末执行的游戏逻辑。
    /// </summary>
    public void EnqueueBusinessDeferred(Action action) => _businessDeferredScheduler.Enqueue(action);

    /// <summary>
    ///     将一个系统级延迟动作加入队列，在下次 FlushEndOfFrameDeferred() 时执行（在业务队列之后）。
    ///     适用于存档、关卡切换等系统编排操作。
    /// </summary>
    public void EnqueueSystemDeferred(Action action) => _systemDeferredScheduler.Enqueue(action);

    /// <summary>
    ///     注入"当前会话管理器"的提供者。帧驱动与会话作用域操作经此解析 <see cref="ISessionManager" />，
    ///     从而 Runtime 仅触达 SessionManager（再由它下查 SessionRun），不直达任何 SceneHost。
    ///     由 <see cref="Snd.SndContext" /> 在 Bootstrap 时注入。
    /// </summary>
    internal void SetSessionManagerProvider(Func<ISessionManager> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _sessionManagerProvider = provider;
    }

    internal ISessionManager SessionManager => _sessionManagerProvider();

    /// <summary>
    ///     依次执行业务延迟队列和系统延迟队列中的所有待执行动作。
    ///     通常在每帧结束时由宿主主循环调用。
    /// </summary>
    public void FlushEndOfFrameDeferred()
    {
        _businessDeferredScheduler.Tick();
        _sessionManagerProvider().KillPendingAllSessions();
        _systemDeferredScheduler.Tick();
    }

    /// <summary>
    ///     由宿主环境的帧边界触发。Core 内部按固定顺序驱动：
    ///     实体帧处理 → 业务延迟队列 → 清理待杀实体 → 系统延迟队列 → 控制台 pump。
    ///     Adapter 不应直接调用 FlushEndOfFrameDeferred、ProcessAll 或 ProcessPending，
    ///     只应调用此方法将帧控制权移交给 Core。
    /// </summary>
    void IOrigoFrameDriver.DriveFrame(double delta)
    {
        _sessionManagerProvider().ProcessAllSessions(delta, includeForeground: true);
        FlushEndOfFrameDeferred();
        Console?.ProcessPending();
    }

    /// <summary>
    ///     重置控制台状态：清空待执行输入队列。
    ///     输出已改为发布-订阅模型，不在 Core 中保留历史。
    /// </summary>
    public void ResetConsoleState() => ConsoleInput?.Clear();
}
