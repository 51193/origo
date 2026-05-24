using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    internal void LoadFromPayload(SaveGamePayload payload) => _sessionLifecycle.LoadFromPayload(payload);

    internal ISessionRun LoadAndMountForeground(string levelId) => _sessionLifecycle.LoadAndMountForeground(levelId);

    internal void SwitchForeground(string newLevelId) => _sessionLifecycle.SwitchForeground(newLevelId);

    private sealed class SessionLifecycle
    {
        private readonly ProgressRun _owner;

        internal SessionLifecycle(ProgressRun owner)
        {
            _owner = owner;
        }

        internal void LoadFromPayload(SaveGamePayload payload)
        {
            ArgumentNullException.ThrowIfNull(payload);

            var saveContext = new SaveContext(
                _owner.ProgressBlackboard, new Blackboard.Blackboard(), _owner._progressRuntime.SndWorld);
            saveContext.DeserializeProgress(payload.ProgressNode);

            if (payload.ProgressStateMachinesNode.IsNull)
                throw new InvalidOperationException("Save payload missing required ProgressStateMachinesNode.");

            _owner.ProgressScope.StateMachines.DeserializeFromNode(
                payload.ProgressStateMachinesNode,
                _owner._progressRuntime.ConverterRegistry);

            var topology = ParseSessionTopologyFromProgress();
            if (topology.Count == 0)
                throw new InvalidOperationException(
                    $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}' before load.");

            _owner._sessionManager.Clear();
            try
            {
                foreach (var descriptor in topology)
                    MountSessionFromDescriptor(payload, descriptor);
            }
            catch
            {
                _owner._sessionManager.Clear();
                throw;
            }

            var fg = _owner.ForegroundSession
                     ?? throw new InvalidOperationException("No active foreground session after topology restore.");
            VerifyProgressActiveLevelInvariant(fg.LevelId);
        }

        internal ISessionRun LoadAndMountForeground(string levelId)
        {
            ValidateLevelId(levelId, nameof(levelId), "Level id cannot be null or whitespace.");

            var levelPayload = _owner._progressRuntime.StorageService.ResolveLevelPayload(_owner.SaveId, levelId);
            if (levelPayload is not null)
            {
                ValidateLevelPayload(levelId, levelPayload);
                return MountForegroundFromPayload(levelId, levelPayload);
            }

            return MountEmptyForeground(levelId);
        }

        internal void SwitchForeground(string newLevelId)
        {
            ValidateLevelId(newLevelId, nameof(newLevelId), "New level id cannot be null or whitespace.");

            _owner.PersistProgress();
            ResetForeground(true);
            LoadAndMountForeground(newLevelId);
        }

        private ISessionRun MountForegroundFromPayload(string levelId, LevelPayload levelPayload)
        {
            ResetForeground(false);

            var session = _owner._sessionManager.CreateForegroundFromPayload(
                levelId, _owner._progressRuntime.ForegroundSceneHost, levelPayload);
            return FinalizeForegroundMount(levelId, session);
        }

        private ISessionRun MountEmptyForeground(string levelId)
        {
            ResetForeground(true);

            var session =
                _owner._sessionManager.CreateForegroundSession(levelId, _owner._progressRuntime.ForegroundSceneHost);
            return FinalizeForegroundMount(levelId, session);
        }

        private ISessionRun FinalizeForegroundMount(string levelId, ISessionRun session)
        {
            FlushStateMachinesAfterSceneReady();
            WriteForegroundTopology(levelId);
            return session;
        }

        private void WriteForegroundTopology(string levelId)
        {
            ValidateLevelId(levelId, nameof(levelId), "Level id cannot be null or whitespace.");
            _owner.ProgressBlackboard.Set(WellKnownKeys.SessionTopology,
                SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, levelId, false));
        }

        private void VerifyProgressActiveLevelInvariant(string expectedActiveLevelId)
        {
            var fg = _owner.ForegroundSession
                     ?? throw new InvalidOperationException("No active foreground session after load.");

            if (!string.Equals(fg.LevelId, expectedActiveLevelId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Foreground session level '{fg.LevelId}' does not match expected active level '{expectedActiveLevelId}'.");

            var (found, rawTopology) = _owner.ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
            if (!found || string.IsNullOrWhiteSpace(rawTopology))
                throw new InvalidOperationException(
                    $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}': expected foreground '{expectedActiveLevelId}'.");

            var topologyActiveLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);
            if (!string.Equals(topologyActiveLevelId, expectedActiveLevelId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Progress '{WellKnownKeys.SessionTopology}' foreground ('{topologyActiveLevelId}') does not match expected active level '{expectedActiveLevelId}'.");
        }

        private void FlushStateMachinesAfterSceneReady()
        {
            _owner.ProgressScope.StateMachines.FlushAllAfterLoad();
            _owner.ForegroundSession?.GetSessionStateMachines().FlushAllAfterLoad();
        }

        private void ResetForeground(bool clearScene)
        {
            _owner._sessionManager.DestroyForeground();
            if (clearScene)
                _owner._progressRuntime.SndRuntime.ClearAll();
        }

        private static void ValidateLevelId(string levelId, string paramName, string message)
        {
            if (string.IsNullOrWhiteSpace(levelId))
                throw new ArgumentException(message, paramName);
        }

        private void ValidateLevelPayload(string levelId, LevelPayload payload)
        {
            EnsureNodeValid(payload.SndSceneNode, levelId, "snd_scene.json");
            EnsureNodeValid(payload.SessionNode, levelId, "session.json");
            EnsureNodeValid(payload.SessionStateMachinesNode, levelId, "session_state_machines.json");

            _ = _owner._progressRuntime.ConverterRegistry.Read<StateMachineContainerPayload>(
                    payload.SessionStateMachinesNode)
                ?? throw new InvalidOperationException(
                    $"Target level '{levelId}' has invalid session state machines json (null payload).");
        }

        private static void EnsureNodeValid(DataSourceNode node, string levelId, string fileName)
        {
            try
            {
                if (node.IsNull)
                    throw new InvalidOperationException(
                        $"Target level '{levelId}' has invalid {fileName} (empty).");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Target level '{levelId}' has invalid {fileName} (empty).", ex);
            }
        }

        private void MountSessionFromDescriptor(
            SaveGamePayload payload,
            SessionTopologyCodec.SessionDescriptor descriptor)
        {
            if (string.Equals(descriptor.Key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
            {
                if (payload.Levels.TryGetValue(descriptor.LevelId, out var fgPayload))
                    MountForegroundFromPayload(descriptor.LevelId, fgPayload);
                else
                    MountEmptyForeground(descriptor.LevelId);
                return;
            }

            _owner._sessionManager.CreateBackgroundSession(
                descriptor.Key, descriptor.LevelId, descriptor.SyncProcess);
            if (payload.Levels.TryGetValue(descriptor.LevelId, out var bgPayload))
                _owner._sessionManager.LoadSessionFromPayload(descriptor.Key, bgPayload);
        }

        private List<SessionTopologyCodec.SessionDescriptor> ParseSessionTopologyFromProgress()
        {
            var (found, raw) = _owner.ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
            if (!found || string.IsNullOrWhiteSpace(raw))
                return new List<SessionTopologyCodec.SessionDescriptor>();

            return SessionTopologyCodec.Parse(raw);
        }
    }
}
