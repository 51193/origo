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
///     Unified runtime entry point for Origo within the host game.
///     Aggregates the SND subsystem and system-level blackboard.
///     <para>
///         Threading model: no cross-thread synchronization is performed;
///         <see cref="EnqueueBusinessDeferred" /> and <see cref="EnqueueSystemDeferred" />
///         should be called on the host main thread (or single-threaded game main loop),
///         paired with <see cref="FlushEndOfFrameDeferred" />.
///     </para>
/// </summary>
public sealed class OrigoRuntime : IOrigoFrameDriver
{
    private readonly ActionScheduler _businessDeferredScheduler;
    private readonly ActionScheduler _systemDeferredScheduler;
    private readonly ISndSceneHost _adapterSceneHost;
    private Func<ISessionManager> _sessionManagerProvider = static () => EmptySessionManager.Instance;

    /// <summary>
    ///     Creates the runtime and wires the SND world over the given scene host
    ///     and I/O infrastructure.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    internal OrigoRuntime(
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
        _adapterSceneHost = sndSceneHost;
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
    ///     Logger service instance used throughout the runtime, available to all subsystems for logging.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>Framework metadata (name, version, banner).</summary>
    public OrigoMeta Meta { get; }

    /// <summary>
    ///     SND world instance that manages the strategy pool, type mapping, codecs, and template configuration.
    ///     Serves as the core data layer of the SND subsystem. Note: Snd.World points to the same instance as this property.
    /// </summary>
    public SndWorld SndWorld { get; }

    /// <summary>
    ///     The scene host injected by the adapter layer. Read once by SndContext during bootstrap
    ///     and passed down the SessionManager construction chain.
    /// </summary>
    internal ISndSceneHost GetAdapterSceneHost() => _adapterSceneHost;

    /// <summary>
    ///     System-level blackboard whose lifetime spans the entire application run.
    ///     Stores global state (e.g., continue slot ID, active save ID).
    ///     Points to the same instance as SndContext.Blackboard.SystemBlackboard.
    /// </summary>
    public IBlackboard SystemBlackboard { get; }

    /// <summary>
    ///     Console input queue. Null if not injected at startup. Thread-safe.
    ///     The adapter layer posts command lines via Enqueue; Core consumes them via Console.ProcessPending().
    /// </summary>
    public IConsoleInputSource? ConsoleInput { get; }

    /// <summary>
    ///     Console output publishing channel. Null if not injected at startup.
    ///     Core publishes messages; the adapter layer / strategies subscribe and receive.
    /// </summary>
    public IConsoleOutputChannel? ConsoleOutputChannel { get; }

    /// <summary>
    ///     Console facade instance, created only when both the input queue and output channel are injected.
    ///     Internally holds references to ConsoleInput and ConsoleOutputChannel.
    /// </summary>
    public OrigoConsole? Console { get; }

    /// <summary>
    ///     Enqueues a business-logic deferred action to be executed on the next
    ///     <see cref="FlushEndOfFrameDeferred" />.
    ///     Suitable for game logic that should run at end of frame.
    /// </summary>
    internal void EnqueueBusinessDeferred(Action action) => _businessDeferredScheduler.Enqueue(action);

    /// <summary>
    ///     Enqueues a system-level deferred action to be executed on the next
    ///     <see cref="FlushEndOfFrameDeferred" /> (after the business queue).
    ///     Suitable for system orchestration operations such as saving and level transitions.
    /// </summary>
    internal void EnqueueSystemDeferred(Action action) => _systemDeferredScheduler.Enqueue(action);

    /// <summary>
    ///     Injects a provider for the current session manager.
    ///     Frame driver and session-scoped operations resolve <see cref="ISessionManager" />
    ///     through this, ensuring the Runtime only reaches SessionManager (which in turn resolves
    ///     SessionRun) without directly touching any SceneHost.
    ///     Injected by <see cref="Snd.SndContext" /> during bootstrap.
    /// </summary>
    internal void SetSessionManagerProvider(Func<ISessionManager> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _sessionManagerProvider = provider;
    }

    /// <summary>
    ///     The current session manager. Non-entity code (such as console command handlers)
    ///     accesses <see cref="ISessionManager" /> through this property; strategy code should use
    ///     <see cref="ISndEntity.OwningSession" />. SessionManager itself is returned as public.
    /// </summary>
    public ISessionManager SessionManager => _sessionManagerProvider();

    /// <summary>
    ///     Executes all pending actions in the business deferred queue and system deferred queue
    ///     in sequence. Typically called by the host main loop at the end of each frame.
    /// </summary>
    internal void FlushEndOfFrameDeferred()
    {
        _businessDeferredScheduler.Tick();
        _sessionManagerProvider().KillPendingAllSessions();
        _systemDeferredScheduler.Tick();
    }

    /// <summary>
    ///     Triggered by the host environment's frame boundary. Core internally drives in a fixed order:
    ///     entity frame processing → business deferred queue → cleanup of pending-kill entities →
    ///     system deferred queue → console pump.
    ///     Adapters should not directly call FlushEndOfFrameDeferred, ProcessAll, or ProcessPending;
    ///     they should only call this method to hand frame control to Core.
    /// </summary>
    void IOrigoFrameDriver.DriveFrame(double delta)
    {
        _sessionManagerProvider().ProcessAllSessions(delta, includeForeground: true);
        FlushEndOfFrameDeferred();
        Console?.ProcessPending();
    }

    /// <summary>
    ///     Resets console state: clears the pending input queue.
    ///     Output has been moved to a publish-subscribe model and no longer retains history in Core.
    /// </summary>
    internal void ResetConsoleState() => ConsoleInput?.Clear();
}
