using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Genome;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for the Fisher exact test and the full-set BH correction.</summary>
public sealed class GeneEnrichmentTests
{
    [Theory]
    [InlineData(3, 1, 1, 3, 0.4857142857142857)]
    [InlineData(2, 7, 8, 2, 0.0230141375652212)]
    [InlineData(0, 5, 5, 1, 0.0151515151515152)]
    [InlineData(1, 9, 11, 3, 0.0027594561852201)]
    [InlineData(0, 10, 10, 0, 0.0000108250882245)]
    [InlineData(5, 0, 0, 5, 0.0079365079365079)]
    [InlineData(12, 12, 0, 24, 0.0000776291914463)]
    public void FisherExact_KnownTables_ReturnIndependentValues(int a, int b, int c, int d, double expected)
    {
        Assert.Equal(expected, GeneEnrichment.FisherExact(a, b, c, d), 12);
    }

    [Fact]
    public void FisherExact_NullTableIsCertain()
    {
        Assert.Equal(1d, GeneEnrichment.FisherExact(0, 0, 0, 0), 12);
        Assert.Equal(1d, GeneEnrichment.FisherExact(2, 2, 2, 2), 12);
    }

    [Fact]
    public void FisherExact_NegativeCell_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneEnrichment.FisherExact(-1, 0, 0, 1));
    }

    [Fact]
    public void BenjaminiHochberg_KnownExample_ReturnsExpectedAdjustedValues()
    {
        // Benjamini & Hochberg (1995) worked example, m = 10.
        double[] pValues = [0.001, 0.008, 0.039, 0.041, 0.042, 0.06, 0.074, 0.205, 0.212, 0.216];
        double[] expected =
        [
            0.01, 0.04, 0.084, 0.084, 0.084, 0.1, 0.1057142857142857, 0.216, 0.216, 0.216,
        ];

        var qValues = GeneEnrichment.BenjaminiHochberg(pValues);

        Assert.Equal(expected.Length, qValues.Count);
        for (var index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], qValues[index], 12);
    }

    [Fact]
    public void BenjaminiHochberg_QValuesAreMonotoneAndAtLeastP()
    {
        double[] pValues = [0.0001, 0.0002, 0.0005, 0.01, 0.02, 0.5, 0.9];

        var qValues = GeneEnrichment.BenjaminiHochberg(pValues);

        for (var index = 0; index < pValues.Length; index++)
        {
            Assert.True(qValues[index] >= pValues[index] - 1e-12);
            Assert.InRange(qValues[index], 0d, 1d);
            if (index > 0)
                Assert.True(qValues[index] >= qValues[index - 1] - 1e-12);
        }
    }

    [Fact]
    public void BenjaminiHochberg_InvalidPValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeneEnrichment.BenjaminiHochberg([0.5, 1.5]));
        Assert.Throws<ArgumentException>(() => GeneEnrichment.BenjaminiHochberg([0.5, double.NaN]));
    }

    [Fact]
    public void Analyze_CorrectsOverEveryTestedSignatureWithoutTruncation()
    {
        var (rows, labels) = BuildPopulationWithManyTestedSignatures(60);

        var analysis = GeneEnrichment.Analyze(rows, labels, 0);

        Assert.Equal(60, analysis.TestedCount);
        Assert.Equal(60, analysis.Signatures.Count);
        for (var index = 1; index < analysis.Signatures.Count; index++)
        {
            Assert.True(analysis.Signatures[index].PValue >= analysis.Signatures[index - 1].PValue);
            Assert.True(analysis.Signatures[index].QValue >= analysis.Signatures[index - 1].QValue - 1e-12);
        }

        Assert.All(analysis.Signatures, result =>
        {
            Assert.True(result.QValue >= result.PValue - 1e-12);
            Assert.InRange(result.QValue, 0d, 1d);
        });
    }

    [Fact]
    public void Analyze_TestsSignaturesAbsentFromTheClusterToo()
    {
        var rows = new List<FeatureRow>();
        var labels = new List<int>();
        // "sig.depleted" appears only in the rest cluster, but its overall support
        // passes the filter, so a full tested set must include it.
        for (var index = 0; index < 12; index++)
        {
            var signatures = new HashSet<string>(["sig.cluster"], StringComparer.Ordinal)
            {
                "sig.common",
                $"sig.cluster{index}",
            };
            rows.Add(Row($"cluster-{index}", signatures));
            labels.Add(0);
        }

        for (var index = 0; index < 24; index++)
        {
            var signatures = new HashSet<string>(["sig.common", "sig.depleted"], StringComparer.Ordinal)
            {
                $"sig.rest{index}",
            };
            rows.Add(Row($"rest-{index}", signatures));
            labels.Add(1);
        }

        var analysis = GeneEnrichment.Analyze(rows, [.. labels], 0);

        var depleted = analysis.Signatures.Single(result => result.Signature == "sig.depleted");
        Assert.Equal(0, depleted.ClusterWith);
        Assert.Equal(24, depleted.RestWith);
        Assert.True(depleted.OddsRatio < 1);
        Assert.Contains(analysis.Signatures, result => result.Signature == "sig.cluster");
    }

    [Fact]
    public void Analyze_SignaturesBelowMinimumSupportAreNotTested()
    {
        var (rows, labels) = BuildPopulation();

        var analysis = GeneEnrichment.Analyze(rows, labels, 0);

        Assert.DoesNotContain(analysis.Signatures, result => result.Signature == "sig.thin");
        Assert.Equal(analysis.TestedCount, analysis.Signatures.Count);
    }

    [Fact]
    public void Analyze_ExcludesNoiseRowsFromBothSides()
    {
        var (rows, labels) = BuildPopulation();
        var withNoise = labels.Concat([-1, -1, -1]).ToArray();
        var noiseRows = rows.Concat([Row("noise-1", ["sig.noise"]), Row("noise-2", ["sig.noise"]), Row("noise-3", ["sig.noise"])]).ToArray();

        var clean = GeneEnrichment.Analyze(rows, labels, 0);
        var noisy = GeneEnrichment.Analyze(noiseRows, withNoise, 0);

        Assert.Equal(clean.TestedCount, noisy.TestedCount);
        var cleanExclusive = clean.Signatures.Single(result => result.Signature == "sig.exclusive");
        var noisyExclusive = noisy.Signatures.Single(result => result.Signature == "sig.exclusive");
        Assert.Equal(cleanExclusive.ClusterTotal, noisyExclusive.ClusterTotal);
        Assert.Equal(cleanExclusive.RestTotal, noisyExclusive.RestTotal);
        Assert.Equal(cleanExclusive.PValue, noisyExclusive.PValue, 12);
    }

    [Fact]
    public void Analyze_ClusterWithoutRows_ReturnsEmpty()
    {
        var (rows, labels) = BuildPopulation();

        var analysis = GeneEnrichment.Analyze(rows, labels, 9);

        Assert.Empty(analysis.Signatures);
        Assert.Equal(0, analysis.TestedCount);
    }

    [Fact]
    public void Analyze_AllRowsInOneCluster_ReturnsEmpty()
    {
        var (rows, _) = BuildPopulation();
        var labels = new int[rows.Count];

        var analysis = GeneEnrichment.Analyze(rows, labels, 0);

        Assert.Empty(analysis.Signatures);
    }

    [Fact]
    public void Analyze_MismatchedLabels_Throws()
    {
        var (rows, labels) = BuildPopulation();

        Assert.Throws<ArgumentException>(() => GeneEnrichment.Analyze(rows, labels[..^1], 0));
    }

    private static (IReadOnlyList<FeatureRow> Rows, int[] Labels) BuildPopulation()
    {
        const int clusterRows = 12;
        const int restRows = 24;
        var rows = new List<FeatureRow>(clusterRows + restRows);
        for (var index = 0; index < clusterRows; index++)
        {
            var signatures = new HashSet<string>(["sig.exclusive", "sig.common"], StringComparer.Ordinal);
            if (index < 6)
                signatures.Add("sig.cluster_biased");
            if (index < 2)
                signatures.Add("sig.rest_biased");
            if (index < 1)
                signatures.Add("sig.rare");
            if (index < 2)
                signatures.Add("sig.thin");
            rows.Add(Row($"cluster-{index}", signatures));
        }

        for (var index = 0; index < restRows; index++)
        {
            var signatures = new HashSet<string>(["sig.common"], StringComparer.Ordinal);
            if (index < 3)
                signatures.Add("sig.cluster_biased");
            if (index < 8)
                signatures.Add("sig.rest_biased");
            if (index < 2)
                signatures.Add("sig.rare");
            rows.Add(Row($"rest-{index}", signatures));
        }

        return (rows, [.. Enumerable.Repeat(0, clusterRows), .. Enumerable.Repeat(1, restRows)]);
    }

    private static (IReadOnlyList<FeatureRow> Rows, int[] Labels) BuildPopulationWithManyTestedSignatures(int signatureCount)
    {
        var rows = new List<FeatureRow>();
        var labels = new List<int>();
        for (var index = 0; index < 12; index++)
        {
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            // Every generated signature occurs in rows 0..2 overall.
            for (var generated = 0; generated < signatureCount; generated++)
                if (index < 3)
                    signatures.Add($"sig.many{generated}");
            rows.Add(Row($"cluster-{index}", signatures));
            labels.Add(0);
        }

        for (var index = 0; index < 24; index++)
        {
            rows.Add(Row($"rest-{index}", []));
            labels.Add(1);
        }

        return (rows, [.. labels]);
    }

    private static FeatureRow Row(string name, IEnumerable<string> signatures) =>
        new("save.json", 1d, name, 0, 1,
            new Dictionary<string, double>(StringComparer.Ordinal),
            signatures.ToHashSet(StringComparer.Ordinal));
}
