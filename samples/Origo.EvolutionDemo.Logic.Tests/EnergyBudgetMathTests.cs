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

    [Fact]
    public void SprintSurcharge_UsesConfiguredFractionOnlyWhileSprinting()
    {
        Assert.Equal(0.66f, EnergyBudgetMath.SprintSurchargePerSecond(1.1f, 0.5f, true, 1.2f), 5);
        Assert.Equal(0f, EnergyBudgetMath.SprintSurchargePerSecond(1.1f, 0.5f, false, 1.2f), 5);
        Assert.Equal(0f, EnergyBudgetMath.SprintSurchargePerSecond(1.1f, 0f, true, 1.2f), 5);
    }

    [Fact]
    public void SprintSurcharge_RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnergyBudgetMath.SprintSurchargePerSecond(-1f, 0.5f, true, 1.2f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnergyBudgetMath.SprintSurchargePerSecond(1.1f, float.NaN, true, 1.2f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnergyBudgetMath.SprintSurchargePerSecond(1.1f, 0.5f, true, -0.1f));
    }

    [Theory]
    [InlineData(0, 0.06f, 0f)]
    [InlineData(1, 0.06f, 0.06f)]
    [InlineData(5, 0.06f, 0.3f)]
    public void OrganUpkeep_MultipliesOrganCountByPerOrganCost(int organCount, float perOrgan, float expected)
    {
        Assert.Equal(expected, EnergyBudgetMath.OrganUpkeepPerSecond(organCount, perOrgan), 5);
    }

    [Fact]
    public void OrganUpkeep_RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyBudgetMath.OrganUpkeepPerSecond(-1, 0.06f));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyBudgetMath.OrganUpkeepPerSecond(3, -0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyBudgetMath.OrganUpkeepPerSecond(3, float.NaN));
    }
}
