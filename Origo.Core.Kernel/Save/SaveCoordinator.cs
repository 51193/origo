using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;
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
        ProgressRuntime progressRuntime)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(progressBlackboard);
        ArgumentNullException.ThrowIfNull(progressStateMachines);
        ArgumentNullException.ThrowIfNull(progressRuntime);
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
        try
        {
            ((SessionRun)fgSession).FireBeforeSaveHooks();
        }
        catch
        {
            // A failed foreground hook may have left a framework-owned key
            // overwritten; restore it before propagating so the next save is
            // not poisoned by this failed attempt.
            TryRestoreSessionTopology(topologyValue);
            throw;
        }

        _progressBlackboard.SetValue(WellKnownKeys.SessionTopology, topologyValue);

        // Serialize background sessions before the progress payload: their
        // BeforeSave hooks may write progress blackboard/state-machine state,
        // and those writes must be captured by the progress serialization below.
        Dictionary<string, LevelPayload>? bgPayloads = null;
        SaveGamePayload? payload = null;
        DataSourceNode? progressSmNode = null;
        DataSourceNode? sessionSmNode = null;
        try
        {
            try
            {
                bgPayloads = new Dictionary<string, LevelPayload>(
                    _sessionManager.SerializeBackgroundSessions(), StringComparer.Ordinal);
            }
            catch
            {
                // Background hooks run here, after the foreground hooks and
                // the initial framework write. Restore the framework value even
                // when a hook throws so a failed save cannot leave a corrupted
                // topology in the progress blackboard for the next attempt.
                TryRestoreSessionTopology(topologyValue);
                throw;
            }

            // Reassert the framework-owned topology after ALL BeforeSave hooks
            // (foreground and background) have run. A background hook may write
            // this key; its write must never reach the serialized progress
            // blackboard or override the complete session set.
            _progressBlackboard.SetValue(WellKnownKeys.SessionTopology, topologyValue);

            var saveContext = new SaveContext(
                _progressBlackboard, fgSession.SessionBlackboard, _progressRuntime.SndWorld);

            progressSmNode =
                ((StateMachineContainer)_progressStateMachines).SerializeToNode(_progressRuntime.ConverterRegistry);
            sessionSmNode = ((StateMachineContainer)fgSession.GetSessionStateMachines())
                .SerializeToNode(_progressRuntime.ConverterRegistry);

            payload = saveContext.SaveGame(
                ((SessionRun)fgSession).SceneHost,
                newSaveId,
                fgSession.LevelId,
                mergedMeta,
                progressSmNode,
                sessionSmNode);

            AppendBackgroundPayloads(payload, bgSessions, bgPayloads);
            var builtPayload = payload;
            payload = null;
            return builtPayload;
        }
        catch
        {
            if (payload is not null)
            {
                SavePayloadDisposal.Dispose(payload);
            }
            else
            {
                // SaveGame did not return ownership of these caller-owned nodes.
                progressSmNode?.Dispose();
                sessionSmNode?.Dispose();
            }

            if (bgPayloads is not null)
                foreach (var bgPayload in bgPayloads.Values)
                    SavePayloadDisposal.Dispose(bgPayload);
            throw;
        }
    }

    private void TryRestoreSessionTopology(string topologyValue)
    {
        try
        {
            _progressBlackboard.SetValue(WellKnownKeys.SessionTopology, topologyValue);
        }
        catch (Exception ex)
        {
            _progressRuntime.Logger.Log(LogLevel.Warning, nameof(SaveCoordinator),
                new LogMessageBuilder()
                    .AddContext("topologyKey", WellKnownKeys.SessionTopology)
                    .Build($"Failed to restore the framework session topology after a failed save: {ex.Message}"));
        }
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
        using var progressNode = serializer.SerializeProgress();
        using var smNode = ((StateMachineContainer)_progressStateMachines)
            .SerializeToNode(_progressRuntime.ConverterRegistry);

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

        // The topology string is persisted into the progress blackboard and
        // hashed as a text node. Dictionary enumeration order is not part of
        // the logical session set, so sort background entries by key; the same
        // sessions must produce the same payload hash (canonical save).
        topologyItems.AddRange(bgSessions
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp =>
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

    private static void AppendBackgroundPayloads(
        SaveGamePayload payload,
        IReadOnlyList<KeyValuePair<string, ISessionRun>> bgSessions,
        Dictionary<string, LevelPayload> bgPayloads)
    {
        foreach (var kvp in bgSessions)
        {
            if (!bgPayloads.TryGetValue(kvp.Key, out var bgPayload))
                throw new InvalidOperationException(
                    $"Cannot persist background session '{kvp.Key}': it disappeared between the " +
                    "session snapshot and payload serialization. Destroy sessions only through " +
                    "the session manager, and do not mutate sessions during a save.");

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
