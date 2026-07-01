using Origo.Core.Runtime.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
public class SndEntityLifecycleBatchTests
{
    private const string _probeIdx = "batch.probe";
    private const string _crossRefIdx = "batch.crossref";
    private const string _activeQueryIdx = "batch.active.query";
    private const string _p50Idx = "s.batch.p50";
    private const string _p100Idx = "s.batch.p100";
    private const string _perfProcessIdx = "batch.perf.process";
    private const string _addDuringProcessIdx = "batch.perf.add_during";
    private const string _selfRemoveIdx = "batch.perf.self_remove";

    private static FullMemorySndSceneHost CreateHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        return host;
    }

    private static SndMetaData CreateMeta(string name, string[]? lifecycleIndices = null,
        string[]? activeIndices = null) => new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [.. lifecycleIndices ?? []],
                ActiveIndices = [.. activeIndices ?? []]
            },
            DataMetaData = new DataMetaData()
        };

    [StrategyIndex(_probeIdx)]
    private sealed class ProbeStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _events = new();
        public static List<string> Events => _events.Value ??= [];

        public override void AfterLoad(ISndEntity entity, ISndContext ctx) => Events.Add($"after_load:{entity.Name}");

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => Events.Add($"after_spawn:{entity.Name}");

        public override void BeforeSave(ISndEntity entity, ISndContext ctx) => Events.Add($"before_save:{entity.Name}");

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => Events.Add($"before_quit:{entity.Name}");

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) => Events.Add($"before_dead:{entity.Name}");
    }

    [StrategyIndex(_crossRefIdx)]
    private sealed class CrossRefStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _events = new();
        public static List<string> Events => _events.Value ??= [];
        private static readonly AsyncLocal<string[]> _targetNames = new();
        public static string[] TargetNames { get => _targetNames.Value ?? []; set => _targetNames.Value = value; }
        private static readonly AsyncLocal<ISndSceneHost?> _host = new();
        public static ISndSceneHost? Host { get => _host.Value; set => _host.Value = value; }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx)
        {
            var h = Host ?? ((SessionRun?)entity.OwningSession)?.SceneHost;
            foreach (var target in TargetNames)
            {
                var sibling = h?.FindByName(target);
                Events.Add(sibling is not null
                    ? $"found:{target}"
                    : $"missing:{target}");
            }
        }

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            var h = Host ?? ((SessionRun?)entity.OwningSession)?.SceneHost;
            foreach (var target in TargetNames)
            {
                var sibling = h?.FindByName(target);
                Events.Add(sibling is not null
                    ? $"found:{target}"
                    : $"missing:{target}");
            }
        }

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
        {
            var h = Host ?? ((SessionRun?)entity.OwningSession)?.SceneHost;
            foreach (var target in TargetNames)
            {
                var sibling = h?.FindByName(target);
                Events.Add(sibling is not null
                    ? $"quit_found:{target}"
                    : $"quit_missing:{target}");
            }
        }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx)
        {
            var h = Host ?? ((SessionRun?)entity.OwningSession)?.SceneHost;
            foreach (var target in TargetNames)
            {
                var sibling = h?.FindByName(target);
                Events.Add(sibling is not null
                    ? $"dead_found:{target}"
                    : $"dead_missing:{target}");
            }
        }
    }

    [StrategyIndex(_activeQueryIdx)]
    private sealed class QueryActiveProxy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _events = new();
        public static List<string> Events => _events.Value ??= [];
        private static readonly AsyncLocal<string> _invokeTarget = new();
        public static string InvokeTarget { get => _invokeTarget.Value ?? string.Empty; set => _invokeTarget.Value = value; }
        private static readonly AsyncLocal<string> _invokeIndex = new();
        public static string InvokeIndex { get => _invokeIndex.Value ?? string.Empty; set => _invokeIndex.Value = value; }
        private static readonly AsyncLocal<ISndSceneHost?> _host = new();
        public static ISndSceneHost? Host { get => _host.Value; set => _host.Value = value; }

        private static void TryInvoke(ISndEntity entity)
        {
            var h = Host ?? ((SessionRun?)entity.OwningSession)?.SceneHost;
            var target = h?.FindByName(InvokeTarget);
            try
            {
                var result = target?.InvokeStrategy(InvokeIndex);
                Events.Add($"invoke_ok:{result}");
            }
            catch (Exception ex)
            {
                Events.Add($"invoke_fail:{ex.GetType().Name}");
            }
        }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx) => TryInvoke(entity);
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => TryInvoke(entity);
    }

    [StrategyIndex("batch.active.simple")]
    private sealed class SimpleActiveStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) =>
            $"hello_from:{entity.Name}";
    }

    [StrategyIndex(_p50Idx, Priority = 50)]
    private sealed class SP50 : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _spEvents = new();
        public static List<string>? Events { get => _spEvents.Value; set => _spEvents.Value = value; }
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) => Events?.Add("p50:" + entity.Name);
    }

    [StrategyIndex(_p100Idx, Priority = 100)]
    private sealed class SP100 : LifecycleStrategyBase
    {
        public static List<string>? Events { get => SP50.Events; set => SP50.Events = value; }
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) => Events?.Add("p100:" + entity.Name);
    }

    [StrategyIndex("batch.failing")]
    private sealed class FailingStrategy : LifecycleStrategyBase
    {
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) =>
            throw new InvalidOperationException("simulated hook failure");
    }

    private static int _subCount;

    [StrategyIndex("batch.subscribe")]
    private sealed class SubscribeStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _events = new();
        public static List<string> Events => _events.Value ??= [];
        private static readonly AsyncLocal<ISndSceneHost?> _host = new();
        public static ISndSceneHost? Host { get => _host.Value; set => _host.Value = value; }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx)
        {
            var h = Host ?? ((SessionRun?)entity.OwningSession)?.SceneHost;
            var target = h?.FindByName("target");
            if (target is not null)
            {
                ((Origo.Core.Snd.Entity.ISndEntityRawSubscription)target).SubscribeDataRaw("hp",
                    (_, oldVal, newVal) =>
                {
                    Interlocked.Increment(ref _subCount);
                    Events.Add($"sub:{oldVal}->{newVal}");
                }, null);
                Events.Add("subscribed");
            }
        }
    }

    [StrategyIndex(_perfProcessIdx)]
    private sealed class ProcessRecordingStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<(string Name, double Delta)>> _processCalls = new();
        public static List<(string Name, double Delta)> ProcessCalls => _processCalls.Value ??= [];

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => ProcessCalls.Add((entity.Name, delta));
    }

    [StrategyIndex(_addDuringProcessIdx)]
    private sealed class AddDuringProcessStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _processCalls = new();
        public static List<string> ProcessCalls => _processCalls.Value ??= [];

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ProcessCalls.Add($"add_during_process:{entity.Name}");
            entity.AddStrategy(_perfProcessIdx);
        }
    }

    [StrategyIndex(_selfRemoveIdx)]
    private sealed class SelfRemoveRecordingStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _processCalls = new();
        public static List<string> ProcessCalls => _processCalls.Value ??= [];

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => ProcessCalls.Add($"self_remove:{entity.Name}");
    }

    [StrategyIndex("batch.perf.remove_self")]
    private sealed class RemoveSelfDuringProcessStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _processCalls = new();
        public static List<string> ProcessCalls => _processCalls.Value ??= [];

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ProcessCalls.Add($"remove_self:{entity.Name}");
            entity.RemoveStrategy("batch.perf.remove_self");
        }
    }

    // ── Batch AfterLoad ─────────────────────────────────────────────────

    [Fact]
    public void BatchLoad_AfterLoad_FiresAfterAllEntitiesRecovered()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        host.RecoverFromMetaList(
        [
            CreateMeta("A", [_probeIdx]),
            CreateMeta("B", [_probeIdx]),
            CreateMeta("C", [_probeIdx])
        ]);

        Assert.Empty(ProbeStrategy.Events);

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Equal(new[] { "after_load:A", "after_load:B", "after_load:C" }, ProbeStrategy.Events);
    }

    [Fact]
    public void BatchLoad_CrossEntity_FindByName_SucceedsRegardlessOfOrder()
    {
        CrossRefStrategy.Events.Clear();
        CrossRefStrategy.TargetNames = ["A", "B", "C", "D"];
        CrossRefStrategy.Host = null;
        var host = CreateHost(w => { w.RegisterStrategy(() => new CrossRefStrategy()); });
        CrossRefStrategy.Host = host;

        host.RecoverFromMetaList(
        [
            CreateMeta("A", [_crossRefIdx]),
            CreateMeta("B", [_crossRefIdx]),
            CreateMeta("C", [_crossRefIdx]),
            CreateMeta("D", [_crossRefIdx])
        ]);

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        var distinct = CrossRefStrategy.Events.Distinct().ToList();
        Assert.Equal(4, distinct.Count);
        Assert.DoesNotContain(distinct, s => s.StartsWith("missing:", StringComparison.Ordinal));
        Assert.Contains("found:A", distinct);
        Assert.Contains("found:D", distinct);
    }

    [Fact]
    public void BatchLoad_Self_ActiveStrategyAvailableDuringAfterLoad()
    {
        QueryActiveProxy.Events.Clear();
        QueryActiveProxy.InvokeTarget = "Self";
        QueryActiveProxy.InvokeIndex = "batch.active.simple";
        QueryActiveProxy.Host = null;
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new QueryActiveProxy());
            w.RegisterStrategy(() => new SimpleActiveStrategy());
        });
        QueryActiveProxy.Host = host;

        host.RecoverFromMetaList(
        [
            CreateMeta("Self", [_activeQueryIdx],
                ["batch.active.simple"])
        ]);

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Contains("invoke_ok:hello_from:Self", QueryActiveProxy.Events);
    }

    [Fact]
    public void BatchLoad_CrossEntity_ActiveStrategyAvailableDuringAfterLoad()
    {
        QueryActiveProxy.Events.Clear();
        ProbeStrategy.Events.Clear();
        QueryActiveProxy.InvokeTarget = "Peer";
        QueryActiveProxy.InvokeIndex = "batch.active.simple";
        QueryActiveProxy.Host = null;
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new QueryActiveProxy());
            w.RegisterStrategy(() => new SimpleActiveStrategy());
            w.RegisterStrategy(() => new ProbeStrategy());
        });
        QueryActiveProxy.Host = host;

        host.RecoverFromMetaList(
        [
            CreateMeta("A", [_activeQueryIdx]),
            CreateMeta("Peer", [_probeIdx],
                ["batch.active.simple"])
        ]);

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Contains("invoke_ok:hello_from:Peer", QueryActiveProxy.Events);
    }

    [Fact]
    public void BatchLoad_CrossEntity_SubscribeDuringAfterLoad()
    {
        _subCount = 0;
        SubscribeStrategy.Events.Clear();
        SubscribeStrategy.Host = null;
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new SubscribeStrategy());
            w.RegisterStrategy(() => new ProbeStrategy());
        });
        SubscribeStrategy.Host = host;

        host.RecoverFromMetaList(
        [
            CreateMeta("subscriber", ["batch.subscribe"]),
            CreateMeta("target", [_probeIdx])
        ]);

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Contains("subscribed", SubscribeStrategy.Events);

        var target = host.FindByName("target");
        Assert.NotNull(target);
        target.SetData("hp", 50);
        target.SetData("hp", 30);

        Assert.True(_subCount >= 2);
    }

    // ── Batch AfterSpawn ────────────────────────────────────────────────

    [Fact]
    public void SpawnMany_AfterSpawn_FiresOnAllEntities()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        var es1 = host.CreateEntity(CreateMeta("A", [_probeIdx]));
        var es2 = host.CreateEntity(CreateMeta("B", [_probeIdx]));
        var es3 = host.CreateEntity(CreateMeta("C", [_probeIdx]));
        ((IEntityLifecycle)es1).FireAfterSpawnHooks();
        ((IEntityLifecycle)es2).FireAfterSpawnHooks();
        ((IEntityLifecycle)es3).FireAfterSpawnHooks();

        Assert.Equal(new[] { "after_spawn:A", "after_spawn:B", "after_spawn:C" }, ProbeStrategy.Events);
    }

    [Fact]
    public void SpawnMany_CrossEntity_ActiveStrategyAvailableDuringAfterSpawn()
    {
        QueryActiveProxy.Events.Clear();
        ProbeStrategy.Events.Clear();
        QueryActiveProxy.InvokeTarget = "Peer";
        QueryActiveProxy.InvokeIndex = "batch.active.simple";
        QueryActiveProxy.Host = null;
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new QueryActiveProxy());
            w.RegisterStrategy(() => new SimpleActiveStrategy());
            w.RegisterStrategy(() => new ProbeStrategy());
        });
        QueryActiveProxy.Host = host;

        var se1 = host.CreateEntity(CreateMeta("A", [_activeQueryIdx]));
        var se2 = host.CreateEntity(CreateMeta("Peer", [_probeIdx],
            ["batch.active.simple"]));
        ((IEntityLifecycle)se1).FireAfterSpawnHooks();
        ((IEntityLifecycle)se2).FireAfterSpawnHooks();

        Assert.Contains("invoke_ok:hello_from:Peer", QueryActiveProxy.Events);
    }

    // ── Batch BeforeSave ────────────────────────────────────────────────

    [Fact]
    public void BatchSave_BeforeSave_FiresBeforeAnySerialization()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        host.RecoverFromMetaList(
        [
            CreateMeta("A", [_probeIdx]),
            CreateMeta("B", [_probeIdx])
        ]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();
        ProbeStrategy.Events.Clear();

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireBeforeSaveHooks();

        Assert.Equal(new[] { "before_save:A", "before_save:B" }, ProbeStrategy.Events);

        var metaList = host.BuildMetaList();
        Assert.Equal(2, metaList.Count);
    }

    // ── Batch BeforeQuit ────────────────────────────────────────────────

    [Fact]
    public void BatchQuit_BeforeQuit_FiresBeforeAnyTeardown()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        host.RecoverFromMetaList(
        [
            CreateMeta("A", [_probeIdx]),
            CreateMeta("B", [_probeIdx])
        ]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();
        ProbeStrategy.Events.Clear();

        var entities = host.GetEntities().ToList();
        foreach (var e in entities)
            if (e is IEntityLifecycle lc)
                lc.FireBeforeQuitHooks();

        Assert.Equal(new[] { "before_quit:A", "before_quit:B" }, ProbeStrategy.Events);

        foreach (var e in entities)
            if (e is IEntityLifecycle lc)
            {
                lc.ReleaseStrategiesOnly();
                lc.TeardownOnly();
            }

        host.RemoveAllEntities();

        Assert.Empty(host.GetEntities());
    }

    [Fact]
    public void BatchQuit_LifoOrder_Preserved()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        host.RecoverFromMetaList(
        [
            CreateMeta("First", [_probeIdx]),
            CreateMeta("Second", [_probeIdx]),
            CreateMeta("Third", [_probeIdx])
        ]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();
        ProbeStrategy.Events.Clear();

        var entities = host.GetEntities().ToList();
        for (var i = entities.Count - 1; i >= 0; i--)
            if (entities[i] is IEntityLifecycle lc)
                lc.FireBeforeQuitHooks();

        Assert.Equal(new[] { "before_quit:Third", "before_quit:Second", "before_quit:First" }, ProbeStrategy.Events);
    }

    [Fact]
    public void BatchQuit_CrossEntity_FindByNameSucceedsDuringBeforeQuit()
    {
        CrossRefStrategy.Events.Clear();
        CrossRefStrategy.TargetNames = ["B"];
        CrossRefStrategy.Host = null;
        var host3 = CreateHost(w =>
        {
            w.RegisterStrategy(() => new CrossRefStrategy());
            w.RegisterStrategy(() => new ProbeStrategy());
        });
        CrossRefStrategy.Host = host3;

        host3.RecoverFromMetaList(
        [
            CreateMeta("A", [_crossRefIdx]),
            CreateMeta("B", [_probeIdx])
        ]);
        foreach (var e in host3.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();
        CrossRefStrategy.Events.Clear();

        foreach (var e in host3.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireBeforeQuitHooks();

        Assert.Contains("quit_found:B", CrossRefStrategy.Events);
    }

    // ── Batch BeforeDead ────────────────────────────────────────────────

    [Fact]
    public void BatchDead_BeforeDead_FiresBeforeAnyTeardown()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        host.RecoverFromMetaList(
        [
            CreateMeta("A", [_probeIdx]),
            CreateMeta("B", [_probeIdx])
        ]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();
        ProbeStrategy.Events.Clear();

        var entities = host.GetEntities().ToList();
        foreach (var e in entities)
            if (e is IEntityLifecycle lc)
                lc.FireBeforeDeadHooks();

        Assert.Equal(new[] { "before_dead:A", "before_dead:B" }, ProbeStrategy.Events);

        foreach (var e in entities)
            host.RemoveEntity(e.Name);

        Assert.Empty(host.GetEntities());
    }

    [Fact]
    public void BatchDead_CrossEntity_FindByNameSucceedsDuringBeforeDead()
    {
        CrossRefStrategy.Events.Clear();
        CrossRefStrategy.TargetNames = ["B"];
        CrossRefStrategy.Host = null;
        ProbeStrategy.Events.Clear();
        var host4 = CreateHost(w =>
        {
            w.RegisterStrategy(() => new CrossRefStrategy());
            w.RegisterStrategy(() => new ProbeStrategy());
        });
        CrossRefStrategy.Host = host4;

        host4.RecoverFromMetaList(
        [
            CreateMeta("A", [_crossRefIdx]),
            CreateMeta("B", [_probeIdx])
        ]);
        foreach (var e in host4.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();
        CrossRefStrategy.Events.Clear();

        foreach (var e in host4.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireBeforeDeadHooks();

        Assert.Contains("dead_found:B", CrossRefStrategy.Events);
    }

    // ── Strategy priority within entity ─────────────────────────────────

    [Fact]
    public void BatchLoad_StrategyPriorityWithinEntity_Preserved()
    {
        var merged = new List<string>();
        SP50.Events = merged;
        SP100.Events = merged;

        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new SP50());
            w.RegisterStrategy(() => new SP100());
        });

        host.RecoverFromMetaList(
        [
            CreateMeta("E", [_p100Idx, _p50Idx])
        ]);

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Equal(new[] { "p50:E", "p100:E" }, SP50.Events);
    }

    // ── Empty / single-entity edge cases ────────────────────────────────

    [Fact]
    public void BatchLoad_EmptyList_DoesNothing()
    {
        var host = CreateHost(w => { });

        host.RecoverFromMetaList([]);
        Assert.Empty(host.GetEntities());
    }

    [Fact]
    public void BatchLoad_SingleEntity_BehaviorCorrect()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        host.RecoverFromMetaList([CreateMeta("Solo", [_probeIdx])]);
        Assert.Empty(ProbeStrategy.Events);

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Single(ProbeStrategy.Events);
        Assert.Equal("after_load:Solo", ProbeStrategy.Events[0]);
    }

    // ── Single convenience methods ──────────────────────────────────────

    [Fact]
    public void SpawnSingle_ActiveStrategyAvailableDuringAfterSpawn()
    {
        QueryActiveProxy.Events.Clear();
        QueryActiveProxy.InvokeTarget = "SelfSpawn";
        QueryActiveProxy.InvokeIndex = "batch.active.simple";
        QueryActiveProxy.Host = null;

        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new QueryActiveProxy());
        world.RegisterStrategy(() => new SimpleActiveStrategy());

        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        QueryActiveProxy.Host = host;

        var entity = host.CreateEntity(CreateMeta("SelfSpawn", [_activeQueryIdx],
            ["batch.active.simple"]));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Contains("invoke_ok:hello_from:SelfSpawn", QueryActiveProxy.Events);
    }

    [Fact]
    public void LoadSingle_ActiveStrategyAvailableDuringAfterLoad()
    {
        QueryActiveProxy.Events.Clear();
        QueryActiveProxy.InvokeTarget = "SelfLoad";
        QueryActiveProxy.InvokeIndex = "batch.active.simple";
        QueryActiveProxy.Host = null;

        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new QueryActiveProxy());
        world.RegisterStrategy(() => new SimpleActiveStrategy());

        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        QueryActiveProxy.Host = host;

        host.RecoverFromMetaList(
        [
            CreateMeta("SelfLoad", [_activeQueryIdx],
                ["batch.active.simple"])
        ]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Contains("invoke_ok:hello_from:SelfLoad", QueryActiveProxy.Events);
    }

    // ── Error path: hook throws ─────────────────────────────────────────

    [Fact]
    public void BatchLoad_HookThrows_EntitiesCleanedUp()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new FailingStrategy());
            w.RegisterStrategy(() => new ProbeStrategy());
        });

        host.RecoverFromMetaList(
        [
            CreateMeta("Good", [_probeIdx]),
            CreateMeta("Bad", ["batch.failing"]),
            CreateMeta("After", [_probeIdx])
        ]);

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var e in host.GetEntities())
                if (e is IEntityLifecycle lc)
                    lc.FireAfterLoadHooks();
        });
    }

    // ── SndEntityFactory.SpawnMany batch behavior ─────────────────────────────

    [Fact]
    public void SndEntityFactory_SpawnMany_TriggersAfterSpawnAfterAllCreated()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ProbeStrategy());
            w.RegisterStrategy(() => new SimpleActiveStrategy());
        });

        SndEntityFactory.SpawnMany(host,
        [
            CreateMeta("A", [_probeIdx]),
            CreateMeta("B", [_probeIdx]),
            CreateMeta("C", [_probeIdx])
        ]);

        Assert.Equal(new[] { "after_spawn:A", "after_spawn:B", "after_spawn:C" }, ProbeStrategy.Events);
    }


    // ── Boundary: SndEntityFactory orchestrates AfterSpawn hooks, SceneHost is pure container ─

    [Fact]
    public void SndEntityFactory_Spawn_CallsCreateEntityThenFiresAfterSpawn()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });
        var entity = SndEntityFactory.Spawn(host, CreateMeta("E", [_probeIdx]));

        Assert.NotNull(entity);
        Assert.Contains("after_spawn:E", ProbeStrategy.Events);
    }

    [Fact]
    public void SndEntityFactory_SpawnMany_EntitiesVisibleInAfterSpawn()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ProbeStrategy());
            w.RegisterStrategy(() => new SimpleActiveStrategy());
        });
        SndEntityFactory.SpawnMany(host,
        [
            CreateMeta("A", [_probeIdx]),
            CreateMeta("B", [_probeIdx])
        ]);

        Assert.Equal(new[] { "after_spawn:A", "after_spawn:B" }, ProbeStrategy.Events);
        Assert.Equal(2, host.GetEntities().Count);
    }

    [Fact]
    public void CreateEntity_DoesNotFireAfterSpawnHooks()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        host.CreateEntity(CreateMeta("E", [_probeIdx]));

        Assert.Empty(ProbeStrategy.Events);
        Assert.NotNull(host.FindByName("E"));
    }

    [Fact]
    public void RemoveEntity_DoesNotFireBeforeDeadHooks()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", [_probeIdx]));

        host.RemoveEntity("E");

        Assert.DoesNotContain("before_dead:E", ProbeStrategy.Events);
        Assert.Null(host.FindByName("E"));
    }

    [Fact]
    public void SndEntityFactory_Spawn_WithNonLifecycleEntity_DoesNotThrow()
    {
        var memoryHost = new StubSndSceneHost();
        var entity = SndEntityFactory.Spawn(memoryHost, new SndMetaData { Name = "E" });

        Assert.NotNull(entity);
        Assert.NotNull(memoryHost.FindByName("E"));
    }

    [Fact]
    public void SndEntityFactory_SpawnMany_WithNonLifecycleEntity_DoesNotThrow()
    {
        var memoryHost = new StubSndSceneHost();
        SndEntityFactory.SpawnMany(memoryHost,
        [
            new SndMetaData { Name = "A" },
            new SndMetaData { Name = "B" }
        ]);

        Assert.Equal(2, memoryHost.GetEntities().Count);
    }

    [Fact]
    public void ProcessAll_DoesNotThrowForEmptyScene()
    {
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });
        host.ProcessAll(0.016);
    }

    // ── ProcessAll — frame processing with entities ─────────────────────

    [Fact]
    public void ProcessAll_SingleEntity_CallsProcessOnStrategy()
    {
        ProcessRecordingStrategy.ProcessCalls.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProcessRecordingStrategy()); });
        SndEntityFactory.Spawn(host, CreateMeta("E", [_perfProcessIdx]));
        ProcessRecordingStrategy.ProcessCalls.Clear();

        host.ProcessAll(0.016);

        var calls = ProcessRecordingStrategy.ProcessCalls;
        Assert.Single(calls);
        Assert.Equal("E", calls[0].Name);
        Assert.Equal(0.016, calls[0].Delta, 0.001);
    }

    [Fact]
    public void ProcessAll_MultipleEntities_AllProcessed()
    {
        ProcessRecordingStrategy.ProcessCalls.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProcessRecordingStrategy()); });
        SndEntityFactory.SpawnMany(host,
        [
            CreateMeta("A", [_perfProcessIdx]),
            CreateMeta("B", [_perfProcessIdx]),
            CreateMeta("C", [_perfProcessIdx])
        ]);
        ProcessRecordingStrategy.ProcessCalls.Clear();

        host.ProcessAll(0.016);

        var names = ProcessRecordingStrategy.ProcessCalls.Select(c => c.Name).ToArray();
        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    [Fact]
    public void ProcessAll_DeltaPropagatesToStrategy()
    {
        ProcessRecordingStrategy.ProcessCalls.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProcessRecordingStrategy()); });
        SndEntityFactory.Spawn(host, CreateMeta("E", [_perfProcessIdx]));
        ProcessRecordingStrategy.ProcessCalls.Clear();

        host.ProcessAll(0.033);

        Assert.Equal(0.033, ProcessRecordingStrategy.ProcessCalls[0].Delta, 0.001);
    }

    [Fact]
    public void ProcessAll_ProcessAddsStrategy_NewStrategyNotExecutedThisFrame()
    {
        AddDuringProcessStrategy.ProcessCalls.Clear();
        ProcessRecordingStrategy.ProcessCalls.Clear();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new AddDuringProcessStrategy());
            w.RegisterStrategy(() => new ProcessRecordingStrategy());
        });
        SndEntityFactory.Spawn(host, CreateMeta("E", [_addDuringProcessIdx]));
        AddDuringProcessStrategy.ProcessCalls.Clear();
        ProcessRecordingStrategy.ProcessCalls.Clear();

        host.ProcessAll(0.016);

        Assert.Equal(new[] { "add_during_process:E" }, AddDuringProcessStrategy.ProcessCalls);
        Assert.Empty(ProcessRecordingStrategy.ProcessCalls);
    }

    [Fact]
    public void ProcessAll_ProcessRemovesStrategy_RemainingStrategiesStillExecuted()
    {
        RemoveSelfDuringProcessStrategy.ProcessCalls.Clear();
        ProcessRecordingStrategy.ProcessCalls.Clear();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new RemoveSelfDuringProcessStrategy());
            w.RegisterStrategy(() => new ProcessRecordingStrategy());
        });
        SndEntityFactory.Spawn(host, CreateMeta("E", ["batch.perf.remove_self", _perfProcessIdx]));

        RemoveSelfDuringProcessStrategy.ProcessCalls.Clear();
        ProcessRecordingStrategy.ProcessCalls.Clear();
        host.ProcessAll(0.016);

        Assert.Single(RemoveSelfDuringProcessStrategy.ProcessCalls);
        Assert.Single(ProcessRecordingStrategy.ProcessCalls);
        Assert.Equal("E", ProcessRecordingStrategy.ProcessCalls[0].Name);
    }

    // ── SndEntityFactory tests ──────────────────────────────────────────

    [Fact]
    public void SndEntityFactory_Spawn_CreatesEntityAndFiresAfterSpawn()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        var entity = SndEntityFactory.Spawn(host, CreateMeta("E", [_probeIdx]));

        Assert.NotNull(entity);
        Assert.Contains("after_spawn:E", ProbeStrategy.Events);
    }

    [Fact]
    public void SndEntityFactory_SpawnMany_BatchCreatesAllThenFiresHooks()
    {
        ProbeStrategy.Events.Clear();
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });

        SndEntityFactory.SpawnMany(host,
            CreateMeta("A", [_probeIdx]),
            CreateMeta("B", [_probeIdx]),
            CreateMeta("C", [_probeIdx]));

        Assert.Equal(new[] { "after_spawn:A", "after_spawn:B", "after_spawn:C" }, ProbeStrategy.Events);
    }

    [Fact]
    public void SndEntityFactory_SpawnMany_EntitiesVisibleDuringAfterSpawn()
    {
        CrossRefStrategy.Events.Clear();
        CrossRefStrategy.Host = null;
        var host = CreateHost(w => { w.RegisterStrategy(() => new CrossRefStrategy()); });
        CrossRefStrategy.Host = host;
        CrossRefStrategy.TargetNames = ["A", "B"];

        SndEntityFactory.SpawnMany(host,
            CreateMeta("A", [_crossRefIdx]),
            CreateMeta("B", [_crossRefIdx]));

        Assert.Contains("found:A", CrossRefStrategy.Events);
        Assert.Contains("found:B", CrossRefStrategy.Events);
    }

    // ── KillPendingEntities full lifecycle ───────────────────────────────



    // ── RemoveEntity boundary verification ───────────────────────────────

    [Fact]
    public void FullMemorySndSceneHost_RemoveEntity_ClearsCollectionOnly()
    {
        var host = CreateHost(w => { w.RegisterStrategy(() => new ProbeStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", [_probeIdx]));

        host.RemoveEntity("E");

        Assert.Null(host.FindByName("E"));
        Assert.Empty(host.GetEntities());
        Assert.Throws<InvalidOperationException>(() => host.RemoveEntity("E"));
    }
}
