using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class DemoSettingsTests
{
    [Fact]
    public void Json_RoundTripsAllAdvancedValues()
    {
        var settings = DemoSettings.Defaults();
        settings.PondRadius = 12_345f;
        settings.InitialPopulation = 321;
        settings.PopulationCap = 4_321;
        settings.FoodPatchCount = 23;
        settings.FoodPatchFalloff = 3.2f;
        settings.FoodSourceWalkSpeedMax = 42f;
        settings.FoodSourceCyclePeriodMax = 333f;
        settings.OvergrowthEnabled = false;
        settings.OvergrowthMutationRate = 0.37d;
        settings.SpikeRadius = 99f;
        settings.SpikeDamageFraction = 0.42f;
        settings.SpikeKillStealFraction = 0.25f;
        settings.SpikeArmorDamageMultiplier = 0.33f;
        settings.SprintSurchargeFraction = 1.75f;
        settings.OrganUpkeepPerSecond = 0.07f;
        settings.ArmorSpeedMultiplier = 0.7f;
        settings.IterateCheckpointIntervalSeconds = 300f;

        var restored = DemoSettings.FromJson(settings.ToJson());

        Assert.Equal(settings.PondRadius, restored.PondRadius);
        Assert.Equal(settings.InitialPopulation, restored.InitialPopulation);
        Assert.Equal(settings.PopulationCap, restored.PopulationCap);
        Assert.Equal(settings.FoodPatchCount, restored.FoodPatchCount);
        Assert.Equal(settings.FoodPatchFalloff, restored.FoodPatchFalloff);
        Assert.Equal(settings.FoodSourceWalkSpeedMax, restored.FoodSourceWalkSpeedMax);
        Assert.Equal(settings.FoodSourceCyclePeriodMax, restored.FoodSourceCyclePeriodMax);
        Assert.False(restored.OvergrowthEnabled);
        Assert.Equal(settings.OvergrowthMutationRate, restored.OvergrowthMutationRate);
        Assert.Equal(settings.SpikeRadius, restored.SpikeRadius);
        Assert.Equal(settings.SpikeDamageFraction, restored.SpikeDamageFraction);
        Assert.Equal(settings.SpikeKillStealFraction, restored.SpikeKillStealFraction);
        Assert.Equal(settings.SpikeArmorDamageMultiplier, restored.SpikeArmorDamageMultiplier);
        Assert.Equal(settings.SprintSurchargeFraction, restored.SprintSurchargeFraction);
        Assert.Equal(settings.OrganUpkeepPerSecond, restored.OrganUpkeepPerSecond);
        Assert.Equal(settings.ArmorSpeedMultiplier, restored.ArmorSpeedMultiplier);
        Assert.Equal(settings.IterateCheckpointIntervalSeconds, restored.IterateCheckpointIntervalSeconds);
    }

    [Fact]
    public void FromJson_MigratesPreVersionedPatchLayout()
    {
        var restored = DemoSettings.FromJson(
            "{\"FoodPatchRadius\":420,\"FoodPatchFalloff\":1.8,\"FoodPatchLocalCapacity\":250}");

        Assert.Equal(900f, restored.FoodPatchRadius);
        Assert.Equal(0.55f, restored.FoodPatchFalloff);
        Assert.Equal(300, restored.FoodPatchLocalCapacity);
        Assert.Equal(2, restored.FoodPatchLayoutVersion);
    }

    [Fact]
    public void FromJson_InvalidTextFallsBackToDefaults()
    {
        var settings = DemoSettings.FromJson("{not json");

        Assert.Equal(10_000f, settings.PondRadius);
        Assert.True(settings.OvergrowthEnabled);
    }

    [Theory]
    [InlineData(-5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(300f, 300f)]
    [InlineData(90_000f, 86_400f)]
    public void Normalize_ClampsIterateCheckpointInterval(float input, float expected)
    {
        var settings = DemoSettings.Defaults();
        settings.IterateCheckpointIntervalSeconds = input;

        settings.Normalize();

        Assert.Equal(expected, settings.IterateCheckpointIntervalSeconds);
    }

    [Fact]
    public void Defaults_DisableIterateCheckpoints()
    {
        Assert.Equal(0f, DemoSettings.Defaults().IterateCheckpointIntervalSeconds);
    }

    [Fact]
    public void Normalize_ClampsSprintAndSpikeTuning()
    {
        var settings = DemoSettings.Defaults();
        settings.SprintSurchargeFraction = -1f;
        settings.SpikeDamageFraction = 2f;
        settings.SpikeKillStealFraction = -1f;
        settings.SpikeArmorDamageMultiplier = -1f;
        settings.OrganUpkeepPerSecond = -1f;
        settings.ArmorSpeedMultiplier = 2f;

        settings.Normalize();

        Assert.Equal(0f, settings.SprintSurchargeFraction);
        Assert.Equal(1f, settings.SpikeDamageFraction);
        Assert.Equal(0f, settings.SpikeKillStealFraction);
        Assert.Equal(0f, settings.SpikeArmorDamageMultiplier);
        Assert.Equal(0f, settings.OrganUpkeepPerSecond);
        Assert.Equal(1f, settings.ArmorSpeedMultiplier);
    }

    [Fact]
    public void Defaults_PinOrganAndArmorTradeOffs()
    {
        var settings = DemoSettings.Defaults();

        Assert.Equal(0.06f, settings.OrganUpkeepPerSecond);
        Assert.Equal(0.9f, settings.ArmorSpeedMultiplier);
        Assert.Equal(0.4f, settings.SpikeArmorDamageMultiplier);
    }

    [Fact]
    public void TuningFacade_ReadsActiveSettings()
    {
        var previous = DemoSettings.Current;
        try
        {
            var settings = DemoSettings.Defaults();
            settings.PondRadius = 7_777f;
            settings.FoodPatchRadius = 123f;
            settings.IterateCheckpointIntervalSeconds = 42f;
            settings.SprintSurchargeFraction = 1.75f;
            settings.SpikeDamageFraction = 0.42f;
            settings.SpikeKillStealFraction = 0.25f;
            settings.SpikeArmorDamageMultiplier = 0.33f;
            settings.OrganUpkeepPerSecond = 0.07f;
            settings.ArmorSpeedMultiplier = 0.7f;
            DemoSettings.Current = settings;

            Assert.Equal(7_777f, Tuning.PondRadius);
            Assert.Equal(123f, Tuning.FoodPatchRadius);
            Assert.Equal(42f, Tuning.IterateCheckpointIntervalSeconds);
            Assert.Equal(1.75f, Tuning.SprintSurchargeFraction);
            Assert.Equal(0.42f, Tuning.SpikeDamageFraction);
            Assert.Equal(0.25f, Tuning.SpikeKillStealFraction);
            Assert.Equal(0.33f, Tuning.SpikeArmorDamageMultiplier);
            Assert.Equal(0.07f, Tuning.OrganUpkeepPerSecond);
            Assert.Equal(0.7f, Tuning.ArmorSpeedMultiplier);
        }
        finally
        {
            DemoSettings.Current = previous;
        }
    }
    [Fact]
    public void Defaults_PinFoodComposition()
    {
        var settings = DemoSettings.Defaults();
        Assert.Equal(0.40f, settings.ToughFoodShare);
        Assert.Equal(0.40f, settings.ToxicFoodShare);
        Assert.Equal(18f, settings.FoodEnergy);
        Assert.Equal(25f, settings.ToughFoodEnergy);
        Assert.Equal(32f, settings.ToxicFoodEnergy);
    }

}
