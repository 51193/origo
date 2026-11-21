using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class FoodMathTests
{
    [Fact]
    public void SoftFood_IsFullValueForAllBodies()
    {
        Assert.Equal(18f, FoodMath.EnergyFor(FoodType.Soft, 18f, 25f, 32f, false, false));
        Assert.Equal(18f, FoodMath.EnergyFor(FoodType.Soft, 18f, 25f, 32f, true, true));
    }

    [Fact]
    public void ToughFood_RewardsHardDigest()
    {
        Assert.Equal(10f, FoodMath.EnergyFor(FoodType.Tough, 18f, 25f, 32f, false, false));
        Assert.Equal(25f, FoodMath.EnergyFor(FoodType.Tough, 18f, 25f, 32f, true, false));
    }

    [Fact]
    public void ToxicFood_RewardsToxinResistance()
    {
        Assert.Equal(8f, FoodMath.EnergyFor(FoodType.Toxic, 18f, 25f, 32f, false, false));
        Assert.Equal(32f, FoodMath.EnergyFor(FoodType.Toxic, 18f, 25f, 32f, false, true));
    }

    [Fact]
    public void NegativeConfiguredEnergy_IsClampedToZero()
    {
        Assert.Equal(0f, FoodMath.EnergyFor(FoodType.Soft, -5f, -5f, -5f, true, true));
    }

    [Fact]
    public void FoodChoice_PrefersHigherEnergyThenNearerThenLowerIndex()
    {
        Assert.True(FoodMath.IsBetterChoice(25f, 18f, 9f, 1f, 2, 1));
        Assert.False(FoodMath.IsBetterChoice(18f, 25f, 1f, 9f, 2, 1));
        Assert.True(FoodMath.IsBetterChoice(18f, 18f, 4f, 9f, 2, 1));
        Assert.False(FoodMath.IsBetterChoice(18f, 18f, 9f, 9f, 2, 1));
        Assert.True(FoodMath.IsBetterChoice(18f, 18f, 9f, 9f, 1, 2));
    }
}
