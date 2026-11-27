using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>Cumulative creature deaths by cause for one save.</summary>
/// <param name="Starvation">Deaths with zero or negative energy.</param>
/// <param name="Predation">Deaths caused by a lethal spike hit.</param>
/// <param name="Aging">Deaths at the creature's maximum age.</param>
public sealed record DeathCounts(int Starvation, int Predation, int Aging)
{
    /// <summary>Sum of the three tracked causes.</summary>
    public int Total => Starvation + Predation + Aging;
}

/// <summary>Cumulative death counters for one save, or null when the save has no counters.</summary>
/// <param name="SaveId">Short save identifier.</param>
/// <param name="Path">Scene file path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="Tick">Simulation tick.</param>
/// <param name="Counts">Death counts, or null for saves written before the counters existed.</param>
/// <param name="RootDeaths">Cumulative deaths per root lineage, or null when absent.</param>
public sealed record SaveMortality(
    string SaveId,
    string Path,
    double Time,
    double Tick,
    DeathCounts? Counts,
    IReadOnlyDictionary<int, RootDeathTally>? RootDeaths);

/// <summary>Final cumulative death counts and shares for a save sequence.</summary>
/// <param name="Counts">Latest cumulative counts, or null without counters.</param>
/// <param name="PredationShare">Predation deaths divided by tracked deaths; null without counters or deaths.</param>
/// <param name="StarvationShare">Starvation deaths divided by tracked deaths; null without counters or deaths.</param>
/// <param name="AgingShare">Aging deaths divided by tracked deaths; null without counters or deaths.</param>
public sealed record MortalitySequenceSummary(
    DeathCounts? Counts,
    double? PredationShare,
    double? StarvationShare,
    double? AgingShare);

/// <summary>Aggregate per-root death totals for a save sequence.</summary>
/// <param name="Roots">Roots with at least one tracked death.</param>
/// <param name="Counts">Totals by cause.</param>
/// <param name="PredationShare">Predation share of tracked root deaths.</param>
/// <param name="StarvationShare">Starvation share of tracked root deaths.</param>
/// <param name="AgingShare">Aging share of tracked root deaths.</param>
public sealed record RootMortalitySummary(
    int Roots,
    DeathCounts Counts,
    double? PredationShare,
    double? StarvationShare,
    double? AgingShare);

/// <summary>Per-save mortality records and the latest cumulative summaries.</summary>
/// <param name="Saves">Save mortality in simulation order.</param>
/// <param name="Sequence">Latest cumulative counts and shares.</param>
/// <param name="RootDeaths">Latest cumulative per-root death map, or null.</param>
/// <param name="RootSummary">Latest aggregate per-root death summary, or null.</param>
public sealed record MortalityAnalysis(
    IReadOnlyList<SaveMortality> Saves,
    MortalitySequenceSummary Sequence,
    IReadOnlyDictionary<int, RootDeathTally>? RootDeaths,
    RootMortalitySummary? RootSummary);

/// <summary>Reads cumulative starvation, predation, and aging counters from v7 saves.</summary>
public static class SaveMortalityAnalyzer
{
    private const string StarvationKey = "evo.starvationDeaths";
    private const string PredationKey = "evo.predationDeaths";
    private const string AgingKey = "evo.ageDeaths";

    /// <summary>Analyzes saves in simulation-time order.</summary>
    /// <exception cref="ArgumentNullException">The save list is null.</exception>
    public static MortalityAnalysis Analyze(IReadOnlyList<SaveSnapshot> saves)
    {
        ArgumentNullException.ThrowIfNull(saves);
        var ordered = saves
            .OrderBy(static save => save.Time)
            .ThenBy(static save => save.Id, StringComparer.Ordinal)
            .ThenBy(static save => save.Path, StringComparer.Ordinal)
            .ToArray();
        var results = new List<SaveMortality>(ordered.Length);
        DeathCounts? latest = null;
        IReadOnlyDictionary<int, RootDeathTally>? latestRoots = null;
        foreach (var save in ordered)
        {
            var counts = TryRead(save);
            var roots = TryReadRoots(save);
            results.Add(new SaveMortality(save.Id, save.Path, save.Time, save.Tick, counts, roots));
            if (counts is not null)
                latest = counts;
            if (roots is not null)
                latestRoots = roots;
        }

        var rootSummary = latestRoots is null ? null : SummarizeRoots(latestRoots);
        if (latest is null)
            return new MortalityAnalysis(
                results, new MortalitySequenceSummary(null, null, null, null), latestRoots, rootSummary);
        var total = latest.Total;
        return new MortalityAnalysis(
            results,
            new MortalitySequenceSummary(
                latest,
                total > 0 ? latest.Predation / (double)total : null,
                total > 0 ? latest.Starvation / (double)total : null,
                total > 0 ? latest.Aging / (double)total : null),
            latestRoots,
            rootSummary);
    }

    private static IReadOnlyDictionary<int, RootDeathTally>? TryReadRoots(SaveSnapshot save)
    {
        if (!save.ControllerStrings.TryGetValue("evo.deathByRoot", out var text)
            || string.IsNullOrWhiteSpace(text))
            return null;
        return RootDeathCodec.Parse(text);
    }

    private static RootMortalitySummary SummarizeRoots(IReadOnlyDictionary<int, RootDeathTally> roots)
    {
        var starvation = roots.Values.Sum(static tally => tally.Starvation);
        var predation = roots.Values.Sum(static tally => tally.Predation);
        var aging = roots.Values.Sum(static tally => tally.Aging);
        var counts = new DeathCounts(starvation, predation, aging);
        var total = counts.Total;
        return new RootMortalitySummary(
            roots.Count,
            counts,
            total > 0 ? predation / (double)total : null,
            total > 0 ? starvation / (double)total : null,
            total > 0 ? aging / (double)total : null);
    }

    private static DeathCounts? TryRead(SaveSnapshot save)
    {
        var hasAny = save.ControllerNumbers.ContainsKey(StarvationKey)
            || save.ControllerNumbers.ContainsKey(PredationKey)
            || save.ControllerNumbers.ContainsKey(AgingKey);
        if (!hasAny)
            return null;
        return new DeathCounts(
            (int)System.Math.Max(0d, ReadOrZero(save, StarvationKey)),
            (int)System.Math.Max(0d, ReadOrZero(save, PredationKey)),
            (int)System.Math.Max(0d, ReadOrZero(save, AgingKey)));
    }

    private static double ReadOrZero(SaveSnapshot save, string key) =>
        save.ControllerNumbers.TryGetValue(key, out var value) && double.IsFinite(value) ? value : 0d;

}
