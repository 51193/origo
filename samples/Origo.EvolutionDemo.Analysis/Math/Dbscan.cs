using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>Density-based clustering for outlier detection on standardized features.</summary>
public static class Dbscan
{
    public static KMeans.Result Fit(FeatureMatrix matrix, double eps, int minPoints, int seed = 1)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (matrix.RowCount == 0)
            return new KMeans.Result([], [], 0, 0, 0);
        if (eps <= 0)
            eps = EstimateEps(matrix, minPoints);
        var n = matrix.RowCount;
        var labels = Enumerable.Repeat(-1, n).ToArray();
        var visited = new bool[n];
        var cluster = 0;
        for (var point = 0; point < n; point++)
        {
            if (visited[point])
                continue;
            visited[point] = true;
            var neighbors = Neighbors(matrix, point, eps);
            if (neighbors.Count < minPoints)
                continue;
            Expand(matrix, point, neighbors, labels, visited, cluster, eps, minPoints);
            cluster++;
        }

        // Map noise (-1) to its own label after real clusters so report code sees it.
        var noise = n > 0 ? cluster : 0;
        var hasNoise = labels.Any(static label => label < 0);
        if (hasNoise)
            for (var i = 0; i < n; i++)
                if (labels[i] < 0)
                    labels[i] = noise;
        var count = hasNoise ? cluster + 1 : cluster;
        if (count == 0)
            count = 1;
        var silhouette = cluster <= 1 ? 0 : ApproxSilhouette(matrix, labels, cluster);
        return new KMeans.Result(labels, [], 0, count, silhouette);
    }

    private static void Expand(
        FeatureMatrix matrix,
        int point,
        List<int> neighbors,
        int[] labels,
        bool[] visited,
        int cluster,
        double eps,
        int minPoints)
    {
        labels[point] = cluster;
        var queue = new Queue<int>(neighbors);
        var queued = new HashSet<int>(neighbors);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited[current])
            {
                visited[current] = true;
                var currentNeighbors = Neighbors(matrix, current, eps);
                if (currentNeighbors.Count >= minPoints)
                    foreach (var candidate in currentNeighbors)
                        if (queued.Add(candidate))
                            queue.Enqueue(candidate);
            }

            if (labels[current] < 0)
                labels[current] = cluster;
        }
    }

    private static List<int> Neighbors(FeatureMatrix matrix, int point, double eps)
    {
        var result = new List<int>();
        for (var other = 0; other < matrix.RowCount; other++)
            if (other != point && Squared(matrix.RawRows[point], matrix.RawRows[other]) <= eps * eps)
                result.Add(other);
        return result;
    }

    private static double EstimateEps(FeatureMatrix matrix, int minPoints)
    {
        var distances = new List<double>();
        foreach (var row in matrix.RawRows)
        {
            var nearest = matrix.RawRows
                .Where(candidate => !ReferenceEquals(candidate, row))
                .Select(candidate => System.Math.Sqrt(Squared(row, candidate)))
                .OrderBy(static value => value)
                .Take(System.Math.Max(1, minPoints))
                .DefaultIfEmpty(0)
                .Last();
            distances.Add(nearest);
        }
        distances.Sort();
        return distances.Count == 0 ? 0.5 : distances[distances.Count / 2];
    }

    private static double ApproxSilhouette(FeatureMatrix matrix, int[] labels, int clusterCount)
    {
        var sample = Enumerable.Range(0, matrix.RowCount)
            .Where(index => labels[index] < clusterCount)
            .Take(300).ToArray();
        var total = 0d;
        var count = 0;
        foreach (var i in sample)
        {
            var own = labels[i];
            var ownSum = 0d;
            var ownCount = 0;
            var otherMin = double.PositiveInfinity;
            for (var j = 0; j < matrix.RowCount; j++)
            {
                if (i == j || labels[j] >= clusterCount)
                    continue;
                var distance = System.Math.Sqrt(Squared(matrix.RawRows[i], matrix.RawRows[j]));
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
            var a = ownSum / ownCount;
            total += (otherMin - a) / System.Math.Max(otherMin, a);
            count++;
        }
        return count == 0 ? 0 : total / count;
    }

    private static double Squared(double[] a, double[] b)
    {
        var sum = 0d;
        for (var i = 0; i < a.Length; i++)
        {
            var delta = a[i] - b[i];
            sum += delta * delta;
        }
        return sum;
    }
}
