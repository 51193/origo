using System;
using Origo.EvolutionDemo.Analysis.Math;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for the standard (Rousseeuw) silhouette coefficient.</summary>
public sealed class SilhouetteTests
{
    [Fact]
    public void Compute_MeanBasedDefinition_ReturnsTextbookValue()
    {
        // Two clusters on a line: {0, 1} and {2, 3} with d(i, j) = |i - j|.
        // Row 0: a = 1, b = (2 + 3) / 2 = 2.5 -> s = 1.5 / 2.5 = 0.6.
        // Row 1: a = 1, b = (1 + 2) / 2 = 1.5 -> s = 0.5 / 1.5 = 1/3.
        // Rows 2 and 3 are symmetric; the mean is 7/15.
        var silhouette = Silhouette.Compute(4, [0, 0, 1, 1], 2, IndexDistance, sampleSize: 800);

        Assert.Equal(7d / 15d, silhouette!.Value, 12);
    }

    [Fact]
    public void Compute_SingletonClusterRowsScoreZero()
    {
        // Clusters {0, 1}, {2}, {3}: row 0 -> s = (2 - 1) / 2 = 0.5,
        // row 1 -> b = 1 (distance to 2) so s = 0, rows 2 and 3 are singletons -> 0.
        var silhouette = Silhouette.Compute(4, [0, 0, 1, 2], 3, IndexDistance, sampleSize: 100);

        Assert.Equal(0.125, silhouette!.Value, 12);
    }

    [Fact]
    public void Compute_ExcludesNoiseRowsAndNoiseNeighbours()
    {
        // Same partition as the textbook case plus one noise row; the noise row is
        // not scored and does not contribute to any distance mean.
        var silhouette = Silhouette.Compute(5, [0, 0, 1, 1, -1], 2, IndexDistance, sampleSize: 100);

        Assert.Equal(7d / 15d, silhouette!.Value, 12);
    }

    [Fact]
    public void Compute_FewerThanTwoOccupiedClusters_ReturnsNull()
    {
        Assert.Null(Silhouette.Compute(4, [0, 0, 0, 0], 1, IndexDistance, sampleSize: 100));
        Assert.Null(Silhouette.Compute(4, [-1, -1, -1, -1], 2, IndexDistance, sampleSize: 100));
        Assert.Null(Silhouette.Compute(0, [], 2, IndexDistance, sampleSize: 100));
    }

    [Fact]
    public void Compute_AllSingletonClusters_ReturnsZero()
    {
        var labels = new[] { 0, 1, 2, 3 };

        var silhouette = Silhouette.Compute(4, labels, 4, IndexDistance, sampleSize: 100);

        Assert.Equal(0d, silhouette!.Value, 12);
    }

    [Fact]
    public void SampleIndexes_IsSystematicAndReproducible()
    {
        Assert.Equal([0, 2, 5, 7], Silhouette.SampleIndexes(10, 4));
        Assert.Equal([0, 1, 2, 3], Silhouette.SampleIndexes(4, 100));
        Assert.Equal(Silhouette.SampleIndexes(1619, 1000), Silhouette.SampleIndexes(1619, 1000));
    }

    [Fact]
    public void Compute_SampledValueStaysInRange()
    {
        var labels = new int[100];
        for (var row = 0; row < labels.Length; row++)
            labels[row] = row < 50 ? 0 : 1;

        var silhouette = Silhouette.Compute(100, labels, 2, IndexDistance, sampleSize: 10);

        Assert.InRange(silhouette!.Value, -1d, 1d);
        Assert.True(silhouette.Value > 0.5);
    }

    [Fact]
    public void Compute_LabelsShorterThanRowCount_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Silhouette.Compute(4, [0, 0], 2, IndexDistance, sampleSize: 100));
    }

    [Fact]
    public void Compute_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => Silhouette.Compute(4, null!, 2, IndexDistance, sampleSize: 100));
        Assert.Throws<ArgumentNullException>(
            () => Silhouette.Compute(4, [0, 0, 1, 1], 2, null!, sampleSize: 100));
    }

    private static double IndexDistance(int left, int right) => System.Math.Abs(left - right);
}
