using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Analysis.Math;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Deterministic synthetic feature matrices shared by the regression tests.</summary>
internal static class TestMatrices
{
    /// <summary>Builds a two-dimensional <c>[x, y]</c> feature matrix.</summary>
    internal static FeatureMatrix TwoColumns(IReadOnlyList<double[]> rows) => new(["x", "y"], rows);

    /// <summary>
    ///     Builds <paramref name="clusterCount" /> visually separated groups of
    ///     <paramref name="pointsPerCluster" /> rows each. The small deterministic
    ///     offset keeps rows inside a group distinct without blurring the groups.
    /// </summary>
    internal static FeatureMatrix WellSeparated(int clusterCount, int pointsPerCluster)
    {
        var rows = new List<double[]>(clusterCount * pointsPerCluster);
        for (var cluster = 0; cluster < clusterCount; cluster++)
        {
            for (var point = 0; point < pointsPerCluster; point++)
            {
                var offset = (point % 7) * 0.05d;
                rows.Add([(100d * cluster) + offset, offset]);
            }
        }

        return TwoColumns(rows);
    }

    /// <summary>Computes the sum of squared distances to the assigned cluster centers.</summary>
    internal static double Inertia(FeatureMatrix matrix, int[] labels, double[][] centers)
    {
        var total = 0d;
        for (var row = 0; row < matrix.RowCount; row++)
        {
            var center = centers[labels[row]];
            var sum = 0d;
            for (var column = 0; column < matrix.Columns.Count; column++)
            {
                var delta = matrix.RawRows[row][column] - center[column];
                sum += delta * delta;
            }

            total += sum;
        }

        return total;
    }
}
