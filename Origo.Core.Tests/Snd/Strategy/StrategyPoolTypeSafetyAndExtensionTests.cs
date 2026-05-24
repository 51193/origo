using System;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class StrategyPoolTypeSafetyAndExtensionTests
{
    [Fact]
    public void GetStrategy_WrongBranchGeneric_ThrowsInvalidOperation()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolEntityStrategy());
        pool.Register(() => new PoolStateMachineStrategy());

        Assert.Throws<InvalidOperationException>(() => pool.GetStrategy<StateMachineStrategyBase>("pool.entity"));
        Assert.Throws<InvalidOperationException>(() => pool.GetStrategy<EntityStrategyBase>("pool.sm"));
    }

    [Fact]
    public void GetStrategy_WrongBranchGeneric_DoesNotLeakReferenceCount()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolEntityStrategy());

        Assert.Throws<InvalidOperationException>(() => pool.GetStrategy<StateMachineStrategyBase>("pool.entity"));

        var first = pool.GetStrategy<EntityStrategyBase>("pool.entity");
        pool.ReleaseStrategy("pool.entity");
        var second = pool.GetStrategy<EntityStrategyBase>("pool.entity");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void StackStateMachine_WhenSecondAcquireFails_ReleasesFirstAcquire()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolStateMachineStrategy());
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));

        Assert.Throws<InvalidOperationException>(() =>
            new StackStateMachine("machine", "pool.sm", "missing.pop", pool, sndContext));

        var first = pool.GetStrategy<StateMachineStrategyBase>("pool.sm");
        pool.ReleaseStrategy("pool.sm");
        var second = pool.GetStrategy<StateMachineStrategyBase>("pool.sm");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetStrategy_ThirdDomainBase_AssignsThroughExpectedAbstraction()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new ExtensionDomainConcreteStrategy());

        var s = pool.GetStrategy<ExtensionDomainStrategyBase>("ext.domain.probe");
        Assert.Equal("ok", s.ProbeValue());
    }

    /// <summary>示例：在统一根基类之上扩展第三领域策略基类，仍复用同一策略池与索引机制。</summary>
    public abstract class ExtensionDomainStrategyBase : BaseStrategy
    {
        public abstract string ProbeValue();
    }

    [StrategyIndex("ext.domain.probe")]
    private sealed class ExtensionDomainConcreteStrategy : ExtensionDomainStrategyBase
    {
        public override string ProbeValue() => "ok";
    }

    [StrategyIndex("pool.entity")]
    private sealed class PoolEntityStrategy : EntityStrategyBase
    {
    }

    [StrategyIndex("pool.sm")]
    private sealed class PoolStateMachineStrategy : StateMachineStrategyBase
    {
    }
}
