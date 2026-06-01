using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Serialization;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    internal SaveMetaBuildContext BuildSaveMetaContext(string saveId) => _saveCoordinator.BuildSaveMetaContext(saveId);

    internal SaveGamePayload BuildSavePayload(
        string newSaveId,
        IReadOnlyDictionary<string, string>? mergedMeta = null) =>
        _saveCoordinator.BuildSavePayload(newSaveId, mergedMeta);

    internal void PersistProgress() => _saveCoordinator.PersistProgress();

    private sealed class SaveCoordinator
    {
        private readonly ProgressRun _owner;

        internal SaveCoordinator(ProgressRun owner)
        {
            _owner = owner;
        }

        internal SaveMetaBuildContext BuildSaveMetaContext(string saveId)
        {
            var fgSession = RequireForegroundSession();
            return new SaveMetaBuildContext(
                saveId,
                fgSession.LevelId,
                _owner.ProgressBlackboard,
                fgSession.SessionBlackboard,
                fgSession.SceneHost);
        }

        internal SaveGamePayload BuildSavePayload(
            string newSaveId,
            IReadOnlyDictionary<string, string>? mergedMeta)
        {
            var fgSession = RequireForegroundSession();
            EnsureActiveLevelInvariant(fgSession);

            var bgSessions = _owner._sessionManager.GetBackgroundSessions();
            var topologyItems = BuildSessionTopology(fgSession, bgSessions);
            _owner.ProgressBlackboard.Set(WellKnownKeys.SessionTopology, SessionTopologyCodec.Join(topologyItems));

            var saveContext = new SaveContext(
                _owner.ProgressBlackboard, fgSession.SessionBlackboard, _owner._progressRuntime.SndWorld);

            var progressSmNode =
                _owner.ProgressScope.StateMachines.SerializeToNode(_owner._progressRuntime.ConverterRegistry);
            var sessionSmNode = fgSession.GetSessionStateMachines()
                .SerializeToNode(_owner._progressRuntime.ConverterRegistry);

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
            var fgSession = _owner.ForegroundSession;
            if (fgSession is not null)
            {
                var bgSessions = _owner._sessionManager.GetBackgroundSessions();
                var topologyItems = BuildSessionTopology(fgSession, bgSessions);
                _owner.ProgressBlackboard.Set(WellKnownKeys.SessionTopology,
                    SessionTopologyCodec.Join(topologyItems));
            }

            var sessionBb = fgSession?.SessionBlackboard ?? new Blackboard.Blackboard();

            var serializer = new SaveContext(_owner.ProgressBlackboard, sessionBb, _owner._progressRuntime.SndWorld);
            var progressNode = serializer.SerializeProgress();
            var smNode = _owner.ProgressScope.StateMachines.SerializeToNode(_owner._progressRuntime.ConverterRegistry);

            _owner._progressRuntime.StorageService.WriteProgressOnlyToCurrent(progressNode, smNode);
        }

        private ISessionRun RequireForegroundSession() => _owner.ForegroundSession ??
                                                          throw new InvalidOperationException(
                                                              "No active foreground session.");

        private List<string> BuildSessionTopology(
            ISessionRun fgSession,
            IReadOnlyList<KeyValuePair<string, ISessionRun>> bgSessions)
        {
            var topologyItems = new List<string>
            {
                SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, fgSession.LevelId, false)
            };

            topologyItems.AddRange(bgSessions.Select(kvp =>
            {
                var syncProcess = _owner._sessionManager.GetSyncProcess(kvp.Key);
                return SessionTopologyCodec.Serialize(kvp.Key, kvp.Value.LevelId, syncProcess);
            }));

            return topologyItems;
        }

        private void AppendBackgroundPayloads(
            SaveGamePayload payload,
            IReadOnlyList<KeyValuePair<string, ISessionRun>> bgSessions)
        {
            var bgPayloads = _owner._sessionManager.SerializeBackgroundSessions();
            foreach (var kvp in bgSessions)
                if (bgPayloads.TryGetValue(kvp.Key, out var bgPayload))
                    payload.Levels[kvp.Value.LevelId] = bgPayload;
        }

        private void EnsureActiveLevelInvariant(ISessionRun fgSession)
        {
            var (found, rawTopology) = _owner.ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
            if (!found || string.IsNullOrWhiteSpace(rawTopology))
                throw new InvalidOperationException(
                    $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}' before save (save id: '{_owner.SaveId}').");

            var topologyActiveLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);
            if (!string.Equals(topologyActiveLevelId, fgSession.LevelId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Progress '{WellKnownKeys.SessionTopology}' foreground ('{topologyActiveLevelId}') does not match foreground level '{fgSession.LevelId}' (save id: '{_owner.SaveId}').");
        }
    }
}
