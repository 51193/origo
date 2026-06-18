using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class AutoInitializerGuardTests
{
    [Fact]
    public void DiscoverAndRegisterStrategies_WithoutAttribute_Throws()
    {
        var logger = new TestLogger();
        var world = TestFactory.CreateSndWorld(logger: logger);

        Assert.Throws<InvalidOperationException>(() =>
            OrigoAutoInitializer.DiscoverAndRegisterStrategies(world, logger));
    }

    [Fact]
    public void SndWorld_RegisterStrategy_WithStatefulInstanceField_Throws()
    {
        var world = TestFactory.CreateSndWorld();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            world.RegisterStrategy(() => new StatefulFieldAutoInitStrategy()));
        Assert.Contains("invalid instance members", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SndWorld_RegisterStrategy_WithWritableInstanceProperty_Throws()
    {
        var world = TestFactory.CreateSndWorld();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            world.RegisterStrategy(() => new PropertyStatefulAutoInitStrategy()));
        Assert.Contains("invalid instance members", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SndWorld_RegisterStrategy_WithOnlyStaticFields_Succeeds()
    {
        var world = TestFactory.CreateSndWorld();

        world.RegisterStrategy(() => new StatelessAutoInitStrategy());

        var strategy = world.StrategyPool.GetStrategy<LifecycleStrategyBase>("auto.init.stateless.local");
        Assert.NotNull(strategy);
    }

    [Fact]
    public void DiscoverAndRegisterStrategies_WithBroadSkipPrefixes_ReturnsZero()
    {
        var logger = new TestLogger();
        var world = TestFactory.CreateSndWorld(logger: logger);

        var registered = OrigoAutoInitializer.DiscoverAndRegisterStrategies(
            world, logger, new[] { "Origo" });

        Assert.Equal(0, registered);
    }

    [StrategyIndex(IndexConst)]
    private sealed class AnnotatedStrategy : LifecycleStrategyBase
    {
        public const string IndexConst = "annotated.strategy";
    }

    [StrategyIndex(IndexConst)]
    private sealed class StatefulFieldAutoInitStrategy : LifecycleStrategyBase
    {
        public const string IndexConst = "auto.init.stateful.local";
        private int _counter;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => _counter++;
    }

    [StrategyIndex(IndexConst)]
    private sealed class StatelessAutoInitStrategy : LifecycleStrategyBase
    {
        public const string IndexConst = "auto.init.stateless.local";
        private static int _counter;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => _counter++;
    }

    [StrategyIndex(IndexConst)]
    private sealed class PropertyStatefulAutoInitStrategy : LifecycleStrategyBase
    {
        public const string IndexConst = "auto.init.stateful.property.local";
        public int Counter { get; set; }
    }

    [Fact]
    public void SndWorld_RegisterStrategy_WithReadonlyInstanceField_Succeeds()
    {
        var world = TestFactory.CreateSndWorld();

        world.RegisterStrategy(() => new ReadonlyFieldStrategy());

        var strategy = world.StrategyPool.GetStrategy<LifecycleStrategyBase>("auto.init.readonly.local");
        Assert.NotNull(strategy);
    }

    [StrategyIndex(IndexConst)]
    private sealed class ReadonlyFieldStrategy : LifecycleStrategyBase
    {
        public const string IndexConst = "auto.init.readonly.local";
        private readonly int _readonlyValue = 42;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            _ = _readonlyValue;
        }
    }
}
