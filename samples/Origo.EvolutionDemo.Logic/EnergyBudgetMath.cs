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
}
