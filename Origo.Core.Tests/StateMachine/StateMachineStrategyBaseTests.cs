using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class StateMachineStrategyBaseTests
{
    [Fact]
    public void DefaultHooks_DoNotScheduleActions()
    {
        var strategy = new TestSmStrategy();
        var smCtx = new StateMachineStrategyContext("machine1", null, "state_a");
        var ctx = new StubStateMachineContext();

        strategy.OnPushRuntime(smCtx, ctx);
        strategy.OnPushAfterLoad(smCtx, ctx);
        strategy.OnPopRuntime(smCtx, ctx);
        strategy.OnPopBeforeQuit(smCtx, ctx);

        Assert.Equal(0, ctx.EnqueueCount);
    }

    // ── Integration: StackStateMachine with strategy hooks ──────────

    private const string _smPushIdx = "test.sm.push";
    private const string _smPopIdx = "test.sm.pop";

    [StrategyIndex(_smPushIdx)]
    private sealed class TrackingPushStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _pushRuntimeCalls = new();
        public static List<string> PushRuntimeCalls => _pushRuntimeCalls.Value ??= [];
        private static readonly AsyncLocal<List<string>> _pushAfterLoadCalls = new();
        public static List<string> PushAfterLoadCalls => _pushAfterLoadCalls.Value ??= [];

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) => PushRuntimeCalls.Add(context.AfterTop ?? "");

        public override void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx) => PushAfterLoadCalls.Add(context.AfterTop ?? "");
    }

    [StrategyIndex(_smPopIdx)]
    private sealed class TrackingPopStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _popRuntimeCalls = new();
        public static List<string> PopRuntimeCalls => _popRuntimeCalls.Value ??= [];
        private static readonly AsyncLocal<List<string>> _popBeforeQuitCalls = new();
        public static List<string> PopBeforeQuitCalls => _popBeforeQuitCalls.Value ??= [];

        public override void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) => PopRuntimeCalls.Add(context.BeforeTop ?? "");

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) => PopBeforeQuitCalls.Add(context.BeforeTop ?? "");
    }

    [Fact]
    public void Push_TriggersOnPushRuntime()
    {
        TrackingPushStrategy.PushRuntimeCalls.Clear();
        TrackingPushStrategy.PushAfterLoadCalls.Clear();
        TrackingPopStrategy.PopRuntimeCalls.Clear();
        TrackingPopStrategy.PopBeforeQuitCalls.Clear();

        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new TrackingPushStrategy());
        pool.Register(() => new TrackingPopStrategy());

        var ctx = new StubStateMachineContext();
        var sm = new StackStateMachine("machine1", _smPushIdx, _smPopIdx, pool, ctx);

        sm.Push("state_a");

        Assert.Single(TrackingPushStrategy.PushRuntimeCalls);
        Assert.Equal("state_a", TrackingPushStrategy.PushRuntimeCalls[0]);
        Assert.Empty(TrackingPushStrategy.PushAfterLoadCalls);

        sm.Dispose();
    }

    [Fact]
    public void Pop_TriggersOnPopRuntime()
    {
        TrackingPushStrategy.PushRuntimeCalls.Clear();
        TrackingPopStrategy.PopRuntimeCalls.Clear();
        TrackingPopStrategy.PopBeforeQuitCalls.Clear();

        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new TrackingPushStrategy());
        pool.Register(() => new TrackingPopStrategy());

        var ctx = new StubStateMachineContext();
        var sm = new StackStateMachine("machine1", _smPushIdx, _smPopIdx, pool, ctx);

        sm.Push("state_a");
        var result = sm.TryPopRuntime(out _);

        Assert.True(result);
        Assert.Single(TrackingPopStrategy.PopRuntimeCalls);
        Assert.Equal("state_a", TrackingPopStrategy.PopRuntimeCalls[0]);

        sm.Dispose();
    }

    [Fact]
    public void Quit_PopTriggersOnPopBeforeQuit()
    {
        TrackingPushStrategy.PushRuntimeCalls.Clear();
        TrackingPopStrategy.PopBeforeQuitCalls.Clear();

        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new TrackingPushStrategy());
        pool.Register(() => new TrackingPopStrategy());

        var ctx = new StubStateMachineContext();
        var sm = new StackStateMachine("machine1", _smPushIdx, _smPopIdx, pool, ctx);

        sm.Push("state_a");
        sm.TryPopOnQuit(out _);

        Assert.Single(TrackingPopStrategy.PopBeforeQuitCalls);
        Assert.Equal("state_a", TrackingPopStrategy.PopBeforeQuitCalls[0]);

        sm.Dispose();
    }

    [Fact]
    public void AfterLoad_TriggersOnPushAfterLoad_BottomToTop()
    {
        TrackingPushStrategy.PushAfterLoadCalls.Clear();
        TrackingPushStrategy.PushRuntimeCalls.Clear();

        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new TrackingPushStrategy());
        pool.Register(() => new TrackingPopStrategy());

        var ctx = new StubStateMachineContext();
        var sm = new StackStateMachine("machine1", _smPushIdx, _smPopIdx, pool, ctx);

        sm.Push("state_bottom");
        sm.Push("state_top");

        TrackingPushStrategy.PushRuntimeCalls.Clear();

        sm.FlushAfterLoad();

        Assert.Equal(2, TrackingPushStrategy.PushAfterLoadCalls.Count);
        Assert.Equal("state_bottom", TrackingPushStrategy.PushAfterLoadCalls[0]);
        Assert.Equal("state_top", TrackingPushStrategy.PushAfterLoadCalls[1]);

        sm.Dispose();
    }

    [Fact]
    public void Container_PopAllOnQuit_TriggersPopBeforeQuit_OnAllMachines()
    {
        TrackingPopStrategy.PopBeforeQuitCalls.Clear();
        TrackingPushStrategy.PushRuntimeCalls.Clear();

        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new TrackingPushStrategy());
        pool.Register(() => new TrackingPopStrategy());

        var ctx = new StubStateMachineContext();
        var container = new StateMachineContainer(pool, ctx);
        container.CreateOrGet("machine_a", _smPushIdx, _smPopIdx);
        container.CreateOrGet("machine_b", _smPushIdx, _smPopIdx);

        container.TryGet("machine_a", out var smA);
        container.TryGet("machine_b", out var smB);
        smA!.Push("state_a");
        smB!.Push("state_b");

        container.PopAllOnQuit();

        Assert.Equal(2, TrackingPopStrategy.PopBeforeQuitCalls.Count);
        Assert.Contains("state_a", TrackingPopStrategy.PopBeforeQuitCalls);
        Assert.Contains("state_b", TrackingPopStrategy.PopBeforeQuitCalls);
    }

    private sealed class TestSmStrategy : StateMachineStrategyBase
    {
    }

    private sealed class StubStateMachineContext : IStateMachineContext
    {
        public int EnqueueCount { get; private set; }
        public IBlackboard SystemBlackboard { get; } = new Blackboard.Blackboard();
        public IBlackboard? ProgressBlackboard => null;
        public IBlackboard? SessionBlackboard => null;
        public ISndSceneReadAccess SceneAccess => throw new NotImplementedException();

        public void EnqueueBusinessDeferred(Action action)
        {
            EnqueueCount++;
            action();
        }

        public static void FlushDeferredActionsForCurrentFrame()
        {
        }

        public int GetPendingPersistenceRequestCount() => 0;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. LevelBuilder — cover builder methods
// ─────────────────────────────────────────────────────────────────────────────
