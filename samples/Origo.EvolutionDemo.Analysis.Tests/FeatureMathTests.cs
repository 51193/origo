using System;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Math;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for standardization fail-fast, dropped columns, and exact PCA.</summary>
public sealed class FeatureMathTests
{
    [Fact]
    public void Standardizer_DropsConstantColumnsAndNamesThem()
    {
        var matrix = new FeatureMatrix(
            ["varying", "constant", "other"],
            [[1d, 5d, 0d], [2d, 5d, 10d], [3d, 5d, 20d]]);

        var result = Standardizer.Fit(matrix);

        Assert.Equal(["varying", "other"], result.Matrix.Columns);
        Assert.Equal(["constant"], result.DroppedColumns);
        Assert.Equal(3, result.Matrix.RowCount);
    }

    [Fact]
    public void Standardizer_StandardizesKeptColumns()
    {
        var matrix = new FeatureMatrix(["x"], [[1d], [2d], [3d], [4d]]);

        var result = Standardizer.Fit(matrix);

        var column = result.Matrix.RawRows.Select(row => row[0]).ToArray();
        Assert.Equal(0d, column.Average(), 12);
        Assert.Equal(1d, System.Math.Sqrt(column.Sum(value => value * value) / column.Length), 12);
    }

    [Fact]
    public void Standardizer_NonFiniteValue_Throws()
    {
        var matrix = new FeatureMatrix(["x", "y"], [[1d, 2d], [double.NaN, 3d], [4d, 5d]]);

        var exception = Assert.Throws<System.IO.InvalidDataException>(() => Standardizer.Fit(matrix));

        Assert.Contains("'x'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("row 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pca_ExplainedVarianceRatiosMatchKnownCovariance()
    {
        // Two anti-correlated standardized columns: covariance [[1,-1],[-1,1]] with
        // eigenvalues 2 and 0, so the explained-variance ratios are 1 and 0.
        var standardized = Standardizer.Fit(new FeatureMatrix(
            ["a", "b"],
            [[1d, 4d], [2d, 3d], [3d, 2d], [4d, 1d]])).Matrix;

        var result = Pca.Analyze(standardized, components: 2);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal(1d, result.ExplainedVarianceRatios[0], 9);
        Assert.Equal(0d, result.ExplainedVarianceRatios[1], 9);
        // The dominant component is the (1, -1) direction.
        Assert.Equal(1d / System.Math.Sqrt(2), System.Math.Abs(result.Components[0][0]), 9);
        Assert.Equal(-result.Components[0][0], result.Components[0][1], 12);
        AssertOrthonormal(result.Components);
        Assert.InRange(result.ExplainedVarianceRatios.Sum(), 0d, 1d + 1e-9);
    }

    [Fact]
    public void Pca_UncorrelatedColumnsHaveEqualVarianceShares()
    {
        var standardized = Standardizer.Fit(new FeatureMatrix(
            ["a", "b"],
            [[1d, 1d], [-1d, 1d], [1d, -1d], [-1d, -1d]])).Matrix;

        var result = Pca.Analyze(standardized, components: 2);

        Assert.Equal(0.5, result.ExplainedVarianceRatios[0], 9);
        Assert.Equal(0.5, result.ExplainedVarianceRatios[1], 9);
        AssertOrthonormal(result.Components);
    }

    [Fact]
    public void Pca_ComponentsAreEigenvectorsOfTheCovariance()
    {
        var matrix = new FeatureMatrix(
            ["a", "b", "c"],
            [[1d, 2d, 0d], [2d, 1d, 1d], [4d, 3d, 2d], [5d, 5d, 1d], [7d, 6d, 3d], [8d, 7d, 2d]]);
        var standardized = Standardizer.Fit(matrix).Matrix;
        var dimension = standardized.Columns.Count;
        var covariance = new double[dimension, dimension];
        foreach (var row in standardized.RawRows)
            for (var i = 0; i < dimension; i++)
                for (var j = 0; j < dimension; j++)
                    covariance[i, j] += row[i] * row[j] / standardized.RowCount;

        var trace = 0d;
        for (var i = 0; i < dimension; i++)
            trace += covariance[i, i];
        var result = Pca.Analyze(standardized, components: 3);

        for (var component = 0; component < result.Components.Count; component++)
        {
            var vector = result.Components[component];
            var product = new double[dimension];
            for (var i = 0; i < dimension; i++)
                for (var j = 0; j < dimension; j++)
                    product[i] += covariance[i, j] * vector[j];
            var eigenvalue = result.ExplainedVarianceRatios[component] * trace;
            for (var i = 0; i < dimension; i++)
                Assert.Equal(eigenvalue * vector[i], product[i], 9);
        }
    }

    [Fact]
    public void Pca_ProjectionRowsMatchComponentsAndRatiosStayWithinOne()
    {
        var matrix = TestMatrices.WellSeparated(3, 10);
        var standardized = Standardizer.Fit(matrix).Matrix;

        var result = Pca.Analyze(standardized, components: 3);
        var componentCount = result.Components.Count;

        Assert.Equal(matrix.RowCount, result.Projection.Length);
        Assert.Equal(2, componentCount);
        for (var row = 0; row < matrix.RowCount; row++)
            for (var component = 0; component < componentCount; component++)
            {
                var expected = standardized.RawRows[row]
                    .Zip(result.Components[component], static (value, weight) => value * weight)
                    .Sum();
                Assert.Equal(expected, result.Projection[row][component], 9);
            }

        Assert.InRange(result.ExplainedVarianceRatios.Sum(), 0d, 1d + 1e-9);
    }

    [Fact]
    public void Pca_EmptyMatrix_ReturnsEmptyResult()
    {
        var result = Pca.Analyze(new FeatureMatrix(["x", "y"], []), components: 2);

        Assert.Empty(result.Projection);
        Assert.Empty(result.Components);
        Assert.Empty(result.ExplainedVarianceRatios);
    }

    private static void AssertOrthonormal(System.Collections.Generic.IReadOnlyList<double[]> components)
    {
        for (var left = 0; left < components.Count; left++)
            for (var right = 0; right < components.Count; right++)
            {
                var dot = components[left].Zip(components[right], static (a, b) => a * b).Sum();
                Assert.Equal(left == right ? 1d : 0d, dot, 9);
            }
    }
}
