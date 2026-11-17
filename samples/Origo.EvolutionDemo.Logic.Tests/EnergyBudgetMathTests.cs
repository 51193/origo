using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class EnergyBudgetMathTests
{
    [Theory]
    [InlineData(0f, 0.01f, 0f)]
    [InlineData(10f, 0.01f, 0.1f)]
    [InlineData(24.5f, 0.02f, 0.49f)]
    public void UpkeepPerSecond_MultipliesBudgetByScale(float budget, float scale, float expected)
    {
        Assert.Equal(expected, EnergyBudgetMath.UpkeepPerSecond(budget, scale), 5);
    }

    [Fact]
    public void UpkeepPerSecond_ZeroScaleDisablesUpkeep()
    {
        Assert.Equal(0f, EnergyBudgetMath.UpkeepPerSecond(120f, 0f));
    }

    [Fact]
    public void UpkeepPerSecond_RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyBudgetMath.UpkeepPerSecond(-1f, 0.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyBudgetMath.UpkeepPerSecond(float.NaN, 0.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyBudgetMath.UpkeepPerSecond(10f, -0.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyBudgetMath.UpkeepPerSecond(10f, float.PositiveInfinity));
    }
}
