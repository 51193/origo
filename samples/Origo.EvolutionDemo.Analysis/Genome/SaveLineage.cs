using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.IO;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>Living lineage structure for one save.</summary>
/// <param name="Lineages">Distinct living lineage ids.</param>
/// <param name="FounderLineages">Lineages containing at least one generation-0 creature.</param>
/// <param name="FounderLineageShare">Founder lineages divided by living lineages.</param>
/// <param name="Gen0Individuals">Living generation-0 creatures.</param>
/// <param name="Gen0Share">Generation-0 creatures divided by all creatures.</param>
/// <param name="LargestLineageShare">Largest living lineage divided by all creatures.</param>
/// <param name="LineageEntropy">Normalized Shannon entropy over living lineage sizes; 0 with fewer than two lineages.</param>
/// <param name="NewLineages">Ids present now but not in the previous save; 0 for the first save.</param>
/// <param name="NewGen0Lineages">New ids containing at least one generation-0 creature; 0 for the first save.</param>
/// <param name="LostLineages">Previous ids no longer present; 0 for the first save.</param>
/// <param name="PriorLineageSurvival">Share of previous ids still present; null for the first save.</param>
/// <param name="LineageTurnover">(new + lost) / (previous + current); 0 for the first save.</param>
public sealed record LineageSummary(
    int Lineages,
    int FounderLineages,
    double FounderLineageShare,
    int Gen0Individuals,
    double Gen0Share,
    double LargestLineageShare,
    double LineageEntropy,
    int NewLineages,
    int NewGen0Lineages,
    int LostLineages,
    double? PriorLineageSurvival,
    double LineageTurnover);

/// <summary>Living creatures grouped by spawn origin.</summary>
/// <param name="Founders">Original generation-0 founders.</param>
/// <param name="Children">Ordinary reproduction children.</param>
/// <param name="Enrichment">Founders inserted by enrichment spawning.</param>
/// <param name="Innovation">Generation-0 carriers inserted by organ innovation.</param>
/// <param name="Overgrowth">Children created by global overgrowth.</param>
/// <param name="Unknown">Saves without a spawn-origin marker.</param>
public sealed record SpawnOriginCounts(
    int Founders,
    int Children,
    int Enrichment,
    int Innovation,
    int Overgrowth,
    int Unknown)
{
    /// <summary>Total living creatures carrying an origin.</summary>
    public int Total => Founders + Children + Enrichment + Innovation + Overgrowth + Unknown;
}

/// <summary>Living lineage and spawn-origin summary for one save.</summary>
/// <param name="SaveId">Short save identifier.</param>
/// <param name="Path">Scene file path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="Tick">Simulation tick.</param>
/// <param name="Summary">Mutation-lineage structure and turnover.</param>
/// <param name="RootSummary">Root-lineage structure and turnover; root ids survive mutation lineage changes.</param>
/// <param name="Origins">Living creatures by spawn origin.</param>
public sealed record SaveLineage(
    string SaveId,
    string Path,
    double Time,
    double Tick,
    LineageSummary Summary,
    LineageSummary RootSummary,
    SpawnOriginCounts Origins);

/// <summary>One living mutation lineage in one save.</summary>
/// <param name="SaveId">Short save identifier.</param>
/// <param name="Path">Scene file path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="Lineage">Mutation-lineage id.</param>
/// <param name="Creatures">Living creatures carrying the id.</param>
/// <param name="Share">Share of the save's living creatures.</param>
/// <param name="FounderCreatures">Generation-0 creatures in the lineage.</param>
/// <param name="MinGeneration">Minimum living generation in the lineage.</param>
/// <param name="MaxGeneration">Maximum living generation in the lineage.</param>
/// <param name="IsNew">True when the id was absent from the previous save.</param>
/// <param name="IsNewGen0">True when the id is new and contains a generation-0 creature.</param>
public sealed record LineageDetail(
    string SaveId,
    string Path,
    double Time,
    int Lineage,
    int Creatures,
    double Share,
    int FounderCreatures,
    int MinGeneration,
    int MaxGeneration,
    bool IsNew,
    bool IsNewGen0);

/// <summary>Sequence-level lineage turnover and founder-injection proxy.</summary>
/// <param name="DistinctLineages">Distinct lineage ids observed across all saves.</param>
/// <param name="PeakLivingLineages">Largest number of living lineages in one save.</param>
/// <param name="FinalLivingLineages">Living lineages in the last save.</param>
/// <param name="TotalNewLineages">Sum of per-interval new lineage ids.</param>
/// <param name="TotalNewGen0Lineages">Sum of new ids containing a generation-0 creature.</param>
/// <param name="LostLineages">Ids that appeared and were absent from the final save.</param>
/// <param name="MeanPriorLineageSurvival">Mean prior-lineage survival over transitions, null without transitions.</param>
/// <param name="MeanLineageTurnover">Mean turnover over transitions, null without transitions.</param>
public sealed record LineageSequenceSummary(
    int DistinctLineages,
    int PeakLivingLineages,
    int FinalLivingLineages,
    int TotalNewLineages,
    int TotalNewGen0Lineages,
    int LostLineages,
    double? MeanPriorLineageSurvival,
    double? MeanLineageTurnover);

/// <summary>Per-save lineage summaries, per-lineage details, and sequence turnover.</summary>
/// <param name="Saves">Save summaries in simulation order.</param>
/// <param name="Details">One row per living mutation lineage per save.</param>
/// <param name="Sequence">Cross-save mutation-lineage turnover.</param>
/// <param name="RootSequence">Cross-save root-lineage turnover.</param>
/// <param name="Origins">Living creatures by spawn origin across all saves.</param>
public sealed record LineageAnalysis(
    IReadOnlyList<SaveLineage> Saves,
    IReadOnlyList<LineageDetail> Details,
    LineageSequenceSummary Sequence,
    LineageSequenceSummary RootSequence,
    SpawnOriginCounts Origins);

/// <summary>Derives living lineage structure and founder-injection proxies from v7 saves.</summary>
public static class SaveLineageAnalyzer
{
    /// <summary>Analyzes saves in simulation-time order.</summary>
    /// <exception cref="ArgumentNullException">The save list is null.</exception>
    public static LineageAnalysis Analyze(IReadOnlyList<SaveSnapshot> saves)
    {
        ArgumentNullException.ThrowIfNull(saves);
        var ordered = saves
            .OrderBy(static save => save.Time)
            .ThenBy(static save => save.Id, StringComparer.Ordinal)
            .ThenBy(static save => save.Path, StringComparer.Ordinal)
            .ToArray();
        var results = new List<SaveLineage>(ordered.Length);
        var details = new List<LineageDetail>();
        HashSet<int>? previousMutation = null;
        HashSet<int>? previousRoot = null;
        var mutationSummaries = new List<LineageSummary>(ordered.Length);
        var rootSummaries = new List<LineageSummary>(ordered.Length);
        var everMutation = new HashSet<int>();
        var everRoot = new HashSet<int>();
        var origins = new SpawnOriginCounts(0, 0, 0, 0, 0, 0);
        foreach (var save in ordered)
        {
            var mutationGroups = Group(save.Creatures, static creature => creature.Lineage);
            var rootGroups = Group(save.Creatures, static creature => creature.RootLineage);
            var total = save.Creatures.Count;
            var mutation = Summarize(mutationGroups, total, previousMutation);
            var root = Summarize(rootGroups, total, previousRoot);
            var saveOrigins = CountOrigins(save.Creatures);
            origins = Add(origins, saveOrigins);
            results.Add(new SaveLineage(
                save.Id, save.Path, save.Time, save.Tick,
                mutation.Summary, root.Summary, saveOrigins));
            mutationSummaries.Add(mutation.Summary);
            rootSummaries.Add(root.Summary);
            foreach (var group in mutationGroups)
            {
                var count = group.Count();
                details.Add(new LineageDetail(
                    save.Id,
                    save.Path,
                    save.Time,
                    group.Key,
                    count,
                    total == 0 ? 0d : count / (double)total,
                    group.Count(static creature => creature.Generation == 0),
                    group.Min(static creature => creature.Generation),
                    group.Max(static creature => creature.Generation),
                    mutation.NewKeys.Contains(group.Key),
                    mutation.NewGen0Keys.Contains(group.Key)));
            }

            everMutation.UnionWith(mutation.Current);
            everRoot.UnionWith(root.Current);
            previousMutation = mutation.Current;
            previousRoot = root.Current;
        }

        var finalMutation = previousMutation ?? [];
        var finalRoot = previousRoot ?? [];
        return new LineageAnalysis(
            results,
            details,
            Sequence(mutationSummaries, everMutation, finalMutation),
            Sequence(rootSummaries, everRoot, finalRoot),
            origins);
    }

    private static IGrouping<int, CreatureRecord>[] Group(
        IReadOnlyList<CreatureRecord> creatures, Func<CreatureRecord, int> key) =>
        [.. creatures.GroupBy(key).OrderByDescending(static group => group.Count()).ThenBy(static group => group.Key)];

    private static MutationSummary Summarize(
        IGrouping<int, CreatureRecord>[] groups, int total, HashSet<int>? previous)
    {
        var current = groups.Select(static group => group.Key).ToHashSet();
        var founderLineages = groups
            .Where(static group => group.Any(static creature => creature.Generation == 0))
            .Select(static group => group.Key)
            .ToHashSet();
        var gen0 = groups.Sum(static group => group.Count(static creature => creature.Generation == 0));
        var newKeys = previous is null ? [] : current.Except(previous).ToHashSet();
        var lostKeys = previous is null ? [] : previous.Except(current).ToHashSet();
        var newGen0 = newKeys.Where(founderLineages.Contains).ToHashSet();
        var survival = previous is null || previous.Count == 0
            ? (double?)null
            : previous.Count(current.Contains) / (double)previous.Count;
        var turnover = previous is null || previous.Count + current.Count == 0
            ? 0d
            : (newKeys.Count + lostKeys.Count) / (double)(previous.Count + current.Count);
        var counts = new int[groups.Length];
        for (var i = 0; i < groups.Length; i++)
            counts[i] = groups[i].Count();
        var summary = new LineageSummary(
            groups.Length,
            founderLineages.Count,
            groups.Length == 0 ? 0d : founderLineages.Count / (double)groups.Length,
            gen0,
            total == 0 ? 0d : gen0 / (double)total,
            groups.Length == 0 ? 0d : groups[0].Count() / (double)total,
            Entropy(counts, total),
            newKeys.Count,
            newGen0.Count,
            lostKeys.Count,
            survival,
            turnover);
        return new MutationSummary(summary, current, newKeys, newGen0);
    }

    private static LineageSequenceSummary Sequence(
        List<LineageSummary> summaries, HashSet<int> ever, HashSet<int> final)
    {
        var transitions = summaries
            .Where(static summary => summary.PriorLineageSurvival is not null)
            .ToArray();
        return new LineageSequenceSummary(
            ever.Count,
            summaries.Count == 0 ? 0 : summaries.Max(static summary => summary.Lineages),
            final.Count,
            summaries.Sum(static summary => summary.NewLineages),
            summaries.Sum(static summary => summary.NewGen0Lineages),
            ever.Count(id => !final.Contains(id)),
            transitions.Length == 0
                ? null
                : transitions.Average(static summary => summary.PriorLineageSurvival!.Value),
            transitions.Length == 0
                ? null
                : transitions.Average(static summary => summary.LineageTurnover));
    }

    private static SpawnOriginCounts CountOrigins(IReadOnlyList<CreatureRecord> creatures)
    {
        var founders = 0;
        var children = 0;
        var enrichment = 0;
        var innovation = 0;
        var overgrowth = 0;
        var unknown = 0;
        foreach (var creature in creatures)
            switch (creature.SpawnOrigin)
            {
                case "founder": founders++; break;
                case "child": children++; break;
                case "enrichment": enrichment++; break;
                case "innovation": innovation++; break;
                case "overgrowth": overgrowth++; break;
                default: unknown++; break;
            }

        return new SpawnOriginCounts(founders, children, enrichment, innovation, overgrowth, unknown);
    }

    private static SpawnOriginCounts Add(SpawnOriginCounts left, SpawnOriginCounts right) =>
        new(
            left.Founders + right.Founders,
            left.Children + right.Children,
            left.Enrichment + right.Enrichment,
            left.Innovation + right.Innovation,
            left.Overgrowth + right.Overgrowth,
            left.Unknown + right.Unknown);

    private static double Entropy(int[] counts, int total)
    {
        if (counts.Length <= 1 || total <= 0)
            return 0d;
        var entropy = 0d;
        foreach (var count in counts)
        {
            if (count <= 0)
                continue;
            var share = count / (double)total;
            entropy -= share * System.Math.Log(share);
        }

        return entropy / System.Math.Log(counts.Length);
    }

    private sealed record MutationSummary(
        LineageSummary Summary,
        HashSet<int> Current,
        HashSet<int> NewKeys,
        HashSet<int> NewGen0Keys);
}
