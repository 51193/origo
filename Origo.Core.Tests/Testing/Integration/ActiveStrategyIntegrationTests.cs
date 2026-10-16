using System;
using System.Collections.Generic;
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
        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);

        var result = entity.InvokeStrategy(TestStrategyIndices.EchoActive, 21);
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
        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);

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
        target.AddActiveStrategy(TestStrategyIndices.EchoActive);

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
        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);

        var result = entity.InvokeStrategy(TestStrategyIndices.EchoActive, 5);
        Assert.Equal(10, result);

        harness.SaveAndReload("active_save");

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var loadedEntity = gameSession.FindByName("actor");
        Assert.NotNull(loadedEntity);

        var afterLoadResult = loadedEntity.InvokeStrategy(TestStrategyIndices.EchoActive, 7);
        Assert.Equal(14, afterLoadResult);
    }

    [Fact]
    public void ActiveStrategy_AfterLoad_InvokeWorks()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", []);
        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);
        entity.SetData("counter", 1);

        harness.SaveAndReload("active_load_save");

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var loadedEntity = gameSession.FindByName("actor");
        Assert.NotNull(loadedEntity);

        var result = loadedEntity.InvokeStrategy(TestStrategyIndices.EchoActive, 3);
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
        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);

        harness.RunFrames(3);

        var count = harness.GetEntityData<int>("hybrid", "count");
        Assert.Equal(3, count);

        var result = entity.InvokeStrategy(TestStrategyIndices.EchoActive, count);
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
            () => entity.InvokeStrategy(TestStrategyIndices.EchoActive, 1));

        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);

        var result = entity.InvokeStrategy(TestStrategyIndices.EchoActive, 2);
        Assert.Equal(4, result);

        harness.DriveFrame();
        result = entity.InvokeStrategy(TestStrategyIndices.EchoActive, 3);
        Assert.Equal(6, result);

        entity.RemoveActiveStrategy(TestStrategyIndices.EchoActive);
        harness.DriveFrame();

        Assert.Throws<InvalidOperationException>(
            () => entity.InvokeStrategy(TestStrategyIndices.EchoActive, 4));
    }

    [Fact]
    public void ErrorPath_InvokeActiveStrategyOnKilledEntity_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", []);
        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);

        var result = entity.InvokeStrategy(TestStrategyIndices.EchoActive, 7);
        Assert.Equal(14, result);

        harness.RequestKillEntity("actor");
        harness.DriveFrame();

        Assert.Throws<InvalidOperationException>(
            () => entity.InvokeStrategy(TestStrategyIndices.EchoActive, 7));
    }

    [Fact]
    public void ErrorPath_AddDuplicateActiveStrategy_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("actor", []);
        entity.AddActiveStrategy(TestStrategyIndices.EchoActive);

        Assert.Throws<InvalidOperationException>(
            () => entity.AddActiveStrategy(TestStrategyIndices.EchoActive));
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex(TestStrategyIndices.EchoActive)]
    private sealed class EchoActiveStrategy : SharedEchoActiveStrategy { }

    [StrategyIndex("test.int.active.self_invoke")]
    private sealed class SelfInvokeStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var result = entity.InvokeStrategy(TestStrategyIndices.EchoActive, 5);
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
                var result = peer.InvokeStrategy(TestStrategyIndices.EchoActive, 7);
                entity.SetData("peer_result", result);
            }
        }
    }

    [StrategyIndex("test.int.active.frame_counter")]
    private sealed class AdvFrameCounterStrategy : SharedFrameCounterStrategy { }
}
