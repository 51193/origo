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
///     流程级运行时实现。
///     构造时接收 <see cref="SystemRuntime" /> 与 <see cref="ProgressParameters" />，
///     内部基于 SystemRuntime 构建 <see cref="ProgressRuntime" /> 作为本层唯一运行时容器。
///     <para>
///         SessionManager 作为独立的运行时构造层，由 ProgressRun 创建并持有。
///         所有会话操作均委托给 <see cref="SessionManager" />。
///     </para>
/// </summary>
public sealed partial class ProgressRun : IDisposable
{
    private readonly ProgressRuntime _progressRuntime;
    private readonly SaveCoordinator _saveCoordinator;
    private readonly SessionLifecycle _sessionLifecycle;
    private readonly SessionManager _sessionManager;
    private bool _disposed;

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

    internal ISessionRun? ForegroundSession => _sessionManager.ForegroundSession;

    public IBlackboard ProgressBlackboard => ProgressScope.Blackboard;

    public ISessionManager SessionManager => _sessionManager;

    public string SaveId { get; private set; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var watch = Stopwatch.StartNew();
        _progressRuntime.Logger.Log(LogLevel.Info, nameof(ProgressRun),
            $"Disposing ProgressRun (saveId: '{SaveId}').");

        try
        {
            _sessionManager.Clear();
        }
        catch (Exception ex)
        {
            _progressRuntime.Logger.Log(LogLevel.Warning, nameof(ProgressRun),
                $"Session clear failed during Dispose (saveId: '{SaveId}'): {ex.Message}");
        }

        try
        {
            _progressRuntime.StorageService.DeleteCurrentDirectory();
        }
        catch (Exception ex)
        {
            _progressRuntime.Logger.Log(LogLevel.Warning, nameof(ProgressRun),
                $"Delete current directory failed during Dispose (saveId: '{SaveId}'): {ex.Message}");
        }

        ProgressScope.StateMachines.PopAllOnQuit();
        ProgressScope.StateMachines.Clear();
        ProgressBlackboard.Clear();
        _progressRuntime.Logger.Log(LogLevel.Info, nameof(ProgressRun),
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Disposed ProgressRun (saveId: '{SaveId}')."));
    }

    public IStateMachineContainer GetProgressStateMachines() => ProgressScope.StateMachines;

    internal void SetSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        SaveId = saveId;
    }

    internal ISessionRun RequireForegroundSession() => ForegroundSession ??
        throw new InvalidOperationException("No active foreground session.");

    internal List<string> BuildSessionTopology() =>
        _saveCoordinator.BuildSessionTopology(RequireForegroundSession());

    internal void EnsureActiveLevelInvariant()
    {
        var fgSession = RequireForegroundSession();
        var (found, rawTopology) = ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        if (!found || string.IsNullOrWhiteSpace(rawTopology))
            throw new InvalidOperationException(
                $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}' (save id: '{SaveId}').");

        var topologyActiveLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);
        if (!string.Equals(topologyActiveLevelId, fgSession.LevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Progress '{WellKnownKeys.SessionTopology}' foreground ('{topologyActiveLevelId}') does not match foreground level '{fgSession.LevelId}' (save id: '{SaveId}').");
    }
}
