using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>Numeric matrix with named columns and optional row labels.</summary>
public sealed class FeatureMatrix(IReadOnlyList<string> columns, IReadOnlyList<double[]> rows)
{
    public IReadOnlyList<string> Columns { get; } = columns;
    public IReadOnlyList<double[]> RawRows { get; } = rows;
    public int RowCount => RawRows.Count;

    public FeatureMatrix SelectColumns(IReadOnlySet<int> indexes)
    {
        var selected = indexes.OrderBy(static index => index).ToArray();
        return new FeatureMatrix(
            [.. selected.Select(index => Columns[index])],
            [.. RawRows.Select(row => selected.Select(index => row[index]).ToArray())]);
    }
}

/// <summary>Standardizes columns with median imputation and z-scores.</summary>
public static class Standardizer
{
    public static (FeatureMatrix Matrix, double[] Means, double[] Scales, int[] KeptColumns) Fit(
        FeatureMatrix source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var means = new double[source.Columns.Count];
        var scales = new double[source.Columns.Count];
        var kept = new List<int>();
        var rows = new double[source.RowCount][];
        for (var row = 0; row < source.RowCount; row++)
            rows[row] = new double[source.Columns.Count];
        for (var column = 0; column < source.Columns.Count; column++)
        {
            var values = source.RawRows.Select(row => row[column]).ToArray();
            var median = Median(values);
            var cleaned = values.Select(value => double.IsFinite(value) ? value : median).ToArray();
            var mean = cleaned.Length == 0 ? 0 : cleaned.Average();
            var variance = cleaned.Length == 0 ? 0 : cleaned.Sum(value => (value - mean) * (value - mean));
            var std = System.Math.Sqrt(variance / System.Math.Max(1, cleaned.Length));
            if (!double.IsFinite(std) || std < 1e-12)
                continue;
            means[column] = mean;
            scales[column] = std;
            kept.Add(column);
            for (var row = 0; row < source.RowCount; row++)
                rows[row][column] = (cleaned[row] - mean) / std;
        }

        var output = new FeatureMatrix(
            [.. source.Columns.Where((_, index) => kept.Contains(index))],
            [.. rows.Select(row => kept.Select(index => row[index]).ToArray())]);
        return (output, means, scales, kept.ToArray());
    }

    private static double Median(double[] values)
    {
        var finite = values.Where(double.IsFinite).OrderBy(static value => value).ToArray();
        if (finite.Length == 0)
            return 0;
        return finite.Length % 2 == 1
            ? finite[finite.Length / 2]
            : (finite[(finite.Length / 2) - 1] + finite[finite.Length / 2]) / 2d;
    }
}

/// <summary>Power-iteration PCA for the first few components.</summary>
public static class Pca
{
    public static double[][] Project(FeatureMatrix standardized, int components, int seed = 1)
    {
        ArgumentNullException.ThrowIfNull(standardized);
        var n = standardized.RowCount;
        var d = standardized.Columns.Count;
        var count = System.Math.Min(components, d);
        var result = new double[n][];
        for (var row = 0; row < n; row++)
            result[row] = new double[count];
        if (n == 0 || d == 0)
            return result;
        var covariance = new double[d, d];
        foreach (var row in standardized.RawRows)
            for (var i = 0; i < d; i++)
                for (var j = i; j < d; j++)
                    covariance[i, j] += row[i] * row[j];
        for (var i = 0; i < d; i++)
            for (var j = i; j < d; j++)
            {
                covariance[i, j] /= System.Math.Max(1, n);
                covariance[j, i] = covariance[i, j];
            }

        var random = new Random(seed);
        var vectors = new List<double[]>();
        for (var component = 0; component < count; component++)
        {
            var vector = Enumerable.Range(0, d).Select(_ => random.NextDouble() - 0.5).ToArray();
            for (var iteration = 0; iteration < 80; iteration++)
            {
                var next = new double[d];
                for (var i = 0; i < d; i++)
                    for (var j = 0; j < d; j++)
                        next[i] += covariance[i, j] * vector[j];
                foreach (var existing in vectors)
                {
                    var dot = Dot(next, existing);
                    for (var i = 0; i < d; i++)
                        next[i] -= dot * existing[i];
                }

                var norm = System.Math.Sqrt(Dot(next, next));
                if (norm < 1e-12)
                    break;
                for (var i = 0; i < d; i++)
                    next[i] /= norm;
                vector = next;
            }

            vectors.Add(vector);
            for (var row = 0; row < n; row++)
                result[row][component] = Dot(standardized.RawRows[row], vector);
        }

        return result;
    }

    private static double Dot(double[] a, double[] b)
    {
        var sum = 0d;
        for (var i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }
}

/// <summary>Deterministic k-means++ with restarts and silhouette selection.</summary>
public static class KMeans
{
    public sealed record Result(int[] Labels, double[][] Centers, double Inertia, int K, double Silhouette);

    public static Result Fit(FeatureMatrix matrix, int requestedK, int seed = 1)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var n = matrix.RowCount;
        if (n == 0)
            return new Result([], [], 0, 0, 0);
        if (requestedK == 1 || n < 3)
            return new Result(new int[n], [[.. Enumerable.Repeat(0d, matrix.Columns.Count)]], 0, 1, 0);
        var maxK = System.Math.Min(requestedK > 0 ? requestedK : 8, n - 1);
        var candidates = requestedK > 0 ? requestedK : 2;
        var best = (Result?)null;
        for (var k = 2; k <= maxK; k++)
        {
            var candidate = Run(matrix, k, seed + (k * 7919), restarts: requestedK > 0 ? 10 : 6);
            var minCluster = System.Math.Max(2, n / 100);
            var sizes = Enumerable.Range(0, candidate.K)
                .Select(cluster => candidate.Labels.Count(label => label == cluster))
                .ToArray();
            if (sizes.Any(size => size < minCluster))
                continue;
            if (best is null || candidate.Silhouette > best.Silhouette)
                best = candidate;
            if (requestedK > 0)
                break;
            if (candidates++ > 8)
                break;
        }

        return best ?? new Result(new int[n], [], 0, 1, 0);
    }

    private static Result Run(FeatureMatrix matrix, int k, int seed, int restarts)
    {
        var random = new Random(seed);
        Result? best = null;
        for (var restart = 0; restart < restarts; restart++)
        {
            var centers = Initialize(matrix, k, random);
            var labels = new int[matrix.RowCount];
            var changed = true;
            var iterations = 0;
            while (changed && iterations++ < 100)
            {
                changed = false;
                for (var row = 0; row < matrix.RowCount; row++)
                {
                    var label = Nearest(matrix.RawRows[row], centers);
                    if (labels[row] != label)
                    {
                        labels[row] = label;
                        changed = true;
                    }
                }

                Recompute(matrix, labels, centers);
            }

            var inertia = Inertia(matrix, labels, centers);
            var silhouette = Silhouette(matrix, labels, sample: 800, seed + restart);
            var candidate = new Result(labels, centers, inertia, k, silhouette);
            if (best is null || silhouette > best.Silhouette
                || (System.Math.Abs(silhouette - best.Silhouette) < 1e-9 && inertia < best.Inertia))
                best = candidate;
        }

        return best!;
    }

    private static double[][] Initialize(FeatureMatrix matrix, int k, Random random)
    {
        var centers = new List<double[]> { matrix.RawRows[random.Next(matrix.RowCount)].ToArray() };
        while (centers.Count < k)
        {
            var weights = matrix.RawRows.Select(row =>
                centers.Min(center => Squared(row, center))).ToArray();
            var total = weights.Sum();
            if (total <= 0)
            {
                centers.Add([.. matrix.RawRows[random.Next(matrix.RowCount)]]);
                continue;
            }

            var target = random.NextDouble() * total;
            var selected = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                target -= weights[i];
                if (target <= 0)
                {
                    selected = i;
                    break;
                }
            }

            centers.Add([.. matrix.RawRows[selected]]);
        }

        return [.. centers];
    }

    private static void Recompute(FeatureMatrix matrix, int[] labels, double[][] centers)
    {
        var sums = new double[centers.Length][];
        var counts = new int[centers.Length];
        for (var i = 0; i < centers.Length; i++)
            sums[i] = new double[matrix.Columns.Count];
        for (var row = 0; row < matrix.RowCount; row++)
        {
            var label = labels[row];
            counts[label]++;
            for (var column = 0; column < matrix.Columns.Count; column++)
                sums[label][column] += matrix.RawRows[row][column];
        }

        for (var i = 0; i < centers.Length; i++)
            if (counts[i] > 0)
                for (var column = 0; column < matrix.Columns.Count; column++)
                    centers[i][column] = sums[i][column] / counts[i];
    }

    private static double Silhouette(FeatureMatrix matrix, int[] labels, int sample, int seed)
    {
        var n = matrix.RowCount;
        var indexes = n <= sample
            ? Enumerable.Range(0, n).ToArray()
            : [.. Enumerable.Range(0, n).OrderBy(_ => new Random(seed).Next()).Take(sample)];
        var random = new Random(seed);
        if (n > sample)
            indexes = [.. Enumerable.Range(0, n).OrderBy(_ => random.Next()).Take(sample)];
        var total = 0d;
        var count = 0;
        foreach (var i in indexes)
        {
            var own = labels[i];
            var a = 0d;
            var aCount = 0;
            var other = double.PositiveInfinity;
            for (var j = 0; j < n; j++)
            {
                if (i == j)
                    continue;
                var distance = System.Math.Sqrt(Squared(matrix.RawRows[i], matrix.RawRows[j]));
                if (labels[j] == own)
                {
                    a += distance;
                    aCount++;
                }
                else if (distance < other)
                    other = distance;
            }

            if (aCount == 0 || double.IsPositiveInfinity(other))
                continue;
            var meanOwn = a / aCount;
            var score = (other - meanOwn) / System.Math.Max(other, meanOwn);
            total += score;
            count++;
        }

        return count == 0 ? 0 : total / count;
    }

    private static double Inertia(FeatureMatrix matrix, int[] labels, double[][] centers)
    {
        var total = 0d;
        for (var row = 0; row < matrix.RowCount; row++)
            total += Squared(matrix.RawRows[row], centers[labels[row]]);
        return total;
    }

    private static int Nearest(double[] row, double[][] centers)
    {
        var best = 0;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < centers.Length; i++)
        {
            var distance = Squared(row, centers[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
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
