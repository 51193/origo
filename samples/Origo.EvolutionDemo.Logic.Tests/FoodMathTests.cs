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
}
