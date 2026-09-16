using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ObserverTopologyIntegrationTests
{
    [Fact]
    public void Observer_Mount_TriggersOnMountedAndDataChange()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            Assert.Contains(events, e => e.EventType == "on_mounted" && e.TargetName == "target");

            harness.SetEntityData("target", "hp", 50);
            Assert.Contains(events, e => e.EventType == "on_data_changed"
                                          && e.TargetName == "target"
                                          && e.DataKey == "hp"
                                          && e.NewValue != null && e.NewValue.Value.AsInt32() == 50);
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_Unmount_StopsNotifying()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            harness.SetEntityData("target", "hp", 10);
            Assert.Contains(events, e => e.EventType == "on_data_changed");

            observer.UnmountObserverStrategy(target, "test.int.obs.topology");

            events.Clear();
            harness.SetEntityData("target", "hp", 20);
            Assert.Empty(events);
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_TargetKilled_TriggersOnUnmounted()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            harness.RequestKillEntity("target");
            harness.DriveFrame();

            Assert.Contains(events, e => e.EventType == "on_unmounted" && e.TargetName == "target");
            Assert.Null(harness.FindEntity("target"));
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_OldAndNewValues_CorrectOnChange()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new ValueCapturingObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            harness.SetEntityData("target", "hp", 100);

            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.value_capture");

            harness.SetEntityData("target", "hp", 50);
            Assert.Contains(events, e => e.EventType == "on_data_changed"
                                          && e.DataKey == "hp"
                                          && e.OldValue != null && e.OldValue.Value.AsInt32() == 100
                                          && e.NewValue != null && e.NewValue.Value.AsInt32() == 50);

            harness.SetEntityData("target", "hp", 0);
            Assert.Contains(events, e => e.EventType == "on_data_changed"
                                          && e.DataKey == "hp"
                                          && e.OldValue != null && e.OldValue.Value.AsInt32() == 50
                                          && e.NewValue != null && e.NewValue.Value.AsInt32() == 0);
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_MultipleTargets_NotifiedIndependently()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TargetAwareObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var targetA = harness.SpawnEntity("target_a", []);
            var targetB = harness.SpawnEntity("target_b", []);
            var observer = harness.SpawnEntity("observer", []);

            observer.MountObserverStrategy(targetA, "test.int.obs.target_aware");
            observer.MountObserverStrategy(targetB, "test.int.obs.target_aware");

            harness.SetEntityData("target_a", "hp", 10);
            Assert.Contains(events, e => e.EventType == "on_data_changed"
                                          && e.TargetName == "target_a"
                                          && e.NewValue != null && e.NewValue.Value.AsInt32() == 10);

            harness.SetEntityData("target_b", "hp", 20);
            Assert.Contains(events, e => e.EventType == "on_data_changed"
                                          && e.TargetName == "target_b"
                                          && e.NewValue != null && e.NewValue.Value.AsInt32() == 20);
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_FrameDriven_StrategyMountsObserverInProcess()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .WithStrategy(() => new AutoMountObserverLifecycleStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            harness.SpawnEntity("auto_observer", ["test.int.obs.auto_mount"]);
            harness.DriveFrame();

            Assert.Contains(events, e => e.EventType == "on_mounted" && e.TargetName == "target");

            harness.SetEntityData("target", "hp", 42);
            Assert.Contains(events, e => e.EventType == "on_data_changed"
                                          && e.TargetName == "target"
                                          && e.NewValue != null && e.NewValue.Value.AsInt32() == 42);
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_AfterLoadFiresBeforeObserverRecoveryOnReload()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .WithStrategy(() => new LifecycleOrderProbeStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", ["test.int.obs.lifecycle_order_probe"]);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            events.Clear();
            harness.SaveAndReload("observer_order_afterload");

            var afterLoad = events.FindIndex(e =>
                e.EventType == "after_load" && e.TargetName == "observer");
            var onMounted = events.FindIndex(e =>
                e.EventType == "on_mounted" && e.TargetName == "target");

            Assert.True(afterLoad >= 0, "Observer AfterLoad must run during reload.");
            Assert.True(onMounted > afterLoad,
                "Observer recovery must mount bindings only after every entity AfterLoad has run.");
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_OnUnmountedFiresBeforeTargetBeforeDead()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .WithStrategy(() => new LifecycleOrderProbeStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", ["test.int.obs.lifecycle_order_probe"]);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            events.Clear();
            harness.RequestKillEntity("target");
            harness.DriveFrame();

            var onUnmounted = events.FindIndex(e =>
                e.EventType == "on_unmounted" && e.TargetName == "target");
            var beforeDead = events.FindIndex(e =>
                e.EventType == "before_dead" && e.TargetName == "target");

            Assert.True(onUnmounted >= 0, "Observer teardown must run during target death.");
            Assert.True(beforeDead > onUnmounted,
                "Observer bindings must be unwired before the target BeforeDead hook runs.");
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    // ── error paths ─────────────────────────────────────────────────

    [Fact]
    public void Observer_MountWithInvalidIndex_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .Build();

        var target = harness.SpawnEntity("target", []);
        var observer = harness.SpawnEntity("observer", []);

        var ex = Assert.Throws<InvalidOperationException>(
            () => observer.MountObserverStrategy(target, "nonexistent.strategy.index"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Observer_DuplicateMount_Throws()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);

            observer.MountObserverStrategy(target, "test.int.obs.topology");

            Assert.Single(events, e => e.EventType == "on_mounted");

            // Mounting the same (observer, target, index) twice would double
            // the subscription and the pool reference; it is rejected.
            var ex = Assert.Throws<InvalidOperationException>(() =>
                observer.MountObserverStrategy(target, "test.int.obs.topology"));
            Assert.Contains("already mounted", ex.Message);

            Assert.Single(events, e => e.EventType == "on_mounted");
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_MountToKilledEntity_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        var target = harness.SpawnEntity("target", []);
        var observer = harness.SpawnEntity("observer", []);

        harness.RequestKillEntity("target");
        harness.DriveFrame();

        var ex = Assert.Throws<InvalidOperationException>(
            () => observer.MountObserverStrategy(target, "test.int.obs.topology"));
        Assert.Contains("pending kill", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Observer_KilledObserverCannotMount_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        var target = harness.SpawnEntity("target", []);
        var observer = harness.SpawnEntity("observer", []);

        harness.RequestKillEntity("observer");
        harness.DriveFrame();

        var ex = Assert.Throws<InvalidOperationException>(
            () => observer.MountObserverStrategy(target, "test.int.obs.topology"));
        Assert.Contains("pending kill", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Observer_MountAcrossSessions_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        var target = harness.SpawnEntity("target", []);
        var otherSession = harness.CreateBackgroundSession("other", "other_level");
        var foreignObserver = otherSession.Spawn(new SndMetaData
        {
            Name = "foreign_observer",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => foreignObserver.MountObserverStrategy(target, "test.int.obs.topology"));
        Assert.Contains("different sessions", ex.Message, StringComparison.Ordinal);
    }

    // ── Save/load round-trip restoration ─────────────────────────────

    [Fact]
    public void Observer_Bindings_RestoredAcrossSaveAndReload()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            harness.SetEntityData("target", "hp", 50);
            Assert.Contains(events, e => e.EventType == "on_data_changed" && e.TargetName == "target");

            harness.SaveAndReload("obs_roundtrip");

            var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
            Assert.NotNull(gameSession);
            events.Clear();
            var reloadedTarget = gameSession.FindByName("target");
            Assert.NotNull(reloadedTarget);
            Assert.NotNull(gameSession.FindByName("observer"));

            reloadedTarget.SetData("hp", 75);
            Assert.Contains(events, e => e.EventType == "on_data_changed" && e.TargetName == "target");
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_OnMounted_FiresAgainAfterReload()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            var mountedBeforeReload = events.Count(e => e.EventType == "on_mounted");
            Assert.Equal(1, mountedBeforeReload);

            harness.SaveAndReload("obs_roundtrip");

            var mountedAfterReload = events.Count(e => e.EventType == "on_mounted" && e.TargetName == "target");
            Assert.Equal(mountedBeforeReload + 1, mountedAfterReload);
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    // ── Session quit teardown ─────────────────────────────────────────

    [Fact]
    public void Observer_OnUnmounted_FiresWhenSessionIsDestroyed()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            events.Clear();
            harness.Context.Runtime.SessionManager.DestroySession("game");

            Assert.Contains(events, e => e.EventType == "on_unmounted" && e.TargetName == "target");
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [Fact]
    public void Observer_TargetDataNoLongerNotifiesAfterSessionDestroyed()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            harness.SetEntityData("target", "hp", 50);
            Assert.Contains(events, e => e.EventType == "on_data_changed");

            harness.Context.Runtime.SessionManager.DestroySession("game");

            events.Clear();
            target.SetData("hp", 60);
            Assert.DoesNotContain(events, e => e.EventType == "on_data_changed");
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.obs.lifecycle_order_probe")]
    private sealed class LifecycleOrderProbeStrategy : LifecycleStrategyBase
    {
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) =>
            EventCollector.Events?.Add(
                new TestObserverEvent("after_load", entity.Name, null, null, null));

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) =>
            EventCollector.Events?.Add(
                new TestObserverEvent("before_dead", entity.Name, null, null, null));
    }

    [StrategyIndex("test.int.obs.topology")]
    [ObserveData("hp")]
    private sealed class TopologyObserverStrategy : ObserverStrategyBase
    {
        public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
            ISndEntity target, string dataKey,
            TypedData oldValue, TypedData newValue) =>
            EventCollector.Events?.Add(
                TestObserverEvent.OnDataChanged(target.Name, dataKey, oldValue, newValue));

        public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            EventCollector.Events?.Add(TestObserverEvent.OnMounted(target.Name));

        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            EventCollector.Events?.Add(TestObserverEvent.OnUnmounted(target.Name));
    }

    [StrategyIndex("test.int.obs.value_capture")]
    [ObserveData("hp")]
    private sealed class ValueCapturingObserverStrategy : ObserverStrategyBase
    {
        public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
            ISndEntity target, string dataKey,
            TypedData oldValue, TypedData newValue) =>
            EventCollector.Events?.Add(
                TestObserverEvent.OnDataChanged(target.Name, dataKey, oldValue, newValue));
    }

    [StrategyIndex("test.int.obs.target_aware")]
    [ObserveData("hp")]
    private sealed class TargetAwareObserverStrategy : ObserverStrategyBase
    {
        public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
            ISndEntity target, string dataKey,
            TypedData oldValue, TypedData newValue) =>
            EventCollector.Events?.Add(
                TestObserverEvent.OnDataChanged(target.Name, dataKey, oldValue, newValue));
    }

    [StrategyIndex("test.int.obs.auto_mount")]
    private sealed class AutoMountObserverLifecycleStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            var target = entity.OwningSession.FindByName("target");
            if (target is not null)
                entity.MountObserverStrategy(target, "test.int.obs.topology");
        }
    }
}
