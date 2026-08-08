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
///     Save coordinator responsible for building save payloads, persisting
///     progress state, and managing metadata. Extracted from
///     <see cref="Runtime.Lifecycle.ProgressRun" /> as a standalone class
///     for testability and separation of concerns.
/// </summary>
internal sealed class SaveCoordinator
{
    private readonly SessionManager _sessionManager;
    private readonly IBlackboard _progressBlackboard;
    private readonly IStateMachineContainer _progressStateMachines;
    private readonly ProgressRuntime _progressRuntime;

    internal SaveCoordinator(
        SessionManager sessionManager,
        IBlackboard progressBlackboard,
        IStateMachineContainer progressStateMachines,
        ProgressRuntime progressRuntime,
        string saveId)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(progressBlackboard);
        ArgumentNullException.ThrowIfNull(progressStateMachines);
        ArgumentNullException.ThrowIfNull(progressRuntime);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveId);
        _sessionManager = sessionManager;
        _progressBlackboard = progressBlackboard;
        _progressStateMachines = progressStateMachines;
        _progressRuntime = progressRuntime;
    }

    internal SaveMetaBuildContext BuildSaveMetaContext(string saveId)
    {
        var fgSession = RequireForegroundSession();
        return new SaveMetaBuildContext(
            saveId,
            fgSession.LevelId,
            _progressBlackboard,
            fgSession.SessionBlackboard,
            ((SessionRun)fgSession).SceneHost);
    }

    internal SaveGamePayload BuildSavePayload(
        string newSaveId,
        IReadOnlyDictionary<string, string>? mergedMeta)
    {
        var fgSession = RequireForegroundSession();
        EnsureActiveLevelInvariant(fgSession, newSaveId);

        var bgSessions = _sessionManager.GetBackgroundSessions();
        var topologyItems = BuildSessionTopology(fgSession);
        var topologyValue = SessionTopologyCodec.Join(topologyItems);

        // BeforeSave hooks may write blackboard data; the topology is a
        // framework-owned key, so it is written AFTER hooks fire to guarantee
        // the serialized value is the framework-computed one.
        ((SessionRun)fgSession).FireBeforeSaveHooks();
        _progressBlackboard.SetValue(WellKnownKeys.SessionTopology, topologyValue);

        var saveContext = new SaveContext(
            _progressBlackboard, fgSession.SessionBlackboard, _progressRuntime.SndWorld);

        var progressSmNode =
            ((StateMachineContainer)_progressStateMachines).SerializeToNode(_progressRuntime.ConverterRegistry);
        var sessionSmNode = ((StateMachineContainer)fgSession.GetSessionStateMachines())
            .SerializeToNode(_progressRuntime.ConverterRegistry);

        var payload = saveContext.SaveGame(
            ((SessionRun)fgSession).SceneHost,
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
        var fgSession = _sessionManager.ForegroundSession
            ?? throw new InvalidOperationException(
                "Cannot persist progress: no active foreground session. " +
                "Ensure a foreground session is loaded before calling PersistProgress.");

        var topologyItems = BuildSessionTopology(fgSession);
        _progressBlackboard.SetValue(WellKnownKeys.SessionTopology,
            SessionTopologyCodec.Join(topologyItems));

        var serializer = new SaveContext(_progressBlackboard, fgSession.SessionBlackboard, _progressRuntime.SndWorld);
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

    private void EnsureActiveLevelInvariant(ISessionRun fgSession, string saveId)
    {
        TopologyInvariant.EnsureActiveLevel(_progressBlackboard,
            fgSession.LevelId, $"save id: '{saveId}'");
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
