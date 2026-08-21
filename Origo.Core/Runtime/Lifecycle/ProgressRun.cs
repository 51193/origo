using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed || _disposing) return;
        _disposing = true;
        var watch = Stopwatch.StartNew();
        _progressRuntime.Logger.Log(LogLevel.Info, nameof(ProgressRun),
            $"Disposing ProgressRun (saveId: '{SaveId}').");

        Exception? firstFailure = null;
        void RecordFailure(Exception ex, string step)
        {
            if (firstFailure is not null)
            {
                _progressRuntime.Logger.Log(LogLevel.Warning, nameof(ProgressRun),
                    new LogMessageBuilder()
                        .AddContext("saveId", SaveId)
                        .AddContext("cleanupStep", step)
                        .Build($"ProgressRun dispose step failed after an earlier failure: {ex.Message}"));
                return;
            }

            firstFailure = ex;
        }

        try
        {
            // Session, directory, and progress-level teardown steps run
            // independently: a throwing user hook must not skip later cleanup
            // steps. The first failure is rethrown after every step has run.
            try
            {
                _sessionManager.Clear();
            }
            catch (Exception ex)
            {
                RecordFailure(ex, "session clear");
            }

            try
            {
                _progressRuntime.StorageService.DeleteCurrentDirectory();
            }
            catch (Exception ex)
            {
                RecordFailure(ex, "current directory delete");
            }
        }
        finally
        {
            try
            {
                ProgressScope.StateMachines.PopAllOnQuit();
            }
            catch (Exception ex)
            {
                RecordFailure(ex, "progress state machine quit pop");
            }
            finally
            {
                try
                {
                    ProgressScope.StateMachines.Clear();
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, "progress state machine clear");
                }
                finally
                {
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

        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
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
