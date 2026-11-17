using System;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class CreatureGenomeTests
{
    [Fact]
    public void RandomFounder_HasValidV4GenomeAndPhenotype()
    {
        var genome = CreatureGenome.RandomFounder(new Random(9));
        Assert.True(genome.GeneCount > 0);
        Assert.True(CreatureGenome.TryDecode(genome.Body, out _));
        Assert.True(genome.Phenotype.MaxEnergy > 0f);
    }

    [Fact]
    public void Mutate_ReportsChangeAndKeepsSerializedGenomeValid()
    {
        var parent = CreatureGenome.RandomFounder(new Random(10));
        var profile = MutationProfile.FromSettings(DemoSettings.Defaults()).WithSteps(3);
        var child = CreatureGenome.Mutate(parent, profile, new Random(11), out var changes);
        Assert.True(changes > 0);
        Assert.NotEqual(parent.Body, child.Body);
        Assert.True(CreatureGenome.TryDecode(child.Body, out _));
    }

    [Fact]
    public void Mutate_WithZeroRates_ChangesNothing()
    {
        var settings = DemoSettings.Defaults();
        settings.BodyMutationRate = 0d;
        settings.BrainMutationRate = 0d;
        settings.BodyLengthMutationRate = 0d;
        var parent = CreatureGenome.RandomFounder(new Random(12));
        var profile = MutationProfile.FromSettings(settings).WithSteps(3);

        var child = CreatureGenome.Mutate(parent, profile, new Random(13), out var changes);

        Assert.Equal(0, changes);
        Assert.Equal(parent.Body, child.Body);
    }

    [Fact]
    public void SerializedBody_RoundTripsAndKeepsHexDisplayForm()
    {
        var genome = CreatureGenome.RandomFounder(new Random(17));
        var serialized = genome.SerializedBody;

        Assert.StartsWith(GenomeV4Codec.Base64Prefix, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(GenomeV4Codec.Base64Prefix, genome.Body, StringComparison.Ordinal);
        var restored = CreatureGenome.FromSerialized(serialized);
        Assert.Equal(genome.Body, restored.Body);
        Assert.True(CreatureGenome.TryDecode(serialized, out _));
        Assert.True(CreatureGenome.TryDecode(genome.Body, out _));
    }
}
