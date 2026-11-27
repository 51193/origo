using System;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for living-lineage turnover and founder-injection proxies.</summary>
public sealed class LineageTests
{
    [Fact]
    public void Analyze_LineageStructureAndTurnoverAcrossSaves()
    {
        using var directory = new TemporaryDirectory();
        var save1 = SaveFixture.CreateV7Save(
            directory, "lineage_1",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 4),
            time: 10d,
            lineages: [1, 1, 2, 2],
            generations: [0, 1, 0, 3]);
        var save2 = SaveFixture.CreateV7Save(
            directory, "lineage_2",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 4),
            time: 20d,
            lineages: [1, 2, 3, 3],
            generations: [0, 2, 0, 1]);
        var save3 = SaveFixture.CreateV7Save(
            directory, "lineage_3",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 4),
            time: 30d,
            lineages: [2, 2, 3, 3],
            generations: [2, 3, 0, 1]);

        var analysis = SaveLineageAnalyzer.Analyze(SaveSnapshotReader.Read([save1, save2, save3]));

        Assert.Equal(3, analysis.Saves.Count);
        var first = analysis.Saves[0].Summary;
        Assert.Equal(2, first.Lineages);
        Assert.Equal(2, first.FounderLineages);
        Assert.Equal(1d, first.FounderLineageShare, 9);
        Assert.Equal(2, first.Gen0Individuals);
        Assert.Equal(0.5d, first.Gen0Share, 9);
        Assert.Equal(0.5d, first.LargestLineageShare, 9);
        Assert.Equal(1d, first.LineageEntropy, 9);
        Assert.Equal(0, first.NewLineages);
        Assert.Equal(0, first.NewGen0Lineages);
        Assert.Equal(0, first.LostLineages);
        Assert.Null(first.PriorLineageSurvival);
        Assert.Equal(0d, first.LineageTurnover, 9);

        var second = analysis.Saves[1].Summary;
        Assert.Equal(3, second.Lineages);
        Assert.Equal(2, second.FounderLineages);
        Assert.Equal(2 / 3d, second.FounderLineageShare, 9);
        Assert.Equal(0.5d, second.Gen0Share, 9);
        Assert.Equal(1, second.NewLineages);
        Assert.Equal(1, second.NewGen0Lineages);
        Assert.Equal(0, second.LostLineages);
        Assert.Equal(1d, second.PriorLineageSurvival!.Value, 9);
        Assert.Equal(0.2d, second.LineageTurnover, 9);
        Assert.Equal(0.9464d, second.LineageEntropy, 4);

        var third = analysis.Saves[2].Summary;
        Assert.Equal(2, third.Lineages);
        Assert.Equal(1, third.FounderLineages);
        Assert.Equal(0.25d, third.Gen0Share, 9);
        Assert.Equal(0, third.NewLineages);
        Assert.Equal(0, third.NewGen0Lineages);
        Assert.Equal(1, third.LostLineages);
        Assert.Equal(2 / 3d, third.PriorLineageSurvival!.Value, 9);
        Assert.Equal(0.2d, third.LineageTurnover, 9);
        Assert.Equal(1d, third.LineageEntropy, 9);

        var sequence = analysis.Sequence;
        Assert.Equal(3, sequence.DistinctLineages);
        Assert.Equal(3, sequence.PeakLivingLineages);
        Assert.Equal(2, sequence.FinalLivingLineages);
        Assert.Equal(1, sequence.TotalNewLineages);
        Assert.Equal(1, sequence.TotalNewGen0Lineages);
        Assert.Equal(1, sequence.LostLineages);
        Assert.Equal(5 / 6d, sequence.MeanPriorLineageSurvival!.Value, 9);
        Assert.Equal(0.2d, sequence.MeanLineageTurnover!.Value, 9);

        var newFounder = Assert.Single(
            analysis.Details,
            detail => detail.SaveId == "lineage_2" && detail.Lineage == 3);
        Assert.True(newFounder.IsNew);
        Assert.True(newFounder.IsNewGen0);
        Assert.Equal(1, newFounder.FounderCreatures);
        Assert.Equal(0, newFounder.MinGeneration);
        Assert.Equal(1, newFounder.MaxGeneration);
        Assert.False(analysis.Details[0].IsNew);
    }

    [Fact]
    public void Analyze_EmptySaveHasZeroLineageMetrics()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(directory, "lineage_empty", []);
        var analysis = SaveLineageAnalyzer.Analyze(SaveSnapshotReader.Read([save]));
        var summary = Assert.Single(analysis.Saves).Summary;

        Assert.Equal(0, summary.Lineages);
        Assert.Equal(0, summary.FounderLineages);
        Assert.Equal(0d, summary.LineageEntropy, 9);
        Assert.Equal(0d, summary.LargestLineageShare, 9);
        Assert.Null(summary.PriorLineageSurvival);
        Assert.Equal(0, analysis.Sequence.DistinctLineages);
        Assert.Null(analysis.Sequence.MeanPriorLineageSurvival);
    }
}
