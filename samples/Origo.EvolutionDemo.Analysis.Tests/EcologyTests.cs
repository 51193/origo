using System;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>
///     Regression tests for save-level food, access, origin, and diet metrics and
///     the per-creature features that expose the same observations.
/// </summary>
public sealed class EcologyTests
{
    private const string ConfigJson =
        "{\"PondRadius\":1000,\"FoodPatchRadius\":60,\"FoodPatchCount\":3,\"FoodCap\":200,\"PopulationCap\":50}";

    [Fact]
    public void Analyze_FoodTypesAccessPatchCoverageOriginAndDietAreDerived()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateEcologySave(directory);
        var snapshot = Assert.Single(SaveSnapshotReader.Read(save));

        var ecology = Assert.Single(SaveEcologyAnalyzer.Analyze([snapshot]));

        Assert.Equal(2, ecology.Food.Soft);
        Assert.Equal(1, ecology.Food.Tough);
        Assert.Equal(1, ecology.Food.Toxic);
        Assert.Equal(1, ecology.Food.Corpse);
        Assert.Equal(4, ecology.Food.Total);
        Assert.Equal(4, ecology.LivePatchCount);
        Assert.Equal(60d, ecology.PatchRadius);
        Assert.Equal(1000d, ecology.PondRadius);
        Assert.Equal(4d * 60d * 60d / (1000d * 1000d), ecology.PatchCoverage!.Value, 9);
        Assert.Contains(ecology.Warnings, warning => warning.Contains("world.config", StringComparison.Ordinal));
        Assert.Equal(0.5d, ecology.FounderShare, 9);
        Assert.Equal(1.5d, ecology.MeanGeneration, 9);
        Assert.Equal(7, ecology.MaxGeneration);
        Assert.Equal(4, ecology.Access.Creatures);
        Assert.Equal(0.5d, ecology.Access.FractionInPickup, 9);
        Assert.Equal(0.5d, ecology.Access.FractionInSense, 9);
        Assert.Equal(0.5d, ecology.Access.FractionIn300, 9);
        Assert.Equal(1.5d, ecology.Access.MeanFoodInSense, 9);
        Assert.Equal(1.5d, ecology.Access.MeanFoodIn300, 9);
        Assert.Equal(300d, ecology.Access.NearestFoodP50!.Value, 6);
        Assert.Equal(707.106781d, ecology.Access.NearestSourceP50!.Value, 4);
        Assert.Equal(0.5d, ecology.LivingDiet.Soft, 9);
        Assert.Equal(0.25d, ecology.LivingDiet.Tough, 9);
        Assert.Equal(0.25d, ecology.LivingDiet.Toxic, 9);
        Assert.Equal(0.5d, ecology.LivingDiet.CorpseOfSoft, 9);
        Assert.Equal(0, ecology.ExperiencedCreatures);
        Assert.Equal(0d, ecology.SpecialistShare60, 9);
        Assert.Null(ecology.MeanExperiencedEntropy);
        Assert.NotNull(ecology.ControllerDiet);
        Assert.Equal(0.5d, ecology.ControllerDiet!.Soft, 9);
        Assert.Equal(0.25d, ecology.ControllerDiet.Tough, 9);
        Assert.Equal(0.25d, ecology.ControllerDiet.Toxic, 9);
        Assert.Equal(0.2d, ecology.ControllerDiet.CorpseOfSoft, 9);
    }

    [Fact]
    public void Extract_AccessOriginMealPresenceAndRatesArePerCreature()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateEcologySave(directory);
        var result = FeatureExtractor.Extract(SaveSnapshotReader.Read(save), maxCreatures: 0, seed: 1);

        Assert.Equal(4, result.Rows.Count);
        var near = result.Rows[0].Values;
        Assert.Equal(3d, near["access.foodInPickup"]);
        Assert.Equal(3d, near["access.foodInSense"]);
        Assert.Equal(3d, near["access.foodIn300"]);
        Assert.Equal(0d, near["access.nearestFoodDistance"]);
        Assert.Equal(0d, near["access.nearestSourceDistance"]);
        Assert.Equal(1d, near["origin.generationZero"]);
        Assert.Equal(1d, near["origin.generationLe2"]);
        Assert.Equal(1d, near["diet.hasMeals"]);
        Assert.Equal(0.5d, near["diet.corpseOfSoftShare"], 9);
        Assert.Equal(4d / 30d * 100d, near["behavior.mealsPer100s"], 9);
        Assert.Equal(1d / 30d * 100d, near["behavior.childrenPer100s"], 9);
        Assert.Equal(14d, near["body.foodPickupRadius"], 9);

        var far = result.Rows[2].Values;
        Assert.Equal(0d, far["access.foodInPickup"]);
        Assert.Equal(0d, far["access.foodInSense"]);
        Assert.Equal(0d, far["access.foodIn300"]);
        Assert.Equal(600d, far["access.nearestFoodDistance"]);
        Assert.Equal(1414.213562d, far["access.nearestSourceDistance"], 5);
        Assert.Equal(0d, far["origin.generationZero"]);
        Assert.Equal(4d / 32d * 100d, far["behavior.mealsPer100s"], 9);
    }

    [Fact]
    public void Analyze_WithoutWorldConfigLeavesPatchCoverageUnavailable()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "save_no_config", SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1));

        var ecology = Assert.Single(SaveEcologyAnalyzer.Analyze(SaveSnapshotReader.Read(save)));

        Assert.Null(ecology.PatchRadius);
        Assert.Null(ecology.PondRadius);
        Assert.Null(ecology.PatchCoverage);
        Assert.Empty(ecology.Warnings);
    }

    private static string CreateEcologySave(TemporaryDirectory directory) =>
        SaveFixture.CreateV7Save(
            directory,
            "save_ecology",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 4),
            creaturePositions: [(0d, 0d), (0d, 0d), (1000d, 1000d), (1000d, 1000d)],
            generations: [0, 0, 3, 3],
            foodEntities:
            [
                new SaveFixture.FoodSpec("food_soft_0", 0d, 0d, FoodType.Soft, false),
                new SaveFixture.FoodSpec("food_corpse_0", 0d, 0d, FoodType.Soft, true),
                new SaveFixture.FoodSpec("food_tough_0", 5d, 0d, FoodType.Tough, false),
                new SaveFixture.FoodSpec("food_toxic_0", 2000d, 2000d, FoodType.Toxic, false),
            ],
            sourcePositions: [(0d, 0d)],
            extraControllerPairs: new System.Collections.Generic.Dictionary<string, object>
            {
                ["world.config"] = ConfigJson,
                ["world.foodPatchCount"] = 4d,
                ["evo.maxGeneration"] = 7d,
                ["evo.foodSoftMeals"] = 10d,
                ["evo.foodToughMeals"] = 5d,
                ["evo.foodToxicMeals"] = 5d,
                ["evo.corpseMeals"] = 2d,
            });
}
