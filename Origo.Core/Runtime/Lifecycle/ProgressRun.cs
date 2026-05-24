using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd;

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
        _saveCoordinator = new SaveCoordinator(this);

        _progressRuntime.Logger.Log(LogLevel.Info, "ProgressRun",
            $"Created ProgressRun (saveId: '{progressParams.SaveId}').");
    }

    internal RunStateScope ProgressScope { get; }

    internal ISessionRun? ForegroundSession => _sessionManager.ForegroundSession;

    public IBlackboard ProgressBlackboard => ProgressScope.Blackboard;

    public ISessionManager SessionManager => _sessionManager;

    public string SaveId { get; private set; }

    public void Dispose()
    {
        if (_disposed) return;
        // Set flag first to prevent recursive Dispose calls (e.g. from cleanup callbacks).
        _disposed = true;
        _progressRuntime.Logger.Log(LogLevel.Info, "ProgressRun",
            $"Disposing ProgressRun (saveId: '{SaveId}').");

        try
        {
            PersistProgress();
        }
        catch (Exception ex)
        {
            _progressRuntime.Logger.Log(LogLevel.Warning, "ProgressRun",
                $"Auto-persist failed during Dispose (saveId: '{SaveId}'): {ex.Message}");
        }

        _sessionManager.Clear();
        _progressRuntime.StorageService.DeleteCurrentDirectory();

        ProgressScope.StateMachines.PopAllOnQuit();
        ProgressScope.StateMachines.Clear();
        ProgressBlackboard.Clear();
    }

    /// <inheritdoc />
    public StateMachineContainer GetProgressStateMachines() => ProgressScope.StateMachines;

    internal void SetSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        SaveId = saveId;
    }
}
