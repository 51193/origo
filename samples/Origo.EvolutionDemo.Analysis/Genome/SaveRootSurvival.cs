using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>One checkpoint of root survival and turnover.</summary>
/// <param name="SaveId">Save identifier.</param>
/// <param name="Path">Scene path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="LivingRoots">Roots present in this save.</param>
/// <param name="NewRoots">Roots absent from the previous save.</param>
/// <param name="LostRoots">Previous roots absent now.</param>
/// <param name="Hazard">Lost roots divided by previous living roots; null for the first save.</param>
/// <param name="Retention">Prior roots still present divided by previous living roots; null for the first save.</param>
public sealed record RootSurvivalPoint(
    string SaveId,
    string Path,
    double Time,
    int LivingRoots,
    int NewRoots,
    int LostRoots,
    double? Hazard,
    double? Retention);

/// <summary>One root's observed lifespan.</summary>
/// <param name="RootLineage">Root id.</param>
/// <param name="FirstSeen">First observed simulation time.</param>
/// <param name="LastSeen">Last observed simulation time.</param>
/// <param name="Extinct">Absent from the final analyze save.</param>
/// <param name="Lifetime">LastSeen - FirstSeen; only meaningful for extinct roots.</param>
public sealed record RootLifetime(
    int RootLineage,
    double FirstSeen,
    double LastSeen,
    bool Extinct,
    double Lifetime);

/// <summary>Aggregate root survival statistics.</summary>
/// <param name="DistinctRoots">Roots observed at any checkpoint.</param>
/// <param name="InitialLiving">Living roots in the first save.</param>
/// <param name="FinalLiving">Living roots in the final save.</param>
/// <param name="ExtinctRoots">Observed roots absent from the final save.</param>
/// <param name="ExtinctionShare">Extinct observed roots divided by distinct roots.</param>
/// <param name="MedianExtinctLifetime">Median lifespan of extinct roots, or null.</param>
/// <param name="MeanExtinctLifetime">Mean lifespan of extinct roots, or null.</param>
/// <param name="MeanHazard">Mean interval hazard across points with a prior save, or null.</param>
/// <param name="MedianPositiveExtinctLifetime">Median positive lifespan of extinct roots, or null.</param>
public sealed record RootSurvivalSummary(
    int DistinctRoots,
    int InitialLiving,
    int FinalLiving,
    int ExtinctRoots,
    double ExtinctionShare,
    double? MedianExtinctLifetime,
    double? MeanExtinctLifetime,
    double? MeanHazard,
    double? MedianPositiveExtinctLifetime);

/// <summary>Root survival points and aggregate lifespan summary.</summary>
/// <param name="Points">Checkpoint sequence.</param>
/// <param name="Summary">Lifetime and hazard summary.</param>
/// <param name="Lifetimes">Observed roots and their lifespan.</param>
public sealed record RootSurvivalAnalysis(
    IReadOnlyList<RootSurvivalPoint> Points,
    RootSurvivalSummary Summary,
    IReadOnlyList<RootLifetime> Lifetimes);

/// <summary>Builds root survival, hazard, and lifetime summaries from lineage details.</summary>
public static class RootSurvivalAnalyzer
{
    /// <summary>Analyzes root details in simulation order.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="lineage" /> is null.</exception>
    public static RootSurvivalAnalysis Analyze(LineageAnalysis lineage)
    {
        ArgumentNullException.ThrowIfNull(lineage);
        var detailsByPath = lineage.RootDetails
            .GroupBy(static detail => detail.Path, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var points = new List<RootSurvivalPoint>(lineage.Saves.Count);
        HashSet<int>? previous = null;
        var firstSeen = new Dictionary<int, double>();
        var lastSeen = new Dictionary<int, double>();
        foreach (var save in lineage.Saves
            .OrderBy(static item => item.Time)
            .ThenBy(static item => item.SaveId, StringComparer.Ordinal))
        {
            var current = detailsByPath.TryGetValue(save.Path, out var details)
                ? details.Select(static detail => detail.RootLineage).ToHashSet()
                : [];
            foreach (var root in current)
            {
                firstSeen.TryAdd(root, save.Time);
                lastSeen[root] = save.Time;
            }

            var newRoots = previous is null ? 0 : current.Except(previous).Count();
            var lostRoots = previous is null ? 0 : previous.Except(current).Count();
            var hazard = previous is null || previous.Count == 0 ? (double?)null : lostRoots / (double)previous.Count;
            var retention = previous is null || previous.Count == 0 ? (double?)null : 1d - hazard;
            points.Add(new RootSurvivalPoint(
                save.SaveId, save.Path, save.Time, current.Count, newRoots, lostRoots, hazard, retention));
            previous = current;
        }

        var final = previous ?? [];
        var lifetimes = firstSeen.Keys
            .OrderBy(static root => root)
            .Select(root => new RootLifetime(
                root,
                firstSeen[root],
                lastSeen[root],
                !final.Contains(root),
                lastSeen[root] - firstSeen[root]))
            .ToArray();
        var extinct = lifetimes.Where(static item => item.Extinct).ToArray();
        var median = Median(extinct.Select(static item => item.Lifetime));
        var mean = extinct.Length == 0 ? (double?)null : extinct.Average(static item => item.Lifetime);
        var positiveMedian = Median(extinct.Where(static item => item.Lifetime > 0)
            .Select(static item => item.Lifetime));
        var hazards = points.Where(static point => point.Hazard is not null)
            .Select(static point => point.Hazard!.Value).ToArray();
        var summary = new RootSurvivalSummary(
            lifetimes.Length,
            points.Count == 0 ? 0 : points[0].LivingRoots,
            final.Count,
            extinct.Length,
            lifetimes.Length == 0 ? 0d : extinct.Length / (double)lifetimes.Length,
            median,
            mean,
            hazards.Length == 0 ? null : hazards.Average(),
            positiveMedian);
        return new RootSurvivalAnalysis(points, summary, lifetimes);
    }

    private static double? Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(static value => value).ToArray();
        if (ordered.Length == 0)
            return null;
        return ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[(ordered.Length / 2) - 1] + ordered[ordered.Length / 2]) / 2d;
    }
}
