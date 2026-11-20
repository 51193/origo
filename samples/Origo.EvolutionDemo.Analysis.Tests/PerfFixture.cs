using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Origo.EvolutionDemo.Analysis.Perf;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Builds synthetic schema-2 profile ticks and JSONL files for the perf tests.</summary>
internal static class PerfFixture
{
    /// <summary>Creates one sampled tick; phases are given as <c>(name, ms, allocBytes)</c> tuples.</summary>
    internal static TickProfile Tick(
        int tick,
        double simTime,
        double wallMs = 10d,
        long allocBytes = 1024L,
        int gc0 = 0,
        int gc1 = 0,
        int gc2 = 0,
        int population = 100,
        int food = 50,
        int corpses = 1,
        params (string Name, double Ms, long AllocBytes)[] phases) =>
        new(tick, simTime, wallMs, allocBytes, gc0, gc1, gc2, population, food, corpses,
            phases.ToDictionary(
                static phase => phase.Name,
                static phase => new PhaseSample(phase.Ms, phase.AllocBytes),
                StringComparer.Ordinal));

    /// <summary>Two top-level systems plus two subphases nested in the first system.</summary>
    internal static (string Name, double Ms, long AllocBytes)[] StandardPhases(
        double alphaMs = 12d,
        double betaMs = 3d,
        double alphaChildMs = 6d) =>
    [
        ("sys.alpha", alphaMs, 1024L),
        ("sys.beta", betaMs, 512L),
        ("sys.alpha.child", alphaChildMs, 256L),
        ("sys.alpha.other", 2d, 128L),
    ];

    /// <summary>Writes <paramref name="ticks" /> as schema-2 JSONL and returns the file path.</summary>
    internal static string WriteProfile(string directory, string fileName, IEnumerable<TickProfile> ticks)
    {
        var path = Path.Combine(directory, fileName);
        using var writer = new StreamWriter(path);
        foreach (var tick in ticks)
        {
            writer.WriteLine(JsonSerializer.Serialize(new
            {
                schema = 2,
                tick = tick.Tick,
                simTime = tick.SimTime,
                wallMs = tick.WallMs,
                allocBytes = tick.AllocBytes,
                gc0 = tick.Gc0,
                gc1 = tick.Gc1,
                gc2 = tick.Gc2,
                population = tick.Population,
                food = tick.Food,
                corpses = tick.Corpses,
                phases = tick.Phases.ToDictionary(
                    static phase => phase.Key,
                    static phase => new { ms = phase.Value.Ms, allocBytes = phase.Value.AllocBytes }),
            }));
        }

        return path;
    }

    /// <summary>100 samples sampled every 10 ticks, one gen-0 collection per sample, standard phases.</summary>
    internal static TickProfile[] SampledSeries(int sampleCount = 100, int tickInterval = 10)
    {
        var ticks = new TickProfile[sampleCount];
        for (var index = 0; index < sampleCount; index++)
            ticks[index] = Tick((index + 1) * tickInterval, 1d + index, gc0: 1, gc1: 2, phases: StandardPhases());
        return ticks;
    }

    /// <summary>
    ///     A two-segment profile: 11 smoke samples (wall 20 ms, simTime 1..11) then 100
    ///     benchmark samples (wall 10 ms, simTime 1..100). Both segments expose the same
    ///     phase keys and use a continuous 10-tick sampling interval.
    /// </summary>
    internal static TickProfile[] TwoSegmentProfile()
    {
        var smoke = Enumerable.Range(0, 11)
            .Select(index => Tick(10 + (index * 10), 1d + index, wallMs: 20d, phases: StandardPhases()))
            .ToArray();
        var benchmark = Enumerable.Range(0, 100)
            .Select(index => Tick(120 + (index * 10), 1d + index, wallMs: 10d, phases: StandardPhases()))
            .ToArray();
        return [.. smoke, .. benchmark];
    }
}
