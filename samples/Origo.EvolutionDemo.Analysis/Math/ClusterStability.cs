using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>Bootstrap stability of k-means labels measured by adjusted Rand index.</summary>
public static class ClusterStability
{
    public static double Bootstrap(FeatureMatrix matrix, int k, int rounds, int seed)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (k <= 1 || matrix.RowCount < 6 || rounds <= 0)
            return 0;
        var random = new Random(seed);
        var scores = new List<double>();
        for (var round = 0; round < rounds; round++)
        {
            var indexes = Enumerable.Range(0, matrix.RowCount)
                .OrderBy(_ => random.Next()).Take((int)(matrix.RowCount * 0.8)).ToArray();
            var left = Sample(matrix, indexes);
            var right = Sample(matrix, indexes.Reverse().ToArray());
            var leftLabels = KMeans.Fit(left, k, seed + round * 2).Labels;
            var rightLabels = KMeans.Fit(right, k, seed + round * 2 + 1).Labels;
            // Both samples contain the same rows in reversed order.
            var rightByOriginal = indexes.Select((original, index) => (original, label: rightLabels[index]))
                .ToDictionary(static pair => pair.original, static pair => pair.label);
            scores.Add(AdjustedRand(indexes.Select((original, index) => (leftLabels[index], rightByOriginal[original])).ToArray()));
        }

        return scores.Average();
    }

    private static FeatureMatrix Sample(FeatureMatrix matrix, int[] indexes) =>
        new(matrix.Columns, indexes.Select(index => matrix.RawRows[index]).ToArray());

    private static double AdjustedRand((int Left, int Right)[] pairs)
    {
        var n = pairs.Length;
        if (n < 2)
            return 0;
        var left = pairs.GroupBy(static pair => pair.Left).ToDictionary(static group => group.Key, static group => group.Count());
        var right = pairs.GroupBy(static pair => pair.Right).ToDictionary(static group => group.Key, static group => group.Count());
        var both = pairs.GroupBy(static pair => pair).ToDictionary(static group => group.Key, static group => group.Count());
        double Choose2(double value) => value * (value - 1) / 2d;
        var sumBoth = both.Values.Sum(count => Choose2(count));
        var sumLeft = left.Values.Sum(count => Choose2(count));
        var sumRight = right.Values.Sum(count => Choose2(count));
        var expected = sumLeft * sumRight / Choose2(n);
        var max = (sumLeft + sumRight) / 2d;
        return System.Math.Abs(max - expected) < 1e-12 ? 1 : (sumBoth - expected) / (max - expected);
    }
}
