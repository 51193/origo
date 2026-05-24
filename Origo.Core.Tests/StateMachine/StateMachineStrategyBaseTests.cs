using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
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

    private sealed class TestSmStrategy : StateMachineStrategyBase
    {
    }

    private sealed class StubStateMachineContext : IStateMachineContext
    {
        public int EnqueueCount { get; private set; }
        public IBlackboard SystemBlackboard { get; } = new Blackboard.Blackboard();
        public IBlackboard? ProgressBlackboard => null;
        public IBlackboard? SessionBlackboard => null;
        public ISndSceneAccess SceneAccess => throw new NotImplementedException();

        public void EnqueueBusinessDeferred(Action action)
        {
            EnqueueCount++;
            action();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. LevelBuilder — cover builder methods
// ─────────────────────────────────────────────────────────────────────────────
