using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class AdvancedGameplayIntegrationTests
{
    [Fact]
    public void BatchSpawn_100Entities_AllProcessed()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BatchFrameCounterStrategy())
            .Build();

        for (var i = 0; i < 100; i++)
            harness.SpawnEntity($"entity_{i}", ["test.int.adv.batch_counter"]);

        harness.RunFrames(5);

        for (var i = 0; i < 100; i++)
        {
            var count = harness.GetEntityData<int>($"entity_{i}", "count");
            Assert.Equal(5, count);
        }
    }

    [Fact]
    public void BatchSpawn_ThenBatchKill_AllCleanedUp()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BatchFrameCounterStrategy())
            .Build();

        for (var i = 0; i < 100; i++)
            harness.SpawnEntity($"entity_{i}", ["test.int.adv.batch_counter"]);

        for (var i = 0; i < 100; i++)
            harness.RequestKillEntity($"entity_{i}");

        harness.DriveFrame();

        Assert.Empty(harness.GetEntities());
    }

    [Fact]
    public void ConsoleCommand_SndCount_PublishesOutput()
    {
        var harness = GameplaySimulationHarness.Create().Build();

        harness.ClearConsoleOutput();
        harness.SubmitConsoleCommand("snd_count");
        harness.DriveFrame();

        Assert.Contains(harness.ConsoleOutput, s => s.Contains("Snd count:"));
    }

    [Fact]
    public void ConsoleCommand_BbSetSystemLayer_RoundTrip()
    {
        var harness = GameplaySimulationHarness.Create().Build();

        harness.SubmitConsoleCommand("bb_set system test_int 42");
        harness.DriveFrame();
        harness.SubmitConsoleCommand("bb_set system test_str hello");
        harness.DriveFrame();

        var (foundInt, intValue) = harness.Runtime.SystemBlackboard.TryGet<int>("test_int");
        Assert.True(foundInt);
        Assert.Equal(42, intValue);

        var (foundStr, strValue) = harness.Runtime.SystemBlackboard.TryGet<string>("test_str");
        Assert.True(foundStr);
        Assert.Equal("hello", strValue);

        harness.ClearConsoleOutput();
        harness.SubmitConsoleCommand("bb_get system test_int");
        harness.DriveFrame();
        Assert.Contains(harness.ConsoleOutput, s => s.Contains("42"));
    }

    [Fact]
    public void EntityDataSetGet_DirectAPI_RoundTrip()
    {
        var harness = GameplaySimulationHarness.Create().Build();

        var entity = harness.SpawnEntity("player", []);
        entity.SetData("hp", 100);
        entity.SetData("name", "hero");
        entity.SetData("alive", true);

        Assert.Equal(100, entity.GetData<int>("hp"));
        Assert.Equal("hero", entity.GetData<string>("name"));
        Assert.True(entity.GetData<bool>("alive"));

        var (found, hp) = entity.TryGetData<int>("hp");
        Assert.True(found);
        Assert.Equal(100, hp);
    }

    [Fact]
    public void MultiStrategyEntity_LifecyclePlusObserver()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BatchFrameCounterStrategy())
            .WithStrategy(() => new DataObserverIntegrationStrategy())
            .Build();

        DataObserverIntegrationStrategy.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", ["test.int.adv.batch_counter"]);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.adv.data_observer");

            harness.DriveFrame();

            Assert.Contains(events, e => e.Contains("changed:count"));
            var count = harness.GetEntityData<int>("target", "count");
            Assert.Equal(1, count);
        }
        finally
        {
            DataObserverIntegrationStrategy.Events = null;
        }
    }

    [Fact]
    public void MultiStrategyEntity_LifecyclePlusActive()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BatchFrameCounterStrategy())
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        var entity = harness.SpawnEntity("hybrid", ["test.int.adv.batch_counter"]);
        entity.AddActiveStrategy("test.int.adv.echo");

        harness.RunFrames(3);

        var count = harness.GetEntityData<int>("hybrid", "count");
        Assert.Equal(3, count);

        var result = harness.InvokeEntityStrategy("hybrid", "test.int.adv.echo", 42);
        Assert.Equal(84, result);
    }

    [Fact]
    public void MultiStrategyEntity_AllThreeTypes()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BatchFrameCounterStrategy())
            .WithStrategy(() => new DataObserverIntegrationStrategy())
            .WithStrategy(() => new EchoActiveStrategy())
            .Build();

        DataObserverIntegrationStrategy.Events = events;
        try
        {
            var target = harness.SpawnEntity("triple", ["test.int.adv.batch_counter"]);
            target.AddActiveStrategy("test.int.adv.echo");

            var observer = harness.SpawnEntity("watcher", []);
            observer.MountObserverStrategy(target, "test.int.adv.data_observer");

            harness.DriveFrame();

            Assert.Contains(events, e => e.Contains("changed:count"));
            var count = harness.GetEntityData<int>("triple", "count");
            Assert.Equal(1, count);

            var result = harness.InvokeEntityStrategy("triple", "test.int.adv.echo", 10);
            Assert.Equal(20, result);
        }
        finally
        {
            DataObserverIntegrationStrategy.Events = null;
        }
    }

    [Fact]
    public void SaveLoad_MultipleEntities_StatePreserved()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BatchFrameCounterStrategy())
            .Build();

        for (var i = 0; i < 10; i++)
            harness.SpawnEntity($"e_{i}", ["test.int.adv.batch_counter"]);

        harness.RunFrames(3);

        for (var i = 0; i < 10; i++)
            harness.SetEntityData($"e_{i}", "tag", i * 10);

        harness.SessionBlackboard.SetValue("global_round", 1);

        var saveId = harness.SaveAndReload("multi_entity_save");

        Assert.True(harness.FileSystem.Exists($"root/save_{saveId}/progress.json"));

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var (foundFlag, round) = gameSession.SessionBlackboard.TryGet<int>("global_round");
        Assert.True(foundFlag);
        Assert.Equal(1, round);

        var entities = gameSession.GetEntities();
        Assert.Equal(10, entities.Count);

        foreach (var e in entities)
        {
            var count = e.GetData<int>("count");
            Assert.Equal(3, count);
            var tag = e.GetData<int>("tag");
            Assert.Equal(0, tag % 10);
        }
    }

    [Fact]
    public void ErrorPath_RequestKillUnknownEntity_Throws()
    {
        var harness = GameplaySimulationHarness.Create().Build();

        Assert.Throws<InvalidOperationException>(
            () => harness.RequestKillEntity("nonexistent"));
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.adv.batch_counter")]
    private sealed class BatchFrameCounterStrategy : SharedFrameCounterStrategy { }

    [StrategyIndex("test.int.adv.data_observer")]
    [ObserveData("count")]
    private sealed class DataObserverIntegrationStrategy : ObserverStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static List<string>? Events
        {
            get => _events.Value;
            set => _events.Value = value;
        }

        public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
            ISndEntity target, string dataKey,
            TypedData oldValue, TypedData newValue) =>
            Events?.Add($"changed:{dataKey}");
    }

    [StrategyIndex("test.int.adv.echo")]
    private sealed class EchoActiveStrategy : SharedEchoActiveStrategy { }
}
