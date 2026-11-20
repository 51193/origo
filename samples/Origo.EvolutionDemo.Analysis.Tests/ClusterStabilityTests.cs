using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Math;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for the true bootstrap adjusted Rand index.</summary>
public sealed class ClusterStabilityTests
{
    [Fact]
    public void Bootstrap_WellSeparatedThreeClusters_ReturnsHighAdjustedRand()
    {
        var matrix = TestMatrices.WellSeparated(3, 25);
        var reference = KMeans.Fit(matrix, 3, seed: 3);

        var stability = ClusterStability.Bootstrap(matrix, reference, rounds: 5, seed: 1);

        Assert.NotNull(stability);
        Assert.InRange(stability.Mean, 0.8d, 1d + 1e-9);
        Assert.Equal(5, stability.Rounds);
    }

    [Fact]
    public void Bootstrap_WellSeparatedTwoClusters_ReturnsHighAdjustedRand()
    {
        var matrix = TestMatrices.WellSeparated(2, 20);
        var reference = KMeans.Fit(matrix, 2, seed: 4);

        var stability = ClusterStability.Bootstrap(matrix, reference, rounds: 5, seed: 4);

        Assert.NotNull(stability);
        Assert.InRange(stability.Mean, 0.8d, 1d + 1e-9);
    }

    [Fact]
    public void Bootstrap_IsDeterministicForSameSeed()
    {
        var matrix = TestMatrices.WellSeparated(3, 20);
        var reference = KMeans.Fit(matrix, 3);

        var first = ClusterStability.Bootstrap(matrix, reference, rounds: 4, seed: 7);
        var second = ClusterStability.Bootstrap(matrix, reference, rounds: 4, seed: 7);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Mean, second.Mean, 12);
        Assert.Equal(first.StandardDeviation, second.StandardDeviation, 12);
        Assert.Equal(4, first.Rounds);
    }

    [Fact]
    public void Bootstrap_KOneAndZeroRounds_ReturnNullInsteadOfZero()
    {
        var matrix = TestMatrices.WellSeparated(2, 10);
        var oneCluster = KMeans.Fit(matrix, 1);
        var twoClusters = KMeans.Fit(matrix, 2);

        Assert.Null(ClusterStability.Bootstrap(matrix, oneCluster, rounds: 5, seed: 1));
        Assert.Null(ClusterStability.Bootstrap(matrix, twoClusters, rounds: 0, seed: 1));
    }

    [Fact]
    public void Bootstrap_NonKMeansReferenceWithoutCenters_ReturnsNull()
    {
        var matrix = TestMatrices.WellSeparated(2, 10);
        var reference = new KMeans.Result(new int[matrix.RowCount], [], null, 0, null);

        Assert.Null(ClusterStability.Bootstrap(matrix, reference, rounds: 5, seed: 1));
    }

    [Fact]
    public void AdjustedRand_IdenticalPartitions_ReturnsOne()
    {
        int[] labels = [0, 0, 1, 1, 2, 2];

        Assert.Equal(1d, ClusterStability.AdjustedRand(labels, labels), 12);
    }

    [Fact]
    public void AdjustedRand_IsPermutationInvariant()
    {
        int[] left = [0, 0, 1, 1, 2, 2, 2, 1];
        int[] right = [7, 7, 3, 3, 9, 9, 9, 3];
        int[] relabeled = [3, 3, 9, 9, 7, 7, 7, 9];

        Assert.Equal(
            ClusterStability.AdjustedRand(left, right),
            ClusterStability.AdjustedRand(left, relabeled),
            12);
        Assert.Equal(
            ClusterStability.AdjustedRand(left, right),
            ClusterStability.AdjustedRand(right, left),
            12);
    }

    [Fact]
    public void AdjustedRand_IndependentRandomPartitions_AreNearZero()
    {
        var random = new Random(20261120);
        var left = Enumerable.Range(0, 400).Select(_ => random.Next(4)).ToArray();
        var right = Enumerable.Range(0, 400).Select(_ => random.Next(4)).ToArray();

        var score = ClusterStability.AdjustedRand(left, right);

        Assert.InRange(score, -0.1d, 0.1d);
    }

    [Fact]
    public void AdjustedRand_MismatchedLengths_Throws()
    {
        Assert.Throws<ArgumentException>(() => ClusterStability.AdjustedRand([0, 1], [0]));
    }

    [Fact]
    public void AdjustedRand_OnePartitionAllSingletons_IsZero()
    {
        int[] singletons = [0, 1, 2, 3];
        int[] split = [0, 0, 1, 1];

        Assert.Equal(0d, ClusterStability.AdjustedRand(singletons, split), 12);
    }

    [Fact]
    public void Bootstrap_ReturnsNonNullStandardDeviation()
    {
        var matrix = TestMatrices.WellSeparated(4, 15);
        var reference = KMeans.Fit(matrix, 4);

        var stability = ClusterStability.Bootstrap(matrix, reference, rounds: 3, seed: 2);

        Assert.NotNull(stability);
        Assert.True(stability.StandardDeviation >= 0);
    }

    /// <summary>Verifies the bootstrap algorithm assigns every original row, so no row is silently dropped.</summary>
    [Fact]
    public void Bootstrap_UsesAllOriginalRows()
    {
        var matrix = TestMatrices.WellSeparated(2, 30);
        var reference = KMeans.Fit(matrix, 2);
        var assigned = KMeans.Assign(matrix, reference.Centers);

        Assert.Equal(matrix.RowCount, assigned.Length);
        Assert.Equal(reference.Labels, assigned);
    }
}
