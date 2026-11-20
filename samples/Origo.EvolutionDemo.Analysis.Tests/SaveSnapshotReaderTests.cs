using System;
using System.IO;
using System.Linq;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for the v7 save reader contract and its failure modes.</summary>
public sealed class SaveSnapshotReaderTests
{
    [Fact]
    public void Read_ValidSave_ExposesCreaturesCountersAndEntities()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateSimpleReflex());
        var save = SaveFixture.CreateV7Save(
            directory, "save_reader", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 4),
            time: 150.5d, tick: 1500d, food: 3, corpses: 2, foodSources: 2);

        var snapshots = SaveSnapshotReader.Read(save);

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(150.5d, snapshot.Time, 9);
        Assert.Equal(1500d, snapshot.Tick, 9);
        Assert.Equal(4, snapshot.Creatures.Count);
        Assert.Equal(3, snapshot.Food);
        Assert.Equal(2, snapshot.Corpses);
        Assert.Equal(2, snapshot.FoodSources);
        Assert.Equal(7d, snapshot.ControllerNumbers["evo.genomeVersion"], 9);
        Assert.Equal(20261101d, snapshot.ControllerNumbers["evo.seed"], 9);
        Assert.Equal(3d, snapshot.ControllerNumbers["evo.spikeKills"], 9);
        Assert.All(snapshot.Creatures, creature => Assert.Equal(genome, creature.Genome));
    }

    [Fact]
    public void Read_MultipleSavePaths_ArePathOrderedAndDeduplicated()
    {
        using var directory = new TemporaryDirectory();
        var second = SaveFixture.CreateV7Save(directory, "save_b", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2), time: 20d);
        var first = SaveFixture.CreateV7Save(directory, "save_a", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2), time: 10d);

        var snapshots = SaveSnapshotReader.Read(second, first, second);

        Assert.Equal(2, snapshots.Count);
        Assert.True(string.CompareOrdinal(snapshots[0].Path, snapshots[1].Path) < 0);
    }

    [Fact]
    public void Read_DirectoryRoot_RecursivelyReadsEverySave()
    {
        using var directory = new TemporaryDirectory();
        var genome = GenomeV4Factory.CreateSimpleReflex();
        SaveFixture.CreateV7Save(directory, "save_a", SaveFixture.Creatures(genome, 2), time: 10d);
        SaveFixture.CreateV7Save(directory, "save_b", SaveFixture.Creatures(genome, 3), time: 20d);

        var snapshots = SaveSnapshotReader.Read(directory.Path);

        Assert.Equal(2, snapshots.Count);
        Assert.Equal(5, snapshots.Sum(static snapshot => snapshot.Creatures.Count));
    }

    [Fact]
    public void Read_NonV7Save_ThrowsExplicitVersionError()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_v6", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2),
            genomeVersion: 6d);

        var exception = Assert.Throws<InvalidDataException>(() => SaveSnapshotReader.Read(save));

        Assert.Contains("not a v7 save", exception.Message, StringComparison.Ordinal);
        Assert.Contains("genomeVersion='6'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_SaveWithoutController_ThrowsExplicitVersionError()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_no_controller", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2),
            includeController: false);

        Assert.Throws<InvalidDataException>(() => SaveSnapshotReader.Read(save));
    }

    [Fact]
    public void Read_MissingPath_ThrowsFileNotFound()
    {
        using var directory = new TemporaryDirectory();

        var exception = Assert.Throws<FileNotFoundException>(
            () => SaveSnapshotReader.Read(Path.Combine(directory.Path, "does-not-exist")));

        Assert.Contains("does-not-exist", exception.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MalformedJson_ThrowsJsonException()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.WriteRawScene(directory, "save_broken", "{ not json");

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => SaveSnapshotReader.Read(save));
    }
}
