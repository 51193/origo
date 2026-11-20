using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Converts the compiled V4 energy budget into a per-second upkeep. The
///     budget already accounts for body tissue, neural tissue, and genome
///     complexity; the scale controls how strongly the demo charges it so the
///     simulation can trade complexity against metabolism.
/// </summary>
public static class EnergyBudgetMath
{
    /// <summary>Returns the upkeep per second for one expressed phenotype.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when a value is negative or not finite; the budget contract is
    ///     non-negative finite seconds^-1.
    /// </exception>
    public static float UpkeepPerSecond(float maintenancePerSecond, float scale)
    {
        if (!float.IsFinite(maintenancePerSecond) || maintenancePerSecond < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(maintenancePerSecond), "Phenotype maintenance must be a finite, non-negative value.");
        if (!float.IsFinite(scale) || scale < 0f)
            throw new ArgumentOutOfRangeException(nameof(scale), "Genome upkeep scale must be finite and non-negative.");
        return maintenancePerSecond * scale;
    }

    /// <summary>
    ///     Returns the extra movement upkeep per second while sprinting. The
    ///     surcharge is a fraction of the normal driven movement cost, so the
    ///     caller's per-distance cost grows with the sprint multiplier instead of
    ///     making sprinting a free distance increase.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when a value is negative or not finite.
    /// </exception>
    public static float SprintSurchargePerSecond(
        float moveEnergyPerSecond,
        float drive,
        bool sprinting,
        float surchargeFraction)
    {
        if (!float.IsFinite(moveEnergyPerSecond) || moveEnergyPerSecond < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(moveEnergyPerSecond), "Move energy must be a finite, non-negative value.");
        if (!float.IsFinite(drive) || drive < 0f)
            throw new ArgumentOutOfRangeException(nameof(drive), "Drive must be a finite, non-negative value.");
        if (!float.IsFinite(surchargeFraction) || surchargeFraction < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(surchargeFraction), "Sprint surcharge must be a finite, non-negative value.");
        return sprinting ? moveEnergyPerSecond * surchargeFraction * drive : 0f;
    }
}
