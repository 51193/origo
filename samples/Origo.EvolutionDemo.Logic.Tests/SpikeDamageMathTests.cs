using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class SpikeDamageMathTests
{
    [Fact]
    public void UnarmoredNonLethalHit_DoesNotStealEnergy()
    {
        var result = SpikeDamageMath.Resolve(200f, 200f, 0.5f, armored: false, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);

        Assert.Equal(0f, result.StolenEnergy, 5);
        Assert.Equal(100f, result.RemainingEnergy, 5);
        Assert.False(result.Lethal);
    }

    [Fact]
    public void Armor_ScalesDamageDown()
    {
        var result = SpikeDamageMath.Resolve(200f, 200f, 0.5f, armored: true, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);

        Assert.Equal(0f, result.StolenEnergy, 5);
        Assert.Equal(150f, result.RemainingEnergy, 5);
        Assert.False(result.Lethal);
    }

    [Fact]
    public void LethalHit_StealsConfiguredFractionOfRemainingEnergy()
    {
        var result = SpikeDamageMath.Resolve(40f, 200f, 0.5f, armored: false, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);

        Assert.Equal(20f, result.StolenEnergy, 5);
        Assert.Equal(0f, result.RemainingEnergy, 5);
        Assert.True(result.Lethal);
    }

    [Fact]
    public void ArmorCanPreventLethalHit()
    {
        var unarmored = SpikeDamageMath.Resolve(60f, 200f, 0.5f, armored: false, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);
        var armored = SpikeDamageMath.Resolve(60f, 200f, 0.5f, armored: true, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);

        Assert.True(unarmored.Lethal);
        Assert.False(armored.Lethal);
        Assert.Equal(10f, armored.RemainingEnergy, 5);
    }

    [Fact]
    public void ZeroDamage_LeavesTargetAlive()
    {
        var result = SpikeDamageMath.Resolve(10f, 200f, 0f, armored: false, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);

        Assert.Equal(0f, result.StolenEnergy);
        Assert.Equal(10f, result.RemainingEnergy);
        Assert.False(result.Lethal);
    }

    [Fact]
    public void SequentialHits_UseRemainingEnergySoDamageCannotBeUndone()
    {
        var first = SpikeDamageMath.Resolve(200f, 200f, 0.5f, armored: false, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);
        var second = SpikeDamageMath.Resolve(first.RemainingEnergy, 200f, 0.5f, armored: false, armorDamageMultiplier: 0.5f, killStealFraction: 0.5f);

        Assert.Equal(100f, first.RemainingEnergy, 5);
        Assert.Equal(0f, second.RemainingEnergy, 5);
        Assert.True(second.Lethal);
        Assert.Equal(50f, second.StolenEnergy, 5);
    }

    [Fact]
    public void Resolve_RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpikeDamageMath.Resolve(-1f, 200f, 0.5f, false, 0.5f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpikeDamageMath.Resolve(10f, 0f, 0.5f, false, 0.5f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpikeDamageMath.Resolve(10f, 200f, 1.5f, false, 0.5f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpikeDamageMath.Resolve(10f, 200f, float.NaN, false, 0.5f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpikeDamageMath.Resolve(10f, 200f, 0.5f, true, -0.1f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpikeDamageMath.Resolve(10f, 200f, 0.5f, false, 0.5f, -0.1f));
    }
}
