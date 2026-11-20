using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Math;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for DBSCAN noise handling, minPts semantics, and validation.</summary>
public sealed class DbscanTests
{
    [Fact]
    public void Fit_NoiseRowsKeepMinusOneAndAreNotCountedAsClusters()
    {
        var matrix = BuildClusteredWithNoise();

        var result = Dbscan.Fit(matrix, eps: 1d, minPoints: 3);

        Assert.Equal(3, result.K);
        Assert.Equal(matrix.RowCount, result.Labels.Length);
        Assert.Equal(2, result.Labels.Count(label => label == -1));
        Assert.Equal(5, result.Labels.Count(label => label == 0));
        Assert.Equal(5, result.Labels.Count(label => label == 1));
        Assert.Equal(5, result.Labels.Count(label => label == 2));
        Assert.Null(result.Inertia);
    }

    [Fact]
    public void Fit_SilhouetteExcludesNoiseRows()
    {
        var matrix = BuildClusteredWithNoise();

        var result = Dbscan.Fit(matrix, eps: 1d, minPoints: 3);

        // Noise rows are excluded from the mean, so every scored row sits far from
        // the other clusters and close to its own group.
        Assert.NotNull(result.Silhouette);
        Assert.InRange(result.Silhouette.Value, 0.9d, 1d + 1e-9);
    }

    [Fact]
    public void Fit_MinPointsCountsThePointItself()
    {
        // Three points pair within eps. With minPts = 3 the core test includes the
        // point itself (two neighbours + self), so they form one cluster; the old
        // exclusive semantics would label every point as noise.
        var matrix = TestMatrices.TwoColumns([[0d, 0d], [0.1d, 0d], [0.2d, 0d]]);

        var inclusive = Dbscan.Fit(matrix, eps: 1d, minPoints: 3);
        var exclusive = Dbscan.Fit(matrix, eps: 1d, minPoints: 4);

        Assert.Equal(1, inclusive.K);
        Assert.All(inclusive.Labels, label => Assert.Equal(0, label));
        Assert.Equal(0, exclusive.K);
        Assert.All(exclusive.Labels, label => Assert.Equal(-1, label));
    }

    [Fact]
    public void Fit_AllNoise_ReportsZeroClustersAndNoSilhouette()
    {
        var matrix = TestMatrices.TwoColumns([[0d, 0d], [50d, 50d], [100d, 100d]]);

        var result = Dbscan.Fit(matrix, eps: 1d, minPoints: 3);

        Assert.Equal(0, result.K);
        Assert.All(result.Labels, label => Assert.Equal(-1, label));
        Assert.Null(result.Silhouette);
    }

    [Fact]
    public void Fit_EmptyMatrix_ReturnsEmptyResult()
    {
        var result = Dbscan.Fit(new FeatureMatrix(["x", "y"], []), eps: 1d, minPoints: 3);

        Assert.Equal(0, result.K);
        Assert.Empty(result.Labels);
        Assert.Null(result.Silhouette);
    }

    [Fact]
    public void Fit_EstimatedEpsilon_IsDeterministicAndFormsClusters()
    {
        var matrix = BuildClusteredWithNoise();

        var first = Dbscan.Fit(matrix, eps: 0d, minPoints: 3);
        var second = Dbscan.Fit(matrix, eps: 0d, minPoints: 3);

        Assert.Equal(3, first.K);
        Assert.Equal(first.Labels, second.Labels);
        Assert.Equal(first.Silhouette, second.Silhouette);
    }

    [Fact]
    public void Fit_NegativeEpsilon_Throws()
    {
        var matrix = TestMatrices.TwoColumns([[0d, 0d], [1d, 1d], [2d, 2d]]);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Dbscan.Fit(matrix, eps: -0.1d, minPoints: 3));

        Assert.Equal("eps", exception.ParamName);
    }

    [Fact]
    public void Fit_NonPositiveMinPoints_Throws()
    {
        var matrix = TestMatrices.TwoColumns([[0d, 0d], [1d, 1d], [2d, 2d]]);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Dbscan.Fit(matrix, eps: 1d, minPoints: 0));

        Assert.Equal("minPoints", exception.ParamName);
    }

    private static FeatureMatrix BuildClusteredWithNoise()
    {
        var rows = new List<double[]>();
        foreach (var center in new[] { 0d, 100d, 200d })
            for (var point = 0; point < 5; point++)
                rows.Add([center + (point * 0.01d), point * 0.01d]);
        rows.Add([50d, 50d]);
        rows.Add([150d, -50d]);
        return TestMatrices.TwoColumns(rows);
    }
}
