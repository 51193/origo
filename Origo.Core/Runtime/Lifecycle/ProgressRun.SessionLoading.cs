using Origo.Core.Runtime.StateMachine;
using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.StateMachine;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Session loading partial for <see cref="ProgressRun" />: load from
///     payload, mount foreground sessions, and switch foreground levels.
///     Delegates to <see cref="SessionLifecycle" />.
/// </summary>
internal sealed partial class ProgressRun
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

            try
            {
                foreach (var descriptor in topology)
                    MountSessionFromDescriptor(payload, descriptor);
            }
            catch (Exception ex)
            {
                _owner._progressRuntime.Logger.Log(LogLevel.Warning, nameof(ProgressRun),
                    new LogMessageBuilder()
                        .AddContext("saveId", _owner.SaveId)
                        .Build($"Session mount failed during load, all sessions cleared: {ex.Message}"));
                try
                {
                    // Dispose runs user hooks (BeforeQuit) which can throw on
                    // their own; a cleanup failure must never mask the
                    // original mount failure, so it is logged and dropped.
                    _owner._sessionManager.Clear();
                }
                catch (Exception cleanupEx)
                {
                    _owner._progressRuntime.Logger.Log(LogLevel.Warning, nameof(ProgressRun),
                        new LogMessageBuilder()
                            .AddContext("saveId", _owner.SaveId)
                            .Build($"Session cleanup after failed load failed: {cleanupEx.Message}"));
                }

                throw;
            }

            _ = _owner._sessionManager.ForegroundSession
                     ?? throw new InvalidOperationException("No active foreground session after topology restore.");

            // Re-solidify the topology from the live session set. The
            // foreground mount is finalized before background sessions are
            // mounted, so the intermediate value would otherwise be
            // foreground-only; the blackboard must always hold the complete,
            // recoverable snapshot of the current runtime state.
            WriteForegroundTopology(_owner.RequireForegroundSession().LevelId);
            _owner.EnsureActiveLevelInvariant();
        }

        internal ISessionRun LoadAndMountForeground(string levelId)
        {
            ValidateLevelId(levelId, nameof(levelId), "Level id cannot be null or whitespace.");

            var levelPayload = _owner._progressRuntime.StorageService.ResolveLevelPayload(_owner.SaveId, levelId);
            if (levelPayload is not null)
            {
                ValidateLevelPayload(levelId, levelPayload);
                return MountForegroundFromPayload(levelId, levelPayload, writeTopology: true);
            }

            return MountEmptyForeground(levelId, writeTopology: true);
        }

        internal void SwitchForeground(string newLevelId)
        {
            ValidateLevelId(newLevelId, nameof(newLevelId), "New level id cannot be null or whitespace.");

            PersistForegroundLevelState();
            PersistAndDestroyBackgroundIfExists(newLevelId);
            ResetForeground(true);
            try
            {
                LoadAndMountForeground(newLevelId);
            }
            catch
            {
                // The old foreground is already destroyed and persisted; a
                // failure while mounting the new one must not leave a
                // half-loaded foreground session mounted (it would be exposed
                // through ISessionManager.ForegroundSession with partial
                // state). Dispose it; cleanup failures are logged and never
                // mask the original mount exception.
                try
                {
                    _owner._sessionManager.DestroyForeground();
                }
                catch (Exception cleanupEx)
                {
                    _owner._progressRuntime.Logger.Log(LogLevel.Warning, nameof(ProgressRun),
                        new LogMessageBuilder()
                            .AddContext("saveId", _owner.SaveId)
                            .Build($"Foreground cleanup after failed switch failed: {cleanupEx.Message}"));
                }

                throw;
            }

            _owner.PersistProgress();
        }

        private void PersistForegroundLevelState()
        {
            var fg = _owner._sessionManager.ForegroundSession;
            if (fg is not null)
                _owner._sessionManager.PersistSession(ISessionManager.ForegroundKey);
        }

        private void PersistAndDestroyBackgroundIfExists(string levelId)
        {
            var bgSessions = _owner._sessionManager.GetBackgroundSessions();
            foreach (var kvp in bgSessions)
                if (string.Equals(kvp.Value.LevelId, levelId, StringComparison.Ordinal))
                {
                    _owner._sessionManager.PersistSession(kvp.Key);
                    _owner._sessionManager.DestroySession(kvp.Key);
                    return;
                }
        }

        private ISessionRun MountForegroundFromPayload(string levelId, LevelPayload levelPayload, bool writeTopology)
        {
            ResetForeground(false);

            var session = _owner._sessionManager.CreateForegroundFromPayload(levelId, levelPayload);
            return FinalizeForegroundMount(levelId, session, writeTopology);
        }

        private ISessionRun MountEmptyForeground(string levelId, bool writeTopology)
        {
            ResetForeground(true);

            var session =
                _owner._sessionManager.CreateForegroundSession(levelId);
            return FinalizeForegroundMount(levelId, session, writeTopology);
        }

        private ISessionRun FinalizeForegroundMount(string levelId, ISessionRun session, bool writeTopology)
        {
            FlushStateMachinesAfterSceneReady();
            if (writeTopology)
                WriteForegroundTopology(levelId);
            return session;
        }

        private void WriteForegroundTopology(string levelId)
        {
            ValidateLevelId(levelId, nameof(levelId), "Level id cannot be null or whitespace.");

            _owner.ProgressBlackboard.SetValue(WellKnownKeys.SessionTopology,
                SessionTopologyCodec.Join(_owner.BuildSessionTopology()));
        }

        private void FlushStateMachinesAfterSceneReady() =>
            // Only the progress-level container is flushed here. The
            // foreground session's container is flushed exactly once inside
            // SessionRun.LoadFromPayload (payload-restored stacks); freshly
            // mounted sessions have no restored stacks to flush. Flushing it
            // again would re-fire OnPushAfterLoad on every payload load.
            _owner.ProgressScope.StateMachines.FlushAllAfterLoad();

        private void ResetForeground(bool clearScene)
        {
            // Destroy first: SessionRun.Dispose is the single orchestrated
            // teardown path for the old foreground (BeforeQuit hooks, observer
            // binding teardown, strategy pool release, node teardown), and it
            // relies on the host's entity collection still being populated.
            // Clearing the host first would make ReleaseAllEntitiesAndClear a
            // no-op, silently skipping hooks and leaking strategy references
            // and stale observer bindings. Dispose's finally clears the scene
            // host regardless; ClearAdapterScene below covers the case where
            // no foreground session existed at all.
            _owner._sessionManager.DestroyForeground();
            if (clearScene)
                _owner._sessionManager.ClearAdapterScene();
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
            catch (Exception ex)
            {
                // IsNull forces lazy expansion of a DataSourceNode; expansion
                // failures must surface as an invalid-payload error carrying
                // the original parsing exception, not as an unrelated one.
                if (ex is InvalidOperationException)
                    throw;
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
                // The topology was deserialized from the progress payload and is
                // already complete; the foreground mount must not overwrite it
                // before background sessions have mounted. The full topology is
                // re-solidified once after the mount loop in LoadFromPayload.
                if (payload.Levels.TryGetValue(descriptor.LevelId, out var fgPayload))
                    MountForegroundFromPayload(descriptor.LevelId, fgPayload, writeTopology: false);
                else
                    MountEmptyForeground(descriptor.LevelId, writeTopology: false);
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
                return [];

            return SessionTopologyCodec.Parse(raw);
        }
    }
}
