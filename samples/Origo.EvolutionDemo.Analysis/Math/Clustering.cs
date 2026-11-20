using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>
///     Result of standardizing a feature matrix: the z-scored matrix restricted to
///     the non-constant columns plus the names of the dropped constant columns.
/// </summary>
public sealed record StandardizationResult(
    FeatureMatrix Matrix,
    double[] Means,
    double[] Scales,
    IReadOnlyList<int> KeptColumns,
    IReadOnlyList<string> DroppedColumns);

/// <summary>
///     Standardizes columns with population z-scores. Non-finite input values are a
///     hard error; constant columns (zero scale) are dropped and named in
///     <see cref="StandardizationResult.DroppedColumns" /> so the report can show
///     the used/raw feature counts.
/// </summary>
public static class Standardizer
{
    /// <summary>Standardizes every non-constant column of <paramref name="source" />.</summary>
    /// <exception cref="InvalidDataException">A column contains a non-finite value.</exception>
    public static StandardizationResult Fit(FeatureMatrix source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var columnCount = source.Columns.Count;
        var means = new double[columnCount];
        var scales = new double[columnCount];
        var kept = new List<int>();
        var dropped = new List<string>();
        var rows = new double[source.RowCount][];
        for (var row = 0; row < source.RowCount; row++)
            rows[row] = new double[columnCount];
        for (var column = 0; column < columnCount; column++)
        {
            var name = source.Columns[column];
            var values = new double[source.RowCount];
            for (var row = 0; row < source.RowCount; row++)
            {
                var value = source.RawRows[row][column];
                if (!double.IsFinite(value))
                    throw new InvalidDataException(
                        $"Feature column '{name}' has a non-finite value '{value}' at row {row}.");
                values[row] = value;
            }

            var mean = values.Length == 0 ? 0 : values.Average();
            var variance = values.Length == 0
                ? 0
                : values.Sum(value => (value - mean) * (value - mean)) / values.Length;
            var std = System.Math.Sqrt(variance);
            if (!double.IsFinite(std) || std < 1e-12)
            {
                dropped.Add(name);
                continue;
            }

            means[column] = mean;
            scales[column] = std;
            kept.Add(column);
            for (var row = 0; row < values.Length; row++)
                rows[row][column] = (values[row] - mean) / std;
        }

        var matrix = new FeatureMatrix(
            [.. kept.Select(index => source.Columns[index])],
            [.. rows.Select(row => kept.Select(index => row[index]).ToArray())]);
        return new StandardizationResult(matrix, means, scales, kept, dropped);
    }
}

/// <summary>Principal components of the standardized covariance matrix.</summary>
/// <param name="Projection">Row projections onto the returned components.</param>
/// <param name="Components">Orthonormal eigenvectors, one per returned component.</param>
/// <param name="ExplainedVarianceRatios">Variance share of each returned component; the ratios sum to at most one.</param>
public sealed record PcaResult(
    double[][] Projection,
    IReadOnlyList<double[]> Components,
    IReadOnlyList<double> ExplainedVarianceRatios);

/// <summary>
///     Exact eigen-decomposition of the standardized covariance matrix (cyclic
///     Jacobi). Unlike power iteration it yields orthonormal components even when
///     the covariance is rank-deficient, and the explained-variance ratios are the
///     exact eigenvalue shares of the covariance trace.
/// </summary>
public static class Pca
{
    /// <summary>Computes the first <paramref name="components" /> principal components.</summary>
    public static PcaResult Analyze(FeatureMatrix standardized, int components)
    {
        ArgumentNullException.ThrowIfNull(standardized);
        ArgumentOutOfRangeException.ThrowIfNegative(components);
        var n = standardized.RowCount;
        var d = standardized.Columns.Count;
        var count = System.Math.Min(components, d);
        var projection = new double[n][];
        for (var row = 0; row < n; row++)
            projection[row] = new double[count];
        if (n == 0 || d == 0 || count == 0)
            return new PcaResult(projection, [], []);
        var covariance = new double[d, d];
        foreach (var row in standardized.RawRows)
            for (var i = 0; i < d; i++)
                for (var j = i; j < d; j++)
                    covariance[i, j] += row[i] * row[j];
        for (var i = 0; i < d; i++)
            for (var j = i; j < d; j++)
            {
                covariance[i, j] /= n;
                covariance[j, i] = covariance[i, j];
            }

        var trace = 0d;
        for (var i = 0; i < d; i++)
            trace += covariance[i, i];
        var (eigenvalues, eigenvectors) = JacobiEigen(covariance);
        var order = Enumerable.Range(0, d)
            .OrderByDescending(index => eigenvalues[index])
            .ThenBy(static index => index)
            .Take(count)
            .ToArray();
        var resultComponents = new List<double[]>(count);
        var ratios = new List<double>(count);
        foreach (var index in order)
        {
            var vector = new double[d];
            for (var i = 0; i < d; i++)
                vector[i] = eigenvectors[i, index];
            Orient(vector);
            resultComponents.Add(vector);
            var ratio = trace <= 0 ? 0 : eigenvalues[index] / trace;
            ratios.Add(System.Math.Clamp(ratio, 0d, 1d));
            for (var row = 0; row < n; row++)
                projection[row][resultComponents.Count - 1] =
                    Dot(standardized.RawRows[row], vector);
        }

        return new PcaResult(projection, resultComponents, ratios);
    }

    private static (double[] Eigenvalues, double[,] Eigenvectors) JacobiEigen(double[,] source)
    {
        var size = source.GetLength(0);
        var matrix = (double[,])source.Clone();
        var vectors = new double[size, size];
        for (var i = 0; i < size; i++)
            vectors[i, i] = 1;
        for (var sweep = 0; sweep < 100; sweep++)
        {
            var offDiagonal = 0d;
            for (var i = 0; i < size; i++)
                for (var j = i + 1; j < size; j++)
                    offDiagonal += matrix[i, j] * matrix[i, j];
            if (offDiagonal < 1e-24)
                break;
            for (var p = 0; p < size - 1; p++)
                for (var q = p + 1; q < size; q++)
                {
                    if (System.Math.Abs(matrix[p, q]) < 1e-18)
                        continue;
                    var angle = 0.5 * System.Math.Atan2(2 * matrix[p, q], matrix[q, q] - matrix[p, p]);
                    var cosine = System.Math.Cos(angle);
                    var sine = System.Math.Sin(angle);
                    for (var k = 0; k < size; k++)
                    {
                        var left = matrix[k, p];
                        var right = matrix[k, q];
                        matrix[k, p] = (cosine * left) - (sine * right);
                        matrix[k, q] = (sine * left) + (cosine * right);
                    }

                    for (var k = 0; k < size; k++)
                    {
                        var upper = matrix[p, k];
                        var lower = matrix[q, k];
                        matrix[p, k] = (cosine * upper) - (sine * lower);
                        matrix[q, k] = (sine * upper) + (cosine * lower);
                    }

                    for (var k = 0; k < size; k++)
                    {
                        var left = vectors[k, p];
                        var right = vectors[k, q];
                        vectors[k, p] = (cosine * left) - (sine * right);
                        vectors[k, q] = (sine * left) + (cosine * right);
                    }
                }
        }

        var eigenvalues = new double[size];
        for (var i = 0; i < size; i++)
            eigenvalues[i] = matrix[i, i];
        return (eigenvalues, vectors);
    }

    private static void Orient(double[] vector)
    {
        for (var i = 0; i < vector.Length; i++)
        {
            if (System.Math.Abs(vector[i]) < 1e-12)
                continue;
            if (vector[i] < 0)
                for (var j = 0; j < vector.Length; j++)
                    vector[j] = -vector[j];
            return;
        }
    }

    private static double Dot(double[] a, double[] b)
    {
        var sum = 0d;
        for (var i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }
}

/// <summary>One candidate solution of an auto-k search.</summary>
/// <param name="K">Requested cluster count of the candidate.</param>
/// <param name="Inertia">SSE to assigned centers; <see langword="null" /> when the method has no centers.</param>
/// <param name="Silhouette">Mean standard silhouette on the common deterministic sample.</param>
/// <param name="Sizes">Rows per cluster in label order.</param>
/// <param name="Flags">Deterministic flags: accepted/rejected, small-clusters, weak-silhouette, cap, selected.</param>
public sealed record ClusterCandidate(
    int K,
    double? Inertia,
    double? Silhouette,
    IReadOnlyList<int> Sizes,
    IReadOnlyList<string> Flags);

/// <summary>
///     Full auto-k selection: the chosen solution, every candidate that was
///     evaluated (never silently discarded), the search cap, and whether the
///     fallback (no validity-constrained candidate) was used.
/// </summary>
public sealed record ClusterSelection(
    KMeans.Result Best,
    IReadOnlyList<ClusterCandidate> Candidates,
    int MaxK,
    bool NoValidCandidate);

/// <summary>
///     Builds the full auto-k candidate table and applies the documented selection
///     rule shared by k-means and Jaccard k-medoids: choose the accepted candidate
///     with the maximum standard silhouette, fall back to the overall maximum when
///     no candidate satisfies the minimum-cluster-size floor, and never silently
///     return a single cluster.
/// </summary>
internal static class AutoSelection
{
    internal static ClusterSelection Build(IReadOnlyList<KMeans.Result> results, int maxK, int floor)
    {
        var candidates = new List<ClusterCandidate>(results.Count);
        foreach (var result in results)
        {
            var sizes = Sizes(result.Labels, result.K);
            var accepted = sizes.All(size => size >= floor);
            var flags = new List<string> { accepted ? "accepted" : "rejected" };
            if (!accepted)
                flags.Add("small-clusters");
            if (result.Silhouette is null || result.Silhouette < KMeans.WeakStructureThreshold)
                flags.Add("weak-silhouette");
            if (result.K == maxK)
                flags.Add("cap");
            candidates.Add(new ClusterCandidate(result.K, result.Inertia, result.Silhouette, sizes, flags));
        }

        var bestIndex = SelectBestIndex(candidates, requireAccepted: true);
        var noValidCandidate = bestIndex < 0;
        if (noValidCandidate)
            bestIndex = SelectBestIndex(candidates, requireAccepted: false);
        var selected = candidates[bestIndex];
        candidates[bestIndex] = selected with
        {
            Flags = [.. selected.Flags, "selected"],
        };
        return new ClusterSelection(results[bestIndex], candidates, maxK, noValidCandidate);
    }

    private static int[] Sizes(int[] labels, int k)
    {
        var sizes = new int[k];
        foreach (var label in labels)
            sizes[label]++;
        return sizes;
    }

    private static int SelectBestIndex(List<ClusterCandidate> candidates, bool requireAccepted)
    {
        var best = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (requireAccepted && !candidate.Flags.Contains("accepted", StringComparer.Ordinal))
                continue;
            if (best < 0)
            {
                best = index;
                continue;
            }

            var current = candidates[best];
            var bySilhouette = CompareNullable(candidate.Silhouette, current.Silhouette);
            if (bySilhouette > 0)
            {
                best = index;
                continue;
            }

            if (bySilhouette == 0)
            {
                var byInertia = CompareNullable(current.Inertia, candidate.Inertia);
                if (byInertia > 0 || (byInertia == 0 && candidate.K < current.K))
                    best = index;
            }
        }

        return best;
    }

    private static int CompareNullable(double? left, double? right)
    {
        if (left is null)
            return right is null ? 0 : -1;
        if (right is null)
            return 1;
        var delta = left.Value - right.Value;
        if (System.Math.Abs(delta) < 1e-12)
            return 0;
        return delta > 0 ? 1 : -1;
    }
}

/// <summary>Deterministic k-means++ with min-inertia restarts and standard silhouette.</summary>
public static class KMeans
{
    /// <summary>Documented restart budget used by both fixed-k and auto-k fits.</summary>
    public const int RestartCount = 10;

    /// <summary>Documented auto-k upper bound (also clipped to <c>n - 1</c>).</summary>
    public const int DefaultMaxK = 8;

    /// <summary>Below this silhouette the report prints a "no substantial structure" warning.</summary>
    public const double WeakStructureThreshold = 0.25;

    /// <summary>Cluster labels, centers, inertia, cluster count, and mean silhouette.</summary>
    public sealed record Result(int[] Labels, double[][] Centers, double? Inertia, int K, double? Silhouette);

    /// <summary>Documented auto-k minimum cluster size: <c>max(2, ceil(0.01 * n))</c>.</summary>
    public static int MinimumClusterSizeFor(int rowCount) =>
        System.Math.Max(2, (int)System.Math.Ceiling(rowCount * 0.01));

    /// <summary>Fits k-means and returns the chosen solution only.</summary>
    public static Result Fit(FeatureMatrix matrix, int requestedK, int seed = 1) =>
        Select(matrix, requestedK, seed).Best;

    /// <summary>Fits k-means, returning the chosen solution and the full candidate table for auto-k.</summary>
    public static ClusterSelection Select(FeatureMatrix matrix, int requestedK, int seed = 1, int minClusterSize = 0)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var n = matrix.RowCount;
        if (n == 0)
        {
            if (requestedK > 0)
                throw new ArgumentOutOfRangeException(
                    nameof(requestedK), $"--k {requestedK} exceeds the analyzable row count 0.");
            return new ClusterSelection(new Result([], [], null, 0, null), [], 0, false);
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
        {
            var fixedResult = Run(matrix, requestedK, seed + (requestedK * 7919));
            return new ClusterSelection(fixedResult, [], 0, false);
        }

        if (n < 3)
            throw new InvalidOperationException(
                $"Auto-k requires at least three rows; the matrix has {n}. Pass an explicit --k.");
        var maxK = System.Math.Min(DefaultMaxK, n - 1);
        var floor = minClusterSize == 0 ? MinimumClusterSizeFor(n) : minClusterSize;
        var results = new List<Result>(maxK - 1);
        for (var k = 2; k <= maxK; k++)
            results.Add(Run(matrix, k, seed + (k * 7919)));
        return AutoSelection.Build(results, maxK, floor);
    }

    /// <summary>Assigns every row of <paramref name="matrix" /> to the nearest of <paramref name="centers" />.</summary>
    public static int[] Assign(FeatureMatrix matrix, double[][] centers)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(centers);
        ArgumentOutOfRangeException.ThrowIfZero(centers.Length);
        var labels = new int[matrix.RowCount];
        for (var row = 0; row < matrix.RowCount; row++)
            labels[row] = Nearest(matrix.RawRows[row], centers);
        return labels;
    }

    private static Result Run(FeatureMatrix matrix, int k, int seed)
    {
        var n = matrix.RowCount;
        var dimension = matrix.Columns.Count;
        if (k == 1)
        {
            var center = new double[dimension];
            foreach (var row in matrix.RawRows)
                for (var column = 0; column < dimension; column++)
                    center[column] += row[column] / n;
            var labels = new int[n];
            var inertia = Inertia(matrix, labels, [center]);
            return new Result(labels, [center], inertia, 1, null);
        }

        var random = new Random(seed);
        double[][]? bestCenters = null;
        var bestLabels = new int[n];
        var bestInertia = double.PositiveInfinity;
        for (var restart = 0; restart < RestartCount; restart++)
        {
            var centers = Initialize(matrix, k, random);
            var labels = new int[n];
            var changed = true;
            var iterations = 0;
            while (changed && iterations++ < 100)
            {
                changed = false;
                for (var row = 0; row < n; row++)
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
            if (inertia < bestInertia)
            {
                bestInertia = inertia;
                bestCenters = centers;
                bestLabels = labels;
            }
        }

        var silhouette = Silhouette.Compute(
            n, bestLabels, k,
            (left, right) => System.Math.Sqrt(Squared(matrix.RawRows[left], matrix.RawRows[right])),
            Silhouette.EuclideanSampleSize);
        return new Result(bestLabels, bestCenters!, bestInertia, k, silhouette);
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
