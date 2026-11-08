using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>Food categories that create distinct digestive niches.</summary>
public enum FoodType
{
    /// <summary>Easily digested food, available to every body.</summary>
    Soft = 0,

    /// <summary>Energy-rich food that needs a hard-digest organ for full value.</summary>
    Tough = 1,

    /// <summary>Energy-rich food that needs toxin resistance for full value.</summary>
    Toxic = 2,
}

/// <summary>Pure digestion-yield rules shared by the simulation and tests.</summary>
public static class FoodMath
{
    /// <summary>The effective energy of a food item for one creature's organs.</summary>
    public static float EnergyFor(
        FoodType type,
        float softEnergy,
        float toughEnergy,
        float toxicEnergy,
        bool hardDigest,
        bool toxinResistance) => type switch
        {
            FoodType.Soft => MathF.Max(0f, softEnergy),
            FoodType.Tough => MathF.Max(0f, toughEnergy) * (hardDigest ? 1f : 0.40f),
            FoodType.Toxic => MathF.Max(0f, toxicEnergy) * (toxinResistance ? 1f : 0.25f),
            _ => 0f,
        };
}
