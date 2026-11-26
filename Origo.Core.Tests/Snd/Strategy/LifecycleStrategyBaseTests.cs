using System;
using System.Collections.Generic;
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

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class LifecycleStrategyBaseTests
{
    [Fact]
    public void DefaultHooks_DoNotMutateEntityData()
    {
        var strategy = new TestLifecycleStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData("score", 7);
        ISndContext ctx = NullSndContext.Instance;

        strategy.Process(entity, 0.016, ctx);
        strategy.AfterSpawn(entity, ctx);
        strategy.AfterLoad(entity, ctx);
        strategy.AfterAdd(entity, ctx);
        strategy.BeforeRemove(entity, ctx);
        strategy.BeforeSave(entity, ctx);
        strategy.BeforeQuit(entity, ctx);
        strategy.BeforeDead(entity, ctx);

        Assert.Equal(7, entity.GetData<int>("score"));
    }

    [Fact]
    public void Process_AddsNewStrategy_DoesNotThrow()
    {
        var strategy = new TestLifecycleStrategyWithAdd();
        var entity = new StubSndEntity("e");
        ISndContext ctx = NullSndContext.Instance;

        var ex = Record.Exception(() => strategy.Process(entity, 0.016, ctx));
        Assert.Null(ex);
    }

    [Fact]
    public void Process_KillsItself_MarksEntity()
    {
        var host = new StubSndSceneHost();
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "res://entry/entry.json"));
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(new SndMetaData { Name = "e" });

        var strategy = new TestLifecycleStrategyKillSelf();
        strategy.Process(entity, 0.016, ctx);

        Assert.True(entity.IsPendingKill);
    }

    [Fact]
    public void Process_KillsOtherEntity_MarksTargetEntity()
    {
        var host = new StubSndSceneHost();
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var io2 = TestFactory.CreateIoGateway(fs);
        var metaAccess2 = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver2 = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io2, metaAccess2, pathResolver2, "root", "res://initial", "res://entry/entry.json"));
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entityA = session.Spawn(new SndMetaData { Name = "A" });
        session.Spawn(new SndMetaData { Name = "B" });

        var strategy = new TestLifecycleStrategyKillOther();
        strategy.Process(entityA, 0.016, ctx);

        Assert.False(entityA.IsPendingKill);
        var entityB = session.FindByName("B");
        Assert.NotNull(entityB);
        Assert.True(entityB.IsPendingKill);
    }

    [Fact]
    public void Process_RequestKillDuringProcess_RemainingStrategiesStillExecuted()
    {
        KillSelfRecordingStrategy.ProcessCalls.Clear();
        var (_, ctx) = CreateHost(w =>
        {
            w.RegisterStrategy(() => new KillSelfRecordingStrategy());
            w.RegisterStrategy(() => new ProcessCalledStrategy());
        });

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(CreateMeta("E", [_killSelfIdx, _processCalledIdx]));
        KillSelfRecordingStrategy.ProcessCalls.Clear();
        ProcessCalledStrategy.Called = false;

        ctx.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: true);

        Assert.Single(KillSelfRecordingStrategy.ProcessCalls);
        Assert.True(ProcessCalledStrategy.Called);
    }

    private const string _killSelfIdx = "test.kill_self";
    private const string _processCalledIdx = "test.process_called";

    [Fact]
    public void Remove_NonexistentStrategy_Throws()
    {
        var (_, ctx) = CreateHost(_ => { });
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(CreateMeta("E"));

        Assert.Throws<InvalidOperationException>(() => entity.RemoveStrategy("nonexistent.index"));
    }

    [Fact]
    public void AddStrategy_WhenAfterAddThrows_RollsBackInsertionAndPoolReference()
    {
        ThrowOnAddStrategy.ProcessCalls = 0;
        var (_, ctx) = CreateHost(w => w.RegisterStrategy(() => new ThrowOnAddStrategy()));
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(CreateMeta("E"));

        Assert.Throws<InvalidOperationException>(() => entity.AddStrategy(_throwOnAddIdx));

        // The rolled-back strategy must not run during Process; it would if it
        // had been left half-attached to the entity's strategy list.
        ctx.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: true);
        Assert.Equal(0, ThrowOnAddStrategy.ProcessCalls);
    }

    private const string _selfRemoveThenThrowIdx = "test.self_remove_then_throw";
    [StrategyIndex(_selfRemoveThenThrowIdx)]
    private sealed class SelfRemoveThenThrowStrategy : LifecycleStrategyBase
    {
        public override void AfterAdd(ISndEntity entity, ISndContext ctx)
        {
            // Removes itself through the public API (which already releases
            // the pool reference), then fails: the Add rollback must not
            // release the pool reference a second time and mask this failure.
            entity.RemoveStrategy(_selfRemoveThenThrowIdx);
            throw new InvalidOperationException("original after-add failure");
        }
    }

    [Fact]
    public void AddStrategy_WhenAfterAddRemovesItselfThenThrows_PropagatesOriginalFailure()
    {
        var (_, ctx) = CreateHost(w => w.RegisterStrategy(() => new SelfRemoveThenThrowStrategy()));
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(CreateMeta("E"));

        var ex = Assert.Throws<InvalidOperationException>(() => entity.AddStrategy(_selfRemoveThenThrowIdx));
        Assert.Contains("original after-add failure", ex.Message, StringComparison.Ordinal);
    }

    private const string _duplicateIdx = "test.duplicate_add";

    [StrategyIndex(_duplicateIdx)]
    private sealed class DuplicateAddTestStrategy : LifecycleStrategyBase
    {
    }

    [Fact]
    public void AddStrategy_SameIndexTwice_Throws()
    {
        var (_, ctx) = CreateHost(w => w.RegisterStrategy(() => new DuplicateAddTestStrategy()));
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(CreateMeta("E"));

        entity.AddStrategy(_duplicateIdx);
        var ex = Assert.Throws<InvalidOperationException>(() => entity.AddStrategy(_duplicateIdx));
        Assert.Contains("already mounted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private const string _throwOnAddIdx = "test.throw_on_add";

    [StrategyIndex(_throwOnAddIdx)]
    private sealed class ThrowOnAddStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<int> _processCalls = new();
        public static int ProcessCalls { get => _processCalls.Value; set => _processCalls.Value = value; }

        public override void AfterAdd(ISndEntity entity, ISndContext ctx) => throw new InvalidOperationException("AfterAdd boom");

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => ProcessCalls++;
    }

    private sealed class TestLifecycleStrategy : LifecycleStrategyBase
    {
    }

    private sealed class TestLifecycleStrategyWithAdd : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => entity.AddStrategy("some.new.strategy");
    }

    private sealed class TestLifecycleStrategyKillSelf : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => entity.OwningSession.RequestKillEntity(entity.Name);
    }

    private sealed class TestLifecycleStrategyKillOther : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => entity.OwningSession.RequestKillEntity("B");
    }

    [StrategyIndex(_killSelfIdx)]
    private sealed class KillSelfRecordingStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _processCalls = new();
        public static List<string> ProcessCalls => _processCalls.Value ??= [];

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ProcessCalls.Add($"kill_self:{entity.Name}");
            entity.OwningSession.RequestKillEntity(entity.Name);
        }
    }

    [StrategyIndex(_processCalledIdx)]
    private sealed class ProcessCalledStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<bool> _called = new();
        public static bool Called { get => _called.Value; set => _called.Value = value; }

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => Called = true;
    }

    private static (FullMemorySndSceneHost host, SndContext ctx) CreateHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();
        return (host, ctx);
    }

    private static SndMetaData CreateMeta(string name, string[]? lifecycleIndices = null) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            LifecycleIndices = [.. lifecycleIndices ?? []]
        },
        DataMetaData = new DataMetaData()
    };

    private const string _spawnInProcessIdx = "test.spawn_in_process";

    [StrategyIndex(_spawnInProcessIdx)]
    private sealed class SpawnInProcessStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
            => entity.OwningSession.Spawn(new SndMetaData
            {
                Name = "spawned_in_process",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(),
                DataMetaData = new DataMetaData()
            });
    }

    private const string _nestedSpawnOnceIdx = "test.nested_spawn_once";
    private const string _balancedMutationIdx = "test.balanced_mutation";
    private const string _markerIdx = "test.marker";

    private static Action? _processMutation;

    [StrategyIndex(_nestedSpawnOnceIdx)]
    private sealed class NestedSpawnOnceStrategy : LifecycleStrategyBase
    {
        public static bool ChildProcessed { get; set; }

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            // The child stops after one generation: on the pre-fix host it is
            // processed in the same frame, which is the regression. A fully
            // recursive spawner would keep the old live-count loop alive
            // forever, so the regression is asserted through the child's
            // Process result instead.
            if (entity.Name == "child")
            {
                ChildProcessed = true;
                return;
            }

            entity.OwningSession.Spawn(CreateMeta("child", [_nestedSpawnOnceIdx]));
        }
    }

    [StrategyIndex(_markerIdx)]
    private sealed class MarkerStrategy : LifecycleStrategyBase
    {
        public static bool Processed { get; set; }

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
            => Processed = true;
    }

    [StrategyIndex(_balancedMutationIdx)]
    private sealed class BalancedMutationStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
            => _processMutation?.Invoke();
    }

    [Fact]
    public void ProcessAll_ContainerModifiedDuringProcess_ThrowsInvalidOperation()
    {
        // Mutating the host while entities process would silently skip
        // entities; the host must fail loudly instead (documented contract).
        var (host, ctx) = CreateHost(w => w.RegisterStrategy(() => new SpawnInProcessStrategy()));
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(CreateMeta("E", [_spawnInProcessIdx]));

        var ex = Assert.Throws<InvalidOperationException>(() => host.ProcessAll(0.016));
        Assert.Contains("modified during ProcessAll", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessAll_NestedSpawn_ThrowsBeforeProcessingSpawnedEntity()
    {
        // A spawn inside Process must fail the frame, and the spawned entity
        // must not be processed by the same ProcessAll call. With the live
        // count as the loop bound, a nested spawner would keep the loop alive
        // until the spawned entity itself stopped spawning.
        NestedSpawnOnceStrategy.ChildProcessed = false;
        var (host, ctx) = CreateHost(w => w.RegisterStrategy(() => new NestedSpawnOnceStrategy()));
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(CreateMeta("parent", [_nestedSpawnOnceIdx]));

        var ex = Assert.Throws<InvalidOperationException>(() => host.ProcessAll(0.016));

        Assert.Contains("modified during ProcessAll", ex.Message, StringComparison.Ordinal);
        Assert.False(NestedSpawnOnceStrategy.ChildProcessed,
            "an entity spawned during ProcessAll must not be processed in the same frame");
    }

    [Fact]
    public void ProcessAll_AddAndRemoveBalanced_ThrowsInvalidOperation()
    {
        // A count-only guard misses a mutation that keeps the entity count
        // unchanged: one removal plus one spawn must still fail the frame.
        var (host, ctx) = CreateHost(w =>
        {
            w.RegisterStrategy(() => new BalancedMutationStrategy());
            w.RegisterStrategy(() => new MarkerStrategy());
        });
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(CreateMeta("A", [_balancedMutationIdx]));
        session.Spawn(CreateMeta("B"));
        MarkerStrategy.Processed = false;
        _processMutation = () =>
        {
            host.RemoveEntity("B");
            session.Spawn(CreateMeta("C", [_markerIdx]));
        };

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => host.ProcessAll(0.016));
            Assert.Contains("modified during ProcessAll", ex.Message, StringComparison.Ordinal);
            Assert.False(MarkerStrategy.Processed,
                "the mutation must be detected before a later or spawned entity is processed");
        }
        finally
        {
            _processMutation = null;
        }
    }

    [Fact]
    public void ProcessAll_RemoveOnly_ThrowsInvalidOperation()
    {
        // With only a post-loop count/version check, removing an entity shifts
        // the list and the next index would read out of range. The per-iteration
        // guard must throw InvalidOperationException before indexing.
        var (host, ctx) = CreateHost(w => w.RegisterStrategy(() => new BalancedMutationStrategy()));
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(CreateMeta("A", [_balancedMutationIdx]));
        session.Spawn(CreateMeta("B"));
        _processMutation = () => host.RemoveEntity("B");

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => host.ProcessAll(0.016));
            Assert.Contains("modified during ProcessAll", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            _processMutation = null;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. StateMachineStrategyBase — virtual hooks coverage
// ─────────────────────────────────────────────────────────────────────────────
