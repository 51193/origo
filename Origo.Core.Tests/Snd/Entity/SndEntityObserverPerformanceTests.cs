using System;
using System.Diagnostics;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Tests.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
public class SndEntityObserverPerformanceTests
{
    private readonly PerfReporter _perf;

    public SndEntityObserverPerformanceTests(ITestOutputHelper output)
    {
        _perf = PerfReporter.ForTest(output);
    }

    private static SndEntity CreateEntity()
    {
        var world = TestFactory.CreateSndWorld();
        return world.CreateEntity(new NullNodeFactory(), NullSndContext.Instance, new TestLogger());
    }

    // ── Teardown auto-cleanup scaling ───────────────────────────────────

    [Fact]
    public void Teardown_AutoCleanup_Scaling()
    {
        var counts = new[] { 10, 100, 500 };

        foreach (var subCount in counts)
        {
            var observer = CreateEntity();
            var target = CreateEntity();
            observer.SetData("name", "observer");
            target.SetData("hp", 100);

            var ignored = 0;
            for (var i = 0; i < subCount; i++)
                observer.ObserveData(target, "hp", (_, __, oldVal, newVal) => ignored++);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            ((IEntityLifecycle)observer).TeardownOnly();
            sw.Stop();
            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"TeardownOnly auto-cleanup — {subCount} cross-entity data subscriptions",
                subCount,
                sw.Elapsed,
                totalAlloc);
        }
    }

    // ── Subscribe per-subscription allocation ───────────────────────────

    [Fact]
    public void Subscribe_PerSubscription_Allocation()
    {
        const int count = 1000;
        var entity = CreateEntity();
        entity.SetData("hp", 100);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            var captured = i;
            entity.Subscribe("hp", (e1, e2, oldVal, newVal) => { _ = captured; });
        }
        sw.Stop();
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.Report(
            $"Subscribe × {count} (closure per subscription)",
            count,
            sw.Elapsed,
            totalAlloc);
    }

    // ── Cross-entity observation matrix teardown ────────────────────────

    [Fact]
    public void CrossEntityObservation_MatrixTeardown_Scaling()
    {
        var sizes = new[] { 10, 50 };

        foreach (var size in sizes)
        {
            var entities = new SndEntity[size];
            for (var i = 0; i < size; i++)
                entities[i] = CreateEntity();

            var ignored = 0;
            for (var i = 0; i < size; i++)
                for (var j = 0; j < size; j++)
                {
                    if (i == j) continue;
                    entities[i].SetData("hp", 100);
                    entities[j].ObserveData(entities[i], "hp", (_, __, oldVal, newVal) => ignored++);
                }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            foreach (var e in entities)
                ((IEntityLifecycle)e).TeardownOnly();
            sw.Stop();
            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            var totalSubs = size * (size - 1);
            _perf.Report(
                $"Cross-entity teardown — {size}×{size} matrix ({totalSubs} subscriptions)",
                totalSubs,
                sw.Elapsed,
                totalAlloc);

            Assert.True(totalAlloc < 10_000_000,
                $"Matrix teardown {size}×{size}: allocated {totalAlloc} bytes (unexpected)");
        }
    }
}
