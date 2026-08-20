using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests: SessionRun's batch hook iteration must tolerate
///     entities being spawned inside a hook (AfterLoad / BeforeSave /
///     BeforeQuit). Adapter hosts expose a live entity view (the Godot
///     adapter's <c>SndEntityCollection</c> returns the backing list), so
///     iterating it directly would throw "Collection was modified" and fail
///     the load/save/dispose operation.
/// </summary>
public class SessionRunHookIterationTests
{
    private const string AfterLoadSpawnIdx = "hook_iter.after_load_spawn";
    private const string BeforeSaveSpawnIdx = "hook_iter.before_save_spawn";
    private const string BeforeQuitSpawnIdx = "hook_iter.before_quit_spawn";
    private const string InfiniteSpawnIdx = "hook_iter.infinite_spawn";
    private const string NormalIdx = "hook_iter.normal";

    [Fact]
    public void LoadFromPayload_AfterLoadHookSpawnsEntity_DoesNotThrow()
    {
        var (ctx, fs, _) = CreateContext();

        fs.SeedFile("root/current/level_level_a/snd_scene.json",
            """
            [
              {
                "name": "A",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": ["hook_iter.after_load_spawn"], "active_indices": [] },
                "data": { "pairs": {} }
              }
            ]
            """);
        fs.SeedFile("root/current/level_level_a/session.json", "{}");
        fs.SeedFile("root/current/level_level_a/session_state_machines.json", "{\"machines\":[]}");

        var fg = (SessionRun)ctx.EnsureProgressRun().LoadAndMountForeground("level_a");

        // The AfterLoad hook spawned entity B; both must be present and the
        // load must have completed.
        var names = fg.GetEntities().Select(e => e.Name).ToList();
        Assert.Contains("A", names);
        Assert.Contains("B", names);
    }

    [Fact]
    public void BuildLevelPayload_BeforeSaveHookSpawnsEntity_DoesNotThrow()
    {
        var (ctx, _, _) = CreateContext();
        var session = (SessionRun)ctx.EnsureProgressRun().LoadAndMountForeground("test_level");
        session.Spawn(CreateMeta("A", BeforeSaveSpawnIdx));

        // The save pipeline fires the BeforeSave hooks during payload
        // construction; a hook that spawns an entity must not break
        // serialization, and the spawned entity must survive the round trip.
        ctx.Save.RequestSaveGame("hook_save");
        ctx.FlushFrame();

        ctx.Save.RequestLoadGame("hook_save");
        ctx.FlushFrame();

        var names = ctx.Runtime.SessionManager.ForegroundSession!.GetEntities()
            .Select(e => e.Name).ToList();
        Assert.Contains("A", names);
        Assert.Contains("B", names);
    }

    [Fact]
    public void Dispose_BeforeQuitHookSpawnsEntity_DoesNotThrowAndReleasesEverything()
    {
        var (ctx, _, logger) = CreateContext();
        var session = (SessionRun)ctx.EnsureProgressRun().LoadAndMountForeground("test_level");
        session.Spawn(CreateMeta("A", BeforeQuitSpawnIdx));

        session.Dispose();

        // Both the original entity and the one spawned inside the quit hook
        // must have been released: no strategy references may remain.
        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    [Fact]
    public void Dispose_QuitHookSpawnsForever_FailsLoudlyInsteadOfHanging()
    {
        var (ctx, _, _) = CreateContext();
        var session = (SessionRun)ctx.EnsureProgressRun().LoadAndMountForeground("test_level");
        session.Spawn(CreateMeta("A", InfiniteSpawnIdx));

        // A quit hook that keeps spawning entities is business-code pathology;
        // the teardown must fail loudly after a bounded number of passes
        // instead of hanging disposal (or silently leaking references).
        var ex = Assert.Throws<InvalidOperationException>(() => session.Dispose());
        Assert.Contains("did not converge", ex.Message, StringComparison.Ordinal);
    }

    private static (SndContext ctx, TestMemoryFileSystem fs, TestLogger logger) CreateContext()
    {
        var logger = new TestLogger();
        var host = new LiveViewSndSceneHost(logger);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var tm = new TypeStringMapping();
        var systemBb = new Blackboard.Blackboard();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, systemBb, dataSourceIo);
        runtime.SndWorld.RegisterStrategy(() => new AfterLoadSpawnStrategy());
        runtime.SndWorld.RegisterStrategy(() => new BeforeSaveSpawnStrategy());
        runtime.SndWorld.RegisterStrategy(() => new BeforeQuitSpawnStrategy());
        runtime.SndWorld.RegisterStrategy(() => new InfiniteSpawnStrategy());
        runtime.SndWorld.RegisterStrategy(() => new NormalStrategy());

        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "res://initial", "res://entry/entry.json"));
        host.BindWorld(runtime.SndWorld);
        host.BindContext(ctx);

        var progressRun = TestFactory.CreateProgressRun(
            "test_save", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);

        return (ctx, fs, logger);
    }

    private static SndMetaData CreateMeta(string name, string lifecycleIndex) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [lifecycleIndex] },
            DataMetaData = new DataMetaData()
        };

    // ── Test strategies ────────────────────────────────────────────────

    [StrategyIndex(AfterLoadSpawnIdx)]
    private sealed class AfterLoadSpawnStrategy : LifecycleStrategyBase
    {
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) =>
            entity.OwningSession.Spawn(CreateMeta("B", NormalIdx));
    }

    [StrategyIndex(BeforeSaveSpawnIdx)]
    private sealed class BeforeSaveSpawnStrategy : LifecycleStrategyBase
    {
        public override void BeforeSave(ISndEntity entity, ISndContext ctx) =>
            entity.OwningSession.Spawn(CreateMeta("B", NormalIdx));
    }

    [StrategyIndex(BeforeQuitSpawnIdx)]
    private sealed class BeforeQuitSpawnStrategy : LifecycleStrategyBase
    {
        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            entity.OwningSession.Spawn(CreateMeta("B", NormalIdx));
    }

    [StrategyIndex(InfiniteSpawnIdx)]
    private sealed class InfiniteSpawnStrategy : LifecycleStrategyBase
    {
        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            entity.OwningSession.Spawn(CreateMeta("B", InfiniteSpawnIdx));
    }

    [StrategyIndex(NormalIdx)]
    private sealed class NormalStrategy : LifecycleStrategyBase
    {
    }

    // ── Live-view scene host: GetEntities returns the backing list ─────

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
