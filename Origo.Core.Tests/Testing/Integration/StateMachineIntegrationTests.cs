using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class StateMachineIntegrationTests
{
    [Fact]
    public void StateMachine_PushPop_InFrameLoop()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new PushTrackingStateMachineStrategy())
            .Build();

        var sm = harness.GameSession.GetSessionStateMachines()
            .CreateOrGet("test_sm", "test.int.sm.push_tracker", "test.int.sm.push_tracker");

        sm.Push("idle");
        harness.DriveFrame();
        var (found1, top1) = sm.Peek();
        Assert.True(found1);
        Assert.Equal("idle", top1);

        sm.Push("attack");
        harness.DriveFrame();
        var (found2, top2) = sm.Peek();
        Assert.True(found2);
        Assert.Equal("attack", top2);

        sm.TryPopRuntime(out _);
        harness.DriveFrame();
        var (found3, top3) = sm.Peek();
        Assert.True(found3);
        Assert.Equal("idle", top3);
    }

    [Fact]
    public void StateMachine_OnPushHook_FiresCorrectly()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new HookRecordingStateMachineStrategy())
            .Build();

        HookRecordingStateMachineStrategy.Events = events;
        try
        {
            var sm = harness.GameSession.GetSessionStateMachines()
                .CreateOrGet("test_sm", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");

            sm.Push("menu");
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_push_runtime:menu");
            var (found, top) = sm.Peek();
            Assert.True(found);
            Assert.Equal("menu", top);
        }
        finally
        {
            HookRecordingStateMachineStrategy.Events = null;
        }
    }

    [Fact]
    public void StateMachine_OnPopHook_FiresCorrectly()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new HookRecordingStateMachineStrategy())
            .Build();

        HookRecordingStateMachineStrategy.Events = events;
        try
        {
            var sm = harness.GameSession.GetSessionStateMachines()
                .CreateOrGet("test_sm", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");

            sm.Push("menu");
            harness.DriveFrame();
            sm.TryPopRuntime(out _);
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_pop_runtime:menu");
            var (found, _) = sm.Peek();
            Assert.False(found);
        }
        finally
        {
            HookRecordingStateMachineStrategy.Events = null;
        }
    }

    [Fact]
    public void StateMachine_OnPopBeforeQuit_FiresOnSessionDestroy()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new HookRecordingStateMachineStrategy())
            .Build();

        HookRecordingStateMachineStrategy.Events = events;
        try
        {
            var sm = harness.GameSession.GetSessionStateMachines()
                .CreateOrGet("test_sm", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");
            sm.Push("active");
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_push_runtime:active");

            harness.Context.Runtime.SessionManager.DestroySession("game");

            Assert.Contains(events, e => e == "on_pop_before_quit:active");
        }
        finally
        {
            HookRecordingStateMachineStrategy.Events = null;
        }
    }

    [Fact]
    public void StateMachine_SaveLoad_PreservesStack()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new HookRecordingStateMachineStrategy())
            .Build();

        HookRecordingStateMachineStrategy.Events = events;
        try
        {
            var sm = harness.GameSession.GetSessionStateMachines()
                .CreateOrGet("test_sm", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");

            sm.Push("menu");
            sm.Push("gameplay");
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_push_runtime:gameplay");

            events.Clear();

            harness.SaveAndReload("sm_save");

            Assert.Contains(events, e => e == "on_push_after_load:menu");
            Assert.Contains(events, e => e == "on_push_after_load:gameplay");

            var restoredSession = harness.Context.Runtime.SessionManager.TryGet("game");
            Assert.NotNull(restoredSession);

            var restoredSm = restoredSession.GetSessionStateMachines()
                .CreateOrGet("test_sm", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");
            var (found, top) = restoredSm.Peek();
            Assert.True(found);
            Assert.Equal("gameplay", top);

            restoredSm.TryPopRuntime(out _);
            harness.DriveFrame();
            var (found2, top2) = restoredSm.Peek();
            Assert.True(found2);
            Assert.Equal("menu", top2);
        }
        finally
        {
            HookRecordingStateMachineStrategy.Events = null;
        }
    }

    [Fact]
    public void StateMachine_MultipleEntities_IndependentStacks()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new HookRecordingStateMachineStrategy())
            .Build();

        HookRecordingStateMachineStrategy.Events = events;
        try
        {
            var smA = harness.GameSession.GetSessionStateMachines()
                .CreateOrGet("sm_a", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");
            var smB = harness.GameSession.GetSessionStateMachines()
                .CreateOrGet("sm_b", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");

            smA.Push("idle_a");
            smB.Push("idle_b");
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_push_runtime:idle_a");
            Assert.Contains(events, e => e == "on_push_runtime:idle_b");

            smA.Push("active_a");
            harness.DriveFrame();

            var (foundA, topA) = smA.Peek();
            Assert.True(foundA);
            Assert.Equal("active_a", topA);

            var (foundB, topB) = smB.Peek();
            Assert.True(foundB);
            Assert.Equal("idle_b", topB);

            smA.TryPopRuntime(out _);
            harness.DriveFrame();

            var (foundA2, topA2) = smA.Peek();
            Assert.True(foundA2);
            Assert.Equal("idle_a", topA2);

            var (foundB2, topB2) = smB.Peek();
            Assert.True(foundB2);
            Assert.Equal("idle_b", topB2);
        }
        finally
        {
            HookRecordingStateMachineStrategy.Events = null;
        }
    }

    [Fact]
    public void StateMachine_EntityLifecycleStrategy_PushesAndPopsState()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new HookRecordingStateMachineStrategy())
            .WithStrategy(() => new SmPushingLifecycleStrategy())
            .Build();

        HookRecordingStateMachineStrategy.Events = events;
        try
        {
            harness.SpawnEntity("sm_entity", ["test.int.sm.lifecycle_pusher"]);
            harness.DriveFrame();

            Assert.Contains(events, e => e == "on_push_runtime:active");

            harness.RunFrames(2);

            Assert.Contains(events, e => e == "on_pop_runtime:active");
            Assert.Contains(events, e => e == "on_push_runtime:idle");
        }
        finally
        {
            HookRecordingStateMachineStrategy.Events = null;
        }
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.sm.push_tracker")]
    private sealed class PushTrackingStateMachineStrategy : SharedNoopStateMachineStrategy { }

    [StrategyIndex("test.int.sm.hook_recorder")]
    private sealed class HookRecordingStateMachineStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static List<string>? Events
        {
            get => _events.Value;
            set => _events.Value = value;
        }

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add($"on_push_runtime:{context.AfterTop}");

        public override void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add($"on_push_after_load:{context.AfterTop}");

        public override void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add($"on_pop_runtime:{context.BeforeTop}");

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add($"on_pop_before_quit:{context.BeforeTop}");
    }

    [StrategyIndex("test.int.sm.lifecycle_pusher")]
    private sealed class SmPushingLifecycleStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            var sm = entity.OwningSession.GetSessionStateMachines()
                .CreateOrGet("entity_sm", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");
            sm.Push("active");
            entity.SetData("sm_frame", 0);
        }

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var frame = entity.GetData<int>("sm_frame");
            frame++;
            entity.SetData("sm_frame", frame);

            var sm = entity.OwningSession.GetSessionStateMachines()
                .CreateOrGet("entity_sm", "test.int.sm.hook_recorder", "test.int.sm.hook_recorder");

            var (found, top) = sm.Peek();
            if (frame >= 3 && found && top == "active")
            {
                sm.TryPopRuntime(out _);
                sm.Push("idle");
            }
        }
    }
}
