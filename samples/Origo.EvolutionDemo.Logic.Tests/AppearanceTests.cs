using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class AppearanceTests
{
    [Fact]
    public void Derive_IsDeterministicAndWithinGamut()
    {
        var genome = CreatureGenome.RandomFounder(new Random(5)).Body;
        var first = GenomeAppearance.Derive(genome);
        var second = GenomeAppearance.Derive(genome);

        Assert.Equal(first, second);
        AssertColorInGamut(first.Body);
        AssertColorInGamut(first.Accent);
        Assert.InRange((int)first.Pattern, 0, CreatureAppearance.PatternCount - 1);
        Assert.InRange(first.ThemeIndex, 0, CreatureAppearance.ThemeCount - 1);
        Assert.False(string.IsNullOrWhiteSpace(first.ThemeName));
        Assert.False(string.IsNullOrWhiteSpace(first.PatternName));
    }

    [Fact]
    public void Derive_SingleDigitMutation_KeepsColorClose()
    {
        var genome = CreatureGenome.RandomFounder(new Random(8)).Body;
        var chars = genome.ToCharArray();
        chars[0] = chars[0] == 'F' ? '0' : 'F';
        var mutant = new string(chars);
        Assert.NotEqual(genome, mutant);

        var original = GenomeAppearance.Derive(genome).Body;
        var changed = GenomeAppearance.Derive(mutant).Body;
        var distance = MathF.Sqrt(
            MathF.Pow(original.R - changed.R, 2f)
            + MathF.Pow(original.G - changed.G, 2f)
            + MathF.Pow(original.B - changed.B, 2f));

        Assert.True(distance < 0.30f, $"Color moved too far for one digit: {distance:0.00}.");
    }

    [Fact]
    public void Derive_WorksForAnyGenomeLength()
    {
        foreach (var length in new[] { 1, 7, 58, 97, 200 })
        {
            var genome = new string('A', length);
            var appearance = GenomeAppearance.Derive(genome);
            AssertColorInGamut(appearance.Body);
            AssertColorInGamut(appearance.Accent);
        }
    }

    [Fact]
    public void Analyze_IdenticalGenomes_HaveZeroDiversity()
    {
        var genomes = new List<string> { CreatureGenome.RandomFounder(new Random(3)).Body, CreatureGenome.RandomFounder(new Random(3)).Body };
        var metrics = GenomeDiversity.Analyze(genomes);

        Assert.Equal(2, metrics.Population);
        Assert.Equal(1, metrics.UniqueGenomes);
        Assert.Equal(0f, metrics.MeanPairwiseDistance);
        Assert.Equal(0f, metrics.MeanAlleleEntropy);
    }

    [Fact]
    public void Analyze_MixedGenomes_ReportsDiversityAndIsLengthAgnostic()
    {
        var genomes = new List<string>
        {
            "AAAA1111",
            "AAAABBBB",
            "CCCC2222",
            "AAAAAAAAAAAA",
        };
        var metrics = GenomeDiversity.Analyze(genomes);

        Assert.Equal(4, metrics.Population);
        Assert.Equal(4, metrics.UniqueGenomes);
        Assert.True(metrics.MeanPairwiseDistance > 0f);
        Assert.True(metrics.MeanAlleleEntropy > 0f);
        Assert.InRange(metrics.MeanPairwiseDistance, 0f, 1f);
        Assert.InRange(metrics.MeanAlleleEntropy, 0f, 1f);
    }

    private static void AssertColorInGamut(AppearanceColor color)
    {
        Assert.InRange(color.R, 0f, 1f);
        Assert.InRange(color.G, 0f, 1f);
        Assert.InRange(color.B, 0f, 1f);
    }
}
