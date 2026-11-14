using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4GenomeTests
{
    [Fact]
    public void Codec_RoundTripsCanonicalGenome()
    {
        var genome = GenomeV4Factory.CreateGraphBrain(6, 4);
        var data = GenomeV4Codec.Serialize(genome);
        var restored = GenomeV4Codec.Deserialize(data);
        Assert.Equal(GenomeV4Codec.Serialize(genome), GenomeV4Codec.Serialize(restored));
        Assert.Equal(genome.Genes.Count, restored.Genes.Count);
        Assert.Equal(genome.NextGeneId, restored.NextGeneId);
    }

    [Fact]
    public void Codec_RejectsUnknownSchema()
    {
        var data = GenomeV4Codec.Serialize(GenomeV4Factory.CreateSimpleReflex());
        data[2] = 0xFF;
        Assert.Throws<System.IO.InvalidDataException>(() => GenomeV4Codec.Deserialize(data));
    }

    [Fact]
    public void Mutator_PreservesStableIdsAndValidity()
    {
        var random = new Random(20261110);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        var expanded = false;
        for (var i = 0; i < 500; i++)
        {
            genome = GenomeV4Mutator.Mutate(genome, random);
            GenomeV4Validator.Validate(genome);
            Assert.Equal(genome.Genes.Count, genome.Genes.Select(static g => g.Id).Distinct().Count());
            expanded |= genome.Genes.Count > 3;
            Assert.True(genome.NextGeneId > genome.Genes.Max(static g => g.Id));
        }

        Assert.True(expanded);
    }

    [Fact]
    public void Mutator_CanGrowVariableBrainGraph()
    {
        var random = new Random(77);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        var maxNeurons = 0;
        for (var i = 0; i < 500; i++)
        {
            genome = GenomeV4Mutator.Mutate(genome, random);
            maxNeurons = Math.Max(maxNeurons,
                genome.Genes.Count(static g => g.Kind == GeneKind.Neuron));
        }

        Assert.True(maxNeurons > 2);
        GenomeV4Validator.Validate(genome);
    }

    [Fact]
    public void DuplicatedGenes_GetFreshStableIds()
    {
        var random = new Random(1);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        var before = genome.Genes.Select(static g => g.Id).ToHashSet();
        var settings = new GenomeMutationSettingsV4
        {
            Duplicate = 1d,
            Expression = 0d,
            CopyCount = 0d,
            Delete = 0d,
            Payload = 0d,
            AddNeuron = 0d,
            AddSynapse = 0d,
            AddReflex = 0d,
            AddOrgan = 0d,
            AddRegulation = 0d,
            AddSegment = 0d,
        };
        var child = GenomeV4Mutator.Mutate(genome, random, settings);
        Assert.Equal(before.Count + 1, child.Genes.Count);
        Assert.Single(child.Genes, gene => !before.Contains(gene.Id));
    }
}
