using System;
using System.Collections.Generic;
using Origo.Core.Logging;
using Origo.Core.Runtime.Console;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class StrategyPoolAndRuntimeTests
{
    [Fact]
    public void SndStrategyPool_ReusesAndReleasesByReferenceCount()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new DemoStrategy());

        var first = pool.GetStrategy<EntityStrategyBase>("demo");
        var second = pool.GetStrategy<EntityStrategyBase>("demo");
        Assert.NotNull(first);
        Assert.Same(first, second);

        pool.ReleaseStrategy("demo");
        var stillSame = pool.GetStrategy<EntityStrategyBase>("demo");
        Assert.Same(first, stillSame);

        pool.ReleaseStrategy("demo");
        pool.ReleaseStrategy("demo");
        var recreated = pool.GetStrategy<EntityStrategyBase>("demo");
        Assert.NotSame(first, recreated);
    }

    [Fact]
    public void SndStrategyPool_GetUnknownStrategy_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);

        Assert.Throws<InvalidOperationException>(() => pool.GetStrategy<EntityStrategyBase>("missing"));
    }

    [Fact]
    public void SndStrategyPool_ReleaseWithoutAcquire_OrDoubleRelease_ThrowsInvalidOperation()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new DemoStrategy());

        Assert.Throws<InvalidOperationException>(() => pool.ReleaseStrategy("demo"));

        pool.GetStrategy<EntityStrategyBase>("demo");
        pool.ReleaseStrategy("demo");
        Assert.Throws<InvalidOperationException>(() => pool.ReleaseStrategy("demo"));
    }

    [Fact]
    public void SndStrategyManager_Recover_WhenFailed_RollsBackAcquiredStrategies()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new RecoverSafeStrategy());
        var manager = new SndStrategyManager(pool, NullLogger.Instance);

        Assert.Throws<InvalidOperationException>(() =>
        {
            manager.RecoverStrategiesOnly(new[] { "recover.safe", "recover.missing" });
            manager.TriggerAfterSpawn(new DummySndEntity("dummy"),
                NullSndContext.Instance);
        });

        var first = pool.GetStrategy<EntityStrategyBase>("recover.safe");
        pool.ReleaseStrategy("recover.safe");
        var second = pool.GetStrategy<EntityStrategyBase>("recover.safe");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void SndRuntime_DelegatesToSceneHost()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new SndRuntime(TestFactory.CreateSndWorld(logger: logger), host);
        var metaA = new SndMetaData { Name = "A" };
        var metaB = new SndMetaData { Name = "B" };

        runtime.Spawn(metaA);
        runtime.SpawnMany(new[] { metaB });

        Assert.Equal(2, runtime.GetEntities().Count);
        Assert.Equal(2, runtime.BuildMetaList().Count);

        runtime.ClearAll();
        Assert.Empty(runtime.GetEntities());
        Assert.Equal(1, host.ClearAllCount);
    }

    [Fact]
    public void SndRuntime_Spawn_DuplicateName_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new SndRuntime(TestFactory.CreateSndWorld(logger: logger), host);

        runtime.Spawn(new SndMetaData { Name = "Dup" });
        Assert.Throws<InvalidOperationException>(() => runtime.Spawn(new SndMetaData { Name = "Dup" }));
    }

    [Fact]
    public void OrigoRuntime_CreatesSndWorldAndSupportsInjectedSystemBlackboard()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var injectedSystemBoard = new Blackboard.Blackboard();
        var runtime = TestFactory.CreateRuntime(logger, host, systemBb: injectedSystemBoard);

        Assert.NotNull(runtime.Snd);
        Assert.NotNull(runtime.SndWorld);
        Assert.Same(injectedSystemBoard, runtime.SystemBlackboard);
    }

    [Fact]
    public void OrigoRuntime_ResetConsoleState_ClearsInputQueueOnly()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputQueue();
        var output = new ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(),
            input, output);
        var messages = new List<string>();
        output.Subscribe(messages.Add);

        input.Enqueue("spawn a b");
        output.Publish("old");

        runtime.ResetConsoleState();

        Assert.False(input.TryDequeueCommand(out _));
        Assert.Single(messages);
        Assert.Equal("old", messages[0]);
    }

    [StrategyIndex("demo")]
    private sealed class DemoStrategy : EntityStrategyBase
    {
    }

    [StrategyIndex("recover.safe")]
    private sealed class RecoverSafeStrategy : EntityStrategyBase
    {
    }
}
