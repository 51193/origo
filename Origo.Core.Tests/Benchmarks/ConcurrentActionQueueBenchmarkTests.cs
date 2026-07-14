using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Scheduling;
using Xunit;

namespace Origo.Core.Tests;

[Trait("Category", "Benchmark")]
public class ConcurrentActionQueueBenchmarkTests(ITestOutputHelper output)
{
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    [Fact]
    public void EnqueueAndExecuteAll_ScalingByActionCount()
    {
        var actionCounts = new[] { 100, 1000, 10_000 };

        // JIT warmup
        {
            var wq = new ConcurrentActionQueue(new TestLogger());
            wq.Enqueue(() => { });
            wq.ExecuteAll();
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var count in actionCounts)
        {
            var queue = new ConcurrentActionQueue(new TestLogger());
            var counter = 0;
            for (var i = 0; i < count; i++)
                queue.Enqueue(() => counter++);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var executed = queue.ExecuteAll();
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            Assert.Equal(count, executed);
            Assert.Equal(count, counter);

            rows.Add(($"{count} actions", count, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("ConcurrentActionQueue Enqueue + ExecuteAll — scaling by count", rows);
    }

    [Fact]
    public void EnqueueThroughput_BulkInsert()
    {
        var sizes = new[] { 1000, 10_000, 50_000 };

        // JIT warmup
        {
            var wq = new ConcurrentActionQueue(new TestLogger());
            wq.Enqueue(() => { });
            wq.Clear();
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var size in sizes)
        {
            var queue = new ConcurrentActionQueue(new TestLogger());

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < size; i++)
                queue.Enqueue(() => { });
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            Assert.Equal(size, queue.Count);
            queue.Clear();

            rows.Add(($"{size} enqueues", size, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("ConcurrentActionQueue Enqueue throughput", rows);
    }
}
