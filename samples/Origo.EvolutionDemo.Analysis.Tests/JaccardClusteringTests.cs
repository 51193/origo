using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Math;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for k-medoids Jaccard clustering auto/fixed-k behavior.</summary>
public sealed class JaccardClusteringTests
{
    [Fact]
    public void Fit_RequestedK_IsHonoredExactly()
    {
        var signatures = BuildTwoGroups(6);

        var result = JaccardClustering.Fit(signatures, 4);

        Assert.Equal(4, result.K);
        Assert.Equal(signatures.Count, result.Labels.Length);
        Assert.All(result.Labels, label => Assert.InRange(label, 0, 3));
        Assert.NotNull(result.Silhouette);
        Assert.Null(result.Inertia);
    }

    [Fact]
    public void Fit_WellSeparatedSignatures_PartitionIntoGroups()
    {
        var signatures = BuildTwoGroups(8);

        var result = JaccardClustering.Fit(signatures, 2);

        Assert.Equal(2, result.K);
        var sizes = Enumerable.Range(0, 2)
            .Select(cluster => result.Labels.Count(label => label == cluster))
            .ToArray();
        Assert.All(sizes, size => Assert.Equal(8, size));
        Assert.True(result.Silhouette > 0.5);
    }

    [Fact]
    public void Fit_RequestedKEqualOrAboveRowCount_Throws()
    {
        var signatures = BuildTwoGroups(3);

        var equals = Assert.Throws<ArgumentOutOfRangeException>(
            () => JaccardClustering.Fit(signatures, signatures.Count));
        Assert.Equal("requestedK", equals.ParamName);
        Assert.Contains("--k", equals.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => JaccardClustering.Fit(signatures, signatures.Count + 2));
    }

    [Fact]
    public void Fit_NegativeK_Throws()
    {
        var signatures = BuildTwoGroups(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => JaccardClustering.Fit(signatures, -1));
    }

    [Fact]
    public void Select_AutoK_ReportsCandidatesUpToDocumentedCap()
    {
        var signatures = BuildTwoGroups(10);

        var selection = JaccardClustering.Select(signatures, 0);

        Assert.Equal(JaccardClustering.DefaultMaxK, selection.MaxK);
        Assert.Equal(selection.MaxK - 1, selection.Candidates.Count);
        Assert.All(selection.Candidates, candidate => Assert.Equal(signatures.Count, candidate.Sizes.Sum()));
        Assert.Contains(selection.Candidates, candidate => candidate.Flags.Contains("selected"));
        Assert.False(selection.NoValidCandidate);
        Assert.Equal(2, selection.Best.K);
    }

    [Fact]
    public void Select_AutoKTooFewRows_Throws()
    {
        IReadOnlyList<IReadOnlySet<string>> signatures =
        [
            new HashSet<string>(["a"]),
            new HashSet<string>(["b"]),
        ];

        Assert.Throws<InvalidOperationException>(() => JaccardClustering.Select(signatures, 0));
    }

    [Fact]
    public void Fit_MedoidInitializationUsesDistinctRows()
    {
        // All-identical signatures exercise the medoid initialization and the
        // degenerate all-zero distance case: distinct medoids must not crash or
        // leave non-finite values.
        var signatures = Enumerable.Range(0, 12)
            .Select(_ => (IReadOnlySet<string>)new HashSet<string>(["same"], StringComparer.Ordinal))
            .ToArray();

        var result = JaccardClustering.Fit(signatures, 5);

        Assert.Equal(5, result.K);
        Assert.All(result.Labels, label => Assert.InRange(label, 0, 4));
        // Every distance is zero, so every row joins the first medoid: one
        // occupied cluster and therefore a null (n/a) silhouette, never NaN.
        Assert.Null(result.Silhouette);
    }

    [Fact]
    public void Fit_SameSeed_IsDeterministic()
    {
        var signatures = BuildTwoGroups(10);

        var first = JaccardClustering.Fit(signatures, 0);
        var second = JaccardClustering.Fit(signatures, 0);

        Assert.Equal(first.K, second.K);
        Assert.Equal(first.Labels, second.Labels);
        Assert.Equal(first.Silhouette, second.Silhouette);
    }

    private static IReadOnlyList<IReadOnlySet<string>> BuildTwoGroups(int sizePerGroup)
    {
        var signatures = new List<IReadOnlySet<string>>(sizePerGroup * 2);
        for (var index = 0; index < sizePerGroup; index++)
        {
            signatures.Add(new HashSet<string>(
                ["alpha.one", "alpha.two", "alpha.three", $"alpha{index % 3}"], StringComparer.Ordinal));
            signatures.Add(new HashSet<string>(
                ["beta.one", "beta.two", "beta.three", $"beta{index % 3}"], StringComparer.Ordinal));
        }

        return signatures;
    }
}
