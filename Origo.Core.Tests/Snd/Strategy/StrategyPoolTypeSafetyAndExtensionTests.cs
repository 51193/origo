using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
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
        Assert.Throws<InvalidOperationException>(() => pool.GetStrategy<LifecycleStrategyBase>("pool.sm"));
    }

    [Fact]
    public void GetStrategy_WrongBranchGeneric_DoesNotLeakReferenceCount()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolEntityStrategy());

        Assert.Throws<InvalidOperationException>(() => pool.GetStrategy<StateMachineStrategyBase>("pool.entity"));

        var first = pool.GetStrategy<LifecycleStrategyBase>("pool.entity");
        pool.ReleaseStrategy("pool.entity");
        var second = pool.GetStrategy<LifecycleStrategyBase>("pool.entity");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void StackStateMachine_WhenSecondAcquireFails_ReleasesFirstAcquire()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolStateMachineStrategy());
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var sndContext = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));

        Assert.Throws<InvalidOperationException>(() =>
            new StackStateMachine("machine", "pool.sm", "missing.pop", pool, sndContext.StateMachineContext));

        var first = pool.GetStrategy<StateMachineStrategyBase>("pool.sm");
        pool.ReleaseStrategy("pool.sm");
        var second = pool.GetStrategy<StateMachineStrategyBase>("pool.sm");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void RecoverStrategiesOnly_WithNonLifecycleStrategy_Throws()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolActiveStrategy());
        pool.Register(() => new PoolEntityStrategy());

        var mgr = new SndStrategyManager(pool, NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            mgr.RecoverStrategiesOnly(["pool.active_for_entity"]));
        Assert.Contains("LifecycleStrategyBase", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoverStrategiesOnly_WithOnlyValidStrategies_Succeeds()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolEntityStrategy());

        var mgr = new SndStrategyManager(pool, NullLogger.Instance);

        var ex = Record.Exception(() =>
            mgr.RecoverStrategiesOnly(["pool.entity"]));
        Assert.Null(ex);
    }

    [Fact]
    public void Register_AbstractStrategyType_Throws()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);

        Assert.Throws<InvalidOperationException>(() =>
            pool.Register(typeof(AbstractPoolStrategy), () => new PoolEntityStrategy()));
    }

    [Fact]
    public void Register_DuplicateIndex_Throws()
    {
        var pool = new SndStrategyPool(NullLogger.Instance);
        pool.Register(() => new PoolEntityStrategy());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            pool.Register(() => new PoolEntityStrategy()));
        Assert.Contains("already registered", ex.Message, StringComparison.Ordinal);
    }

    [StrategyIndex("pool.entity")]
    private sealed class PoolEntityStrategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex("pool.sm")]
    private sealed class PoolStateMachineStrategy : StateMachineStrategyBase
    {
    }

    [StrategyIndex("pool.active_for_entity")]
    private sealed class PoolActiveStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => null;
    }
}

[StrategyIndex("pool.abstract")]
public abstract class AbstractPoolStrategy : LifecycleStrategyBase
{
}
