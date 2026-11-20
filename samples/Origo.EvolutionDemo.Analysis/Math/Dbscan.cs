using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>
///     Density-based clustering (DBSCAN) for outlier detection on standardized
///     features. Noise rows keep label <c>-1</c> and are excluded from the
///     reported cluster count and from the silhouette. The core-point test counts
///     the point itself, so <c>minPts = 3</c> means three points including the
///     query point within <c>eps</c>.
/// </summary>
public static class Dbscan
{
    /// <summary>
    ///     Fits DBSCAN. Labels are real cluster ids starting at zero; noise rows
    ///     keep <c>-1</c>. <paramref name="eps" /> = 0 estimates epsilon from the
    ///     median distance to the <c>minPts - 1</c>-th nearest other row (the
    ///     k-distance heuristic with the point itself counted).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Epsilon is negative or minPts is below one.</exception>
    public static KMeans.Result Fit(FeatureMatrix matrix, double eps, int minPoints)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (eps < 0)
            throw new ArgumentOutOfRangeException(nameof(eps), "DBSCAN epsilon must not be negative.");
        if (minPoints < 1)
            throw new ArgumentOutOfRangeException(nameof(minPoints), "DBSCAN minimum points must be positive.");
        if (matrix.RowCount == 0)
            return new KMeans.Result([], [], null, 0, null);
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
            // minPts includes the point itself.
            if (neighbors.Count + 1 < minPoints)
                continue;
            Expand(matrix, point, neighbors, labels, visited, cluster, eps, minPoints);
            cluster++;
        }

        var silhouette = Silhouette.Compute(
            n, labels, cluster,
            (left, right) => System.Math.Sqrt(Squared(matrix.RawRows[left], matrix.RawRows[right])),
            Silhouette.EuclideanSampleSize);
        return new KMeans.Result(labels, [], null, cluster, silhouette);
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
                if (currentNeighbors.Count + 1 >= minPoints)
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
        // The point itself counts towards minPts, so the k-distance heuristic uses
        // the (minPts - 1)-th nearest *other* row.
        var rank = System.Math.Max(1, minPoints - 1);
        var distances = new List<double>(matrix.RowCount);
        foreach (var row in matrix.RawRows)
        {
            var nearest = matrix.RawRows
                .Where(candidate => !ReferenceEquals(candidate, row))
                .Select(candidate => System.Math.Sqrt(Squared(row, candidate)))
                .OrderBy(static value => value)
                .Take(rank)
                .DefaultIfEmpty(0)
                .Last();
            distances.Add(nearest);
        }

        distances.Sort();
        return distances.Count == 0 ? 0.5 : distances[distances.Count / 2];
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
