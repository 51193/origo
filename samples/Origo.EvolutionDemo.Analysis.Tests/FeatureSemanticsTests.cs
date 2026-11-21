using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>
///     Regression tests for effective feature semantics: zero-filled organ counts,
///     runtime-fallback trait values, no NaN, and skip accounting.
/// </summary>
public sealed class FeatureSemanticsTests
{
    [Fact]
    public void Extract_OrganCountsCoverEveryOrganKindAndZeroFillAbsentKinds()
    {
        var genome = CreateSemanticGenome();
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(directory, "save_features", SaveFixture.Creatures(genome, 3));

        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(3, result.TotalCreatures);
        Assert.Equal(3, result.ConsideredCreatures);
        Assert.Empty(result.SkippedCreatures);
        var values = result.Rows[0].Values;
        foreach (var organKind in Enum.GetValues<OrganKindV4>())
        {
            Assert.True(values.ContainsKey($"organ.{organKind}"), $"missing organ.{organKind}");
            Assert.True(double.IsFinite(values[$"organ.{organKind}"]));
        }

        Assert.Equal(2d, values["organ.Vision"]);
        Assert.Equal(1d, values["organ.SpikeBattery"]);
        Assert.Equal(0d, values["organ.Armor"]);
        Assert.Equal(0d, values["organ.Sprint"]);
        Assert.Equal(3d, values["body.organs"]);
    }

    [Fact]
    public void Extract_TraitValuesAreTheEffectiveRuntimePhenotypeForEveryLifeTrait()
    {
        var genome = CreateSemanticGenome();
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        var body = DemoPhenotypeAdapter.ToBodyPhenotype(phenotype);
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(directory, "save_traits", SaveFixture.Creatures(genome, 2));

        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        var values = result.Rows[0].Values;
        foreach (var traitKind in Enum.GetValues<LifeTraitKindV4>())
        {
            Assert.True(values.ContainsKey($"trait.{traitKind}"), $"missing trait.{traitKind}");
            Assert.True(double.IsFinite(values[$"trait.{traitKind}"]));
        }

        Assert.Equal(body.SizeScale, values["trait.Size"], 5);
        Assert.Equal(2.5d, values["trait.Size"], 5);
        Assert.Equal(body.MoveSpeed, values["trait.Speed"], 5);
        Assert.Equal(60d, values["trait.Speed"], 5);
        Assert.Equal(body.TurnSpeed, values["trait.TurnSpeed"], 5);
        Assert.Equal(body.SenseRange, values["trait.SenseRange"], 5);
        Assert.Equal(body.SpikeRadius, values["trait.SpikeRange"], 5);
        Assert.Equal(body.BasalMetabolism, values["trait.Metabolism"], 5);
        Assert.Equal(body.MaxAge, values["trait.MaxAge"], 5);
        // Absent life-history genes take the runtime fallback defaults, not NaN.
        Assert.Equal(1d, values["trait.Metabolism"], 5);
        Assert.Equal(Tuning.MaxAgeSeconds, values["trait.MaxAge"], 5);
    }

    [Fact]
    public void Extract_EveryFeatureValueIsFinite()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_finite", SaveFixture.Creatures(GenomeV4Factory.CreateDemoFounder(new Random(7)), 3));

        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        Assert.Equal(3, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.All(row.Values, pair =>
            Assert.True(double.IsFinite(pair.Value), $"non-finite feature {pair.Key}")));
    }

    [Fact]
    public void Extract_UndecodableGenomeIsCountedAsSkippedCreature()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(SaveFixture.DefaultGenome());
        var save = SaveFixture.CreateV7Save(directory, "save_skip",
        [
            ("good_0", genome),
            ("bad_0", "b64:AAAA"),
            ("good_1", genome),
            ("good_2", genome),
        ]);

        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(4, result.TotalCreatures);
        Assert.Equal(4, result.ConsideredCreatures);
        var skip = Assert.Single(result.SkippedCreatures);
        Assert.Equal("bad_0", skip.Name);
        Assert.Contains("save_skip", skip.SavePath, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(skip.Reason));
        Assert.DoesNotContain(result.Rows, row => row.Name == "bad_0");
    }

    [Fact]
    public void Extract_MaxSamplingIsPerSaveAndDeterministic()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.DefaultGenome();
        var first = SaveFixture.CreateV7Save(directory, "save_a", SaveFixture.Creatures(genome, 20), time: 10d);
        var second = SaveFixture.CreateV7Save(directory, "save_b", SaveFixture.Creatures(genome, 30), time: 20d);
        var snapshots = SaveSnapshotReader.Read(second, first);

        var result = FeatureExtractor.Extract(snapshots, maxCreatures: 5, seed: 3);

        Assert.Equal(50, result.TotalCreatures);
        Assert.Equal(10, result.ConsideredCreatures);
        Assert.Equal(10, result.Rows.Count);
        Assert.All(
            result.Rows.GroupBy(static row => row.SavePath),
            group => Assert.Equal(5, group.Count()));
        var again = FeatureExtractor.Extract(snapshots, maxCreatures: 5, seed: 3);
        Assert.Equal(
            result.Rows.Select(static row => row.Name),
            again.Rows.Select(static row => row.Name));
    }

    [Fact]
    public void Extract_DriverTraitsComeFromCompiledPhenotypeNotSavedBodyFields()
    {
        var genome = CreateSemanticGenome();
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(directory, "save_driver", SaveFixture.Creatures(genome, 2));

        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        // The fixture stores a different saved moveSpeed (45) on purpose: trait.Speed
        // must come from the expressed genome (60), while body.moveSpeed keeps the save field.
        Assert.Equal(45d, result.Rows[0].Values["body.moveSpeed"], 5);
        Assert.Equal(60d, result.Rows[0].Values["trait.Speed"], 5);
    }

    private static GenomeV4 CreateSemanticGenome()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
            new(2, GeneKind.Organ, 2, 1f, GenePayloadV4.Organ(OrganKindV4.Vision, 0.5f)),
            new(3, GeneKind.Organ, 1, 1f, GenePayloadV4.Organ(OrganKindV4.SpikeBattery, 1f)),
            new(4, GeneKind.LifeHistory, 1, 1f, GenePayloadV4.LifeHistory(LifeTraitKindV4.Size, 2.5f)),
            new(5, GeneKind.LifeHistory, 1, 1f, GenePayloadV4.LifeHistory(LifeTraitKindV4.Speed, 60f)),
        };
        return new GenomeV4(genes, 6);
    }
    [Fact]
    public void Extract_DietCountersAndSharesAreDerived()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_diet", SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1));

        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        var values = result.Rows[0].Values;
        Assert.Equal(2d, values["behavior.softMeals"]);
        Assert.Equal(1d, values["behavior.toughMeals"]);
        Assert.Equal(1d, values["behavior.toxicMeals"]);
        Assert.Equal(4d, values["diet.totalMeals"]);
        Assert.Equal(0.5d, values["diet.softShare"], 8);
        Assert.Equal(0.25d, values["diet.toughShare"], 8);
        Assert.Equal(0.25d, values["diet.toxicShare"], 8);
        Assert.Equal(0.25d, values["diet.corpseShare"], 8);
        Assert.Equal(0.5d, values["diet.dominance"], 8);
        Assert.Equal(0.9464d, values["diet.entropy"], 4);
    }

    [Fact]
    public void Extract_OldSaveWithoutDietCountersReadsZero()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_old_diet", SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            includeDietCounters: false);

        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        var values = result.Rows[0].Values;
        Assert.Equal(0d, values["behavior.softMeals"]);
        Assert.Equal(0d, values["behavior.toughMeals"]);
        Assert.Equal(0d, values["behavior.toxicMeals"]);
        Assert.Equal(0d, values["diet.totalMeals"]);
        Assert.Equal(0d, values["diet.entropy"]);
        Assert.Equal(0d, values["diet.dominance"]);
    }

}
