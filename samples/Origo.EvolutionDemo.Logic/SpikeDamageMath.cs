using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>Result of one spike hit against a creature's energy pool.</summary>
/// <param name="StolenEnergy">Energy transferred from the target to the shooter.</param>
/// <param name="RemainingEnergy">Target energy after the hit; zero means lethal.</param>
/// <param name="Lethal">True when the hit depleted the target's energy.</param>
public readonly record struct SpikeDamageResult(float StolenEnergy, float RemainingEnergy, bool Lethal);

/// <summary>
///     Pure spike damage rules. A hit removes a fraction of the target's maximum
///     energy pool and a target dies when its energy reaches zero. Only a lethal
///     hit transfers energy to the shooter, and only the configured steal
///     fraction of the target's remaining energy, so repeated non-lethal hits
///     cannot be used to farm energy. Armor scales damage down before the hit is
///     resolved.
/// </summary>
public static class SpikeDamageMath
{
    /// <summary>Resolves one spike hit against a target.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when an energy or fraction is negative or not finite, or when
    ///     the target has no positive energy pool.
    /// </exception>
    public static SpikeDamageResult Resolve(
        float currentEnergy,
        float maxEnergy,
        float damageFraction,
        bool armored,
        float armorDamageMultiplier,
        float killStealFraction)
    {
        if (!float.IsFinite(currentEnergy) || currentEnergy < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(currentEnergy), "Target energy must be a finite, non-negative value.");
        if (!float.IsFinite(maxEnergy) || maxEnergy <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(maxEnergy), "Target max energy must be a finite, positive value.");
        if (!float.IsFinite(damageFraction) || damageFraction is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(
                nameof(damageFraction), "Spike damage fraction must be finite and within [0, 1].");
        if (!float.IsFinite(armorDamageMultiplier) || armorDamageMultiplier is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(
                nameof(armorDamageMultiplier), "Armor damage multiplier must be finite and within [0, 1].");
        if (!float.IsFinite(killStealFraction) || killStealFraction is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(
                nameof(killStealFraction), "Kill steal fraction must be finite and within [0, 1].");

        var effectiveFraction = armored ? damageFraction * armorDamageMultiplier : damageFraction;
        var damage = maxEnergy * effectiveFraction;
        var remaining = MathF.Max(0f, currentEnergy - damage);
        var lethal = remaining <= 0f;
        var stolen = lethal ? currentEnergy * killStealFraction : 0f;
        return new SpikeDamageResult(stolen, remaining, lethal);
    }
}
