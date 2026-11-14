using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>Percentile and phase summaries for a profile.</summary>
public sealed class PerfAggregator
{
    public PerfAggregator(IReadOnlyList<TickProfile> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        Ticks = ticks;
        var systems = ticks.SelectMany(static tick => tick.Phases.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        Summaries = [.. systems
            .Select(system => new PhaseSummary(system,
                Percentiles([.. ticks.Select(tick => tick.Phases.GetValueOrDefault(system))])))
            .OrderByDescending(static summary => summary.Stats.Mean)];
        Wall = Percentiles([.. ticks.Select(static tick => tick.WallMs)]);
        Alloc = Percentiles([.. ticks.Select(static tick => (double)tick.AllocBytes)]);
        Population = Percentiles([.. ticks.Select(static tick => (double)tick.Population)]);
        Food = Percentiles([.. ticks.Select(static tick => (double)tick.Food)]);
        Gc0 = ticks.Sum(static tick => tick.Gc0);
        Gc1 = ticks.Sum(static tick => tick.Gc1);
        Gc2 = ticks.Sum(static tick => tick.Gc2);
    }

    public IReadOnlyList<TickProfile> Ticks { get; }
    public IReadOnlyList<PhaseSummary> Summaries { get; }
    public Stats Wall { get; }
    public Stats Alloc { get; }
    public Stats Population { get; }
    public Stats Food { get; }
    public int Gc0 { get; }
    public int Gc1 { get; }
    public int Gc2 { get; }

    public static Stats Percentiles(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            return new Stats(0, 0, 0, 0, 0);
        var sorted = values.OrderBy(static value => value).ToArray();
        double P(double percentile) => sorted[System.Math.Clamp((int)System.Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];
        return new Stats(sorted.Average(), P(0.50), P(0.95), P(0.99), sorted[^1]);
    }

    public sealed record PhaseSummary(string System, Stats Stats);

    public sealed record Stats(double Mean, double P50, double P95, double P99, double Max);
}
