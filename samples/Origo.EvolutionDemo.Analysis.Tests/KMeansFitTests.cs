using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Math;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for fixed-k, auto-k, and error handling in <see cref="KMeans" />.</summary>
public sealed class KMeansFitTests
{
    [Fact]
    public void Fit_RequestedK_IsHonoredExactly()
    {
        var matrix = TestMatrices.WellSeparated(2, 20);

        var result = KMeans.Fit(matrix, 5);

        Assert.Equal(5, result.K);
        Assert.Equal(5, result.Centers.Length);
        Assert.Equal(matrix.RowCount, result.Labels.Length);
        Assert.All(result.Labels, label => Assert.InRange(label, 0, 4));
        Assert.NotNull(result.Silhouette);
    }

    [Fact]
    public void Fit_RequestedK_RecoversSeparatedClustersWithMinimalInertia()
    {
        var matrix = TestMatrices.WellSeparated(3, 10);

        var result = KMeans.Fit(matrix, 3);

        Assert.Equal(3, result.K);
        var sizes = Enumerable.Range(0, 3)
            .Select(cluster => result.Labels.Count(label => label == cluster))
            .ToArray();
        Assert.All(sizes, size => Assert.Equal(10, size));
        Assert.True(result.Silhouette > 0.9);
        // Inertia is the SSE of the reported labels and centers: a balanced split of
        // three well-separated groups must be far below the total variance.
        Assert.Equal(TestMatrices.Inertia(matrix, result.Labels, result.Centers), result.Inertia!.Value, 9);
        Assert.True(result.Inertia < 1d);
    }

    [Fact]
    public void Fit_ReportedInertiaEqualsSseOfReportedSolution()
    {
        var matrix = TestMatrices.WellSeparated(4, 15);

        var result = KMeans.Fit(matrix, 3);

        Assert.Equal(TestMatrices.Inertia(matrix, result.Labels, result.Centers), result.Inertia!.Value, 9);
    }

    [Fact]
    public void Fit_RequestedOne_ReturnsSingleClusterWithoutSilhouette()
    {
        var matrix = TestMatrices.WellSeparated(2, 3);

        var result = KMeans.Fit(matrix, 1);

        Assert.Equal(1, result.K);
        Assert.All(result.Labels, label => Assert.Equal(0, label));
        Assert.Null(result.Silhouette);
        Assert.NotNull(result.Inertia);
        Assert.Equal(TestMatrices.Inertia(matrix, result.Labels, result.Centers), result.Inertia!.Value, 9);
    }

    [Fact]
    public void Fit_RequestedKEqualsRowCount_Throws()
    {
        var matrix = TestMatrices.WellSeparated(2, 4);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => KMeans.Fit(matrix, matrix.RowCount));

        Assert.Equal("requestedK", exception.ParamName);
        Assert.Contains("--k", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fit_RequestedKAboveRowCount_Throws()
    {
        var matrix = TestMatrices.WellSeparated(2, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => KMeans.Fit(matrix, matrix.RowCount + 3));
    }

    [Fact]
    public void Fit_NegativeK_Throws()
    {
        var matrix = TestMatrices.WellSeparated(2, 4);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => KMeans.Fit(matrix, -1));

        Assert.Equal("requestedK", exception.ParamName);
    }

    [Fact]
    public void Fit_AutoKTooFewRows_ThrowsInsteadOfReturningOneCluster()
    {
        var matrix = TestMatrices.WellSeparated(1, 2);

        Assert.Throws<InvalidOperationException>(() => KMeans.Fit(matrix, 0));
    }

    [Fact]
    public void Fit_EmptyMatrix_ReturnsEmptyResult()
    {
        var result = KMeans.Fit(new FeatureMatrix(["x"], []), 0);

        Assert.Equal(0, result.K);
        Assert.Empty(result.Labels);
        Assert.Empty(result.Centers);
        Assert.Null(result.Silhouette);
    }

    [Fact]
    public void Select_AutoK_ReportsEveryCandidateAndSelectsMaximumSilhouette()
    {
        var matrix = TestMatrices.WellSeparated(3, 20);

        var selection = KMeans.Select(matrix, 0);

        Assert.Equal(KMeans.DefaultMaxK, selection.MaxK);
        Assert.Equal(selection.MaxK - 1, selection.Candidates.Count);
        Assert.Equal([2, 3, 4, 5, 6, 7, 8], [.. selection.Candidates.Select(candidate => candidate.K)]);
        Assert.All(selection.Candidates, candidate =>
            Assert.Equal(matrix.RowCount, candidate.Sizes.Sum()));
        Assert.All(selection.Candidates, candidate =>
            Assert.Equal("accepted", candidate.Flags[0]));
        var selected = selection.Candidates.Single(candidate => candidate.Flags.Contains("selected"));
        Assert.Equal(selection.Best.K, selected.K);
        var maximum = selection.Candidates.Max(candidate => candidate.Silhouette!.Value);
        Assert.Equal(maximum, selected.Silhouette!.Value, 12);
        Assert.False(selection.NoValidCandidate);
    }

    [Fact]
    public void AutoSelection_SelectsMaximumSilhouetteAmongAcceptedCandidates()
    {
        // The rejected singleton split has a much higher silhouette; the documented
        // validity constraint must win over the raw maximum.
        var results = new List<KMeans.Result>
        {
            Candidate(k: 2, inertia: 1000, silhouette: 0.9, sizes: [1, 903]),
            Candidate(k: 3, inertia: 900, silhouette: 0.3, sizes: [400, 400, 104]),
        };

        var selection = AutoSelection.Build(results, maxK: 3, floor: 2);

        Assert.False(selection.NoValidCandidate);
        Assert.Equal(3, selection.Best.K);
        var selected = selection.Candidates.Single(candidate => candidate.Flags.Contains("selected"));
        Assert.Equal(3, selected.K);
        Assert.Contains("small-clusters", selection.Candidates.Single(candidate => candidate.K == 2).Flags);
    }

    [Fact]
    public void AutoSelection_NoAcceptedCandidate_FallsBackToGlobalMaximum()
    {
        var results = new List<KMeans.Result>
        {
            Candidate(k: 2, inertia: 1000, silhouette: 0.9, sizes: [1, 903]),
            Candidate(k: 3, inertia: 900, silhouette: 0.3, sizes: [1, 400, 503]),
        };

        var selection = AutoSelection.Build(results, maxK: 3, floor: 10);

        Assert.True(selection.NoValidCandidate);
        Assert.Equal(2, selection.Best.K);
        Assert.Equal(0.9, selection.Best.Silhouette!.Value, 12);
    }

    private static KMeans.Result Candidate(int k, double inertia, double silhouette, int[] sizes)
    {
        var labels = new List<int>(sizes.Sum());
        for (var cluster = 0; cluster < k; cluster++)
            for (var row = 0; row < sizes[cluster]; row++)
                labels.Add(cluster);
        return new KMeans.Result([.. labels], [], inertia, k, silhouette);
    }

    [Fact]
    public void Select_NoValidCandidate_FallsBackWithExplicitFlagAndLoudRejection()
    {
        var matrix = TestMatrices.WellSeparated(3, 20);

        var selection = KMeans.Select(matrix, 0, minClusterSize: 40);

        Assert.True(selection.NoValidCandidate);
        Assert.All(selection.Candidates, candidate => Assert.Equal("rejected", candidate.Flags[0]));
        Assert.Contains(selection.Candidates, candidate => candidate.Flags.Contains("selected"));
        // The fallback still returns the maximum-silhouette candidate, never k=1.
        Assert.True(selection.Best.K >= 2);
        var maximum = selection.Candidates.Max(candidate => candidate.Silhouette!.Value);
        Assert.Equal(maximum, selection.Best.Silhouette!.Value, 12);
    }

    [Fact]
    public void Select_FixedK_IsNotSubjectToMinimumClusterSize()
    {
        var matrix = TestMatrices.WellSeparated(3, 20);

        var selection = KMeans.Select(matrix, 3, minClusterSize: 60);

        Assert.Equal(3, selection.Best.K);
        Assert.Empty(selection.Candidates);
        Assert.False(selection.NoValidCandidate);
    }

    [Fact]
    public void Select_InvalidMinimumClusterSize_Throws()
    {
        var matrix = TestMatrices.WellSeparated(2, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => KMeans.Select(matrix, 0, minClusterSize: 1));
    }

    [Fact]
    public void Select_SameSeed_IsDeterministic()
    {
        var matrix = TestMatrices.WellSeparated(3, 12);

        var first = KMeans.Select(matrix, 0, seed: 7);
        var second = KMeans.Select(matrix, 0, seed: 7);

        Assert.Equal(first.Best.K, second.Best.K);
        Assert.Equal(first.Best.Labels, second.Best.Labels);
        Assert.Equal(first.Best.Inertia, second.Best.Inertia);
        Assert.Equal(first.Best.Silhouette, second.Best.Silhouette);
    }

    [Fact]
    public void Assign_AssignsEveryRowToNearestCenter()
    {
        var matrix = TestMatrices.WellSeparated(2, 5);
        var result = KMeans.Fit(matrix, 2);

        var labels = KMeans.Assign(matrix, result.Centers);

        Assert.Equal(result.Labels, labels);
    }
}
