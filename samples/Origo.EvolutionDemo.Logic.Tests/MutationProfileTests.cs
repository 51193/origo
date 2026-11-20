using System;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class MutationProfileTests
{
    [Fact]
    public void FromSettings_DefaultWeightsStayReachableAndNormalized()
    {
        var profile = MutationProfile.FromSettings(DemoSettings.Defaults());
        var settings = profile.Settings;

        Assert.Equal(0, profile.Steps);
        var total = settings.Expression + settings.CopyCount + settings.Duplicate + settings.Delete
            + settings.Payload + settings.AddNeuron + settings.AddSynapse + settings.AddReflex
            + settings.AddOrgan + settings.AddRegulation + settings.AddSegment;
        Assert.InRange(total, 0.999, 1.0000001);
        Assert.All(
            [
                settings.Expression, settings.CopyCount, settings.Duplicate, settings.Delete,
                settings.Payload, settings.AddNeuron, settings.AddSynapse, settings.AddReflex,
                settings.AddOrgan, settings.AddRegulation, settings.AddSegment,
            ],
            weight => Assert.True(weight > 0d));
    }

    [Fact]
    public void FromSettings_ZeroBodyRateZeroesOnlyBodyOperators()
    {
        var settings = DemoSettings.Defaults();
        settings.BodyMutationRate = 0d;

        var weights = MutationProfile.FromSettings(settings).Settings;

        Assert.Equal(0d, weights.Expression);
        Assert.Equal(0d, weights.CopyCount);
        Assert.Equal(0d, weights.Payload);
        Assert.Equal(0d, weights.AddOrgan);
        Assert.Equal(0d, weights.AddRegulation);
        Assert.True(weights.AddNeuron > 0d);
        Assert.True(weights.Duplicate > 0d);
    }

    [Fact]
    public void FromSettings_ZeroAllRatesProducesNoOperators()
    {
        var settings = DemoSettings.Defaults();
        settings.BodyMutationRate = 0d;
        settings.BrainMutationRate = 0d;
        settings.BodyLengthMutationRate = 0d;

        var weights = MutationProfile.FromSettings(settings).Settings;

        Assert.Equal(0d, weights.Expression + weights.CopyCount + weights.Duplicate + weights.Delete
            + weights.Payload + weights.AddNeuron + weights.AddSynapse + weights.AddReflex
            + weights.AddOrgan + weights.AddRegulation + weights.AddSegment);
    }

    [Fact]
    public void ForOvergrowth_IsStrongerThanNormalReproduction()
    {
        var settings = DemoSettings.Defaults();
        var random = new Random(4);
        var overgrowth = MutationProfile.ForOvergrowth(settings, random);
        var normal = MutationProfile.FromSettings(settings);

        Assert.InRange(overgrowth.Steps, 3, 6);
        Assert.True(overgrowth.Settings.Payload > normal.Settings.Payload,
            "Overgrowth must bias toward structural/payload mutation.");
        Assert.True(overgrowth.Settings.Duplicate > normal.Settings.Duplicate);
    }

    [Fact]
    public void WithSteps_RejectsNegativeCounts()
    {
        var profile = MutationProfile.FromSettings(DemoSettings.Defaults());

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.WithSteps(-1));
        Assert.Equal(3, profile.WithSteps(3).Steps);
    }
}
