using System;
using Origo.Core.Addons.FastNoiseLite;

namespace Origo.Core.Random;

/// <summary>
///     生成二维噪声图（行优先一维数组），用于应用层快速获取可复现地形噪声。
/// </summary>
public static class NoiseMapGenerator
{
    private const float SimplexWeight = 0.7f;
    private const float WorleyWeight = 0.3f;
    private const int DefaultSeed = 1337;
    private const float DefaultFrequency = 0.01f;

    /// <summary>
    ///     生成 Simplex + Worley(70/30) 混合噪声图，返回长度为 <c>size*size</c> 的行优先数组，值域为 <c>0..1</c>。
    /// </summary>
    public static float[] GenerateSimplexWorleyBlendMap(int size, int seed = DefaultSeed,
        float frequency = DefaultFrequency)
    {
        return GenerateSimplexWorleyBlendMap(size, seed, frequency, 1, 2f, 0.5f, 1f);
    }

    /// <summary>
    ///     生成 Simplex + Worley(70/30) 混合噪声图（扩展参数版本）。
    /// </summary>
    /// <param name="size">网格边长</param>
    /// <param name="seed">随机种子</param>
    /// <param name="frequency">噪声频率</param>
    /// <param name="octaves">分形叠加层数</param>
    /// <param name="lacunarity">分形间隙系数</param>
    /// <param name="gain">分形增益系数</param>
    /// <param name="worleyFrequencyMultiplier">Worley 噪声频率倍率（相对 simplex frequency）</param>
    public static float[] GenerateSimplexWorleyBlendMap(int size, int seed, float frequency,
        int octaves, float lacunarity, float gain, float worleyFrequencyMultiplier)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be greater than 0.");

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
            var mixed = simplexValue * SimplexWeight + worleyValue * WorleyWeight;
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
