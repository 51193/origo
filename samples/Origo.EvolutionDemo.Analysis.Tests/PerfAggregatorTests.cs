using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Perf;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for phase classification, GC semantics, tick interval, and percentiles.</summary>
public sealed class PerfAggregatorTests
{
    [Fact]
    public void Summaries_ClassifyOneDotSystemsAndTwoDotSubphases()
    {
        var ticks = PerfFixture.SampledSeries(sampleCount: 3);
        var aggregate = new PerfAggregator(ticks);

        Assert.Equal(4, aggregate.Summaries.Count);
        Assert.Equal(new[] { "sys.alpha", "sys.beta" },
            aggregate.SystemSummaries.Select(static summary => summary.Name).ToArray());
        Assert.Equal(new[] { "sys.alpha.child", "sys.alpha.other" },
            aggregate.SubphaseSummaries.Select(static summary => summary.Name).ToArray());
        Assert.All(aggregate.SubphaseSummaries,
            static summary => Assert.Equal("sys.alpha", summary.Parent));
        Assert.All(aggregate.SystemSummaries, static summary => Assert.Null(summary.Parent));
        Assert.Equal(15d, aggregate.SystemMeanTotal, 10);
        Assert.Equal(6d, aggregate.SubphaseSummaries[0].Stats.Mean, 10);
    }

    [Fact]
    public void Summaries_ThrowWhenPhaseKeySetChangesBetweenSamples()
    {
        var complete = PerfFixture.StandardPhases();
        TickProfile[] ticks =
        [
            PerfFixture.Tick(10, 1d, phases: complete),
            PerfFixture.Tick(20, 2d, phases: [.. complete.Where(static phase => phase.Name != "sys.beta")]),
        ];

        var error = Assert.Throws<InvalidDataException>(() => new PerfAggregator(ticks));
        Assert.Contains("Phase key set changed", error.Message, StringComparison.Ordinal);
        Assert.Contains("sys.beta", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("flat", "no dot")]
    [InlineData("sys.alpha.child.extra", "one-dot system id")]
    [InlineData("other.phase.child", "parent 'other.phase' is not a one-dot system id")]
    public void Summaries_ThrowOnUnknownPhaseShape(string phaseName, string reason)
    {
        TickProfile[] ticks =
        [
            PerfFixture.Tick(10, 1d, phases: [("sys.alpha", 1d, 0L), (phaseName, 1d, 0L)]),
        ];

        var error = Assert.Throws<InvalidDataException>(() => new PerfAggregator(ticks));

        Assert.Contains(phaseName, error.Message, StringComparison.Ordinal);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Summaries_ThrowOnEmptyProfile()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new PerfAggregator([]));

        Assert.Contains("no samples", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Summaries_ThrowWhenProfileHasNoPhaseRecords()
    {
        var error = Assert.Throws<InvalidDataException>(() => new PerfAggregator([PerfFixture.Tick(10, 1d)]));

        Assert.Contains("no phase records", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gc_SumsSampledTicksAndEstimatesRunTotalWithMedianInterval()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 100, tickInterval: 10));

        Assert.Equal(100, aggregate.Gc0SampledSum);
        Assert.Equal(200, aggregate.Gc1SampledSum);
        Assert.Equal(0, aggregate.Gc2SampledSum);
        Assert.Equal(10, aggregate.TickInterval);
        Assert.True(aggregate.HasUniformTickInterval);
        Assert.Equal(1000, aggregate.Gc0RunEstimate);
        Assert.Equal(2000, aggregate.Gc1RunEstimate);
        Assert.Equal(0, aggregate.Gc2RunEstimate);
    }

    [Fact]
    public void Gc_IntervalAndEstimateAreNullForSingleSample()
    {
        var aggregate = new PerfAggregator([PerfFixture.Tick(10, 1d, gc0: 5, phases: PerfFixture.StandardPhases())]);

        Assert.Null(aggregate.TickInterval);
        Assert.Null(aggregate.Gc0RunEstimate);
        Assert.Equal(5, aggregate.Gc0SampledSum);
        Assert.False(aggregate.HasUniformTickInterval);
    }

    [Fact]
    public void TickInterval_IsMedianOfPositiveDeltasAndFlagsNonUniformSampling()
    {
        TickProfile[] ticks =
        [
            PerfFixture.Tick(1, 1d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(2, 2d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(12, 3d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(22, 4d, phases: PerfFixture.StandardPhases()),
        ];
        var aggregate = new PerfAggregator(ticks);

        Assert.Equal(10, aggregate.TickInterval);
        Assert.False(aggregate.HasUniformTickInterval);
    }

    [Fact]
    public void Percentiles_UseHyndmanFanType7()
    {
        var four = PerfAggregator.Percentiles([1d, 2d, 3d, 4d]);
        Assert.Equal(2.5d, four.Mean, 10);
        Assert.Equal(2.5d, four.P50, 10);
        Assert.Equal(3.85d, four.P95, 10);
        Assert.Equal(3.97d, four.P99, 10);
        Assert.Equal(4d, four.Max, 10);

        var ten = PerfAggregator.Percentiles([.. Enumerable.Range(1, 10).Select(static value => (double)value)]);
        Assert.Equal(5.5d, ten.P50, 10);
        Assert.Equal(9.55d, ten.P95, 10);
        Assert.Equal(9.91d, ten.P99, 10);
        Assert.Equal(10d, ten.Max, 10);

        var single = PerfAggregator.Percentiles([7d]);
        Assert.Equal(7d, single.P50, 10);
        Assert.Equal(7d, single.P99, 10);
    }

    [Fact]
    public void Percentiles_ThrowOnEmptySample()
    {
        Assert.Throws<InvalidOperationException>(() => PerfAggregator.Percentiles([]));
    }

    [Fact]
    public void Summaries_ReportPopulationFoodAndCorpses()
    {
        TickProfile[] ticks =
        [
            PerfFixture.Tick(10, 1d, population: 10, food: 4, corpses: 1, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(20, 2d, population: 20, food: 6, corpses: 3, phases: PerfFixture.StandardPhases()),
        ];
        var aggregate = new PerfAggregator(ticks);

        Assert.Equal(15d, aggregate.Population.Mean, 10);
        Assert.Equal(5d, aggregate.Food.Mean, 10);
        Assert.Equal(2d, aggregate.Corpses.Mean, 10);
        Assert.Equal(2, aggregate.SampleCount);
    }
}
