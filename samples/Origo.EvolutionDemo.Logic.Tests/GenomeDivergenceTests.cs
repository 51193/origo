using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GenomeDivergenceTests
{
    [Fact]
    public void CountChangedGenes_IdenticalGenomeIsZero()
    {
        var genome = GenomeV4Factory.CreateSimpleReflex();

        Assert.Equal(0, GenomeDivergence.CountChangedGenes(genome, genome.Clone()));
    }

    [Fact]
    public void CountChangedGenes_PayloadChangeCountsAsOne()
    {
        var parent = GenomeV4Factory.CreateSimpleReflex();
        var child = parent.Clone();
        var payloadGene = FindGene(child, GeneKind.BodySegment);
        payloadGene.Payload[0] ^= 0x01;

        Assert.Equal(1, GenomeDivergence.CountChangedGenes(parent, child));
    }

    [Fact]
    public void CountChangedGenes_AddedAndRemovedGenesCount()
    {
        var parent = GenomeV4Factory.CreateSimpleReflex();
        var added = parent.Clone();
        var newGene = new GeneV4(
            parent.NextGeneId,
            GeneKind.LifeHistory,
            1,
            1f,
            GenePayloadV4.LifeHistory(LifeTraitKindV4.MaxAge, 321f));
        var genes = new List<GeneV4>(added.Genes) { newGene };
        var withCeiling = new GenomeV4(genes, parent.NextGeneId + 1);

        Assert.Equal(1, GenomeDivergence.CountChangedGenes(parent, withCeiling));
        Assert.Equal(1, GenomeDivergence.CountChangedGenes(withCeiling, parent));
    }

    [Fact]
    public void CountChangedGenes_ConfigurationChangesCount()
    {
        var parent = GenomeV4Factory.CreateSimpleReflex();
        var child = parent.Clone();
        var gene = FindGene(child, GeneKind.BodySegment);
        gene.CopyCount = (byte)(gene.CopyCount + 1);
        var second = parent.Clone();
        FindGene(second, GeneKind.BodySegment).Expression = 0.5f;

        Assert.Equal(1, GenomeDivergence.CountChangedGenes(parent, child));
        Assert.Equal(1, GenomeDivergence.CountChangedGenes(parent, second));
    }

    [Fact]
    public void CountChangedGenes_RejectsNull()
    {
        var genome = GenomeV4Factory.CreateSimpleReflex();

        Assert.Throws<ArgumentNullException>(() => GenomeDivergence.CountChangedGenes(null!, genome));
        Assert.Throws<ArgumentNullException>(() => GenomeDivergence.CountChangedGenes(genome, null!));
    }

    private static GeneV4 FindGene(GenomeV4 genome, GeneKind kind) =>
        genome.Genes.Find(gene => gene.Kind == kind)
        ?? throw new InvalidOperationException($"No {kind} gene in the fixture genome.");
}
