using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class StrategyOrderingTests
{
    private const string _producer = "order.z_producer";
    private const string _bridge = "order.m_bridge";
    private const string _consumer = "order.a_consumer";
    private static readonly AsyncLocal<List<string>> _events = new();

    public StrategyOrderingTests() => _events.Value = [];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullRegistry_ProjectsTransitiveOrder_RegardlessOfRegistrationAndMountingOrder(bool reverse)
    {
        var builder = GameplaySimulationHarness.Create();
        if (reverse)
            builder.WithStrategy(() => new Consumer()).WithStrategy(() => new Bridge()).WithStrategy(() => new Producer());
        else
            builder.WithStrategy(() => new Producer()).WithStrategy(() => new Bridge()).WithStrategy(() => new Consumer());
        var game = builder.Build();
        var entity = game.SpawnEntity("worker", reverse ? [_producer, _consumer] : [_consumer, _producer]);
        Assert.Equal(["P:spawn", "C:spawn"], _events.Value);
        _events.Value!.Clear();
        game.DriveFrame();
        Assert.Equal(["P:process", "C:process"], _events.Value);
        Assert.Equal([_producer, _consumer], ((IEntityLifecycle)entity).BuildMetaData().StrategyMetaData!.LifecycleIndices);
    }

    [Fact]
    public void DynamicAddAndRemove_KeepTransitiveOrderAndOptionalTargets()
    {
        var game = CreateGame();
        var entity = game.SpawnEntity("worker", [_consumer]);
        entity.AddStrategy(_producer);
        entity.AddStrategy(_bridge);
        _events.Value!.Clear();
        game.DriveFrame();
        Assert.Equal(["P:process", "B:process", "C:process"], _events.Value);
        entity.RemoveStrategy(_bridge);
        _events.Value.Clear();
        game.DriveFrame();
        Assert.Equal(["P:process", "C:process"], _events.Value);
        Assert.Throws<InvalidOperationException>(() => entity.AddStrategy(_producer));
        Assert.Throws<InvalidOperationException>(() => entity.RemoveStrategy(_bridge));
    }

    [Fact]
    public void SaveLoadQuitAndDead_AllUseSameProjectedOrder()
    {
        var game = CreateGame();
        game.SpawnEntity("worker", [_consumer, _producer]);
        _events.Value!.Clear();
        game.SaveAndReload("ordering");
        Assert.Equal(["P:save", "C:save", "P:quit", "C:quit", "P:load", "C:load"], _events.Value);
        var session = game.Runtime.SessionManager.TryGet("game")!;
        var restored = session.FindByName("worker")!;
        Assert.Equal([_producer, _consumer], ((IEntityLifecycle)restored).BuildMetaData().StrategyMetaData!.LifecycleIndices);
        _events.Value.Clear();
        session.RequestKillEntity("worker");
        game.Context.FlushFrame();
        Assert.Equal(["P:dead", "C:dead"], _events.Value);
        game.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(game.Logger.Warnings, warning => warning.Contains("Strategy leak", StringComparison.Ordinal));
    }

    [Fact]
    public void BootstrapWithoutAutoDiscovery_ValidatesUnusedConstraintsImmediately()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GameplaySimulationHarness.Create().WithStrategy(() => new MissingTarget()).Build());
    }

    [Fact]
    public void UnknownTarget_FailsBeforeAnyEntityOrPoolReferenceIsCreated()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new MissingTarget());
        var ex = Assert.Throws<InvalidOperationException>(() => world.StrategyPool.SealRegistration());
        Assert.Contains("order.missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains("unregistered", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonLifecycleTarget_IsRejected()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new ActiveTarget());
        world.RegisterStrategy(() => new ReferencesActive());
        var ex = Assert.Throws<InvalidOperationException>(() => world.StrategyPool.SealRegistration());
        Assert.Contains("non-lifecycle", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cycles_FailWithAnActualClosedPath_WithoutIncludingUnrelatedPredecessors()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new CycleA());
        world.RegisterStrategy(() => new CycleB());
        world.RegisterStrategy(() => new CycleC());
        world.RegisterStrategy(() => new CycleRoot());
        var ex = Assert.Throws<InvalidOperationException>(() => world.StrategyPool.SealRegistration());
        Assert.Contains("cycle.a -> cycle.b -> cycle.c -> cycle.a", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("cycle.0root ->", ex.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => world.StrategyPool.SealRegistration());
    }

    [Fact]
    public void InvalidDeclarations_FailAtRegistration()
    {
        Assert.Throws<InvalidOperationException>(() => TestFactory.CreateSndWorld().RegisterStrategy(() => new SelfReference()));
        Assert.Throws<InvalidOperationException>(() => TestFactory.CreateSndWorld().RegisterStrategy(() => new BlankReference()));
        Assert.Throws<InvalidOperationException>(() => TestFactory.CreateSndWorld().RegisterStrategy(() => new NullReference()));
        Assert.Throws<InvalidOperationException>(() => TestFactory.CreateSndWorld().RegisterStrategy(() => new OrderedActive()));
    }

    [Fact]
    public void EquivalentBeforeAfterAndDuplicateEdges_DoNotCreateFalseCycles()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new Producer());
        world.RegisterStrategy(() => new Bridge());
        world.RegisterStrategy(() => new Consumer());
        world.StrategyPool.SealRegistration();
        Assert.True(world.StrategyPool.GetLifecycleOrder(_producer) < world.StrategyPool.GetLifecycleOrder(_consumer));
        Assert.Throws<InvalidOperationException>(() => world.StrategyPool.GetLifecycleOrder("unknown"));
        Assert.Throws<InvalidOperationException>(() => world.RegisterStrategy(() => new CycleRoot()));
    }

    [Fact]
    public void DirectEntityRecovery_SealsRegistrationWithoutBootstrap()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new Producer());
        world.RegisterStrategy(() => new Bridge());
        world.RegisterStrategy(() => new Consumer());
        var manager = new SndStrategyManager(world.StrategyPool, NullLogger.Instance);
        manager.RecoverStrategiesOnly([_consumer]);
        Assert.Throws<InvalidOperationException>(() => world.RegisterStrategy(() => new CycleRoot()));
        manager.ReleaseStrategiesOnly();
    }

    private static GameplaySimulationHarness CreateGame() => GameplaySimulationHarness.Create()
        .WithStrategy(() => new Consumer()).WithStrategy(() => new Bridge()).WithStrategy(() => new Producer()).Build();

    private abstract class Probe : LifecycleStrategyBase
    {
        protected abstract string Tag { get; }
        private void Record(string stage) => _events.Value!.Add($"{Tag}:{stage}");
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => Record("process");
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => Record("spawn");
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) => Record("load");
        public override void BeforeSave(ISndEntity entity, ISndContext ctx) => Record("save");
        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => Record("quit");
        public override void BeforeDead(ISndEntity entity, ISndContext ctx) => Record("dead");
    }

    [StrategyIndex(_producer, Before = new[] { _bridge, _bridge })]
    private sealed class Producer : Probe { protected override string Tag => "P"; }
    [StrategyIndex(_bridge, After = new[] { _producer }, Before = new[] { _consumer })]
    private sealed class Bridge : Probe { protected override string Tag => "B"; }
    [StrategyIndex(_consumer)]
    private sealed class Consumer : Probe { protected override string Tag => "C"; }
    [StrategyIndex("order.unknown", Before = new[] { "order.missing" })]
    private sealed class MissingTarget : LifecycleStrategyBase { }
    [StrategyIndex("order.active")]
    private sealed class ActiveTarget : ActiveStrategyBase { public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => input; }
    [StrategyIndex("order.references_active", After = new[] { "order.active" })]
    private sealed class ReferencesActive : LifecycleStrategyBase { }
    [StrategyIndex("cycle.a", Before = new[] { "cycle.b" })]
    private sealed class CycleA : LifecycleStrategyBase { }
    [StrategyIndex("cycle.b", Before = new[] { "cycle.c" })]
    private sealed class CycleB : LifecycleStrategyBase { }
    [StrategyIndex("cycle.c", Before = new[] { "cycle.a" })]
    private sealed class CycleC : LifecycleStrategyBase { }
    [StrategyIndex("cycle.0root", Before = new[] { "cycle.a" })]
    private sealed class CycleRoot : LifecycleStrategyBase { }
    [StrategyIndex("invalid.self", After = new[] { "invalid.self" })]
    private sealed class SelfReference : LifecycleStrategyBase { }
    [StrategyIndex("invalid.blank", Before = new[] { " " })]
    private sealed class BlankReference : LifecycleStrategyBase { }
    [StrategyIndex("invalid.null", After = null!)]
    private sealed class NullReference : LifecycleStrategyBase { }
    [StrategyIndex("invalid.active", Before = new[] { _producer })]
    private sealed class OrderedActive : ActiveStrategyBase { public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => input; }
}
