using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Console;
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
public sealed class OrigoRuntime
{
    private readonly ActionScheduler _businessDeferredScheduler;
    private readonly ActionScheduler _systemDeferredScheduler;

    public OrigoRuntime(
        ILogger logger,
        ISndSceneHost sndSceneHost,
        TypeStringMapping typeStringMapping,
        DataSourceConverterRegistry converterRegistry,
        IDataSourceIoGateway dataSourceIo,
        IBlackboard? systemBlackboard = null,
        ConsoleInputQueue? consoleInput = null,
        IConsoleOutputChannel? consoleOutputChannel = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
        ArgumentNullException.ThrowIfNull(sndSceneHost);
        ArgumentNullException.ThrowIfNull(typeStringMapping);
        ArgumentNullException.ThrowIfNull(converterRegistry);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        SndWorld = new SndWorld(typeStringMapping, Logger, converterRegistry, dataSourceIo);
        Snd = new SndRuntime(SndWorld, sndSceneHost);
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

    /// <summary>
    ///     SND 世界实例，管理策略池、类型映射、编解码器和模板配置。
    ///     是 Snd (SndRuntime) 的核心数据层。注意：Snd.World 与此属性指向同一实例。
    /// </summary>
    public SndWorld SndWorld { get; }

    /// <summary>
    ///     SND 运行时门面，组合 SndWorld（数据/配置层）与 ISndSceneHost（场景宿主），
    ///     提供 Spawn / 查询 / 序列化等统一入口。内部的 Snd.World 与本类的 SndWorld 属性是同一实例。
    /// </summary>
    public SndRuntime Snd { get; }

    /// <summary>
    ///     系统级黑板，生命周期跨越整个应用运行期。
    ///     存储全局状态（如 continue slot ID、active save ID）。与 SndContext.SystemBlackboard 指向同一实例。
    /// </summary>
    public IBlackboard SystemBlackboard { get; }

    /// <summary>
    ///     控制台输入队列，若启动时未注入则为 null。线程安全。
    ///     适配层通过 Enqueue 投递命令行，Core 通过 Console.ProcessPending() 消费。
    /// </summary>
    public ConsoleInputQueue? ConsoleInput { get; }

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
    ///     依次执行业务延迟队列和系统延迟队列中的所有待执行动作。
    ///     通常在每帧结束时由宿主主循环调用。
    /// </summary>
    public void FlushEndOfFrameDeferred()
    {
        _businessDeferredScheduler.Tick();
        _systemDeferredScheduler.Tick();
    }

    /// <summary>
    ///     重置控制台状态：清空待执行输入队列。
    ///     输出已改为发布-订阅模型，不在 Core 中保留历史。
    /// </summary>
    public void ResetConsoleState() => ConsoleInput?.Clear();
}
