using System;
using System.Collections.Generic;
using System.Threading;
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
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        TopologyObserverStrategy.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            Assert.Contains(events, e => e == "on_mounted:target");

            target.SetData("hp", 50);
            Assert.Contains(events, e => e.Contains("changed") && e.Contains("(Int32)50"));
        }
        finally
        {
            TopologyObserverStrategy.Events = null;
        }
    }

    [Fact]
    public void Observer_Unmount_StopsNotifying()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        TopologyObserverStrategy.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            target.SetData("hp", 10);
            Assert.Contains(events, e => e.Contains("changed"));

            observer.UnmountObserverStrategy(target, "test.int.obs.topology");

            events.Clear();
            target.SetData("hp", 20);
            Assert.Empty(events);
        }
        finally
        {
            TopologyObserverStrategy.Events = null;
        }
    }

    [Fact]
    public void Observer_TargetKilled_TriggersOnUnmounted()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .Build();

        TopologyObserverStrategy.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.topology");

            harness.RequestKillEntity("target");
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_unmounted:target");
            Assert.Null(harness.FindEntity("target"));
        }
        finally
        {
            TopologyObserverStrategy.Events = null;
        }
    }

    [Fact]
    public void Observer_OldAndNewValues_CorrectOnChange()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new ValueCapturingObserverStrategy())
            .Build();

        ValueCapturingObserverStrategy.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            target.SetData("hp", 100);

            var observer = harness.SpawnEntity("observer", []);
            observer.MountObserverStrategy(target, "test.int.obs.value_capture");

            target.SetData("hp", 50);
            Assert.Contains(events, e => e.Contains("old:(Int32)100") && e.Contains("new:(Int32)50"));

            target.SetData("hp", 0);
            Assert.Contains(events, e => e.Contains("old:(Int32)50") && e.Contains("new:(Int32)0"));
        }
        finally
        {
            ValueCapturingObserverStrategy.Events = null;
        }
    }

    [Fact]
    public void Observer_MultipleTargets_NotifiedIndependently()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TargetAwareObserverStrategy())
            .Build();

        TargetAwareObserverStrategy.Events = events;
        try
        {
            var targetA = harness.SpawnEntity("target_a", []);
            var targetB = harness.SpawnEntity("target_b", []);
            var observer = harness.SpawnEntity("observer", []);

            observer.MountObserverStrategy(targetA, "test.int.obs.target_aware");
            observer.MountObserverStrategy(targetB, "test.int.obs.target_aware");

            targetA.SetData("hp", 10);
            Assert.Contains(events, e => e.Contains("target:target_a") && e.Contains("(Int32)10"));

            targetB.SetData("hp", 20);
            Assert.Contains(events, e => e.Contains("target:target_b") && e.Contains("(Int32)20"));
        }
        finally
        {
            TargetAwareObserverStrategy.Events = null;
        }
    }

    [Fact]
    public void Observer_FrameDriven_StrategyMountsObserverInProcess()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TopologyObserverStrategy())
            .WithStrategy(() => new AutoMountObserverLifecycleStrategy())
            .Build();

        TopologyObserverStrategy.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            harness.SpawnEntity("auto_observer", ["test.int.obs.auto_mount"]);
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_mounted:target");

            harness.SetEntityData("target", "hp", 42);
            Assert.Contains(events, e => e.Contains("changed") && e.Contains("42"));
        }
        finally
        {
            TopologyObserverStrategy.Events = null;
        }
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.obs.topology")]
    [ObserveData("hp")]
    private sealed class TopologyObserverStrategy : ObserverStrategyBase
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
            Events?.Add($"changed:{dataKey}={newValue}");

        public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            Events?.Add($"on_mounted:{target.Name}");

        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            Events?.Add($"on_unmounted:{target.Name}");
    }

    [StrategyIndex("test.int.obs.value_capture")]
    [ObserveData("hp")]
    private sealed class ValueCapturingObserverStrategy : ObserverStrategyBase
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
            Events?.Add($"old:{oldValue}:new:{newValue}");
    }

    [StrategyIndex("test.int.obs.target_aware")]
    [ObserveData("hp")]
    private sealed class TargetAwareObserverStrategy : ObserverStrategyBase
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
            Events?.Add($"target:{target.Name}:{newValue}");
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
