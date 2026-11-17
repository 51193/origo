using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>Delta report between two profile aggregates computed with the same system set.</summary>
public sealed record PerfDelta(
    string Metric,
    double Baseline,
    double Current,
    double DeltaPercent);

/// <summary>Compares baseline and current profile aggregates.</summary>
public static class PerfComparison
{
    public static IReadOnlyList<PerfDelta> Compare(PerfAggregator baseline, PerfAggregator current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        var deltas = new List<PerfDelta>
        {
            Delta("wall mean ms", baseline.Wall.Mean, current.Wall.Mean),
            Delta("wall p95 ms", baseline.Wall.P95, current.Wall.P95),
            Delta("wall p99 ms", baseline.Wall.P99, current.Wall.P99),
            Delta("alloc mean MiB", baseline.Alloc.Mean / 1048576d, current.Alloc.Mean / 1048576d),
            Delta("alloc p95 MiB", baseline.Alloc.P95 / 1048576d, current.Alloc.P95 / 1048576d),
            Delta("gc0 total", baseline.Gc0, current.Gc0),
            Delta("gc1 total", baseline.Gc1, current.Gc1),
            Delta("gc2 total", baseline.Gc2, current.Gc2),
        };
        var baselineSystems = baseline.SystemSummaries.ToDictionary(static summary => summary.System, StringComparer.Ordinal);
        foreach (var system in current.SystemSummaries)
        {
            if (!baselineSystems.TryGetValue(system.System, out var previous))
                continue;
            deltas.Add(Delta($"{system.System} mean ms", previous.Stats.Mean, system.Stats.Mean));
            deltas.Add(Delta($"{system.System} p95 ms", previous.Stats.P95, system.Stats.P95));
            deltas.Add(Delta($"{system.System} alloc mean MiB",
                previous.Alloc.Mean / 1048576d, system.Alloc.Mean / 1048576d));
        }

        var baselineSubphases = baseline.SubphaseSummaries
            .ToDictionary(static summary => summary.System, StringComparer.Ordinal);
        foreach (var subphase in current.SubphaseSummaries)
        {
            if (!baselineSubphases.TryGetValue(subphase.System, out var previous))
                continue;
            deltas.Add(Delta($"{subphase.System} mean ms", previous.Stats.Mean, subphase.Stats.Mean));
            deltas.Add(Delta($"{subphase.System} p95 ms", previous.Stats.P95, subphase.Stats.P95));
            deltas.Add(Delta($"{subphase.System} alloc mean MiB",
                previous.Alloc.Mean / 1048576d, subphase.Alloc.Mean / 1048576d));
        }

        return deltas;
    }

    private static PerfDelta Delta(string metric, double baseline, double current) =>
        new(metric, baseline, current,
            baseline == 0 ? 0 : (current - baseline) / baseline * 100d);
}
