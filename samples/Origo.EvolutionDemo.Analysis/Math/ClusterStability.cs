using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Math;

/// <summary>
///     True bootstrap stability of a fitted k-means model measured by the adjusted
///     Rand index (ARI). Each round draws <c>n</c> rows with replacement, fits
///     k-means centers on the resample, assigns every original row to its nearest
///     resampled center, and compares that bootstrap labeling with the reference
///     labeling. The result is the mean and population standard deviation over the
///     rounds. Other clustering methods have no k-means centers and report
///     <see langword="null" /> (n/a), never a fabricated zero.
/// </summary>
public static class ClusterStability
{
    /// <summary>Documented default bootstrap round count.</summary>
    public const int DefaultRounds = 5;

    /// <summary>Bootstrap ARI summary: mean, population standard deviation, and round count.</summary>
    public sealed record BootstrapResult(double Mean, double StandardDeviation, int Rounds);

    /// <summary>
    ///     Computes the bootstrap ARI of <paramref name="reference" />, or
    ///     <see langword="null" /> when the metric is not applicable (non-k-means
    ///     reference, k &lt;= 1, or no rounds requested).
    /// </summary>
    public static BootstrapResult? Bootstrap(
        FeatureMatrix matrix,
        KMeans.Result reference,
        int rounds,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.K <= 1 || rounds <= 0 || reference.K >= matrix.RowCount)
            return null;
        var random = new Random(seed);
        var scores = new double[rounds];
        for (var round = 0; round < rounds; round++)
        {
            var indexes = new int[matrix.RowCount];
            for (var index = 0; index < indexes.Length; index++)
                indexes[index] = random.Next(matrix.RowCount);
            var resample = new FeatureMatrix(
                matrix.Columns,
                [.. indexes.Select(index => matrix.RawRows[index])]);
            var fitted = KMeans.Fit(resample, reference.K, seed + (round * 131));
            var bootstrapLabels = KMeans.Assign(matrix, fitted.Centers);
            scores[round] = AdjustedRand(bootstrapLabels, reference.Labels);
        }

        var mean = scores.Average();
        var deviation = System.Math.Sqrt(scores.Sum(score => (score - mean) * (score - mean)) / rounds);
        return new BootstrapResult(mean, deviation, rounds);
    }

    /// <summary>
    ///     Adjusted Rand index between two labelings of the same rows. The index is
    ///     1 for identical partitions up to relabeling, 0 at chance level, and
    ///     symmetric in its arguments.
    /// </summary>
    /// <exception cref="ArgumentException">The labelings have different lengths.</exception>
    public static double AdjustedRand(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Count != right.Count)
            throw new ArgumentException("Both labelings must cover the same rows.", nameof(right));
        var n = left.Count;
        if (n < 2)
            return 1;
        var leftCounts = new Dictionary<int, int>();
        var rightCounts = new Dictionary<int, int>();
        var bothCounts = new Dictionary<(int Left, int Right), int>();
        for (var row = 0; row < n; row++)
        {
            var leftLabel = left[row];
            var rightLabel = right[row];
            leftCounts[leftLabel] = leftCounts.GetValueOrDefault(leftLabel) + 1;
            rightCounts[rightLabel] = rightCounts.GetValueOrDefault(rightLabel) + 1;
            var pair = (leftLabel, rightLabel);
            bothCounts[pair] = bothCounts.GetValueOrDefault(pair) + 1;
        }

        var sumBoth = bothCounts.Values.Sum(static count => ChooseTwo(count));
        var sumLeft = leftCounts.Values.Sum(static count => ChooseTwo(count));
        var sumRight = rightCounts.Values.Sum(static count => ChooseTwo(count));
        var expected = sumLeft * sumRight / ChooseTwo(n);
        var maximum = (sumLeft + sumRight) / 2d;
        return System.Math.Abs(maximum - expected) < 1e-12
            ? 1
            : (sumBoth - expected) / (maximum - expected);
    }

    private static double ChooseTwo(double value) => value * (value - 1) / 2d;
}
