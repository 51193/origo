using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Logging;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Progress-level runtime implementation.
///     Receives <see cref="SystemRuntime" /> and <see cref="ProgressParameters" /> at construction,
///     internally builds <see cref="ProgressRuntime" /> based on SystemRuntime as the sole runtime container
///     for this layer.
///     <para>
///         SessionManager serves as an independent runtime construction layer, created and held by
///         ProgressRun. All session operations are delegated to <see cref="SessionManager" />.
///     </para>
/// </summary>
internal sealed partial class ProgressRun : IDisposable
{
    private readonly ProgressRuntime _progressRuntime;
    private readonly SaveCoordinator _saveCoordinator;
    private readonly SessionLifecycle _sessionLifecycle;
    private readonly SessionManager _sessionManager;
    private bool _disposed;
    private bool _disposing;

    internal ProgressRun(
        SystemRuntime systemRuntime,
        ProgressParameters progressParams,
        IStateMachineContext stateMachineContext,
        ISndContext sndContext)
    {
        var watch = Stopwatch.StartNew();
        ArgumentNullException.ThrowIfNull(systemRuntime);
        ArgumentNullException.ThrowIfNull(stateMachineContext);
        ArgumentNullException.ThrowIfNull(sndContext);
        if (string.IsNullOrWhiteSpace(progressParams.SaveId))
            throw new ArgumentException("Save id cannot be null or whitespace.");

        _progressRuntime = new ProgressRuntime(systemRuntime, stateMachineContext, sndContext);

        var progressBlackboard = new Blackboard.Blackboard();
        var progressMachines = new StateMachineContainer(
            _progressRuntime.SndWorld.StrategyPool, stateMachineContext);
        ProgressScope = new RunStateScope(progressBlackboard, progressMachines);
        SaveId = progressParams.SaveId;

        _sessionManager = new SessionManager(
            _progressRuntime,
            ProgressScope.Blackboard);
        _sessionLifecycle = new SessionLifecycle(this);
        _saveCoordinator = new SaveCoordinator(
            _sessionManager,
            ProgressScope.Blackboard,
            ProgressScope.StateMachines,
            _progressRuntime,
            progressParams.SaveId);

        _progressRuntime.Logger.Log(LogLevel.Info, nameof(ProgressRun),
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Created ProgressRun (saveId: '{progressParams.SaveId}')."));
    }

    internal RunStateScope ProgressScope { get; }

    public IBlackboard ProgressBlackboard => ProgressScope.Blackboard;

    public ISessionManager SessionManager => _sessionManager;

    public string SaveId { get; private set; }

    public void Dispose()
    {
        if (_disposed || _disposing) return;
        _disposing = true;
        var watch = Stopwatch.StartNew();
        _progressRuntime.Logger.Log(LogLevel.Info, nameof(ProgressRun),
            $"Disposing ProgressRun (saveId: '{SaveId}').");

        try
        {
            // Session and directory teardown can throw (exceptions propagate
            // to the caller per the fail-fast contract, matching SessionRun.
            // Dispose); the progress-level state machines and blackboard are
            // still guaranteed to be released and the disposed flag committed
            // via the finally block.
            _sessionManager.Clear();
            _progressRuntime.StorageService.DeleteCurrentDirectory();
        }
        finally
        {
            // Quit-pop hooks can throw (fail-fast: the exception propagates),
            // but the machine disposal, blackboard clear, and the disposed
            // flag commit must still run so no pool reference or half-closed
            // state survives a hook failure.
            try
            {
                ProgressScope.StateMachines.PopAllOnQuit();
            }
            finally
            {
                ProgressScope.StateMachines.Clear();
                ProgressBlackboard.Clear();
                _disposed = true;
                _disposing = false;
                _progressRuntime.Logger.Log(LogLevel.Info, nameof(ProgressRun),
                    new LogMessageBuilder()
                        .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                        .Build($"Disposed ProgressRun (saveId: '{SaveId}')."));
            }
        }
    }

    public IStateMachineContainer GetProgressStateMachines() => ProgressScope.StateMachines;

    internal void SetSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        SaveId = saveId;
    }

    internal ISessionRun RequireForegroundSession() => _sessionManager.ForegroundSession ??
        throw new InvalidOperationException("No active foreground session.");

    internal List<string> BuildSessionTopology() =>
        _saveCoordinator.BuildSessionTopology(RequireForegroundSession());

    internal void EnsureActiveLevelInvariant()
    {
        TopologyInvariant.EnsureActiveLevel(ProgressBlackboard,
            RequireForegroundSession().LevelId, $"save id: '{SaveId}'");
    }
}
