using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ActiveStrategyIntegrationTests
{
    [Fact]
    public void InvokeStrategy_DirectCall_ReturnsResult()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", []);
        entity.AddActiveStrategy("test.int.active.echo");

        var result = entity.InvokeStrategy("test.int.active.echo", 21);
        Assert.Equal(42, result);
    }

    [Fact]
    public void InvokeStrategy_ProcessTriggersActive_WithinFrame()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new SelfInvokeStrategy())
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", ["test.int.active.self_invoke"]);
        entity.AddActiveStrategy("test.int.active.echo");

        harness.DriveFrame();

        var result = harness.GetEntityData<int>("actor", "invoke_result");
        Assert.Equal(10, result);
    }

    [Fact]
    public void InvokeStrategy_PeerEntityActiveStrategy_CrossEntity()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new PeerInvokeStrategy())
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var target = harness.SpawnEntity("target", []);
        target.AddActiveStrategy("test.int.active.echo");

        var caller = harness.SpawnEntity("caller", ["test.int.active.peer_invoke"]);

        harness.DriveFrame();

        var result = harness.GetEntityData<int>("caller", "peer_result");
        Assert.Equal(14, result);
    }

    [Fact]
    public void ActiveStrategyIndices_SaveLoad_Persisted()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", []);
        entity.AddActiveStrategy("test.int.active.echo");

        var result = entity.InvokeStrategy("test.int.active.echo", 5);
        Assert.Equal(10, result);

        var saveId = harness.Context.Save.RequestSaveGameAuto("active_save");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var loadedEntity = gameSession.FindByName("actor");
        Assert.NotNull(loadedEntity);

        var afterLoadResult = loadedEntity.InvokeStrategy("test.int.active.echo", 7);
        Assert.Equal(14, afterLoadResult);
    }

    [Fact]
    public void ActiveStrategy_AfterLoad_InvokeWorks()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", []);
        entity.AddActiveStrategy("test.int.active.echo");
        entity.SetData("counter", 1);

        var saveId = harness.Context.Save.RequestSaveGameAuto("active_load_save");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var loadedEntity = gameSession.FindByName("actor");
        Assert.NotNull(loadedEntity);

        var result = loadedEntity.InvokeStrategy("test.int.active.echo", 3);
        Assert.Equal(6, result);

        var counter = loadedEntity.GetData<int>("counter");
        Assert.Equal(1, counter);
    }

    [Fact]
    public void HybridEntity_LifecycleProcessAndActiveInvoke()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new AdvFrameCounterStrategy())
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("hybrid", ["test.int.active.frame_counter"]);
        entity.AddActiveStrategy("test.int.active.echo");

        harness.RunFrames(3);

        var count = harness.GetEntityData<int>("hybrid", "count");
        Assert.Equal(3, count);

        var result = entity.InvokeStrategy("test.int.active.echo", count);
        Assert.Equal(6, result);
    }

    [Fact]
    public void ActiveStrategy_DynamicAddRemove_InFrameLoop()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", []);

        Assert.Throws<InvalidOperationException>(
            () => entity.InvokeStrategy("test.int.active.echo", 1));

        entity.AddActiveStrategy("test.int.active.echo");

        var result = entity.InvokeStrategy("test.int.active.echo", 2);
        Assert.Equal(4, result);

        harness.DriveFrame();
        result = entity.InvokeStrategy("test.int.active.echo", 3);
        Assert.Equal(6, result);

        entity.RemoveActiveStrategy("test.int.active.echo");
        harness.DriveFrame();

        Assert.Throws<InvalidOperationException>(
            () => entity.InvokeStrategy("test.int.active.echo", 4));
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.active.echo")]
    private sealed class EchoActiveStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) =>
            input is int i ? i * 2 : input;
    }

    [StrategyIndex("test.int.active.self_invoke")]
    private sealed class SelfInvokeStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var result = entity.InvokeStrategy("test.int.active.echo", 5);
            entity.SetData("invoke_result", result);
        }
    }

    [StrategyIndex("test.int.active.peer_invoke")]
    private sealed class PeerInvokeStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var peer = entity.OwningSession.FindByName("target");
            if (peer is not null)
            {
                var result = peer.InvokeStrategy("test.int.active.echo", 7);
                entity.SetData("peer_result", result);
            }
        }
    }

    [StrategyIndex("test.int.active.frame_counter")]
    private sealed class AdvFrameCounterStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
            entity.SetData("count", 0);

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var count = entity.GetData<int>("count");
            entity.SetData("count", count + 1);
        }
    }
}
