using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Blackboard;
using Xunit;

namespace Origo.Core.Tests;

using BlackboardClass = global::Origo.Core.Blackboard.Blackboard;

[Trait("Category", "Benchmark")]
public class BlackboardBenchmarkTests(ITestOutputHelper output)
{
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    [Fact]
    public void SetValue_BulkWrite_ThroughputByType()
    {
        const int iterations = 100_000;

        var rows = new List<(string, int, TimeSpan, long)>
        {
            RunSetValue("Int32", iterations, (bb, i) => bb.SetValue($"k_{i}", i)),
            RunSetValue("Single", iterations, (bb, i) => bb.SetValue($"k_{i}", i * 1.5f)),
            RunSetValue("String", iterations, (bb, i) => bb.SetValue($"k_{i}", $"value_{i}")),
            RunSetValue("Boolean", iterations, (bb, i) => bb.SetValue($"k_{i}", i % 2 == 0)),
        };

        _perf.ReportTable($"Blackboard SetValue bulk write ({iterations:N0} iters)", rows);
    }

    private static (string, int, TimeSpan, long) RunSetValue(
        string type, int iterations, Action<BlackboardClass, int> set)
    {
        var bb = new BlackboardClass();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            set(bb, i);
        sw.Stop();
        var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        return ($"Set {type}", iterations, sw.Elapsed, totalAlloc);
    }

    [Fact]
    public void TryGet_BulkRead_ThroughputByType()
    {
        const int iterations = 500_000;

        var bbInt = new BlackboardClass();
        var bbFloat = new BlackboardClass();
        var bbString = new BlackboardClass();
        var bbBool = new BlackboardClass();

        for (var i = 0; i < iterations; i++)
        {
            bbInt.SetValue($"k_{i}", i);
            bbFloat.SetValue($"k_{i}", i * 1.5f);
            bbString.SetValue($"k_{i}", $"value_{i}");
            bbBool.SetValue($"k_{i}", i % 2 == 0);
        }

        var rows = new List<(string, int, TimeSpan, long)>
        {
            RunTryGet<int>("Int32", bbInt, iterations),
            RunTryGet<float>("Single", bbFloat, iterations),
            RunTryGet<string>("String", bbString, iterations),
            RunTryGet<bool>("Boolean", bbBool, iterations),
        };

        _perf.ReportTable($"Blackboard TryGet bulk read ({iterations:N0} iters)", rows);
    }

    private static (string, int, TimeSpan, long) RunTryGet<T>(
        string type, BlackboardClass bb, int iterations) where T : notnull
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var hits = 0;
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var (found, _) = bb.TryGet<T>($"k_{i}");
            if (found) hits++;
        }
        sw.Stop();
        var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        Assert.Equal(iterations, hits);

        return ($"Get {type}", iterations, sw.Elapsed, totalAlloc);
    }

    [Fact]
    public void SerializeAllDeserializeAll_Roundtrip()
    {
        var sizes = new[] { 100, 500, 1000 };

        // JIT warmup
        {
            var wb = new BlackboardClass();
            wb.SetValue("_w", 42);
            var wd = wb.SerializeAll();
            wb.DeserializeAll(wd);
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var size in sizes)
        {
            var bb = new BlackboardClass();
            for (var i = 0; i < size; i++)
            {
                switch (i % 4)
                {
                    case 0: bb.SetValue($"k_{i}", i); break;
                    case 1: bb.SetValue($"k_{i}", i * 1.5f); break;
                    case 2: bb.SetValue($"k_{i}", $"value_{i}"); break;
                    default: bb.SetValue($"k_{i}", i % 2 == 0); break;
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var data = bb.SerializeAll();
            bb.DeserializeAll(data);
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            rows.Add(($"{size} keys × 2 ops", size * 2, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("Blackboard SerializeAll + DeserializeAll roundtrip", rows);
    }
}
