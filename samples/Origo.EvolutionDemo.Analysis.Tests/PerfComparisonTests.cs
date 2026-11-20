using System;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Perf;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for the symmetric-union compare, n/a semantics, and GC normalization.</summary>
public sealed class PerfComparisonTests
{
    [Fact]
    public void Compare_OneSidedPhaseRowsAreNaAndWarned()
    {
        var baseline = new PerfAggregator(
        [
            PerfFixture.Tick(10, 1d, phases: [("sys.alpha", 10d, 100L)]),
            PerfFixture.Tick(20, 2d, phases: [("sys.alpha", 10d, 100L)]),
        ]);
        var current = new PerfAggregator(
        [
            PerfFixture.Tick(10, 1d, phases: [("sys.alpha", 12d, 100L), ("sys.beta", 5d, 50L)]),
            PerfFixture.Tick(20, 2d, phases: [("sys.alpha", 12d, 100L), ("sys.beta", 5d, 50L)]),
        ]);

        var result = PerfComparison.Compare(baseline, current);
        var betaRows = result.Rows.Where(static row => row.Metric.StartsWith("sys.beta", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, betaRows.Length);
        Assert.All(betaRows, static row =>
        {
            Assert.Null(row.Baseline);
            Assert.NotNull(row.Current);
            Assert.Null(row.DeltaPercent);
        });
        Assert.Contains(result.Warnings,
            static warning => warning.Contains("system 'sys.beta' exists only in the current profile",
                StringComparison.Ordinal));
        var alphaMean = Assert.Single(result.Rows, static row => row.Metric == "sys.alpha mean ms");
        Assert.Equal(20d, alphaMean.DeltaPercent!.Value, 10);
    }

    [Fact]
    public void Compare_ZeroBaselineIsNaAndWarned()
    {
        var baseline = new PerfAggregator(
        [
            PerfFixture.Tick(10, 1d, phases: [("sys.alpha", 0d, 0L)]),
            PerfFixture.Tick(20, 2d, phases: [("sys.alpha", 0d, 0L)]),
        ]);
        var current = new PerfAggregator(
        [
            PerfFixture.Tick(10, 1d, phases: [("sys.alpha", 5d, 0L)]),
            PerfFixture.Tick(20, 2d, phases: [("sys.alpha", 5d, 0L)]),
        ]);

        var result = PerfComparison.Compare(baseline, current);
        var mean = Assert.Single(result.Rows, static row => row.Metric == "sys.alpha mean ms");

        Assert.Equal(0d, mean.Baseline);
        Assert.Equal(5d, mean.Current);
        Assert.Null(mean.DeltaPercent);
        Assert.Contains(result.Warnings,
            static warning => warning.Contains("zero baseline", StringComparison.Ordinal)
                && warning.Contains("'sys.alpha mean ms'", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_NormalizesGcBySamplingInterval()
    {
        // The same workload sampled every tick (1000 samples) and every 10 ticks
        // (100 samples): raw sums differ 10x, normalized estimates are equal.
        var everyTick = Enumerable.Range(0, 1000)
            .Select(index => PerfFixture.Tick(index + 1, 1d + index, gc0: 1, phases: PerfFixture.StandardPhases()))
            .ToArray();
        var everyTenTicks = Enumerable.Range(0, 100)
            .Select(index => PerfFixture.Tick((index + 1) * 10, 1d + index, gc0: 1, phases: PerfFixture.StandardPhases()))
            .ToArray();
        var baseline = new PerfAggregator(everyTick);
        var current = new PerfAggregator(everyTenTicks);

        var result = PerfComparison.Compare(baseline, current);
        var gc0 = Assert.Single(result.Rows, static row => row.Metric == "gc0 est./run");

        Assert.Equal(1000d, gc0.Baseline);
        Assert.Equal(1000d, gc0.Current);
        Assert.Equal(0d, gc0.DeltaPercent!.Value, 10);
        Assert.Contains(result.Warnings,
            static warning => warning.Contains("different or unknown tick intervals", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_SelfCompareHasZeroDeltaForEveryDefinedRow()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 20));
        var result = PerfComparison.Compare(aggregate, aggregate);

        Assert.NotEmpty(result.Rows);
        Assert.All(result.Rows, static row =>
        {
            if (row.Baseline is { } baseline && row.Current is { } current && baseline != 0)
                Assert.Equal(0d, row.DeltaPercent!.Value, 10);
        });
    }

    [Fact]
    public void Compare_SymmetricUnionCoversSystemsAndSubphases()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 10));
        var result = PerfComparison.Compare(aggregate, aggregate);
        var metrics = result.Rows.Select(static row => row.Metric).ToArray();

        Assert.Equal(20, result.Rows.Count);
        Assert.Contains("sys.alpha mean ms", metrics);
        Assert.Contains("sys.beta p95 ms", metrics);
        Assert.Contains("sys.alpha.child alloc mean MiB", metrics);
        Assert.Contains("sys.alpha.other mean ms", metrics);
    }

    [Fact]
    public void Compare_GcRowsUseNormalizedEstimateMetricNames()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 10));
        var result = PerfComparison.Compare(aggregate, aggregate);
        var gcRows = result.Rows.Where(static row => row.Metric.StartsWith("gc", StringComparison.Ordinal)).ToArray();

        Assert.Equal(3, gcRows.Length);
        Assert.All(gcRows, static row => Assert.Equal(PerfMetricUnit.Collections, row.Unit));
        Assert.Equal(["gc0 est./run", "gc1 est./run", "gc2 est./run"], [.. gcRows.Select(static row => row.Metric)]);
    }

    [Fact]
    public void Compare_ThrowsOnNullAggregates()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 3));

        Assert.Throws<ArgumentNullException>(() => PerfComparison.Compare(null!, aggregate));
        Assert.Throws<ArgumentNullException>(() => PerfComparison.Compare(aggregate, null!));
    }
}
