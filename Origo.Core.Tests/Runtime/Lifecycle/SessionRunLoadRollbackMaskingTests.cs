using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests: <c>SessionRun.LoadFromPayload</c> rollback cleanup
///     must run every cleanup step independently and must never mask the
///     original load failure with a cleanup failure (e.g. an
///     <c>OnUnmounted</c> hook that throws while observer bindings are torn
///     down).
/// </summary>
public class SessionRunLoadRollbackMaskingTests
{
    private const string FlushBoomIdx = "rollback.flush_boom";
    private const string UnmountBoomIdx = "rollback.unmount_boom";
    private const string DummyPopIdx = "rollback.dummy_pop";

    [Fact]
    public void LoadFromPayload_WhenFlushFails_OriginalExceptionSurvivesCleanupFailure()
    {
        var (ctx, logger) = CreateContext();

        var payload = new SaveGamePayload
        {
            SaveId = "004",
            ActiveLevelId = "target",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=target=false"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["target"] = new()
                {
                    LevelId = "target",
                    SndSceneNode = TestFactory.NodeFromJson(
                        """
                        [
                          {
                            "name": "OBS",
                            "node": { "pairs": {} },
                            "strategy": {
                              "lifecycle_indices": [],
                              "active_indices": [],
                              "observer_indices": [ { "TARGET": ["rollback.unmount_boom"] } ]
                            },
                            "data": { "pairs": {} }
                          },
                          {
                            "name": "TARGET",
                            "node": { "pairs": {} },
                            "strategy": { "lifecycle_indices": [], "active_indices": [] },
                            "data": { "pairs": {} }
                          }
                        ]
                        """),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson(
                        """
                        { "machines": [ { "key": "m1", "pushIndex": "rollback.flush_boom", "popIndex": "rollback.dummy_pop", "stack": ["state1"] } ] }
                        """)
                }
            }
        };

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(payload, "004", ctx.Runtime.Logger);
        ctx.Save.RequestLoadGame("004");

        // FlushAllAfterLoad throws (the state machine's push strategy fails),
        // then the rollback tears down the recovered observer binding whose
        // OnUnmounted hook throws. The original flush failure must propagate,
        // not the cleanup failure.
        var ex = Assert.ThrowsAny<Exception>(() => ctx.Deferred.FlushDeferredActionsForCurrentFrame());
        Assert.Contains("FLUSH_BOOM", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("UNMOUNT_BOOM", ex.Message, StringComparison.Ordinal);

        // Every cleanup step still runs: the scene host is empty and the
        // session blackboard is cleared.
        Assert.Empty(ctx.Runtime.SessionManager.ForegroundSession?.GetEntities() ?? []);
        Assert.Empty(ctx.Runtime.SessionManager.ForegroundSession?.SessionBlackboard.GetKeys() ?? []);

        // The cleanup failures themselves are surfaced in the log.
        Assert.Contains(logger.Warnings, w => w.Contains("UNMOUNT_BOOM", StringComparison.Ordinal));
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
        runtime.SndWorld.RegisterStrategy(() => new FlushBoomPushStrategy());
        runtime.SndWorld.RegisterStrategy(() => new UnmountBoomObserver());
        runtime.SndWorld.RegisterStrategy(() => new DummyPopStrategy());
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "initial", "entry.json"));
        host.BindWorld(runtime.SndWorld);
        host.BindContext(ctx);
        return (ctx, logger);
    }

    [StrategyIndex(FlushBoomIdx)]
    private sealed class FlushBoomPushStrategy : StateMachineStrategyBase
    {
        public override void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            throw new InvalidOperationException("FLUSH_BOOM");
    }

    [StrategyIndex(UnmountBoomIdx)]
    [ObserveData("any.key")]
    private sealed class UnmountBoomObserver : ObserverStrategyBase
    {
        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            throw new InvalidOperationException("UNMOUNT_BOOM");
    }

    [StrategyIndex(DummyPopIdx)]
    private sealed class DummyPopStrategy : StateMachineStrategyBase
    {
    }

    private sealed class LiveViewSndSceneHost : ISndSceneHost, ISndContextAttachableSceneHost, IOwningSessionBindable, IObserverTopologyHost
    {
        private readonly ILogger _logger;
        private readonly List<ISndEntity> _entities = [];
        private readonly HashSet<string> _pendingKill = [];
        private SndWorld? _world;
        private ISndContext? _context;
        private ISessionRun? _session;
        private ObserverTopology? _topology;

        internal LiveViewSndSceneHost(ILogger logger) => _logger = logger;

        ObserverTopology IObserverTopologyHost.ObserverTopology =>
            _topology ?? throw new InvalidOperationException("Observer topology not bound; BindWorld must run first.");

        internal void BindWorld(SndWorld world)
        {
            _world = world;
            _topology = new ObserverTopology(world.StrategyPool, _logger);
        }

        public void BindContext(ISndContext context) => _context = context;

        public void SetOwningSession(ISessionRun session) => _session = session;

        public ISndEntity CreateEntity(SndMetaData metaData)
        {
            _topology!.BindContext(_context!);
            var entity = _world!.CreateEntity(new TestNodeFactory(), _context!, _logger, _topology);
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
