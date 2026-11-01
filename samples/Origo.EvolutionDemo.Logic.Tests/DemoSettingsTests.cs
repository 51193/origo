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

    [Fact]
    public void TuningFacade_ReadsActiveSettings()
    {
        var previous = DemoSettings.Current;
        try
        {
            var settings = DemoSettings.Defaults();
            settings.PondRadius = 7_777f;
            settings.FoodPatchRadius = 123f;
            DemoSettings.Current = settings;

            Assert.Equal(7_777f, Tuning.PondRadius);
            Assert.Equal(123f, Tuning.FoodPatchRadius);
        }
        finally
        {
            DemoSettings.Current = previous;
        }
    }
}
