using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic;

/// <summary>Genome-level diversity metrics used by the dashboard.</summary>
public readonly record struct DiversityMetrics(
    int Population,
    int UniqueGenomes,
    float MeanPairwiseDistance,
    float MeanAlleleEntropy);

/// <summary>
///     Length-agnostic genetic diversity statistics. Pairwise distance is the
///     normalized Hamming distance between sampled genomes; allele entropy is
///     the mean per-position Shannon entropy of characters.
/// </summary>
public static class GenomeDiversity
{
    private const int SampleLimit = 24;

    /// <summary>Analyzes a collection of raw genomes.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="genomes" /> is null.</exception>
    public static DiversityMetrics Analyze(IReadOnlyList<string> genomes)
    {
        ArgumentNullException.ThrowIfNull(genomes);
        var count = genomes.Count;
        if (count == 0)
            return new DiversityMetrics(0, 0, 0f, 0f);

        var unique = new HashSet<string>(StringComparer.Ordinal);
        var maxLength = 0;
        foreach (var genome in genomes)
        {
            if (genome is null)
                continue;
            unique.Add(genome);
            maxLength = Math.Max(maxLength, genome.Length);
        }

        if (maxLength == 0)
            return new DiversityMetrics(count, 0, 0f, 0f);

        var step = Math.Max(1, count / SampleLimit);
        var sample = new List<string>(SampleLimit);
        for (var i = 0; i < count && sample.Count < SampleLimit; i += step)
            if (genomes[i] is { } genome)
                sample.Add(genome);

        var distanceSum = 0d;
        var pairCount = 0;
        for (var i = 0; i < sample.Count; i++)
        {
            for (var j = i + 1; j < sample.Count; j++)
            {
                distanceSum += Hamming(sample[i], sample[j], maxLength);
                pairCount++;
            }
        }

        var entropySum = 0d;
        Span<int> counts = stackalloc int[16];
        for (var position = 0; position < maxLength; position++)
        {
            counts.Clear();
            var total = 0;
            foreach (var genome in sample)
            {
                if (position >= genome.Length)
                    continue;
                counts[Nibble(genome[position])]++;
                total++;
            }

            if (total == 0)
                continue;
            var entropy = 0d;
            for (var k = 0; k < counts.Length; k++)
            {
                if (counts[k] == 0)
                    continue;
                var p = counts[k] / (double)total;
                entropy -= p * Math.Log2(p);
            }

            entropySum += entropy / 4d;
        }

        return new DiversityMetrics(
            count,
            unique.Count,
            pairCount > 0 ? (float)(distanceSum / pairCount) : 0f,
            maxLength > 0 ? (float)(entropySum / maxLength) : 0f);
    }

    private static double Hamming(string a, string b, int maxLength)
    {
        var differences = 0;
        for (var i = 0; i < maxLength; i++)
        {
            var left = i < a.Length ? char.ToUpperInvariant(a[i]) : '\0';
            var right = i < b.Length ? char.ToUpperInvariant(b[i]) : '\u0001';
            if (left != right)
                differences++;
        }

        return (double)differences / maxLength;
    }

    private static int Nibble(char character)
    {
        var upper = char.ToUpperInvariant(character);
        if (upper is >= '0' and <= '9')
            return upper - '0';
        if (upper is >= 'A' and <= 'F')
            return upper - 'A' + 10;
        return upper % 16;
    }
}
