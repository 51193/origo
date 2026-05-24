using System;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     流程层运行时容器，由 <see cref="ProgressRun" /> 基于 <see cref="SystemRuntime" /> 构建。
///     持有 ProgressRun 内部以及下层 <see cref="SessionManager" /> 构造所需的运行时依赖。
///     <para>
///         暴露面控制：不包含 System 层独有的能力（SystemBlackboard、ActiveSaveSlot），
///         仅暴露 ProgressRun 及其下层所需的子集。
///     </para>
/// </summary>
internal sealed class ProgressRuntime
{
    internal ProgressRuntime(
        SystemRuntime systemRuntime,
        IStateMachineContext stateMachineContext,
        ISndContext sndContext)
    {
        ArgumentNullException.ThrowIfNull(systemRuntime);
        ArgumentNullException.ThrowIfNull(stateMachineContext);
        ArgumentNullException.ThrowIfNull(sndContext);

        Logger = systemRuntime.Logger;
        StorageService = systemRuntime.StorageService;
        SndWorld = systemRuntime.SndWorld;
        SndRuntime = systemRuntime.SndRuntime;
        ForegroundSceneHost = systemRuntime.ForegroundSceneHost;
        StateMachineContext = stateMachineContext;
        SndContext = sndContext;
        SavePathPolicy = systemRuntime.SavePathPolicy;
    }

    internal ILogger Logger { get; }
    internal ISaveStorageService StorageService { get; }
    internal SndWorld SndWorld { get; }
    internal SndRuntime SndRuntime { get; }
    internal ISndSceneHost ForegroundSceneHost { get; }
    internal IStateMachineContext StateMachineContext { get; }
    internal ISndContext SndContext { get; }
    internal ISavePathPolicy SavePathPolicy { get; }

    internal DataSourceConverterRegistry ConverterRegistry => SndWorld.ConverterRegistry;
}
