using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>Unit of a compared metric, used by the HTML renderer.</summary>
public enum PerfMetricUnit
{
    /// <summary>Milliseconds.</summary>
    Milliseconds,

    /// <summary>Mebibytes.</summary>
    Mebibytes,

    /// <summary>Collection-count estimate.</summary>
    Collections,
}

/// <summary>
///     One row of a profile comparison. A null side means the metric is absent from
///     that profile; <see cref="DeltaPercent" /> is null when the delta is undefined
///     (one-sided metric or zero baseline).
/// </summary>
/// <param name="Metric">Metric display name.</param>
/// <param name="Baseline">Baseline value, or null when absent/undefined.</param>
/// <param name="Current">Current value, or null when absent/undefined.</param>
/// <param name="DeltaPercent">Relative change in percent, or null when undefined.</param>
/// <param name="Unit">Metric unit.</param>
public sealed record PerfDelta(
    string Metric,
    double? Baseline,
    double? Current,
    double? DeltaPercent,
    PerfMetricUnit Unit);

/// <summary>Comparison rows plus warnings about undefined or low-confidence rows.</summary>
/// <param name="Rows">Comparison rows.</param>
/// <param name="Warnings">Warnings for one-sided metrics, zero baselines, and sampling-interval mismatches.</param>
public sealed record PerfComparisonResult(IReadOnlyList<PerfDelta> Rows, IReadOnlyList<string> Warnings);

/// <summary>
///     Compares two profile aggregates over the symmetric union of their phase sets.
///     Rows that exist in only one profile are reported as <c>n/a</c> instead of being
///     dropped, and a zero baseline renders <c>n/a</c> rather than a 0 % change.
/// </summary>
public static class PerfComparison
{
    /// <summary>Builds the comparison rows and warnings for a baseline and a current aggregate.</summary>
    public static PerfComparisonResult Compare(PerfAggregator baseline, PerfAggregator current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        var rows = new List<PerfDelta>();
        var warnings = new List<string>();
        Add(rows, "wall mean ms", baseline.Wall.Mean, current.Wall.Mean, PerfMetricUnit.Milliseconds);
        Add(rows, "wall p95 ms", baseline.Wall.P95, current.Wall.P95, PerfMetricUnit.Milliseconds);
        Add(rows, "wall p99 ms", baseline.Wall.P99, current.Wall.P99, PerfMetricUnit.Milliseconds);
        Add(rows, "alloc mean MiB", baseline.Alloc.Mean / 1048576d, current.Alloc.Mean / 1048576d,
            PerfMetricUnit.Mebibytes);
        Add(rows, "alloc p95 MiB", baseline.Alloc.P95 / 1048576d, current.Alloc.P95 / 1048576d,
            PerfMetricUnit.Mebibytes);
        Add(rows, "gc0 est./run", baseline.Gc0RunEstimate, current.Gc0RunEstimate, PerfMetricUnit.Collections);
        Add(rows, "gc1 est./run", baseline.Gc1RunEstimate, current.Gc1RunEstimate, PerfMetricUnit.Collections);
        Add(rows, "gc2 est./run", baseline.Gc2RunEstimate, current.Gc2RunEstimate, PerfMetricUnit.Collections);
        AppendPhaseRows(rows, warnings, "system", baseline.SystemSummaries, current.SystemSummaries);
        AppendPhaseRows(rows, warnings, "subphase", baseline.SubphaseSummaries, current.SubphaseSummaries);
        var zeroBaseline = rows
            .Where(static row => row.DeltaPercent is null && row.Baseline == 0 && row.Current is not null)
            .Select(static row => row.Metric)
            .ToArray();
        if (zeroBaseline.Length > 0)
            warnings.Add(
                $"Metrics with a zero baseline have n/a deltas: {string.Join(", ", zeroBaseline.Select(static metric => $"'{metric}'"))}.");

        var baselineInterval = FormatInterval(baseline.TickInterval);
        var currentInterval = FormatInterval(current.TickInterval);
        if (baseline.TickInterval is null || current.TickInterval is null
            || baseline.TickInterval != current.TickInterval)
            warnings.Add(
                $"Profiles were sampled at different or unknown tick intervals (baseline={baselineInterval}, "
                + $"current={currentInterval}); GC run-total estimates scale by interval and per-tick "
                + "statistics are not directly comparable.");
        return new PerfComparisonResult(rows, warnings);
    }

    private static void AppendPhaseRows(
        List<PerfDelta> rows,
        List<string> warnings,
        string kind,
        IReadOnlyList<PerfAggregator.PhaseSummary> baselinePhases,
        IReadOnlyList<PerfAggregator.PhaseSummary> currentPhases)
    {
        var baseline = baselinePhases.ToDictionary(static phase => phase.Name, StringComparer.Ordinal);
        var current = currentPhases.ToDictionary(static phase => phase.Name, StringComparer.Ordinal);
        var names = baseline.Keys.Union(current.Keys, StringComparer.Ordinal).OrderBy(static name => name,
            StringComparer.Ordinal);
        foreach (var name in names)
        {
            var hasBaseline = baseline.TryGetValue(name, out var previous);
            var hasCurrent = current.TryGetValue(name, out var latest);
            if (!hasBaseline || !hasCurrent)
                warnings.Add(
                    $"{kind} '{name}' exists only in the {(hasBaseline ? "baseline" : "current")} profile; "
                    + "its mean, p95, and alloc rows are n/a.");
            Add(rows, $"{name} mean ms", previous?.Stats.Mean, latest?.Stats.Mean, PerfMetricUnit.Milliseconds);
            Add(rows, $"{name} p95 ms", previous?.Stats.P95, latest?.Stats.P95, PerfMetricUnit.Milliseconds);
            Add(rows, $"{name} alloc mean MiB",
                previous is null ? null : previous.Alloc.Mean / 1048576d,
                latest is null ? null : latest.Alloc.Mean / 1048576d,
                PerfMetricUnit.Mebibytes);
        }
    }

    private static void Add(
        List<PerfDelta> rows,
        string metric,
        double? baseline,
        double? current,
        PerfMetricUnit unit)
    {
        double? delta = baseline is { } previous && current is { } latest && previous != 0
            ? (latest - previous) / previous * 100d
            : null;
        rows.Add(new PerfDelta(metric, baseline, current, delta, unit));
    }

    private static void Add(
        List<PerfDelta> rows,
        string metric,
        long? baseline,
        long? current,
        PerfMetricUnit unit)
    {
        double? delta = baseline is { } previous && current is { } latest && previous != 0
            ? ((double)latest - previous) / previous * 100d
            : null;
        rows.Add(new PerfDelta(metric, baseline, current, delta, unit));
    }

    private static string FormatInterval(int? interval) =>
        interval is { } value ? value.ToString(CultureInfo.InvariantCulture) : "n/a";
}
