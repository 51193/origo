using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class CreatureGenomeTests
{
    [Fact]
    public void RandomFounder_HasValidCombinedGenomes()
    {
        var genome = CreatureGenome.RandomFounder(new Random(9));

        Assert.True(BodyGenome.IsStructurallyValid(genome.Body));
        Assert.True(NeuralGenome.IsValid(genome.Brain));
        Assert.Equal(8, genome.Phenotype.ActiveSensorCount);
    }

    [Fact]
    public void Mutate_ReportsCombinedChangesAndKeepsBrainFixed()
    {
        var parent = CreatureGenome.RandomFounder(new Random(10));
        var child = CreatureGenome.Mutate(
            parent,
            1d,
            0d,
            0d,
            1d,
            new Random(11),
            out var changes);

        Assert.True(changes > 0);
        Assert.NotEqual(parent.Body, child.Body);
        Assert.NotEqual(parent.Brain, child.Brain);
        Assert.Equal(NeuralGenome.Length, child.Brain.Length);
    }
}
