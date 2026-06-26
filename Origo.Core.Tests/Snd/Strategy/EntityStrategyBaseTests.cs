using System;
using System.Collections.Generic;
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

public class EntityStrategyBaseTests
{
    [Fact]
    public void DefaultHooks_DoNotMutateEntityData()
    {
        var strategy = new TestEntityStrategy();
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
        var strategy = new TestEntityStrategyWithAdd();
        var entity = new StubSndEntity("e");
        ISndContext ctx = NullSndContext.Instance;

        var ex = Record.Exception(() => strategy.Process(entity, 0.016, ctx));
        Assert.Null(ex);
    }

    [Fact]
    public void Process_KillsItself_MarksEntity()
    {
        var host = new StubSndSceneHost();
        var meta = new SndMetaData { Name = "e" };
        var entity = host.CreateEntity(meta);
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("entry.json", "[]");
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        var strategy = new TestEntityStrategyKillSelf();
        strategy.Process(entity, 0.016, ctx);

        Assert.True(entity.IsPendingKill);
    }

    [Fact]
    public void Process_KillsOtherEntity_MarksTargetEntity()
    {
        var host = new StubSndSceneHost();
        var metaA = new SndMetaData { Name = "A" };
        var metaB = new SndMetaData { Name = "B" };
        var entityA = host.CreateEntity(metaA);
        host.CreateEntity(metaB);
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("entry.json", "[]");
        var io2 = TestFactory.CreateIoGateway(fs);
        var metaAccess2 = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver2 = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io2, metaAccess2, pathResolver2, "root", "res://initial", "entry.json"));

        var strategy = new TestEntityStrategyKillOther();
        strategy.Process(entityA, 0.016, ctx);

        Assert.False(entityA.IsPendingKill);
        var entityB = host.FindByName("B");
        Assert.NotNull(entityB);
        Assert.True(entityB.IsPendingKill);
    }

    [Fact]
    public void Process_RequestKillDuringProcess_RemainingStrategiesStillExecuted()
    {
        KillSelfRecordingStrategy.ProcessCalls = new List<string>();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new KillSelfRecordingStrategy());
            w.RegisterStrategy(() => new ProcessCalledStrategy());
        });

        var entity = host.CreateEntity(CreateMeta("E", new[] { KillSelfIdx, ProcessCalledIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        KillSelfRecordingStrategy.ProcessCalls.Clear();
        ProcessCalledStrategy.Called = false;

        host.ProcessAll(0.016);

        Assert.Single(KillSelfRecordingStrategy.ProcessCalls);
        Assert.True(ProcessCalledStrategy.Called);
    }

    private const string KillSelfIdx = "test.kill_self";
    private const string ProcessCalledIdx = "test.process_called";

    [Fact]
    public void Remove_NonexistentStrategy_DoesNotThrow()
    {
        var entity = new StubSndEntity("e");

        var ex = Record.Exception(() => entity.RemoveStrategy("nonexistent.index"));
        Assert.Null(ex);
    }

    [Fact]
    public void AddStrategy_WhenAfterAddThrows_RollsBackInsertionAndPoolReference()
    {
        ThrowOnAddStrategy.ProcessCalls = 0;
        var host = CreateHost(w => w.RegisterStrategy(() => new ThrowOnAddStrategy()));
        var entity = host.CreateEntity(CreateMeta("E"));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<InvalidOperationException>(() => entity.AddStrategy(ThrowOnAddIdx));

        // The rolled-back strategy must not run during Process; it would if it
        // had been left half-attached to the entity's strategy list.
        host.ProcessAll(0.016);
        Assert.Equal(0, ThrowOnAddStrategy.ProcessCalls);
    }

    private const string ThrowOnAddIdx = "test.throw_on_add";

    [StrategyIndex(ThrowOnAddIdx)]
    private sealed class ThrowOnAddStrategy : LifecycleStrategyBase
    {
        public static int ProcessCalls { get; set; }

        public override void AfterAdd(ISndEntity entity, ISndContext ctx)
        {
            throw new InvalidOperationException("AfterAdd boom");
        }

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ProcessCalls++;
        }
    }

    private sealed class TestEntityStrategy : LifecycleStrategyBase
    {
    }

    private sealed class TestEntityStrategyWithAdd : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            entity.AddStrategy("some.new.strategy");
        }
    }

    private sealed class TestEntityStrategyKillSelf : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ctx.RequestKillEntity(entity.Name);
        }
    }

    private sealed class TestEntityStrategyKillOther : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ctx.RequestKillEntity("B");
        }
    }

    [StrategyIndex(KillSelfIdx)]
    private sealed class KillSelfRecordingStrategy : LifecycleStrategyBase
    {
        public static List<string> ProcessCalls { get; set; } = null!;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ProcessCalls.Add($"kill_self:{entity.Name}");
            ctx.RequestKillEntity(entity.Name);
        }
    }

    [StrategyIndex(ProcessCalledIdx)]
    private sealed class ProcessCalledStrategy : LifecycleStrategyBase
    {
        public static bool Called { get; set; }

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            Called = true;
        }
    }

    private static FullMemorySndSceneHost CreateHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
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
        return host;
    }

    private static SndMetaData CreateMeta(string name, string[]? entityIndices = null) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            EntityIndices = new List<string>(entityIndices ?? Array.Empty<string>())
        },
        DataMetaData = new DataMetaData()
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. StateMachineStrategyBase — virtual hooks coverage
// ─────────────────────────────────────────────────────────────────────────────
