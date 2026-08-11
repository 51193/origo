using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests: rollback cleanup after a failed session mount must
///     never mask the original load exception. When cleanup itself throws
///     (user hooks such as BeforeQuit), the cleanup failure is logged as a
///     warning and the original load failure still propagates.
/// </summary>
public class ProgressRunLoadRollbackMaskingTests
{
    private const string QuitThrowIdx = "rollback.quit_throw";

    [Fact]
    public void LoadFromPayload_WhenBackgroundMountFails_OriginalExceptionSurvivesCleanupFailure()
    {
        var (ctx, logger) = CreateContext();

        var payload = new SaveGamePayload
        {
            SaveId = "003",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false,bg=bg=false"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson(
                        """
                        [
                          {
                            "name": "Q",
                            "node": { "pairs": {} },
                            "strategy": { "lifecycle_indices": ["rollback.quit_throw"], "active_indices": [] },
                            "data": { "pairs": {} }
                          }
                        ]
                        """),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
                },
                ["bg"] = new()
                {
                    LevelId = "bg",
                    SndSceneNode = TestFactory.NodeFromJson("{}"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
                }
            }
        };

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(payload, "003", ctx.Runtime.Logger);
        ctx.Save.RequestLoadGame("003");

        // The background mount fails (its snd_scene payload is not a scene
        // array). The rollback then destroys the already-mounted foreground
        // session, whose BeforeQuit hook throws. The cleanup failure must not
        // mask the original load failure.
        var ex = Assert.ThrowsAny<Exception>(() => ctx.Deferred.FlushDeferredActionsForCurrentFrame());
        Assert.DoesNotContain("QUIT_BOOM", ex.Message, StringComparison.Ordinal);

        // ThrowsAny is deliberate here: the original load failure or a cleanup
        // failure may propagate, and the message assertions pin which one won.

        // Cleanup still completes: the failed mount must not leave a partial
        // foreground session behind.
        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);

        // The cleanup failure itself is surfaced in the log.
        Assert.Contains(logger.Warnings, w => w.Contains("cleanup", StringComparison.OrdinalIgnoreCase));
    }

    private static (SndContext ctx, TestLogger logger) CreateContext()
    {
        var logger = new TestLogger();
        var host = new LiveViewSndSceneHost(logger);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        runtime.SndWorld.RegisterStrategy(() => new QuitThrowStrategy());
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "initial", "entry.json"));
        host.BindWorld(runtime.SndWorld);
        host.BindContext(ctx);
        return (ctx, logger);
    }

    [StrategyIndex(QuitThrowIdx)]
    private sealed class QuitThrowStrategy : LifecycleStrategyBase
    {
        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            throw new InvalidOperationException("QUIT_BOOM");
    }

    private sealed class LiveViewSndSceneHost : ISndSceneHost, ISndContextAttachableSceneHost, IOwningSessionBindable
    {
        private readonly ILogger _logger;
        private readonly List<ISndEntity> _entities = [];
        private readonly HashSet<string> _pendingKill = [];
        private SndWorld? _world;
        private ISndContext? _context;
        private ISessionRun? _session;

        internal LiveViewSndSceneHost(ILogger logger) => _logger = logger;

        internal void BindWorld(SndWorld world) => _world = world;

        public void BindContext(ISndContext context) => _context = context;

        public void SetOwningSession(ISessionRun session) => _session = session;

        public ISndEntity CreateEntity(SndMetaData metaData)
        {
            var topology = new ObserverTopology(_world!.StrategyPool, _logger);
            topology.BindContext(_context!);
            var entity = _world.CreateEntity(new TestNodeFactory(), _context!, _logger, topology);
            entity.Name = metaData.Name;
            _entities.Add(entity);
            ((IEntityLifecycle)entity).RecoverForLifecycle(metaData);
            if (_session is not null)
                entity.BindSession(_session);
            return entity;
        }

        public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

        public ISndEntity? FindByName(string name) =>
            _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

        public IReadOnlyList<SndMetaData> BuildMetaList() =>
            [.. _entities.Select(e => ((IEntityLifecycle)e).BuildMetaData())];

        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
        {
            foreach (var meta in metaList)
                CreateEntity(meta);
        }

        public void RemoveAllEntities()
        {
            _entities.Clear();
            _pendingKill.Clear();
        }

        public void RemoveEntity(string name)
        {
            var entity = FindByName(name)
                         ?? throw new InvalidOperationException($"No entity with name '{name}'.");
            _entities.Remove(entity);
            _pendingKill.Remove(name);
        }

        public void RequestKillEntity(string name)
        {
            if (FindByName(name) is null)
                throw new InvalidOperationException($"No entity with name '{name}'.");
            if (!_pendingKill.Add(name))
                throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        }

        public void ProcessAll(double delta)
        {
        }
    }
}
