using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Math;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>
///     Builds the shared warning list for one genome-analysis run. The HTML report
///     and the CLI stderr output both render this list so a warning can never
///     appear in only one place.
/// </summary>
public static class GenomeWarnings
{
    /// <summary>Returns the deterministic warnings for <paramref name="input" />.</summary>
    public static IReadOnlyList<string> Build(GenomeReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var warnings = new List<string>(input.ReaderWarnings);
        foreach (var warning in input.Ecology
            .SelectMany(static ecology => ecology.Warnings)
            .Distinct(StringComparer.Ordinal))
            warnings.Add(warning);
        var founderShare = input.Ecology.Count == 0
            ? 0d
            : input.Ecology.Max(static ecology => ecology.FounderShare);
        if (founderShare >= 0.05d)
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"{founderShare:0.0%} of the parsed creatures are generation 0; founder/enrichment injection makes generation, lineage, and cohort-based cluster differences non-evolutionary."));
        if (input.Enrichment.Count > 0
            && (string.Equals(input.Options.Mode, "genotype", StringComparison.Ordinal)
                || string.Equals(input.Options.Mode, "all", StringComparison.Ordinal)))
            warnings.Add(
                $"mode '{input.Options.Mode}' clusters on gene/neural/organ features, so gene-signature enrichment describes the features that define the clusters; use the phenotype or behavior mode for an independent gene test.");
        if (input.Sampled)
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"--max {input.Options.MaxCreatures} sampled {input.ConsideredCreatures} of {input.TotalCreatures} save creatures; the analyzed rows are a sample, not the full population."));
        if (input.SkippedCreatures.Count > 0)
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"analyzed {input.Rows.Count} of {input.TotalCreatures} save creatures: {input.SkippedCreatures.Count} skipped (--allow-skips)."));
        if (input.NoValidCandidate)
        {
            var rejected = input.Candidates
                .Where(static candidate => !candidate.Flags.Contains("accepted", StringComparer.Ordinal))
                .Select(static candidate => string.Create(CultureInfo.InvariantCulture,
                    $"k={candidate.K} sizes=[{string.Join(",", candidate.Sizes)}]"))
                .ToArray();
            warnings.Add(
                $"no auto-k candidate satisfies the minimum cluster size {input.Options.MinClusterSize}; "
                + $"the maximum-silhouette candidate k={input.Clustering.K} was selected anyway "
                + $"({string.Join("; ", rejected)}). Treat the cluster structure as unsupported.");
        }
        else if (input.AutoKMax > 0)
        {
            var rejected = input.Candidates
                .Where(static candidate => !candidate.Flags.Contains("accepted", StringComparer.Ordinal))
                .Select(static candidate => string.Create(CultureInfo.InvariantCulture,
                    $"k={candidate.K} sizes=[{string.Join(",", candidate.Sizes)}]"))
                .ToArray();
            if (rejected.Length > 0)
                warnings.Add($"auto-k rejected candidate(s) below minimum cluster size {input.Options.MinClusterSize}: {string.Join("; ", rejected)}.");
        }

        if (input.Clustering.Silhouette is { } silhouette
            && silhouette < KMeans.WeakStructureThreshold
            && input.Clustering.K > 1)
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"silhouette {silhouette:0.000} is below {KMeans.WeakStructureThreshold:0.00}: there is no substantial cluster structure, so the reported k must not be read as stable structure."));
        // The minimum-cluster-size floor is an auto-k validity constraint; DBSCAN
        // derives its own clusters and is reported through the noise warning instead.
        if (!string.Equals(input.Options.Method, "dbscan", StringComparison.Ordinal))
        {
            var sizes = ClusterSizes(input.Clustering);
            var small = Enumerable.Range(0, input.Clustering.K).Where(cluster => sizes[cluster] < input.Options.MinClusterSize).ToArray();
            if (small.Length > 0)
                warnings.Add(string.Create(CultureInfo.InvariantCulture,
                    $"cluster(s) below the minimum cluster size {input.Options.MinClusterSize}: {string.Join(", ", small.Select(cluster => $"c{cluster} n={sizes[cluster]}"))}."));
        }
        if (input.AutoKMax > 0 && input.Clustering.K == input.AutoKMax)
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"selected k={input.Clustering.K} equals the auto-k search cap ({input.AutoKMax}); the best k may lie above the searched range."));
        var noise = input.Clustering.Labels.Count(static label => label < 0);
        if (noise > 0)
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"DBSCAN labelled {noise} of {input.Rows.Count} rows as noise (-1); noise is excluded from k, silhouette, cluster tables, and enrichment."));
        return warnings;
    }

    private static int[] ClusterSizes(KMeans.Result clustering)
    {
        var sizes = new int[clustering.K];
        foreach (var label in clustering.Labels)
            if (label >= 0 && label < sizes.Length)
                sizes[label]++;
        return sizes;
    }
}
