using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>Cumulative births for one root lineage.</summary>
/// <param name="SaveId">Short save identifier.</param>
/// <param name="Path">Scene file path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="Tick">Simulation tick.</param>
/// <param name="BirthsByRoot">Cumulative births per root, or null when absent.</param>
public sealed record SaveBirths(
    string SaveId,
    string Path,
    double Time,
    double Tick,
    IReadOnlyDictionary<int, int>? BirthsByRoot);

/// <summary>Latest root birth totals and concentration.</summary>
/// <param name="Roots">Roots with at least one birth.</param>
/// <param name="TotalBirths">Total tracked births.</param>
/// <param name="TopRoot">Root with the most births, or null.</param>
/// <param name="TopRootBirths">Births under the top root, or null.</param>
/// <param name="TopRootShare">Top-root births divided by total births, or null.</param>
public sealed record RootBirthsSummary(
    int Roots,
    int TotalBirths,
    int? TopRoot,
    int? TopRootBirths,
    double? TopRootShare);

/// <summary>Per-save births and latest cumulative summary.</summary>
/// <param name="Saves">Birth records in simulation order.</param>
/// <param name="Latest">Latest cumulative births by root, or null.</param>
/// <param name="Summary">Latest root birth summary, or null.</param>
public sealed record BirthsAnalysis(
    IReadOnlyList<SaveBirths> Saves,
    IReadOnlyDictionary<int, int>? Latest,
    RootBirthsSummary? Summary);

/// <summary>Reads cumulative births by root from controller strings.</summary>
public static class BirthsAnalyzer
{
    /// <summary>Analyzes saves in simulation-time order.</summary>
    /// <exception cref="ArgumentNullException">The save list is null.</exception>
    public static BirthsAnalysis Analyze(IReadOnlyList<SaveSnapshot> saves)
    {
        ArgumentNullException.ThrowIfNull(saves);
        var ordered = saves
            .OrderBy(static save => save.Time)
            .ThenBy(static save => save.Id, StringComparer.Ordinal)
            .ThenBy(static save => save.Path, StringComparer.Ordinal)
            .ToArray();
        var results = new List<SaveBirths>(ordered.Length);
        IReadOnlyDictionary<int, int>? latest = null;
        foreach (var save in ordered)
        {
            var births = TryRead(save);
            results.Add(new SaveBirths(save.Id, save.Path, save.Time, save.Tick, births));
            if (births is not null)
                latest = births;
        }

        if (latest is null)
            return new BirthsAnalysis(results, null, null);
        var summary = Summarize(latest);
        return new BirthsAnalysis(results, latest, summary);
    }

    private static IReadOnlyDictionary<int, int>? TryRead(SaveSnapshot save)
    {
        if (!save.ControllerStrings.TryGetValue("evo.birthsByRoot", out var text)
            || string.IsNullOrWhiteSpace(text))
            return null;
        return RootBirthCodec.Parse(text);
    }

    private static RootBirthsSummary Summarize(IReadOnlyDictionary<int, int> births)
    {
        var total = births.Values.Sum();
        if (births.Count == 0)
            return new RootBirthsSummary(0, 0, null, null, null);
        var (root, count) = births.MaxBy(static pair => pair.Value);
        return new RootBirthsSummary(
            births.Count,
            total,
            root,
            count,
            total > 0 ? count / (double)total : null);
    }
}
