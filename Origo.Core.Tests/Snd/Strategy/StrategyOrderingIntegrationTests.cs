using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class StrategyOrderingIntegrationTests
{
    [Fact]
    public void SpawnAndProcess_UnconstrainedStrategies_UseOrdinalIndexOrder()
    {
        var game = GameplaySimulationHarness.Create()
            .WithStrategy(() => new OrderingZulu())
            .WithStrategy(() => new OrderingAlpha()).Build();
        var entity = game.SpawnEntity("worker", ["ordering.zulu", "ordering.alpha"]);
        game.DriveFrame();
        Assert.Equal("AZ", entity.GetData<string>("execution"));
    }

    [Fact]
    public void RemoveStrategy_HookChangesOtherEntries_RemovesRequestedEntry()
    {
        var game = GameplaySimulationHarness.Create()
            .WithStrategy(() => new OrderingZulu())
            .WithStrategy(() => new OrderingAlpha())
            .WithStrategy(() => new OrderingRemover()).Build();
        var entity = game.SpawnEntity("worker", ["ordering.zulu", "ordering.remover"]);
        entity.RemoveStrategy("ordering.remover");
        game.DriveFrame();
        Assert.Equal("A", entity.GetData<string>("execution"));
    }

    [Fact]
    public void RegisterStrategy_AfterFirstLifecycleEntity_FailsExplicitly()
    {
        var game = GameplaySimulationHarness.Create()
            .WithStrategy(() => new OrderingZulu()).Build();
        game.SpawnEntity("worker", ["ordering.zulu"]);
        Assert.Throws<InvalidOperationException>(() =>
            game.Runtime.SndWorld.RegisterStrategy(() => new OrderingAlpha()));
    }

    [StrategyIndex("ordering.alpha")]
    private sealed class OrderingAlpha : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            entity.SetData("execution", entity.GetData<string>("execution") + "A");
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => entity.SetData("execution", "");
    }

    [StrategyIndex("ordering.zulu")]
    private sealed class OrderingZulu : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            entity.SetData("execution", entity.GetData<string>("execution") + "Z");
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => entity.SetData("execution", "");
    }

    [StrategyIndex("ordering.remover")]
    private sealed class OrderingRemover : LifecycleStrategyBase
    {
        public override void BeforeRemove(ISndEntity entity, ISndContext ctx)
        {
            entity.RemoveStrategy("ordering.zulu");
            entity.AddStrategy("ordering.alpha");
        }
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            entity.SetData("execution", entity.GetData<string>("execution") + "R");
    }
}
