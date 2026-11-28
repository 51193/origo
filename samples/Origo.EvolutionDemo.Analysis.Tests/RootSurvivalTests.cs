using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Root survival and hazard tests.</summary>
public sealed class RootSurvivalTests
{
    [Fact]
    public void Analyze_TracksLostRootsHazardAndLifetime()
    {
        using var directory = new TemporaryDirectory();
        var save1 = SaveFixture.CreateV7Save(
            directory, "survival_1",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 4),
            time: 10d,
            rootLineages: [1, 1, 2, 2]);
        var save2 = SaveFixture.CreateV7Save(
            directory, "survival_2",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 5),
            time: 20d,
            rootLineages: [1, 1, 2, 2, 3]);
        var save3 = SaveFixture.CreateV7Save(
            directory, "survival_3",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 4),
            time: 30d,
            rootLineages: [1, 1, 3, 3]);
        var lineage = SaveLineageAnalyzer.Analyze(SaveSnapshotReader.Read([save1, save2, save3]));

        var survival = RootSurvivalAnalyzer.Analyze(lineage);

        Assert.Equal(3, survival.Points.Count);
        Assert.Equal(1, survival.Points[1].NewRoots);
        Assert.Equal(0, survival.Points[1].LostRoots);
        Assert.Equal(1, survival.Points[2].LostRoots);
        Assert.Equal(1d / 3d, survival.Points[2].Hazard!.Value, 9);
        Assert.Equal(3, survival.Summary.DistinctRoots);
        Assert.Equal(1, survival.Summary.ExtinctRoots);
        Assert.Equal(10d, survival.Summary.MedianExtinctLifetime!.Value, 9);
        Assert.Equal(10d, survival.Summary.MedianPositiveExtinctLifetime!.Value, 9);
        Assert.Equal(1d / 3d, survival.Summary.ExtinctionShare, 9);
    }
}
