using Origo.Core.Runtime.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Serialization;
using Origo.Core.Abstractions.Lifecycle;

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
            var fgSession = _owner.RequireForegroundSession();
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
            var fgSession = _owner.RequireForegroundSession();
            _owner.EnsureActiveLevelInvariant();

            var bgSessions = _owner._sessionManager.GetBackgroundSessions();
            var topologyItems = _owner.BuildSessionTopology();
            _owner.ProgressBlackboard.Set(WellKnownKeys.SessionTopology, SessionTopologyCodec.Join(topologyItems));

            var saveContext = new SaveContext(
                _owner.ProgressBlackboard, fgSession.SessionBlackboard, _owner._progressRuntime.SndWorld);

            var progressSmNode =
                _owner.ProgressScope.StateMachines.SerializeToNode(_owner._progressRuntime.ConverterRegistry);
            var sessionSmNode = ((StateMachineContainer)fgSession.GetSessionStateMachines())
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
                var topologyItems = _owner.BuildSessionTopology();
                _owner.ProgressBlackboard.Set(WellKnownKeys.SessionTopology,
                    SessionTopologyCodec.Join(topologyItems));
            }

            var sessionBb = fgSession?.SessionBlackboard ?? new Blackboard.Blackboard();

            var serializer = new SaveContext(_owner.ProgressBlackboard, sessionBb, _owner._progressRuntime.SndWorld);
            var progressNode = serializer.SerializeProgress();
            var smNode = _owner.ProgressScope.StateMachines.SerializeToNode(_owner._progressRuntime.ConverterRegistry);

            _owner._progressRuntime.StorageService.WriteProgressOnlyToCurrent(progressNode, smNode);
        }

        private void AppendBackgroundPayloads(
            SaveGamePayload payload,
            IReadOnlyList<KeyValuePair<string, ISessionRun>> bgSessions)
        {
            var bgPayloads = _owner._sessionManager.SerializeBackgroundSessions();
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
}
