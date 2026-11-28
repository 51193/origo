using System.Collections.Generic;
using System.IO;
using Origo.EvolutionDemo.Analysis.Batch;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Manifest-derived metric expression tests.</summary>
public sealed class BatchExpressionTests
{
    [Fact]
    public void Evaluate_ArithmeticAndMetricNames()
    {
        var metrics = new Dictionary<string, double>
        {
            ["population"] = 100,
            ["roots"] = 4,
            ["deaths"] = 30,
        };

        Assert.Equal(25d, BatchExpression.Evaluate("population / roots", metrics), 9);
        Assert.Equal(125d, BatchExpression.Evaluate("(population + 400) / roots", metrics), 9);
        Assert.Equal(-5d, BatchExpression.Evaluate("-population / 20", metrics), 9);
        Assert.Equal(30d, BatchExpression.Evaluate("deaths", metrics), 9);
    }

    [Fact]
    public void Evaluate_RejectsUnknownMetricAndDivisionByZero()
    {
        var metrics = new Dictionary<string, double> { ["a"] = 1 };

        Assert.Throws<InvalidDataException>(() => BatchExpression.Evaluate("missing + 1", metrics));
        Assert.Throws<InvalidDataException>(() => BatchExpression.Evaluate("a / 0", metrics));
        Assert.Throws<InvalidDataException>(() => BatchExpression.Evaluate("a +", metrics));
    }
}
