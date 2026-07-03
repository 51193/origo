using System.Linq;
using Origo.Core.Logging;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class SndStrategyPoolLeakDetectionTests
{
    [Fact]
    public void LogPoolLeaks_AllReleased_ProducesNoWarnings()
    {
        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new LeakTestStrategy());

        pool.GetStrategy<LifecycleStrategyBase>("leak.test");
        pool.ReleaseStrategy("leak.test");

        pool.LogPoolLeaks();

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void LogPoolLeaks_UnreleasedStrategy_LogsWarning()
    {
        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new LeakTestStrategy());

        pool.GetStrategy<LifecycleStrategyBase>("leak.test");

        pool.LogPoolLeaks();

        Assert.Single(logger.Warnings);
        var warning = logger.Warnings.Single();
        Assert.Contains("leak.test", warning);
        Assert.Contains("refCount", warning);
    }

    [Fact]
    public void LogPoolLeaks_MultipleLeaks_LogsWarningForEach()
    {
        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);
        pool.Register(() => new LeakTestStrategy());
        pool.Register(() => new LeakTestStrategyTwo());

        pool.GetStrategy<LifecycleStrategyBase>("leak.test");
        pool.GetStrategy<LifecycleStrategyBase>("leak.test.two");

        pool.LogPoolLeaks();

        Assert.Equal(2, logger.Warnings.Count);
        Assert.Contains(logger.Warnings, w => w.Contains("leak.test"));
        Assert.Contains(logger.Warnings, w => w.Contains("leak.test.two"));
    }

    [Fact]
    public void LogPoolLeaks_NoStrategiesRegistered_ProducesNoWarnings()
    {
        var logger = new TestLogger();
        var pool = new SndStrategyPool(logger);

        pool.LogPoolLeaks();

        Assert.Empty(logger.Warnings);
    }

    [StrategyIndex("leak.test")]
    private sealed class LeakTestStrategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex("leak.test.two")]
    private sealed class LeakTestStrategyTwo : LifecycleStrategyBase
    {
    }
}
