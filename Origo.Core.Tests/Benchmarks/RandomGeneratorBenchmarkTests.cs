using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

[Trait("Category", "Benchmark")]
public class RandomGeneratorBenchmarkTests(ITestOutputHelper output)
{
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    [Fact]
    public void NextUInt64_Throughput()
    {
        const int iterations = 10_000_000;
        var state = RandomNumberGenerator.CreateStateFromSeed("benchmark");

        var best = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var (s0, s1) = state;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
                (_, s0, s1) = RandomNumberGenerator.NextUInt64(s0, s1);
            sw.Stop();

            GC.KeepAlive(s0);
            GC.KeepAlive(s1);

            if (round >= _warmupRounds && sw.Elapsed < best)
                best = sw.Elapsed;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var (s0a, s1a) = state;
        for (var i = 0; i < iterations; i++)
            (_, s0a, s1a) = RandomNumberGenerator.NextUInt64(s0a, s1a);
        var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        GC.KeepAlive(s0a);

        _perf.ReportTable(
            $"XorShift128+ NextUInt64 ({iterations:N0} iters, min of {_timedRounds})",
            [
                ("Standalone NextUInt64", iterations, best, totalAlloc)
            ]);
    }

    [Fact]
    public void NextFunctions_ThroughputComparison()
    {
        const int iterations = 5_000_000;
        var state = RandomNumberGenerator.CreateStateFromSeed("benchmark");

        var rows = new List<(string, int, TimeSpan, long)>
        {
            RunNextUInt64(state, iterations),
            RunNextInt64(state, iterations),
            RunNextInt32(state, iterations),
        };

        _perf.ReportTable(
            $"XorShift128+ function throughput ({iterations:N0} iters each, min of {_timedRounds})",
            rows);
    }

    private static (string, int, TimeSpan, long) RunNextUInt64((ulong s0, ulong s1) state, int iterations)
    {
        var best = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var (s0, s1) = state;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                ulong nextS0, nextS1;
                (_, nextS0, nextS1) = RandomNumberGenerator.NextUInt64(s0, s1);
                s0 = nextS0;
                s1 = nextS1;
            }
            sw.Stop();
            GC.KeepAlive(s0);
            GC.KeepAlive(s1);

            if (round >= _warmupRounds && sw.Elapsed < best)
                best = sw.Elapsed;
        }

        var (genAlloc, _) = MeasureNextAlloc(state, iterations, 0);
        return ("NextUInt64", iterations, best, genAlloc);
    }

    private static (string, int, TimeSpan, long) RunNextInt64((ulong s0, ulong s1) state, int iterations)
    {
        var best = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var (s0, s1) = state;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                ulong nextS0, nextS1;
                (_, nextS0, nextS1) = RandomNumberGenerator.NextInt64(s0, s1);
                s0 = nextS0;
                s1 = nextS1;
            }
            sw.Stop();
            GC.KeepAlive(s0);
            GC.KeepAlive(s1);

            if (round >= _warmupRounds && sw.Elapsed < best)
                best = sw.Elapsed;
        }

        var (genAlloc, _) = MeasureNextAlloc(state, iterations, 1);
        return ("NextInt64", iterations, best, genAlloc);
    }

    private static (string, int, TimeSpan, long) RunNextInt32((ulong s0, ulong s1) state, int iterations)
    {
        var best = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var (s0, s1) = state;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                ulong nextS0, nextS1;
                (_, nextS0, nextS1) = RandomNumberGenerator.NextInt32(s0, s1);
                s0 = nextS0;
                s1 = nextS1;
            }
            sw.Stop();
            GC.KeepAlive(s0);
            GC.KeepAlive(s1);

            if (round >= _warmupRounds && sw.Elapsed < best)
                best = sw.Elapsed;
        }

        var (genAlloc, _) = MeasureNextAlloc(state, iterations, 2);
        return ("NextInt32", iterations, best, genAlloc);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long alloc, ulong dum) MeasureNextAlloc(
        (ulong s0, ulong s1) state, int iterations, int mode)
    {
        var (s0, s1) = state;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            if (mode == 0)
                (_, s0, s1) = RandomNumberGenerator.NextUInt64(s0, s1);
            else if (mode == 1)
                (_, s0, s1) = RandomNumberGenerator.NextInt64(s0, s1);
            else
                (_, s0, s1) = RandomNumberGenerator.NextInt32(s0, s1);
        }
        var alloc = GC.GetAllocatedBytesForCurrentThread() - start;
        return (alloc, s0);
    }
}
