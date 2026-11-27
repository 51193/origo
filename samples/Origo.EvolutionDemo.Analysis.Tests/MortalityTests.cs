using System.Collections.Generic;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for cumulative death-cause counters.</summary>
public sealed class MortalityTests
{
    [Fact]
    public void Analyze_ReadsCumulativeDeathCausesAndShares()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "mortality",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            extraControllerPairs: new Dictionary<string, object>
            {
                ["evo.starvationDeaths"] = 10d,
                ["evo.predationDeaths"] = 5d,
                ["evo.ageDeaths"] = 5d,
                ["evo.deathByRoot"] = "7:10,5,5",
            });

        var analysis = SaveMortalityAnalyzer.Analyze(SaveSnapshotReader.Read([save]));
        var counts = Assert.Single(analysis.Saves).Counts;

        Assert.NotNull(counts);
        Assert.Equal(10, counts!.Starvation);
        Assert.Equal(5, counts.Predation);
        Assert.Equal(5, counts.Aging);
        Assert.Equal(20, counts.Total);
        Assert.Equal(0.25d, analysis.Sequence.PredationShare!.Value, 9);
        Assert.Equal(0.5d, analysis.Sequence.StarvationShare!.Value, 9);
        Assert.Equal(0.25d, analysis.Sequence.AgingShare!.Value, 9);
        var root = Assert.Single(analysis.RootDeaths!);
        Assert.Equal(7, root.Key);
        Assert.Equal(20, root.Value.Total);
        Assert.Equal(1, analysis.RootSummary!.Roots);
        Assert.Equal(0.25d, analysis.RootSummary.PredationShare!.Value, 9);
    }

    [Fact]
    public void Analyze_MissingCountersReportNull()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "mortality_missing",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1));

        var analysis = SaveMortalityAnalyzer.Analyze(SaveSnapshotReader.Read([save]));

        Assert.Null(Assert.Single(analysis.Saves).Counts);
        Assert.Null(analysis.Sequence.Counts);
        Assert.Null(analysis.Sequence.PredationShare);
    }

    [Fact]
    public void Analyze_PartialCountersTreatMissingCausesAsZero()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "mortality_partial",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            extraControllerPairs: new Dictionary<string, object>
            {
                ["evo.starvationDeaths"] = 4d,
            });

        var analysis = SaveMortalityAnalyzer.Analyze(SaveSnapshotReader.Read([save]));
        var counts = Assert.Single(analysis.Saves).Counts;

        Assert.NotNull(counts);
        Assert.Equal(4, counts!.Starvation);
        Assert.Equal(0, counts.Predation);
        Assert.Equal(0, counts.Aging);
    }
}
