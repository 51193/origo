using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     SessionManager 层运行时容器，由 <see cref="SessionManager" /> 基于 <see cref="ProgressRuntime" /> 构建。
///     持有 SessionManager 及下层 <see cref="SessionRun" /> 构造所需的运行时依赖。
///     <para>
///         双重职责：
///         <list type="number">
///             <item>Session 层所需的私有运行时能力（向下传递给 SessionRun）</item>
///             <item>跨多个 Session 共享的能力组件（SndWorld、StrategyPool 等）</item>
///         </list>
///         这些共享能力以只读或受控接口形式存在，供下层使用。
///     </para>
/// </summary>
internal sealed class SessionManagerRuntime
{
    internal SessionManagerRuntime(ProgressRuntime progressRuntime, IBlackboard progressBlackboard)
    {
        ArgumentNullException.ThrowIfNull(progressRuntime);
        ArgumentNullException.ThrowIfNull(progressBlackboard);

        Logger = progressRuntime.Logger;
        StorageService = progressRuntime.StorageService;
        SndWorld = progressRuntime.SndWorld;
        SndRuntime = progressRuntime.SndRuntime;
        ForegroundSceneHost = progressRuntime.ForegroundSceneHost;
        StateMachineContext = progressRuntime.StateMachineContext;
        SndContext = progressRuntime.SndContext;
        ProgressBlackboard = progressBlackboard;
    }

    internal ILogger Logger { get; }
    internal ISaveStorageService StorageService { get; }
    internal SndWorld SndWorld { get; }
    internal SndRuntime SndRuntime { get; }
    internal ISndSceneHost ForegroundSceneHost { get; }
    internal IStateMachineContext StateMachineContext { get; }
    internal ISndContext SndContext { get; }

    /// <summary>
    ///     The progress-level blackboard, passed directly by <see cref="SessionManager" />
    ///     to avoid depending on <see cref="IStateMachineContext.ProgressBlackboard" /> which
    ///     may be null before the context is fully wired (e.g. in tests or startup ordering).
    /// </summary>
    internal IBlackboard ProgressBlackboard { get; }

    internal DataSourceConverterRegistry ConverterRegistry => SndWorld.ConverterRegistry;
}
