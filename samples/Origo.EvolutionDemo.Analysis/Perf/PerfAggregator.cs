using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>
///     Aggregates sampled profile ticks into wall/alloc/GC statistics,
///     population/food/corpse statistics, and phase summaries for top-level
///     systems and their nested subphases.
/// </summary>
/// <remarks>
///     <para>
///         Classification rule: a phase id with exactly one dot is a system; an id
///         with exactly two dots whose parent (the prefix before the last dot) is a
///         system is a subphase of that system. Every other id is a hard error.
///     </para>
///     <para>
///         The constructor fails fast when the selection is empty, when the phase key
///         set is inconsistent across samples, when a phase id is neither a system nor
///         a subphase of a system, or when no system exists.
///     </para>
///     <para>
///         GC values are per-sampled-tick collection counts. <see cref="Gc0SampledSum" />
///         is the sum over the selected samples; <see cref="Gc0RunEstimate" /> scales it
///         by the sampling interval, which estimates the run total only when sampling is
///         uniform (the producer samples every <c>ORIGO_DEMO_PROFILE_INTERVAL</c> ticks).
///     </para>
///     <para>
///         Percentiles use Hyndman–Fan type 7 linear interpolation.
///     </para>
/// </remarks>
public sealed class PerfAggregator
{
    /// <summary>Creates an aggregate over a non-empty, phase-consistent selection.</summary>
    public PerfAggregator(IReadOnlyList<TickProfile> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        if (ticks.Count == 0)
            throw new InvalidOperationException("Cannot aggregate a profile selection with no samples.");
        Ticks = ticks;
        var phaseNames = ValidatePhases(ticks);
        var summaries = new List<PhaseSummary>(phaseNames.Length);
        foreach (var name in phaseNames)
        {
            var parent = IsSystemName(name) ? null : ParentOf(name);
            summaries.Add(new PhaseSummary(name, parent, Summarize(ticks, name, static phase => phase.Ms),
                Summarize(ticks, name, static phase => phase.AllocBytes)));
        }

        Summaries = [.. summaries.OrderBy(static summary => summary.Name, StringComparer.Ordinal)];
        SystemSummaries =
        [
            .. summaries.Where(static summary => summary.Parent is null)
                .OrderByDescending(static summary => summary.Stats.Mean),
        ];
        SubphaseSummaries =
        [
            .. summaries.Where(static summary => summary.Parent is not null)
                .OrderBy(static summary => summary.Parent, StringComparer.Ordinal)
                .ThenByDescending(static summary => summary.Stats.Mean),
        ];
        Wall = Percentiles([.. ticks.Select(static tick => tick.WallMs)]);
        Alloc = Percentiles([.. ticks.Select(static tick => (double)tick.AllocBytes)]);
        Population = Percentiles([.. ticks.Select(static tick => (double)tick.Population)]);
        Food = Percentiles([.. ticks.Select(static tick => (double)tick.Food)]);
        Corpses = Percentiles([.. ticks.Select(static tick => (double)tick.Corpses)]);
        Gc0SampledSum = ticks.Sum(static tick => (long)tick.Gc0);
        Gc1SampledSum = ticks.Sum(static tick => (long)tick.Gc1);
        Gc2SampledSum = ticks.Sum(static tick => (long)tick.Gc2);
        var deltas = ticks.Zip(ticks.Skip(1), static (left, right) => right.Tick - left.Tick).ToArray();
        var positiveDeltas = deltas.Where(static delta => delta > 0).OrderBy(static delta => delta).ToArray();
        TickInterval = positiveDeltas.Length == 0 ? null : positiveDeltas[positiveDeltas.Length / 2];
        HasUniformTickInterval = deltas.Length > 0 && deltas[0] > 0 && deltas.All(delta => delta == deltas[0]);
        SystemMeanTotal = SystemSummaries.Sum(static summary => summary.Stats.Mean);
    }

    /// <summary>Selected samples.</summary>
    public IReadOnlyList<TickProfile> Ticks { get; }

    /// <summary>Number of selected samples.</summary>
    public int SampleCount => Ticks.Count;

    /// <summary>First selected sample's simulation time.</summary>
    public double SimTimeStart => Ticks[0].SimTime;

    /// <summary>Last selected sample's simulation time.</summary>
    public double SimTimeEnd => Ticks[^1].SimTime;

    /// <summary>All phase summaries, ordered by name.</summary>
    public IReadOnlyList<PhaseSummary> Summaries { get; }

    /// <summary>Top-level systems, ordered by mean descending; the share denominator is their sum.</summary>
    public IReadOnlyList<PhaseSummary> SystemSummaries { get; }

    /// <summary>Nested subphases with their parent system, ordered by parent then mean descending.</summary>
    public IReadOnlyList<PhaseSummary> SubphaseSummaries { get; }

    /// <summary>Sum of the system means; the share denominator. Nested subphases are never added.</summary>
    public double SystemMeanTotal { get; }

    /// <summary>Wall time per sampled tick.</summary>
    public Stats Wall { get; }

    /// <summary>Allocated bytes per sampled tick.</summary>
    public Stats Alloc { get; }

    /// <summary>Population per sampled tick.</summary>
    public Stats Population { get; }

    /// <summary>Food count per sampled tick.</summary>
    public Stats Food { get; }

    /// <summary>Corpse count per sampled tick.</summary>
    public Stats Corpses { get; }

    /// <summary>Sum of gen-0 collection counts over the sampled ticks.</summary>
    public long Gc0SampledSum { get; }

    /// <summary>Sum of gen-1 collection counts over the sampled ticks.</summary>
    public long Gc1SampledSum { get; }

    /// <summary>Sum of gen-2 collection counts over the sampled ticks.</summary>
    public long Gc2SampledSum { get; }

    /// <summary>Median sampled tick delta, or null when it cannot be determined.</summary>
    public int? TickInterval { get; }

    /// <summary>True when every sampled tick delta equals <see cref="TickInterval" />.</summary>
    public bool HasUniformTickInterval { get; }

    /// <summary>Estimated run total of gen-0 collections: sampled sum times the tick interval, or null.</summary>
    public long? Gc0RunEstimate => TickInterval is { } interval ? Gc0SampledSum * interval : null;

    /// <summary>Estimated run total of gen-1 collections: sampled sum times the tick interval, or null.</summary>
    public long? Gc1RunEstimate => TickInterval is { } interval ? Gc1SampledSum * interval : null;

    /// <summary>Estimated run total of gen-2 collections: sampled sum times the tick interval, or null.</summary>
    public long? Gc2RunEstimate => TickInterval is { } interval ? Gc2SampledSum * interval : null;

    /// <summary>
    ///     Computes the arithmetic mean, type-7 interpolated P50/P95/P99, and maximum
    ///     of <paramref name="values" />. An empty sample is a hard error.
    /// </summary>
    public static Stats Percentiles(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new InvalidOperationException("Cannot compute percentiles of an empty sample.");
        var sorted = values.ToArray();
        Array.Sort(sorted);
        return new Stats(sorted.Average(), Quantile(sorted, 0.50), Quantile(sorted, 0.95), Quantile(sorted, 0.99),
            sorted[^1]);
    }

    private static string[] ValidatePhases(IReadOnlyList<TickProfile> ticks)
    {
        var reference =
            ticks[0].Phases.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
        if (reference.Length == 0)
            throw new InvalidDataException(
                "Profile contains no phase records; per-system attribution is impossible.");
        var referenceSet = reference.ToHashSet(StringComparer.Ordinal);
        for (var sample = 1; sample < ticks.Count; sample++)
        {
            var current = ticks[sample].Phases.Keys.ToHashSet(StringComparer.Ordinal);
            if (current.Count == reference.Length && current.SetEquals(referenceSet))
                continue;
            var added = string.Join(", ", current.Except(referenceSet).OrderBy(static key => key, StringComparer.Ordinal));
            var missing = string.Join(", ", referenceSet.Except(current).OrderBy(static key => key, StringComparer.Ordinal));
            throw new InvalidDataException(
                $"Phase key set changed at sample {sample} (tick {ticks[sample].Tick}): "
                + $"added [{added}], missing [{missing}]. A profile must expose the same phase ids on every sample.");
        }

        var systems = reference.Where(static name => IsSystemName(name)).ToHashSet(StringComparer.Ordinal);
        foreach (var name in reference)
        {
            if (IsSystemName(name))
                continue;
            var parent = ParentOf(name);
            if (parent is not null && systems.Contains(parent))
                continue;
            var reason = name.Contains('.')
                ? $"its parent '{parent}' is not a one-dot system id"
                : "it has no dot, so it is not a system id";
            throw new InvalidDataException(
                $"Phase '{name}' is neither a one-dot system id nor a two-dot subphase of a system: {reason}.");
        }

        if (systems.Count == 0)
            throw new InvalidDataException(
                "Profile contains no one-dot system phase ids; expected ids such as 'evolution.core'.");
        return reference;
    }

    private static bool IsSystemName(string name) => name.Count(static ch => ch == '.') == 1;

    private static string? ParentOf(string name)
    {
        var index = name.LastIndexOf('.');
        return index < 0 ? null : name[..index];
    }

    private static Stats Summarize(
        IReadOnlyList<TickProfile> ticks,
        string phase,
        Func<PhaseSample, double> selector)
    {
        var values = new double[ticks.Count];
        for (var index = 0; index < ticks.Count; index++)
            values[index] = selector(ticks[index].Phases[phase]);
        return Percentiles(values);
    }

    private static double Quantile(double[] sorted, double percentile)
    {
        if (sorted.Length == 1)
            return sorted[0];
        var position = (sorted.Length - 1) * percentile;
        var lower = (int)System.Math.Floor(position);
        var fraction = position - lower;
        var upper = System.Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (fraction * (sorted[upper] - sorted[lower]));
    }

    /// <summary>Summary of one phase: its id, parent system (null for a system), and statistics.</summary>
    /// <param name="Name">Phase id.</param>
    /// <param name="Parent">Parent system id for a subphase; null for a top-level system.</param>
    /// <param name="Stats">Millisecond statistics.</param>
    /// <param name="Alloc">Allocation-byte statistics.</param>
    public sealed record PhaseSummary(string Name, string? Parent, Stats Stats, Stats Alloc);

    /// <summary>Distribution statistics of one metric.</summary>
    /// <param name="Mean">Arithmetic mean.</param>
    /// <param name="P50">Interpolated 50th percentile.</param>
    /// <param name="P95">Interpolated 95th percentile.</param>
    /// <param name="P99">Interpolated 99th percentile.</param>
    /// <param name="Max">Maximum.</param>
    public sealed record Stats(double Mean, double P50, double P95, double P99, double Max);
}
