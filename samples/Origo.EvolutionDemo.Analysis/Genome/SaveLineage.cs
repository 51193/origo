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

/// <summary>Living lineage summary for one save.</summary>
/// <param name="SaveId">Short save identifier.</param>
/// <param name="Path">Scene file path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="Tick">Simulation tick.</param>
/// <param name="Summary">Lineage structure and turnover.</param>
public sealed record SaveLineage(
    string SaveId,
    string Path,
    double Time,
    double Tick,
    LineageSummary Summary);

/// <summary>One living lineage in one save.</summary>
/// <param name="SaveId">Short save identifier.</param>
/// <param name="Path">Scene file path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="Lineage">Lineage id.</param>
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
/// <param name="Details">One row per living lineage per save.</param>
/// <param name="Sequence">Cross-save lineage turnover and founder-injection proxy.</param>
public sealed record LineageAnalysis(
    IReadOnlyList<SaveLineage> Saves,
    IReadOnlyList<LineageDetail> Details,
    LineageSequenceSummary Sequence);

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
        HashSet<int>? previous = null;
        var ever = new HashSet<int>();
        foreach (var save in ordered)
        {
            var groups = save.Creatures
                .GroupBy(static creature => creature.Lineage)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key)
                .ToArray();
            var total = save.Creatures.Count;
            var current = groups.Select(static group => group.Key).ToHashSet();
            var founderLineages = groups
                .Where(static group => group.Any(static creature => creature.Generation == 0))
                .Select(static group => group.Key)
                .ToHashSet();
            var gen0 = save.Creatures.Count(static creature => creature.Generation == 0);
            var newLineages = previous is null ? [] : current.Except(previous).ToArray();
            var lostLineages = previous is null ? [] : previous.Except(current).ToArray();
            var newGen0 = newLineages
                .Where(founderLineages.Contains)
                .ToHashSet();
            var survival = previous is null || previous.Count == 0
                ? (double?)null
                : previous.Count(current.Contains) / (double)previous.Count;
            var turnover = previous is null || previous.Count + current.Count == 0
                ? 0d
                : (newLineages.Length + lostLineages.Length) / (double)(previous.Count + current.Count);
            var entropy = Entropy([.. groups.Select(static group => group.Count())], total);
            var largest = groups.Length == 0 ? 0d : groups[0].Count() / (double)total;
            var summary = new LineageSummary(
                groups.Length,
                founderLineages.Count,
                groups.Length == 0 ? 0d : founderLineages.Count / (double)groups.Length,
                gen0,
                total == 0 ? 0d : gen0 / (double)total,
                largest,
                entropy,
                newLineages.Length,
                newGen0.Count,
                lostLineages.Length,
                survival,
                turnover);
            results.Add(new SaveLineage(save.Id, save.Path, save.Time, save.Tick, summary));
            foreach (var group in groups)
            {
                var count = group.Count();
                var founderCreatures = group.Count(static creature => creature.Generation == 0);
                details.Add(new LineageDetail(
                    save.Id,
                    save.Path,
                    save.Time,
                    group.Key,
                    count,
                    total == 0 ? 0d : count / (double)total,
                    founderCreatures,
                    group.Min(static creature => creature.Generation),
                    group.Max(static creature => creature.Generation),
                    newLineages.Contains(group.Key),
                    newGen0.Contains(group.Key)));
            }

            ever.UnionWith(current);
            previous = current;
        }

        var finalSet = previous ?? [];
        var transitions = results
            .Where(static result => result.Summary.PriorLineageSurvival is not null)
            .ToArray();
        var sequence = new LineageSequenceSummary(
            ever.Count,
            results.Count == 0 ? 0 : results.Max(static result => result.Summary.Lineages),
            finalSet.Count,
            results.Sum(static result => result.Summary.NewLineages),
            results.Sum(static result => result.Summary.NewGen0Lineages),
            ever.Count(id => !finalSet.Contains(id)),
            transitions.Length == 0
                ? null
                : transitions.Average(static result => result.Summary.PriorLineageSurvival!.Value),
            transitions.Length == 0
                ? null
                : transitions.Average(static result => result.Summary.LineageTurnover));
        return new LineageAnalysis(results, details, sequence);
    }

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
}
