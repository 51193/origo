using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>k-medoids clustering over gene-signature Jaccard distance.</summary>
public static class JaccardClustering
{
    /// <summary>Documented auto-k upper bound for the k-medoids search.</summary>
    public const int DefaultMaxK = 6;

    /// <summary>Fits k-medoids and returns the chosen solution only.</summary>
    public static KMeans.Result Fit(IReadOnlyList<IReadOnlySet<string>> signatures, int requestedK, int seed = 1) =>
        Select(signatures, requestedK, seed).Best;

    /// <summary>Fits k-medoids, returning the chosen solution and the full auto-k candidate table.</summary>
    /// <exception cref="ArgumentOutOfRangeException">k is negative, k exceeds the row count, or the minimum cluster size is invalid.</exception>
    public static ClusterSelection Select(
        IReadOnlyList<IReadOnlySet<string>> signatures,
        int requestedK,
        int seed = 1,
        int minClusterSize = 0)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        var n = signatures.Count;
        if (n == 0)
        {
            if (requestedK > 0)
                throw new ArgumentOutOfRangeException(
                    nameof(requestedK), $"--k {requestedK} exceeds the analyzable row count 0.");
            return new ClusterSelection(new KMeans.Result([], [], null, 0, null), [], 0, false);
        }

        if (requestedK < 0)
            throw new ArgumentOutOfRangeException(
                nameof(requestedK), "--k must be >= 0 (0 selects k automatically).");
        if (requestedK >= n)
            throw new ArgumentOutOfRangeException(
                nameof(requestedK), $"--k {requestedK} exceeds the analyzable row count {n}.");
        if (minClusterSize != 0 && minClusterSize < 2)
            throw new ArgumentOutOfRangeException(
                nameof(minClusterSize), "--min-cluster-size must be >= 2.");
        if (requestedK > 0)
            return new ClusterSelection(Run(signatures, requestedK, seed + (requestedK * 104729)), [], 0, false);
        if (n < 3)
            throw new InvalidOperationException(
                $"Auto-k requires at least three rows; the matrix has {n}. Pass an explicit --k.");
        var maxK = System.Math.Min(DefaultMaxK, n - 1);
        var floor = minClusterSize == 0 ? KMeans.MinimumClusterSizeFor(n) : minClusterSize;
        var results = new List<KMeans.Result>(maxK - 1);
        for (var k = 2; k <= maxK; k++)
            results.Add(Run(signatures, k, seed + (k * 104729)));
        return AutoSelection.Build(results, maxK, floor);
    }

    private static KMeans.Result Run(IReadOnlyList<IReadOnlySet<string>> signatures, int k, int seed)
    {
        var n = signatures.Count;
        var labels = new int[n];
        if (k == 1)
        {
            var labelsOne = new int[n];
            return new KMeans.Result(labelsOne, [], null, 1, null);
        }

        var random = new Random(seed);
        var medoids = new int[k];
        var pool = Enumerable.Range(0, n).ToArray();
        for (var index = 0; index < k; index++)
        {
            var pick = random.Next(index, n);
            (pool[index], pool[pick]) = (pool[pick], pool[index]);
            medoids[index] = pool[index];
        }

        for (var iteration = 0; iteration < 25; iteration++)
        {
            var changed = false;
            for (var row = 0; row < n; row++)
            {
                var best = 0;
                var bestDistance = double.PositiveInfinity;
                for (var medoid = 0; medoid < k; medoid++)
                {
                    var distance = Distance(signatures[row], signatures[medoids[medoid]]);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = medoid;
                    }
                }

                if (labels[row] != best)
                {
                    labels[row] = best;
                    changed = true;
                }
            }

            for (var cluster = 0; cluster < k; cluster++)
            {
                var indexes = Enumerable.Range(0, n).Where(index => labels[index] == cluster).ToArray();
                if (indexes.Length == 0)
                    continue;
                var bestIndex = indexes[0];
                var bestCost = double.PositiveInfinity;
                foreach (var candidate in indexes)
                {
                    var cost = indexes.Sum(index => Distance(signatures[candidate], signatures[index]));
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestIndex = candidate;
                    }
                }

                if (medoids[cluster] != bestIndex)
                {
                    medoids[cluster] = bestIndex;
                    changed = true;
                }
            }

            if (!changed)
                break;
        }

        // Centers are meaningless for Jaccard; the report only needs labels/K.
        var silhouette = Silhouette.Compute(
            n, labels, k,
            (left, right) => Distance(signatures[left], signatures[right]),
            Silhouette.JaccardSampleSize);
        return new KMeans.Result(labels, [], null, k, silhouette);
    }

    internal static double Distance(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        var intersection = left.Count <= right.Count
            ? left.Count(right.Contains)
            : right.Count(left.Contains);
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : 1d - (intersection / (double)union);
    }
}
