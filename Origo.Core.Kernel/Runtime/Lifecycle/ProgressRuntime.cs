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
///     Progress-layer runtime container, built by <see cref="ProgressRun" /> based on
///     <see cref="SystemRuntime" />. Holds the runtime dependencies needed for ProgressRun internals
///     and the lower <see cref="SessionManager" /> construction.
///     <para>
///         Surface control: does not include System-layer exclusive capabilities
///         (SystemBlackboard, ActiveSaveSlot); only exposes the subset needed by ProgressRun
///         and its lower layers.
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
        AdapterSceneHost = systemRuntime.AdapterSceneHost;
        StateMachineContext = stateMachineContext;
        SndContext = sndContext;
        SavePathPolicy = systemRuntime.SavePathPolicy;
    }

    internal ILogger Logger { get; }
    internal ISaveStorageService StorageService { get; }
    internal SndWorld SndWorld { get; }
    internal ISndSceneHost AdapterSceneHost { get; }
    internal IStateMachineContext StateMachineContext { get; }
    internal ISndContext SndContext { get; }
    internal ISavePathPolicy SavePathPolicy { get; }

    internal DataSourceConverterRegistry ConverterRegistry => SndWorld.ConverterRegistry;
}
