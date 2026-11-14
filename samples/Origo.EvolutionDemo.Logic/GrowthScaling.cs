using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Derives the cosmetic veteran growth of a creature from the trophies it
///     collected: spike kills and scavenged corpse meals. The growth is purely
///     presentational (body size and color tint) and is not inherited by
///     offspring, which keep their genome-derived appearance.
/// </summary>
public static class GrowthScaling
{
    private const float KillSizeStep = 0.045f;
    private const float MealSizeStep = 0.004f;
    private const float MaxSizeBonus = 0.75f;
    private const float KillTintStep = 0.06f;
    private const float MealTintStep = 0.006f;
    private const float MaxTint = 0.85f;

    /// <summary>
    ///     Returns the display size multiplier for a creature with the given
    ///     trophies. Zero trophies return exactly 1; the bonus is capped.
    /// </summary>
    /// <param name="kills">Successful spike kills.</param>
    /// <param name="scavengedMeals">Corpse meals the creature has eaten.</param>
    /// <returns>A multiplier in <c>[1, 1.75]</c>.</returns>
    public static float SizeMultiplier(int kills, int scavengedMeals) =>
        1f + MathF.Min(MaxSizeBonus, TrophyForce(kills, scavengedMeals, KillSizeStep, MealSizeStep));

    /// <summary>
    ///     Returns the body-color tint strength toward the veteran gold for a
    ///     creature with the given trophies; zero trophies return exactly 0.
    /// </summary>
    /// <param name="kills">Successful spike kills.</param>
    /// <param name="scavengedMeals">Corpse meals the creature has eaten.</param>
    /// <returns>A blend factor in <c>[0, 0.85]</c>.</returns>
    public static float TintStrength(int kills, int scavengedMeals) =>
        MathF.Min(MaxTint, TrophyForce(kills, scavengedMeals, KillTintStep, MealTintStep));

    private static float TrophyForce(int kills, int scavengedMeals, float killStep, float mealStep) =>
        (Math.Max(0, kills) * killStep) + (Math.Max(0, scavengedMeals) * mealStep);
}
