using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>k-medoids clustering over gene-signature Jaccard distance.</summary>
public static class JaccardClustering
{
    public static KMeans.Result Fit(IReadOnlyList<IReadOnlySet<string>> signatures, int requestedK, int seed = 1)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        var n = signatures.Count;
        if (n < 3)
            return new KMeans.Result(new int[n], [], 0, 1, 0);
        var maxK = System.Math.Min(requestedK > 0 ? requestedK : 6, n - 1);
        KMeans.Result? best = null;
        for (var k = 2; k <= maxK; k++)
        {
            var candidate = Run(signatures, k, seed + (k * 104729));
            var minCluster = System.Math.Max(2, n / 100);
            if (Enumerable.Range(0, k).Any(cluster => candidate.Labels.Count(label => label == cluster) < minCluster))
                continue;
            if (best is null || candidate.Silhouette > best.Silhouette)
                best = candidate;
        }

        return best ?? new KMeans.Result(new int[n], [], 0, 1, 0);
    }

    private static KMeans.Result Run(IReadOnlyList<IReadOnlySet<string>> signatures, int k, int seed)
    {
        var random = new Random(seed);
        var labels = new int[signatures.Count];
        var medoids = Enumerable.Range(0, k).Select(_ => random.Next(signatures.Count)).Distinct().ToArray();
        while (medoids.Length < k)
            medoids = [.. medoids, random.Next(signatures.Count)];
        for (var iteration = 0; iteration < 25; iteration++)
        {
            var changed = false;
            for (var i = 0; i < signatures.Count; i++)
            {
                var best = 0;
                var bestDistance = double.PositiveInfinity;
                for (var medoid = 0; medoid < k; medoid++)
                {
                    var distance = Distance(signatures[i], signatures[medoids[medoid]]);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = medoid;
                    }
                }

                if (labels[i] != best)
                {
                    labels[i] = best;
                    changed = true;
                }
            }

            for (var cluster = 0; cluster < k; cluster++)
            {
                var indexes = Enumerable.Range(0, signatures.Count)
                    .Where(index => labels[index] == cluster).ToArray();
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
        var silhouette = Silhouette(signatures, labels, sample: 350, seed);
        return new KMeans.Result(labels, [], 0, k, silhouette);
    }

    private static double Distance(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        var intersection = left.Count <= right.Count
            ? left.Count(right.Contains)
            : right.Count(left.Contains);
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : 1d - (intersection / (double)union);
    }

    private static double Silhouette(IReadOnlyList<IReadOnlySet<string>> signatures, int[] labels, int sample, int seed)
    {
        var random = new Random(seed);
        var indexes = Enumerable.Range(0, signatures.Count).OrderBy(_ => random.Next()).Take(sample).ToArray();
        var total = 0d;
        var count = 0;
        foreach (var i in indexes)
        {
            var own = labels[i];
            var ownSum = 0d;
            var ownCount = 0;
            var otherMin = double.PositiveInfinity;
            for (var j = 0; j < signatures.Count; j++)
            {
                if (i == j)
                    continue;
                var distance = Distance(signatures[i], signatures[j]);
                if (labels[j] == own)
                {
                    ownSum += distance;
                    ownCount++;
                }
                else if (distance < otherMin)
                    otherMin = distance;
            }

            if (ownCount == 0 || double.IsPositiveInfinity(otherMin))
                continue;
            var meanOwn = ownSum / ownCount;
            total += (otherMin - meanOwn) / System.Math.Max(otherMin, meanOwn);
            count++;
        }

        return count == 0 ? 0 : total / count;
    }
}
