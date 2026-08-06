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
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

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
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

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
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
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
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. StateMachineStrategyBase — virtual hooks coverage
// ─────────────────────────────────────────────────────────────────────────────
