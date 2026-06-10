using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Serialization;

namespace Origo.Core.Save;

/// <summary>
///     存档协调器，负责构建存档 payload、持久化 progress 状态，以及管理元数据。
///     从 <see cref="Runtime.Lifecycle.ProgressRun" /> 中提取为独立类以便测试和职责分离。
/// </summary>
internal sealed class SaveCoordinator
{
    private readonly SessionManager _sessionManager;
    private readonly IBlackboard _progressBlackboard;
    private readonly IStateMachineContainer _progressStateMachines;
    private readonly ProgressRuntime _progressRuntime;
    private readonly string _saveId;

    internal SaveCoordinator(
        SessionManager sessionManager,
        IBlackboard progressBlackboard,
        IStateMachineContainer progressStateMachines,
        ProgressRuntime progressRuntime,
        string saveId)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _progressBlackboard = progressBlackboard ?? throw new ArgumentNullException(nameof(progressBlackboard));
        _progressStateMachines = progressStateMachines ?? throw new ArgumentNullException(nameof(progressStateMachines));
        _progressRuntime = progressRuntime ?? throw new ArgumentNullException(nameof(progressRuntime));
        _saveId = saveId ?? throw new ArgumentNullException(nameof(saveId));
    }

    internal SaveMetaBuildContext BuildSaveMetaContext(string saveId)
    {
        var fgSession = RequireForegroundSession();
        return new SaveMetaBuildContext(
            saveId,
            fgSession.LevelId,
            _progressBlackboard,
            fgSession.SessionBlackboard,
            fgSession.SceneHost);
    }

    internal SaveGamePayload BuildSavePayload(
        string newSaveId,
        IReadOnlyDictionary<string, string>? mergedMeta)
    {
        var fgSession = RequireForegroundSession();
        EnsureActiveLevelInvariant(fgSession);

        var bgSessions = _sessionManager.GetBackgroundSessions();
        var topologyItems = BuildSessionTopology(fgSession);
        _progressBlackboard.Set(WellKnownKeys.SessionTopology, SessionTopologyCodec.Join(topologyItems));

        var saveContext = new SaveContext(
            _progressBlackboard, fgSession.SessionBlackboard, _progressRuntime.SndWorld);

        var progressSmNode =
            ((StateMachineContainer)_progressStateMachines).SerializeToNode(_progressRuntime.ConverterRegistry);
        var sessionSmNode = ((StateMachineContainer)fgSession.GetSessionStateMachines())
            .SerializeToNode(_progressRuntime.ConverterRegistry);

        var payload = saveContext.SaveGame(
            fgSession.SceneHost,
            newSaveId,
            fgSession.LevelId,
            mergedMeta,
            progressSmNode,
            sessionSmNode);

        AppendBackgroundPayloads(payload, bgSessions);
        return payload;
    }

    internal void PersistProgress()
    {
        var fgSession = _sessionManager.ForegroundSession;
        if (fgSession is not null)
        {
            var topologyItems = BuildSessionTopology(fgSession);
            _progressBlackboard.Set(WellKnownKeys.SessionTopology,
                SessionTopologyCodec.Join(topologyItems));
        }

        var sessionBb = fgSession?.SessionBlackboard ?? new Blackboard.Blackboard();

        var serializer = new SaveContext(_progressBlackboard, sessionBb, _progressRuntime.SndWorld);
        var progressNode = serializer.SerializeProgress();
        var smNode = ((StateMachineContainer)_progressStateMachines).SerializeToNode(_progressRuntime.ConverterRegistry);

        _progressRuntime.StorageService.WriteProgressOnlyToCurrent(progressNode, smNode);
    }

    private ISessionRun RequireForegroundSession() =>
        _sessionManager.ForegroundSession
        ?? throw new InvalidOperationException("No active foreground session.");

    internal List<string> BuildSessionTopology(ISessionRun fgSession)
    {
        var bgSessions = _sessionManager.GetBackgroundSessions();

        var topologyItems = new List<string>
        {
            SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, fgSession.LevelId, false)
        };

        topologyItems.AddRange(bgSessions.Select(kvp =>
        {
            var syncProcess = _sessionManager.GetSyncProcess(kvp.Key);
            return SessionTopologyCodec.Serialize(kvp.Key, kvp.Value.LevelId, syncProcess);
        }));

        return topologyItems;
    }

    private void EnsureActiveLevelInvariant(ISessionRun fgSession)
    {
        var (found, rawTopology) = _progressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        if (!found || string.IsNullOrWhiteSpace(rawTopology))
            throw new InvalidOperationException(
                $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}' (save id: '{_saveId}').");

        var topologyActiveLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);
        if (!string.Equals(topologyActiveLevelId, fgSession.LevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Progress '{WellKnownKeys.SessionTopology}' foreground ('{topologyActiveLevelId}') does not match foreground level '{fgSession.LevelId}' (save id: '{_saveId}').");
    }

    private void AppendBackgroundPayloads(
        SaveGamePayload payload,
        IReadOnlyList<KeyValuePair<string, ISessionRun>> bgSessions)
    {
        var bgPayloads = _sessionManager.SerializeBackgroundSessions();
        foreach (var kvp in bgSessions)
        {
            if (!bgPayloads.TryGetValue(kvp.Key, out var bgPayload))
                continue;

            if (payload.Levels.ContainsKey(kvp.Value.LevelId))
                throw new InvalidOperationException(
                    $"Cannot persist background session '{kvp.Key}': " +
                    $"levelId '{kvp.Value.LevelId}' is already present in the save payload " +
                    "(another session already manages this level). " +
                    "Destroy the conflicting session or use a different levelId.");

            payload.Levels[kvp.Value.LevelId] = bgPayload;
        }
    }
}
