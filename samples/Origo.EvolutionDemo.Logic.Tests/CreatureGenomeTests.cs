using System;
using Origo.EvolutionDemo.Logic;
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
        var child = CreatureGenome.Mutate(parent, 1d, 1d, 0d, 1d, new Random(11), out var changes);
        Assert.True(changes > 0);
        Assert.NotEqual(parent.Body, child.Body);
        Assert.True(CreatureGenome.TryDecode(child.Body, out _));
    }
}
