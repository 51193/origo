using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>One profiled simulation tick.</summary>
public sealed record TickProfile(
    int Tick,
    double SimTime,
    double WallMs,
    long AllocBytes,
    int Gc0,
    int Gc1,
    int Gc2,
    int Population,
    int Food,
    int Corpses,
    IReadOnlyDictionary<string, double> Phases);

/// <summary>Reads the JSONL profile emitted by SimulationProfiler.</summary>
public static class ProfileReader
{
    public static IReadOnlyList<TickProfile> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Profile not found.", path);
        var ticks = new List<TickProfile>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var phases = new Dictionary<string, double>(StringComparer.Ordinal);
            if (root.TryGetProperty("phases", out var phaseObject))
                foreach (var phase in phaseObject.EnumerateObject())
                    phases[phase.Name] = phase.Value.GetDouble();
            ticks.Add(new TickProfile(
                root.GetProperty("tick").GetInt32(),
                root.GetProperty("simTime").GetDouble(),
                root.GetProperty("wallMs").GetDouble(),
                root.GetProperty("allocBytes").GetInt64(),
                root.GetProperty("gc0").GetInt32(),
                root.GetProperty("gc1").GetInt32(),
                root.GetProperty("gc2").GetInt32(),
                root.GetProperty("population").GetInt32(),
                root.GetProperty("food").GetInt32(),
                root.GetProperty("corpses").GetInt32(),
                phases));
        }

        if (ticks.Count == 0)
            throw new InvalidDataException("Profile contains no samples.");
        return ticks;
    }
}
