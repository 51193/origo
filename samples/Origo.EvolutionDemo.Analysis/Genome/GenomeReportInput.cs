using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Analysis.Math;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>One save as displayed in the report overview and the timeline CSV.</summary>
/// <param name="Id">Short save identifier (directory name, or JSON file name).</param>
/// <param name="Path">Full path of the parsed scene file.</param>
/// <param name="Time">Simulation time in seconds.</param>
/// <param name="Tick">Simulation tick stored by the save.</param>
/// <param name="Creatures">Creatures present in the save.</param>
/// <param name="Food">Food entities present in the save.</param>
/// <param name="Corpses">Corpse entities present in the save.</param>
/// <param name="FoodSources">Food-source entities present in the save.</param>
/// <param name="ControllerNumbers">Numeric controller counters from the save.</param>
/// <param name="ControllerStrings">String controller counters from the save.</param>
/// <param name="ControllerBooleans">Boolean controller values from the save.</param>
public sealed record SaveSummary(
    string Id,
    string Path,
    double Time,
    double Tick,
    int Creatures,
    int Food,
    int Corpses,
    int FoodSources,
    IReadOnlyDictionary<string, double> ControllerNumbers,
    IReadOnlyDictionary<string, string> ControllerStrings,
    IReadOnlyDictionary<string, bool> ControllerBooleans);

/// <summary>Resolved genome command options recorded in the report provenance.</summary>
/// <param name="Seed">Random seed for sampling, k-means/Jaccard restarts, and bootstrap ARI.</param>
/// <param name="MaxCreatures">Per-save row cap; zero analyzes every creature.</param>
/// <param name="Mode">Feature mode: all, genotype, phenotype, or behavior.</param>
/// <param name="Method">Clustering method: kmeans, jaccard, or dbscan.</param>
/// <param name="RequestedK">Requested k; zero means auto-k.</param>
/// <param name="StabilityRounds">Bootstrap ARI rounds; zero disables the metric.</param>
/// <param name="Eps">DBSCAN epsilon; zero estimates it.</param>
/// <param name="MinPoints">DBSCAN minimum points, counting the point itself.</param>
/// <param name="MinClusterSize">Auto-k minimum cluster size.</param>
/// <param name="AllowSkips">Whether undecodable creatures were allowed to be skipped.</param>
public sealed record GenomeReportOptions(
    int Seed,
    int MaxCreatures,
    string Mode,
    string Method,
    int RequestedK,
    int StabilityRounds,
    double Eps,
    int MinPoints,
    int MinClusterSize,
    bool AllowSkips);

/// <summary>
///     Everything one genome-analysis run reports, shared by the HTML report and
///     the machine-readable metrics JSON so both always show the same numbers.
/// </summary>
/// <param name="CommandLine">Command line that produced the run (provenance).</param>
/// <param name="GeneratedAtUtc">Wall-clock generation time (provenance).</param>
/// <param name="Options">Resolved command options.</param>
/// <param name="Saves">Save summaries ordered by simulation time, then save id.</param>
/// <param name="Rows">Analyzed creature rows (after sampling).</param>
/// <param name="TotalCreatures">Creatures present in the saves before sampling/skips.</param>
/// <param name="ConsideredCreatures">Parsed creatures attempted after sampling; excludes reader-level skips.</param>
/// <param name="SkippedCreatures">Creatures skipped by the reader or by feature extraction.</param>
/// <param name="ReaderWarnings">Save-reader warnings (integrity markers, ignored entities).</param>
/// <param name="RawColumns">Feature columns selected by the mode before constant-column drops.</param>
/// <param name="UsedFeatureCount">Feature columns that survived standardization.</param>
/// <param name="DroppedColumns">Constant feature columns dropped by standardization.</param>
/// <param name="Pca">PCA projection, components, and explained-variance ratios.</param>
/// <param name="Clustering">Chosen clustering solution.</param>
/// <param name="Candidates">Full auto-k candidate table; empty for fixed k and DBSCAN.</param>
/// <param name="AutoKMax">Auto-k search cap; zero when auto-k was not used.</param>
/// <param name="NoValidCandidate">True when no candidate satisfied the auto-k minimum-cluster-size floor.</param>
/// <param name="Stability">Bootstrap ARI summary, or null when the metric does not apply.</param>
/// <param name="SilhouetteSampleSize">Rows in the deterministic silhouette sample.</param>
/// <param name="Enrichment">Full per-cluster enrichment results (all tested signatures).</param>
/// <param name="Sampled">True when --max actually removed rows from at least one save.</param>
public sealed record GenomeReportInput(
    string CommandLine,
    DateTime GeneratedAtUtc,
    GenomeReportOptions Options,
    IReadOnlyList<SaveSummary> Saves,
    IReadOnlyList<FeatureRow> Rows,
    int TotalCreatures,
    int ConsideredCreatures,
    IReadOnlyList<SkippedCreature> SkippedCreatures,
    IReadOnlyList<string> ReaderWarnings,
    IReadOnlyList<string> RawColumns,
    int UsedFeatureCount,
    IReadOnlyList<string> DroppedColumns,
    PcaResult Pca,
    KMeans.Result Clustering,
    IReadOnlyList<ClusterCandidate> Candidates,
    int AutoKMax,
    bool NoValidCandidate,
    ClusterStability.BootstrapResult? Stability,
    int SilhouetteSampleSize,
    IReadOnlyList<EnrichmentAnalysis> Enrichment,
    bool Sampled);
