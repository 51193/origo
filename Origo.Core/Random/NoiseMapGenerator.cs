using System;
using Origo.Core.Addons.FastNoiseLite;

namespace Origo.Core.Random;

/// <summary>
///     Generates 2D noise maps (row-major 1D array) for fast reproducible terrain noise
///     in application layers.
/// </summary>
public static class NoiseMapGenerator
{
    private const float _simplexWeight = 0.7f;
    private const float _worleyWeight = 0.3f;
    private const int _defaultSeed = 1337;
    private const float _defaultFrequency = 0.01f;

    /// <summary>
    ///     Generates a Simplex + Worley (70/30) blended noise map, returning a row-major array
    ///     of length <c>size*size</c> with values in <c>0..1</c>.
    /// </summary>
    public static float[] GenerateSimplexWorleyBlendMap(int size, int seed = _defaultSeed,
        float frequency = _defaultFrequency) => GenerateSimplexWorleyBlendMap(size, seed, frequency, 1, 2f, 0.5f, 1f);

    /// <summary>
    ///     Generates a Simplex + Worley (70/30) blended noise map (extended parameter version).
    /// </summary>
    /// <param name="size">Grid side length</param>
    /// <param name="seed">Random seed</param>
    /// <param name="frequency">Noise frequency</param>
    /// <param name="octaves">Number of fractal octaves</param>
    /// <param name="lacunarity">Fractal lacunarity</param>
    /// <param name="gain">Fractal gain</param>
    /// <param name="worleyFrequencyMultiplier">Worley noise frequency multiplier (relative to simplex frequency)</param>
    public static float[] GenerateSimplexWorleyBlendMap(int size, int seed, float frequency,
        int octaves, float lacunarity, float gain, float worleyFrequencyMultiplier)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be greater than 0.");
        if (octaves <= 0) throw new ArgumentOutOfRangeException(nameof(octaves), octaves, "Octaves must be greater than 0.");
        if (!float.IsFinite(lacunarity) || lacunarity <= 1f)
            throw new ArgumentOutOfRangeException(nameof(lacunarity), lacunarity, "Lacunarity must be a finite number greater than 1.");
        if (!float.IsFinite(gain) || gain <= 0f)
            throw new ArgumentOutOfRangeException(nameof(gain), gain, "Gain must be a finite number greater than 0.");
        if (!float.IsFinite(frequency) || frequency <= 0f)
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Frequency must be a finite number greater than 0.");
        if (!float.IsFinite(worleyFrequencyMultiplier) || worleyFrequencyMultiplier <= 0f)
            throw new ArgumentOutOfRangeException(nameof(worleyFrequencyMultiplier), worleyFrequencyMultiplier,
                "Worley frequency multiplier must be a finite number greater than 0.");

        var simplex = CreateSimplexNoise(seed, frequency);
        simplex.SetFractalType(FastNoiseLite.FractalType.FBm);
        simplex.SetFractalOctaves(octaves);
        simplex.SetFractalLacunarity(lacunarity);
        simplex.SetFractalGain(gain);

        var worley = CreateWorleyNoise(seed, frequency * worleyFrequencyMultiplier);
        var map = new float[size * size];

        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var simplexValue = simplex.GetNoise(x, y);
                var worleyValue = worley.GetNoise(x, y);
                var mixed = simplexValue * _simplexWeight + worleyValue * _worleyWeight;
                map[y * size + x] = NormalizeToZeroOne(mixed);
            }

        return map;
    }

    private static FastNoiseLite CreateSimplexNoise(int seed, float frequency)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        return noise;
    }

    private static FastNoiseLite CreateWorleyNoise(int seed, float frequency)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
        noise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
        noise.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
        noise.SetFrequency(frequency);
        return noise;
    }

    private static float NormalizeToZeroOne(float value)
    {
        var normalized = (value + 1f) * 0.5f;
        return Math.Clamp(normalized, 0f, 1f);
    }
}
