using System.Collections.Generic;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Root birth summary tests.</summary>
public sealed class BirthsTests
{
    [Fact]
    public void Analyze_ReadsCumulativeBirthsAndTopRoot()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "births",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            extraControllerPairs: new Dictionary<string, object>
            {
                ["evo.birthsByRoot"] = "2:30;7:10",
            });

        var analysis = BirthsAnalyzer.Analyze(SaveSnapshotReader.Read([save]));

        Assert.Equal(40, analysis.Summary!.TotalBirths);
        Assert.Equal(2, analysis.Summary.TopRoot);
        Assert.Equal(30, analysis.Summary.TopRootBirths);
        Assert.Equal(0.75d, analysis.Summary.TopRootShare!.Value, 9);
        Assert.Equal(2, analysis.Latest!.Count);
    }

    [Fact]
    public void Analyze_MissingBirthsStaysNull()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "births_missing",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1));

        var analysis = BirthsAnalyzer.Analyze(SaveSnapshotReader.Read([save]));

        Assert.Null(analysis.Latest);
        Assert.Null(analysis.Summary);
    }
}
