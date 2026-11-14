using System;
using System.Numerics;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Deterministic food-patch centres and radially graded food sampling.
///     Food density falls smoothly from the patch centre to its edge. The default
///     wide radius and gentle falloff spread production across a broad foraging
///     area instead of building a dense core.
/// </summary>
public static class FoodPatchDistribution
{
    /// <summary>Position of a deterministic patch centre for the given pond seed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for non-positive pond radius or patch index.</exception>
    public static Vector2 Center(int seed, int index, int patchCount, float pondRadius)
    {
        if (pondRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pondRadius), "Pond radius must be positive.");
        if (patchCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(patchCount), "Patch count must be positive.");
        if (index < 0 || index >= patchCount)
            throw new ArgumentOutOfRangeException(nameof(index), "Patch index must be inside the patch count.");

        var hash = unchecked((seed * 73856093) ^ ((index + 1) * 19349663) ^ (patchCount * 83492791));
        var rng = new Random(hash);
        var angle = rng.NextDouble() * (Math.PI * 2d);
        var distance = Math.Sqrt(rng.NextDouble()) * (pondRadius * 0.82d);
        return new Vector2(
            (float)(Math.Cos(angle) * distance),
            (float)(Math.Sin(angle) * distance));
    }

    /// <summary>
    ///     Samples a food position inside a patch using an inverse-CDF radial
    ///     distribution. <paramref name="falloff" /> is the exponent of
    ///     <c>(1 - r/R)^falloff</c>; larger values concentrate food nearer the
    ///     centre. A value of zero gives a uniform-area disk.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid geometry.</exception>
    public static Vector2 Sample(
        Random random,
        int seed,
        int index,
        int patchCount,
        float pondRadius,
        float patchRadius,
        float falloff)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (patchRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(patchRadius), "Patch radius must be positive.");
        if (falloff < 0f)
            throw new ArgumentOutOfRangeException(nameof(falloff), "Falloff must not be negative.");

        var center = Center(seed, index, patchCount, pondRadius);
        return SampleAt(random, center, patchRadius, falloff);
    }

    /// <summary>Samples a position around an arbitrary moving patch centre.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid geometry.</exception>
    public static Vector2 SampleAt(Random random, Vector2 center, float patchRadius, float falloff)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (patchRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(patchRadius), "Patch radius must be positive.");
        if (falloff < 0f)
            throw new ArgumentOutOfRangeException(nameof(falloff), "Falloff must not be negative.");

        var angle = random.NextDouble() * (Math.PI * 2d);
        var distance = SampleRadius(random, patchRadius, falloff);
        return center + new Vector2(
            (float)(Math.Cos(angle) * distance),
            (float)(Math.Sin(angle) * distance));
    }

    /// <summary>Samples a radial distance from a graded distribution.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid geometry.</exception>
    public static float SampleRadius(Random random, float patchRadius, float falloff)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (patchRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(patchRadius), "Patch radius must be positive.");
        if (falloff < 0f)
            throw new ArgumentOutOfRangeException(nameof(falloff), "Falloff must not be negative.");

        var uniform = random.NextDouble();
        if (falloff <= 0.0001f)
            return (float)(Math.Sqrt(uniform) * patchRadius);

        var exponent = 1d / (falloff + 1d);
        return (float)(patchRadius * (1d - Math.Pow(1d - uniform, exponent)));
    }
}
