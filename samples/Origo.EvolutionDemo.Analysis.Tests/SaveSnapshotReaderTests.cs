using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic;
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
        Assert.Equal("save_reader", snapshot.Id);
        Assert.Equal(150.5d, snapshot.Time, 9);
        Assert.Equal(1500d, snapshot.Tick, 9);
        Assert.Equal(4, snapshot.RawCreatureCount);
        Assert.Equal(4, snapshot.Creatures.Count);
        Assert.Empty(snapshot.SkippedCreatures);
        Assert.Equal(3, snapshot.Food);
        Assert.Equal(2, snapshot.Corpses);
        Assert.Equal(2, snapshot.FoodSources);
        Assert.Equal(7d, snapshot.ControllerNumbers["evo.genomeVersion"], 9);
        Assert.Equal(20261101d, snapshot.ControllerNumbers["evo.seed"], 9);
        Assert.Equal(3d, snapshot.ControllerNumbers["evo.spikeKills"], 9);
        Assert.Empty(snapshot.ControllerBooleans);
        Assert.All(snapshot.Creatures, creature => Assert.Equal(genome, creature.Genome));
        Assert.Contains(snapshot.Warnings, warning => warning.Contains(".payload.sha", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_FoodEntitiesExposeTypePositionAndSourceState()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_food",
            SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2),
            foodEntities:
            [
                new SaveFixture.FoodSpec("food_soft", 1d, 2d, FoodType.Soft, false),
                new SaveFixture.FoodSpec("food_tough", 3d, 4d, FoodType.Tough, false),
                new SaveFixture.FoodSpec("food_toxic", 5d, 6d, FoodType.Toxic, false),
                new SaveFixture.FoodSpec("food_corpse", 7d, 8d, FoodType.Soft, true),
            ],
            sourcePositions: [(9d, 10d)]);

        var snapshot = Assert.Single(SaveSnapshotReader.Read(save));

        Assert.Equal(4, snapshot.Food);
        Assert.Equal(4, snapshot.Foods.Count);
        Assert.Equal(2, snapshot.Foods.Count(food => food.Type == FoodType.Soft));
        Assert.Single(snapshot.Foods, food => food.FromCorpse);
        Assert.All(snapshot.Foods, food => Assert.True(food.HasPosition));
        var source = Assert.Single(snapshot.FoodSourceEntities);
        Assert.True(source.HasPosition);
        Assert.Equal(3, source.LocalFood);
        Assert.Equal(240, source.LocalCapacity);
        Assert.Equal(7, source.SpawnCount);
        Assert.Equal(14d, snapshot.Creatures[0].FoodPickupRadius);
        Assert.Equal(snapshot.Creatures[0].Lineage, snapshot.Creatures[0].RootLineage);
        Assert.Equal("child", snapshot.Creatures[0].SpawnOrigin);
        Assert.DoesNotContain(snapshot.Warnings, warning => warning.Contains("without finite", StringComparison.Ordinal));
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
    public void Read_SameSceneThroughSaveRootAndLevelDirectory_IsDeduplicated()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(directory, "save_one", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2));

        var snapshots = SaveSnapshotReader.Read(save, Path.Combine(save, "level_pond"));

        Assert.Single(snapshots);
    }

    [Fact]
    public void Read_DirectoryWithSeveralSaves_RequiresPoolThenReadsAll()
    {
        using var directory = new TemporaryDirectory();
        var genome = GenomeV4Factory.CreateSimpleReflex();
        SaveFixture.CreateV7Save(directory, "save_a", SaveFixture.Creatures(genome, 2), time: 10d);
        SaveFixture.CreateV7Save(directory, "save_b", SaveFixture.Creatures(genome, 3), time: 20d);

        var exception = Assert.Throws<InvalidOperationException>(() => SaveSnapshotReader.Read(directory.Path));
        Assert.Contains("contains 2 save scenes", exception.Message, StringComparison.Ordinal);
        Assert.Contains("--pool", exception.Message, StringComparison.Ordinal);

        var pooled = SaveSnapshotReader.Read([directory.Path], allowPool: true);
        Assert.Equal(2, pooled.Count);
        Assert.Equal(5, pooled.Sum(static snapshot => snapshot.Creatures.Count));
    }

    [Fact]
    public void Read_DirectoryWithoutScene_ThrowsDirectoryNotFound()
    {
        using var directory = new TemporaryDirectory();
        var empty = Path.Combine(directory.Path, "no-saves");
        Directory.CreateDirectory(empty);

        Assert.Throws<DirectoryNotFoundException>(() => SaveSnapshotReader.Read(empty));
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
    public void Read_SaveWithoutController_ThrowsExplicitControllerError()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_no_controller", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2),
            includeController: false);

        var exception = Assert.Throws<InvalidDataException>(() => SaveSnapshotReader.Read(save));

        Assert.Contains("no controller entity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingGenomeVersion_ThrowsExplicitError()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_no_version", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2),
            includeVersion: false);

        var exception = Assert.Throws<InvalidDataException>(() => SaveSnapshotReader.Read(save));

        Assert.Contains("no evo.genomeVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingTimeOrTick_ThrowsExplicitError()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_no_time", SaveFixture.Creatures(GenomeV4Factory.CreateSimpleReflex(), 2),
            includeTime: false);

        var exception = Assert.Throws<InvalidDataException>(() => SaveSnapshotReader.Read(save));

        Assert.Contains("evo.time/evo.tick", exception.Message, StringComparison.Ordinal);
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
    public void Read_MalformedJson_ThrowsInvalidDataWithPath()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.WriteRawScene(directory, "save_broken", "{ not json");
        var scene = Path.Combine(save, "level_pond", "snd_scene.json");

        var exception = Assert.Throws<InvalidDataException>(() => SaveSnapshotReader.Read(save));

        Assert.Contains(scene, exception.Message, StringComparison.Ordinal);
        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_CreatureWithoutGenome_IsCountedAsSkippedAndReported()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateSimpleReflex());
        var save = SaveFixture.CreateV7Save(
            directory, "save_skip",
            [("creature_ok", genome), ("creature_missing", string.Empty)]);

        var snapshot = Assert.Single(SaveSnapshotReader.Read(save));

        Assert.Equal(2, snapshot.RawCreatureCount);
        Assert.Single(snapshot.Creatures);
        var skip = Assert.Single(snapshot.SkippedCreatures);
        Assert.Equal("creature_missing", skip.Name);
        Assert.Contains("evo.bodyGenome", skip.Reason, StringComparison.Ordinal);
        Assert.Contains(snapshot.Warnings, warning => warning.Contains("skipped", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_BooleanControllerValues_ArePreserved()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateSimpleReflex());
        var json = """
        [
          {"name":"controller","data":{"pairs":{
            "evo.kind":{"data":"controller"},
            "evo.genomeVersion":{"data":7},
            "evo.time":{"data":10},
            "evo.tick":{"data":100},
            "demo.paused":{"data":true},
            "demo.running":{"data":false}
          }}},
          {"name":"c0","data":{"pairs":{
            "evo.kind":{"data":"creature"},
            "evo.bodyGenome":{"data":"__GENOME__"}
          }}}
        ]
        """.Replace("__GENOME__", genome);
        var save = SaveFixture.WriteRawScene(directory, "save_bool", json);

        var snapshot = Assert.Single(SaveSnapshotReader.Read(save));

        Assert.True(snapshot.ControllerBooleans["demo.paused"]);
        Assert.False(snapshot.ControllerBooleans["demo.running"]);
        Assert.Single(snapshot.Creatures);
    }

    [Fact]
    public void Read_MalformedAndUnknownEntities_AreCounted()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateSimpleReflex());
        var json = """
        [
          {"name":"controller","data":{"pairs":{
            "evo.kind":{"data":"controller"},
            "evo.genomeVersion":{"data":7},
            "evo.time":{"data":10},
            "evo.tick":{"data":100}
          }}},
          {"name":"bad-entity","nope":true},
          {"name":"ufo","data":{"pairs":{"evo.kind":{"data":"spaceship"}}}},
          {"name":"c0","data":{"pairs":{
            "evo.kind":{"data":"creature"},
            "evo.bodyGenome":{"data":"__GENOME__"}
          }}}
        ]
        """.Replace("__GENOME__", genome);
        var save = SaveFixture.WriteRawScene(directory, "save_malformed", json);

        var snapshot = Assert.Single(SaveSnapshotReader.Read(save));

        Assert.Equal(1, snapshot.MalformedEntityCount);
        Assert.Equal(1, snapshot.UnknownKindEntityCount);
        Assert.Equal(1, snapshot.RawCreatureCount);
        Assert.Contains(snapshot.Warnings, warning => warning.Contains("malformed", StringComparison.Ordinal));
        Assert.Contains(snapshot.Warnings, warning => warning.Contains("unknown evo.kind", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_CreatureFallsBackToMutationLineageAndUnknownOrigin()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "fallback",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            lineages: [7],
            generations: [2],
            includeRootLineage: false,
            includeSpawnOrigin: false);

        var creature = Assert.Single(Assert.Single(SaveSnapshotReader.Read([save])).Creatures);

        Assert.Equal(7, creature.RootLineage);
        Assert.Equal("unknown", creature.SpawnOrigin);
    }
}
