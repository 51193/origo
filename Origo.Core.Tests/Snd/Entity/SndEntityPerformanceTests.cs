using System;
using System.Collections.Generic;
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
public class SndEntityPerformanceTests
{
    private readonly PerfReporter _perf;

    public SndEntityPerformanceTests(ITestOutputHelper output)
    {
        _perf = PerfReporter.ForTest(output);
    }

    private static SndEntity CreateEntity()
    {
        var world = TestFactory.CreateSndWorld();
        return world.CreateEntity(new NullNodeFactory(), NullSndContext.Instance, new TestLogger());
    }

    private static SndEntity CreateEntityWithHp(int hp)
    {
        var entity = CreateEntity();
        entity.SetData("hp", hp);
        return entity;
    }

    // ── SetData throughput ──────────────────────────────────────────────

    [Fact]
    public void SetData_Throughput_NoObservers()
    {
        const int iterations = 100_000;
        var entity = CreateEntity();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            entity.SetData("counter", i);
        sw.Stop();
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.Report(
            "SetData throughput (no observers)",
            iterations,
            sw.Elapsed,
            totalAlloc);

        Assert.True(totalAlloc < 50_000,
            $"SetData × {iterations}: allocated {totalAlloc} bytes (unexpected)");
    }

    // ── TryGetData throughput ───────────────────────────────────────────

    [Fact]
    public void TryGetData_Throughput()
    {
        const int iterations = 100_000;
        var entity = CreateEntityWithHp(100);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            entity.TryGetData<int>("hp");
        sw.Stop();
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.Report(
            "TryGetData throughput",
            iterations,
            sw.Elapsed,
            totalAlloc);

        Assert.True(totalAlloc < 50_000,
            $"TryGetData × {iterations}: allocated {totalAlloc} bytes (unexpected)");
    }

    // ── SetData with observers ──────────────────────────────────────────

    [Fact]
    public void SetData_WithObservers_SubscriberScaling()
    {
        const int iterations = 10_000;
        var subscriberCounts = new[] { 1, 10, 100, 1000 };
        var entity = CreateEntity();

        foreach (var subCount in subscriberCounts)
        {
            var callCount = 0;
            var subscribers = new List<Action<TypedData, TypedData>>();
            for (var s = 0; s < subCount; s++)
                subscribers.Add((_, _) => callCount++);

            foreach (var sub in subscribers)
                entity.Subscribe("hp", (_, __, oldVal, newVal) => { });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
                entity.SetData("hp", i);
            sw.Stop();
            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"SetData with {subCount} subscribers",
                iterations,
                sw.Elapsed,
                totalAlloc);
        }
    }

    // ── Same-value skip ─────────────────────────────────────────────────

    [Fact]
    public void SetData_SameValue_SkipEfficiency()
    {
        const int iterations = 1_000_000;
        var entity = CreateEntityWithHp(100);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            entity.SetData("hp", 100);
        sw.Stop();
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.Report(
            "SetData same-value skip (no notify)",
            iterations,
            sw.Elapsed,
            totalAlloc);

        Assert.True(totalAlloc < 10_000,
            $"Same-value SetData × {iterations}: allocated {totalAlloc} bytes (unexpected)");
    }

    // ── NotifyObservers allocation by subscriber count ──────────────────

    [Fact]
    public void NotifyObservers_AllocationBySubscriberCount()
    {
        var subscriberCounts = new[] { 1, 10, 100, 1000 };
        var entity = CreateEntity();
        entity.SetData("hp", 0);

        foreach (var subCount in subscriberCounts)
        {
            for (var s = 0; s < subCount; s++)
                entity.Subscribe("hp", (_, __, oldVal, newVal) => { });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            entity.SetData("hp", 1);
            long singleNotifyAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"NotifyObservers single call — {subCount} subscribers",
                1,
                TimeSpan.Zero,
                singleNotifyAlloc);
        }
    }

    // ── SetData with observer filter ─────────────────────────────────────

    [Fact]
    public void SetData_WithObserverFilter_FilteringOverhead()
    {
        const int iterations = 100_000;
        var entity = CreateEntity();
        entity.SetData("counter", 0);

        entity.Subscribe("counter", (_, __, oldVal, newVal) => { },
            filter: (_, __, oldVal, newVal) => false);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            entity.SetData("counter", i);
        sw.Stop();
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.Report(
            "SetData with filtered observer (always skip)",
            iterations,
            sw.Elapsed,
            totalAlloc);
    }
}
