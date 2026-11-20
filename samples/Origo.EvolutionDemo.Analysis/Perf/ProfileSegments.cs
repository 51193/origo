using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>One continuous simulation segment of a profile.</summary>
/// <remarks>
///     Segments are delimited by a recorded <c>simTime</c> decrease: the demo smoke
///     runner restarts the pond between the smoke acceptance run and the benchmark
///     run while keeping one profiler instance, so a single JSONL file can contain
///     more than one simulation. <see cref="Ticks" /> is never empty.
/// </remarks>
public sealed record ProfileSegment(int Index, IReadOnlyList<TickProfile> Ticks);

/// <summary>A profile segment selection plus an optional simulation-time window.</summary>
/// <param name="Path">Profile path as given on the command line (for diagnostics).</param>
/// <param name="Ticks">Selected ticks after segment selection and window filtering.</param>
/// <param name="SegmentCount">Total number of segments found in the profile.</param>
/// <param name="SelectedSegment">Zero-based selected segment; null means every segment was pooled.</param>
/// <param name="ImplicitLast">True when the last segment was selected by the default rather than by an explicit option.</param>
/// <param name="From">Inclusive lower <c>simTime</c> bound, or null.</param>
/// <param name="To">Inclusive upper <c>simTime</c> bound, or null.</param>
public sealed record ProfileSelection(
    string Path,
    IReadOnlyList<TickProfile> Ticks,
    int SegmentCount,
    int? SelectedSegment,
    bool ImplicitLast,
    double? From,
    double? To)
{
    /// <summary>True when a <c>--from</c> or <c>--to</c> window was requested.</summary>
    public bool HasWindow => From.HasValue || To.HasValue;

    /// <summary>Stable label for the selected segment: <c>all</c>, <c>last</c>, or the zero-based index.</summary>
    public string SegmentLabel => SelectedSegment is null
        ? "all"
        : SelectedSegment == SegmentCount - 1
            ? "last"
            : SelectedSegment.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Stable label for the selected window, or <c>none</c>.</summary>
    public string WindowLabel => (From, To) switch
    {
        ({ } from, { } to) => string.Create(CultureInfo.InvariantCulture, $"{from:0.###}..{to:0.###}"),
        ({ } from, null) => string.Create(CultureInfo.InvariantCulture, $">={from:0.###}"),
        (null, { } to) => string.Create(CultureInfo.InvariantCulture, $"<={to:0.###}"),
        _ => "none",
    };
}

/// <summary>
///     Splits a profile into simulation segments and selects one segment (or all
///     of them) before applying an optional inclusive simulation-time window.
/// </summary>
public static class ProfileSegments
{
    /// <summary>
    ///     Splits <paramref name="ticks" /> into segments; a new segment starts
    ///     whenever <c>simTime</c> decreases. Equal timestamps stay in the segment.
    /// </summary>
    public static IReadOnlyList<ProfileSegment> Split(IReadOnlyList<TickProfile> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        if (ticks.Count == 0)
            throw new InvalidOperationException("Cannot split an empty profile into segments.");
        var segments = new List<ProfileSegment>();
        var start = 0;
        for (var end = 1; end <= ticks.Count; end++)
        {
            if (end < ticks.Count && ticks[end].SimTime >= ticks[end - 1].SimTime)
                continue;
            segments.Add(new ProfileSegment(segments.Count, [.. ticks.Skip(start).Take(end - start)]));
            start = end;
        }

        return segments;
    }

    /// <summary>
    ///     Selects a segment (default: the last one when the profile has several,
    ///     with <see cref="ProfileSelection.ImplicitLast" /> set) and then applies the
    ///     inclusive <c>simTime</c> window to the selected ticks.
    /// </summary>
    /// <param name="ticks">All ticks read from the profile; must not be empty.</param>
    /// <param name="path">Profile path used in error messages.</param>
    /// <param name="segment"><c>all</c>, <c>last</c>, a zero-based segment index, or null for the default.</param>
    /// <param name="from">Inclusive lower <c>simTime</c> bound, or null.</param>
    /// <param name="to">Inclusive upper <c>simTime</c> bound, or null.</param>
    /// <exception cref="InvalidOperationException">The segment choice is invalid or the window selects no samples.</exception>
    public static ProfileSelection Select(
        IReadOnlyList<TickProfile> ticks,
        string path,
        string? segment = null,
        double? from = null,
        double? to = null)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var segments = Split(ticks);
        var (selectedSegment, implicitLast) = ResolveSegment(segments.Count, segment);
        var selected = selectedSegment is { } index ? segments[index].Ticks : ticks;
        if (!from.HasValue && !to.HasValue)
            return new ProfileSelection(path, selected, segments.Count, selectedSegment, implicitLast, null, null);
        IReadOnlyList<TickProfile> windowed =
        [
            .. selected.Where(tick =>
                (!from.HasValue || tick.SimTime >= from.Value)
                && (!to.HasValue || tick.SimTime <= to.Value)),
        ];
        if (windowed.Count == 0)
            throw new InvalidOperationException(
                $"The --from/--to window contains no samples in the selected segment of '{path}'.");
        return new ProfileSelection(path, windowed, segments.Count, selectedSegment, implicitLast, from, to);
    }

    private static (int? Index, bool ImplicitLast) ResolveSegment(int count, string? choice)
    {
        if (choice is null)
            return count > 1 ? (count - 1, true) : (null, false);
        if (string.Equals(choice, "all", StringComparison.Ordinal))
            return (null, false);
        if (string.Equals(choice, "last", StringComparison.Ordinal))
            return (count - 1, false);
        if (!int.TryParse(choice, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0)
            throw new InvalidOperationException(
                $"--segment expects all, last, or a zero-based segment index, got '{choice}'.");
        if (index >= count)
            throw new InvalidOperationException(
                $"--segment {index} is out of range; the profile has {count} segment(s) (valid indexes 0..{count - 1}).");
        return (index, false);
    }
}
