using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>
///     Standard (Rousseeuw) silhouette coefficient shared by every clustering
///     method. For a row <c>i</c> in cluster <c>C_i</c>, <c>a(i)</c> is the mean
///     distance to the other rows of its own cluster and <c>b(i)</c> is the mean
///     distance to the rows of the nearest other cluster (not the nearest other
///     point); <c>s(i) = (b(i) - a(i)) / max(a(i), b(i))</c>. A row whose own
///     cluster is a singleton scores <c>s(i) = 0</c>. Rows labelled
///     <c>-1</c> are DBSCAN noise and are excluded from both the scored rows and
///     the distance sums of their potential neighbours.
/// </summary>
public static class Silhouette
{
    /// <summary>
    ///     Identifier of the silhouette definition used in reports and the
    ///     metrics JSON so an automation consumer can detect a definition change.
    /// </summary>
    public const string DefinitionId = "rousseeuw-mean-nearest-other-cluster-singleton-zero-systematic-sample";

    /// <summary>
    ///     Deterministic sample size used for the Euclidean k-means and DBSCAN
    ///     silhouettes. The sample is a systematic stride over the row index space,
    ///     so it depends only on the row count and can be reproduced outside this
    ///     tool.
    /// </summary>
    public const int EuclideanSampleSize = 1000;

    /// <summary>Deterministic sample size used for the Jaccard k-medoids silhouette.</summary>
    public const int JaccardSampleSize = 350;

    /// <summary>
    ///     Computes the mean silhouette over the documented deterministic sample,
    ///     or <see langword="null" /> when the coefficient is undefined (fewer
    ///     than two occupied clusters).
    /// </summary>
    /// <param name="rowCount">Number of rows in the matrix.</param>
    /// <param name="labels">Cluster label per row; <c>-1</c> marks noise rows.</param>
    /// <param name="clusterCount">Number of real (non-noise) clusters.</param>
    /// <param name="distance">Symmetric row distance function.</param>
    /// <param name="sampleSize">Maximum number of rows to score; rows are sampled systematically.</param>
    public static double? Compute(
        int rowCount,
        IReadOnlyList<int> labels,
        int clusterCount,
        Func<int, int, double> distance,
        int sampleSize)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(distance);
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleSize, 1);
        if (labels.Count != rowCount)
            throw new ArgumentException("A label is required for every row.", nameof(labels));
        if (rowCount == 0)
            return null;
        var sample = SampleIndexes(rowCount, sampleSize);
        var realClusterCount = labels.Count(label => label >= 0);
        if (realClusterCount < 2)
            return null;
        var occupied = new bool[clusterCount];
        foreach (var label in labels)
            if (label >= 0 && label < clusterCount)
                occupied[label] = true;
        if (occupied.Count(static value => value) < 2)
            return null;
        var sums = new double[clusterCount];
        var counts = new int[clusterCount];
        var total = 0d;
        var scored = 0;
        foreach (var row in sample)
        {
            var own = labels[row];
            if (own < 0 || own >= clusterCount)
                continue;
            Array.Clear(sums);
            Array.Clear(counts);
            for (var other = 0; other < rowCount; other++)
            {
                var label = labels[other];
                if (other == row || label < 0 || label >= clusterCount)
                    continue;
                var value = distance(row, other);
                sums[label] += value;
                counts[label]++;
            }

            var score = 0d;
            if (counts[own] > 0)
            {
                var meanOwn = sums[own] / counts[own];
                var meanNearest = double.PositiveInfinity;
                for (var cluster = 0; cluster < clusterCount; cluster++)
                {
                    if (cluster == own || counts[cluster] == 0)
                        continue;
                    var mean = sums[cluster] / counts[cluster];
                    if (mean < meanNearest)
                        meanNearest = mean;
                }

                if (double.IsFinite(meanNearest))
                    score = (meanNearest - meanOwn) / System.Math.Max(meanNearest, meanOwn);
            }

            total += score;
            scored++;
        }

        return scored == 0 ? null : total / scored;
    }

    /// <summary>
    ///     Returns the documented deterministic sample indexes: all rows when the
    ///     matrix fits the sample, otherwise the systematic stride
    ///     <c>floor(i * rowCount / sampleSize)</c>.
    /// </summary>
    public static int[] SampleIndexes(int rowCount, int sampleSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleSize, 1);
        if (rowCount <= sampleSize)
            return [.. Enumerable.Range(0, rowCount)];
        var indexes = new int[sampleSize];
        for (var index = 0; index < sampleSize; index++)
            indexes[index] = (int)((long)index * rowCount / sampleSize);
        return indexes;
    }
}
