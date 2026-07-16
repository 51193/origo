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
///     SessionManager-layer runtime container, built by <see cref="SessionManager" /> based on
///     <see cref="ProgressRuntime" />. Holds the runtime dependencies needed for SessionManager
///     and the lower <see cref="SessionRun" /> construction.
///     <para>
///         Dual responsibilities:
///         <list type="number">
///             <item>Private runtime capabilities needed by the Session layer (passed down to SessionRun)</item>
///             <item>Capability components shared across multiple Sessions (SndWorld, StrategyPool, etc.)</item>
///         </list>
///         These shared capabilities exist as read-only or controlled interfaces for use by lower layers.
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
        AdapterSceneHost = progressRuntime.AdapterSceneHost;
        StateMachineContext = progressRuntime.StateMachineContext;
        SndContext = progressRuntime.SndContext;
        ProgressBlackboard = progressBlackboard;
    }

    internal ILogger Logger { get; }
    internal ISaveStorageService StorageService { get; }
    internal SndWorld SndWorld { get; }
    internal ISndSceneHost AdapterSceneHost { get; }
    internal IStateMachineContext StateMachineContext { get; }
    internal ISndContext SndContext { get; }

    /// <summary>
    ///     The progress-level blackboard, passed directly by <see cref="SessionManager" />
    ///     to avoid depending on <see cref="IStateMachineContext.Blackboard.ProgressBlackboard" /> which
    ///     may be null before the context is fully wired (e.g. in tests or startup ordering).
    /// </summary>
    internal IBlackboard ProgressBlackboard { get; }

    internal DataSourceConverterRegistry ConverterRegistry => SndWorld.ConverterRegistry;
}
