using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Analysis.Math;
using Origo.EvolutionDemo.Analysis.Perf;
using Origo.EvolutionDemo.Analysis.Reports;

namespace Origo.EvolutionDemo.Analysis;

internal static class Program
{
    private static readonly string[] ValidModes = ["all", "genotype", "phenotype", "behavior"];
    private static readonly string[] ValidMethods = ["kmeans", "jaccard", "dbscan"];

    internal static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 2;
            }

            return args[0] switch
            {
                "-h" or "--help" => PrintHelp(),
                "genome" => GenomeCommand([.. args.Skip(1)]),
                "perf" => PerfCommand([.. args.Skip(1)]),
                "compare" => CompareCommand([.. args.Skip(1)]),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static int PrintHelp()
    {
        PrintUsage();
        return 0;
    }

    private static int GenomeCommand(string[] args)
    {
        var saves = new List<string>();
        var output = "genome-report.html";
        var maxCreatures = 5000;
        var seed = 1;
        var requestedK = 0;
        var mode = "all";
        var method = "kmeans";
        var stabilityRounds = ClusterStability.DefaultRounds;
        var dbscanEps = 0d;
        var dbscanMinPoints = 5;
        var minClusterSize = 0;
        var allowSkips = false;
        var pool = false;
        string? timelineCsv = null;
        string? metricsJson = null;
        string? enrichmentCsv = null;
        string? featuresCsv = null;
        string? lineagesCsv = null;
        var timelinePercentiles = false;
        var explicitK = false;
        var explicitStability = false;
        var explicitEps = false;
        var explicitMinPoints = false;
        var explicitMinClusterSize = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    PrintGenomeUsage();
                    return 0;
                case "--save" when i + 1 < args.Length:
                    saves.Add(args[++i]);
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--max" when i + 1 < args.Length:
                    maxCreatures = ParseInt(args[++i], "--max");
                    break;
                case "--seed" when i + 1 < args.Length:
                    seed = ParseInt(args[++i], "--seed");
                    break;
                case "--k" when i + 1 < args.Length:
                    requestedK = ParseInt(args[++i], "--k");
                    explicitK = true;
                    break;
                case "--mode" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--method" when i + 1 < args.Length:
                    method = args[++i];
                    break;
                case "--stability" when i + 1 < args.Length:
                    stabilityRounds = ParseInt(args[++i], "--stability");
                    explicitStability = true;
                    break;
                case "--eps" when i + 1 < args.Length:
                    dbscanEps = ParseDouble(args[++i], "--eps");
                    explicitEps = true;
                    break;
                case "--min-points" when i + 1 < args.Length:
                    dbscanMinPoints = ParseInt(args[++i], "--min-points");
                    explicitMinPoints = true;
                    break;
                case "--min-cluster-size" when i + 1 < args.Length:
                    minClusterSize = ParseInt(args[++i], "--min-cluster-size");
                    explicitMinClusterSize = true;
                    break;
                case "--allow-skips":
                    allowSkips = true;
                    break;
                case "--pool":
                    pool = true;
                    break;
                case "--timeline-csv" when i + 1 < args.Length:
                    timelineCsv = args[++i];
                    break;
                case "--metrics-json" when i + 1 < args.Length:
                    metricsJson = args[++i];
                    break;
                case "--enrichment-csv" when i + 1 < args.Length:
                    enrichmentCsv = args[++i];
                    break;
                case "--features-csv" when i + 1 < args.Length:
                    featuresCsv = args[++i];
                    break;
                case "--lineages-csv" when i + 1 < args.Length:
                    lineagesCsv = args[++i];
                    break;
                case "--timeline-percentiles":
                    timelinePercentiles = true;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown or incomplete genome argument '{args[i]}'; run 'genome --help' for usage.");
            }
        }

        ValidateGenomeOptions(new GenomeOptionValidation(
            saves.Count, maxCreatures, requestedK, method, mode, stabilityRounds, dbscanEps,
            dbscanMinPoints, minClusterSize, explicitK, explicitStability, explicitEps,
            explicitMinPoints, explicitMinClusterSize));
        var snapshots = SaveSnapshotReader.Read([.. saves], pool);
        var ecology = SaveEcologyAnalyzer.Analyze(snapshots);
        var lineage = SaveLineageAnalyzer.Analyze(snapshots);
        var extraction = FeatureExtractor.Extract(snapshots, maxCreatures, seed);
        if (extraction.SkippedCreatures.Count > 0 && !allowSkips)
        {
            var first = extraction.SkippedCreatures[0];
            throw new InvalidOperationException(
                $"{extraction.SkippedCreatures.Count} of {extraction.TotalCreatures} creatures could not be analyzed "
                + $"(first: '{first.Name}' in '{first.SavePath}': {first.Reason}). "
                + "Pass --allow-skips to analyze the remaining creatures and report the skips.");
        }

        var rows = extraction.Rows;
        if (rows.Count < 3)
            throw new InvalidOperationException("Fewer than three analyzable creatures.");
        var columns = rows.SelectMany(static row => row.Values.Keys)
            .Distinct(StringComparer.Ordinal)
            .Where(column => MatchesMode(column, mode))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (columns.Length == 0)
            throw new InvalidOperationException($"No features match mode '{mode}'.");
        var raw = new FeatureMatrix(columns, [.. rows.Select(row =>
            columns.Select(column => RequireValue(row, column)).ToArray())]);
        var standardization = Standardizer.Fit(raw);
        if (standardization.Matrix.Columns.Count == 0)
            throw new InvalidOperationException(
                "Every feature column is constant or non-finite for this run; no clustering is possible.");
        var pca = Pca.Analyze(standardization.Matrix, 3);
        var floor = minClusterSize > 0 ? minClusterSize : KMeans.MinimumClusterSizeFor(rows.Count);
        var selection = method switch
        {
            "jaccard" => JaccardClustering.Select(
                [.. rows.Select(static row => row.GeneSignatures)], requestedK, seed, floor),
            "dbscan" => new ClusterSelection(Dbscan.Fit(standardization.Matrix, dbscanEps, dbscanMinPoints), [], 0, false),
            _ => KMeans.Select(standardization.Matrix, requestedK, seed, floor),
        };
        var clustering = selection.Best;
        var stability = string.Equals(method, "kmeans", StringComparison.Ordinal)
            ? ClusterStability.Bootstrap(standardization.Matrix, clustering, stabilityRounds, seed)
            : null;
        var enrichment = Enumerable.Range(0, clustering.K)
            .Select(cluster => GeneEnrichment.Analyze(rows, clustering.Labels, cluster))
            .ToArray();
        var saveSummaries = BuildSaveSummaries(snapshots);
        var sampled = snapshots.Any(snapshot =>
            maxCreatures > 0 && snapshot.Creatures.Count > maxCreatures);
        var reportInput = new GenomeReportInput(
            $"genome {string.Join(' ', args.Select(QuoteArgument))}",
            DateTime.UtcNow,
            new GenomeReportOptions(
                seed, maxCreatures, mode, method, requestedK, stabilityRounds, dbscanEps,
                dbscanMinPoints, floor, allowSkips),
            saveSummaries,
            ecology,
            lineage,
            rows,
            extraction.TotalCreatures,
            extraction.ConsideredCreatures,
            extraction.SkippedCreatures,
            [.. snapshots.SelectMany(static snapshot => snapshot.Warnings)],
            columns,
            standardization.Matrix.Columns.Count,
            standardization.DroppedColumns,
            pca,
            clustering,
            selection.Candidates,
            selection.MaxK,
            selection.NoValidCandidate,
            stability,
            string.Equals(method, "jaccard", StringComparison.Ordinal)
                ? Silhouette.JaccardSampleSize
                : Silhouette.EuclideanSampleSize,
            enrichment,
            sampled);
        WriteFile(output, GenomeHtmlReport.Build(reportInput));
        if (!string.IsNullOrWhiteSpace(timelineCsv))
            WriteTimelineCsv(timelineCsv, saveSummaries, ecology, lineage.Saves, rows, columns, timelinePercentiles);
        if (!string.IsNullOrWhiteSpace(featuresCsv))
            WriteFeaturesCsv(featuresCsv, rows, clustering);
        if (!string.IsNullOrWhiteSpace(lineagesCsv))
            WriteLineageCsv(lineagesCsv, lineage, saveSummaries);
        if (!string.IsNullOrWhiteSpace(metricsJson))
            WriteFile(metricsJson, GenomeMetricsJson.Build(reportInput));
        if (!string.IsNullOrWhiteSpace(enrichmentCsv))
            WriteEnrichmentCsv(enrichmentCsv, enrichment);
        WriteClustersCsv(Path.ChangeExtension(output, ".clusters.csv"), rows, clustering);
        foreach (var warning in GenomeWarnings.Build(reportInput))
            Console.Error.WriteLine($"WARNING: {warning}");
        var noiseCount = clustering.Labels.Count(static label => label < 0);
        var summary = $"wrote {output} ({rows.Count}/{extraction.TotalCreatures} creatures, method={method}, k={clustering.K}, "
            + $"silhouette={FormatValue(clustering.Silhouette, "0.000")}";
        if (stability is { } bootstrap)
            summary += $", k-bootstrap ARI={bootstrap.Mean:0.000}±{bootstrap.StandardDeviation:0.000}";
        if (noiseCount > 0)
            summary += $", noise={noiseCount}";
        summary += $", features={standardization.Matrix.Columns.Count}/{columns.Length}";
        if (clustering.Labels.Length > 0 && enrichment.Length > 0)
            summary += $", signatures tested={enrichment.Max(static analysis => analysis.TestedCount)}";
        summary += ")";
        Console.WriteLine(summary);
        return 0;
    }

    private static void ValidateGenomeOptions(GenomeOptionValidation validation)
    {
        if (validation.SaveCount == 0)
            throw new InvalidOperationException("genome requires at least one --save <path>.");
        if (validation.MaxCreatures < 0)
            throw new InvalidOperationException("--max must be >= 0 (0 analyzes every creature).");
        if (validation.RequestedK < 0)
            throw new InvalidOperationException("--k must be >= 0 (0 selects k automatically).");
        if (validation.StabilityRounds < 0)
            throw new InvalidOperationException("--stability must be >= 0.");
        if (validation.Eps < 0)
            throw new InvalidOperationException("--eps must be >= 0 (0 estimates epsilon from k-distances).");
        if (validation.MinPoints < 1)
            throw new InvalidOperationException("--min-points must be >= 1 (it includes the point itself).");
        if (validation.MinClusterSize != 0 && validation.MinClusterSize < 2)
            throw new InvalidOperationException("--min-cluster-size must be >= 2.");
        ValidateChoice(validation.Method, ValidMethods, "method");
        ValidateChoice(validation.Mode, ValidModes, "mode");
        var isDbscan = string.Equals(validation.Method, "dbscan", StringComparison.Ordinal);
        var isKMeans = string.Equals(validation.Method, "kmeans", StringComparison.Ordinal);
        if (isDbscan && validation.ExplicitK)
            throw new InvalidOperationException(
                "--k cannot be combined with --method dbscan (DBSCAN derives its own cluster count).");
        if (isDbscan && validation.ExplicitStability)
            throw new InvalidOperationException("--stability applies only to --method kmeans.");
        if (isDbscan && validation.ExplicitMinClusterSize)
            throw new InvalidOperationException("--min-cluster-size applies only to auto-k for kmeans/jaccard.");
        if (!isDbscan && validation.ExplicitEps)
            throw new InvalidOperationException("--eps applies only to --method dbscan.");
        if (!isDbscan && validation.ExplicitMinPoints)
            throw new InvalidOperationException("--min-points applies only to --method dbscan.");
        if (!isKMeans && validation.ExplicitStability)
            throw new InvalidOperationException("--stability applies only to --method kmeans.");
    }

    private static IReadOnlyList<SaveSummary> BuildSaveSummaries(IReadOnlyList<SaveSnapshot> snapshots)
    {
        var resolved = snapshots
            .OrderBy(static snapshot => snapshot.Time)
            .ThenBy(static snapshot => snapshot.Id, StringComparer.Ordinal)
            .ThenBy(static snapshot => snapshot.Path, StringComparer.Ordinal)
            .ToArray();
        var duplicates = resolved
            .GroupBy(static snapshot => snapshot.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        return [.. resolved.Select(snapshot =>
        {
            var id = snapshot.Id;
            if (duplicates.Contains(id))
            {
                counters[id] = counters.GetValueOrDefault(id) + 1;
                id = string.Create(CultureInfo.InvariantCulture, $"{id}#{counters[id]}");
            }

            return new SaveSummary(
                id,
                snapshot.Path,
                snapshot.Time,
                snapshot.Tick,
                snapshot.RawCreatureCount,
                snapshot.Food,
                snapshot.Corpses,
                snapshot.FoodSources,
                snapshot.ControllerNumbers,
                snapshot.ControllerStrings,
                snapshot.ControllerBooleans);
        })];
    }

    private static int CompareCommand(string[] args)
    {
        string? baseline = null;
        string? current = null;
        var output = "perf-compare.html";
        string? segment = null;
        double? fromTime = null;
        double? toTime = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    PrintCompareUsage();
                    return 0;
                case "--baseline" when i + 1 < args.Length:
                    baseline = args[++i];
                    break;
                case "--current" when i + 1 < args.Length:
                    current = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--segment" when i + 1 < args.Length:
                    segment = args[++i];
                    break;
                case "--from" when i + 1 < args.Length:
                    fromTime = ParseDouble(args[++i], "--from");
                    break;
                case "--to" when i + 1 < args.Length:
                    toTime = ParseDouble(args[++i], "--to");
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown or incomplete compare argument '{args[i]}'; run 'compare --help' for usage.");
            }
        }

        if (baseline is null || current is null)
            throw new InvalidOperationException("compare requires --baseline and --current.");
        ValidateWindow(fromTime, toTime, "compare");
        var baselineTicks = ProfileReader.Read(baseline);
        var currentTicks = ProfileReader.Read(current);
        var baselineSelection = ProfileSegments.Select(baselineTicks, baseline, segment, fromTime, toTime);
        var currentSelection = ProfileSegments.Select(currentTicks, current, segment, fromTime, toTime);
        var previous = new PerfAggregator(baselineSelection.Ticks);
        var aggregate = new PerfAggregator(currentSelection.Ticks);
        var comparison = PerfComparison.Compare(previous, aggregate);
        var warnings = new List<string>();
        warnings.AddRange(SelectionWarnings("baseline profile", baselineSelection, baselineTicks.Count, previous));
        warnings.AddRange(SelectionWarnings("current profile", currentSelection, currentTicks.Count, aggregate));
        warnings.AddRange(comparison.Warnings);
        WriteFile(output, PerfCompareHtmlReport.Build(
            new PerfCompareReportInput(baselineSelection, previous, currentSelection, aggregate, comparison, warnings)));
        WriteWarnings(warnings);
        var undefined = comparison.Rows.Count(static row => row.DeltaPercent is null);
        Console.WriteLine($"wrote {output} ({comparison.Rows.Count} metrics, {undefined} n/a)");
        return 0;
    }

    private static int PerfCommand(string[] args)
    {
        string? profile = null;
        var output = "perf-report.html";
        string? segment = null;
        double? fromTime = null;
        double? toTime = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    PrintPerfUsage();
                    return 0;
                case "--profile" when i + 1 < args.Length:
                    profile = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--segment" when i + 1 < args.Length:
                    segment = args[++i];
                    break;
                case "--from" when i + 1 < args.Length:
                    fromTime = ParseDouble(args[++i], "--from");
                    break;
                case "--to" when i + 1 < args.Length:
                    toTime = ParseDouble(args[++i], "--to");
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown or incomplete perf argument '{args[i]}'; run 'perf --help' for usage.");
            }
        }

        if (profile is null)
            throw new InvalidOperationException("perf requires --profile <profile.jsonl>.");
        ValidateWindow(fromTime, toTime, "perf");
        var ticks = ProfileReader.Read(profile);
        var selection = ProfileSegments.Select(ticks, profile, segment, fromTime, toTime);
        var aggregate = new PerfAggregator(selection.Ticks);
        var warnings = SelectionWarnings("profile", selection, ticks.Count, aggregate);
        WriteFile(output, PerfHtmlReport.Build(aggregate, selection, warnings));
        WriteWarnings(warnings);
        var summary = $"wrote {output} ({aggregate.SampleCount} samples";
        if (selection.SegmentCount > 1)
            summary += $", segments={selection.SegmentCount}, segment={selection.SegmentLabel}";
        summary += ")";
        Console.WriteLine(summary);
        return 0;
    }

    private static void ValidateWindow(double? fromTime, double? toTime, string command)
    {
        if (fromTime.HasValue && toTime.HasValue && fromTime > toTime)
            throw new InvalidOperationException($"{command} --from must not be greater than --to.");
    }

    private static List<string> SelectionWarnings(
        string label,
        ProfileSelection selection,
        int totalSamples,
        PerfAggregator aggregate)
    {
        var warnings = new List<string>();
        if (selection.ImplicitLast)
            warnings.Add(
                $"{label} has {selection.SegmentCount} simulation segments; aggregated the last one "
                + $"({selection.Ticks.Count} of {totalSamples} samples). Pass --segment all to pool segments "
                + "or --segment <index> to select one.");
        if (aggregate.SampleCount < 100)
            warnings.Add(
                $"{label} selection contains {aggregate.SampleCount} sample(s); p95/p99 percentile estimates "
                + "are unstable below 100 samples.");
        if (aggregate.TickInterval is { } interval && !aggregate.HasUniformTickInterval)
            warnings.Add(
                $"{label} sampled tick deltas are not uniform (median interval {interval}); the GC run-total "
                + "estimate assumes uniform sampling.");
        return warnings;
    }

    private static void WriteWarnings(IReadOnlyList<string> warnings)
    {
        foreach (var warning in warnings)
            Console.Error.WriteLine($"WARNING: {warning}");
    }

    private static readonly string[] TimelinePercentileColumns =
    [
        "behavior.age", "behavior.energyRatio", "behavior.kills", "behavior.children",
        "behavior.generation", "diet.totalMeals", "diet.entropy", "body.bodyRadius",
        "body.moveSpeed", "body.senseRange", "body.maxAge",
    ];

    private static void WriteTimelineCsv(
        string path,
        IReadOnlyList<SaveSummary> saves,
        IReadOnlyList<SaveEcology> ecology,
        IReadOnlyList<SaveLineage> lineage,
        IReadOnlyList<FeatureRow> rows,
        string[] columns,
        bool includePercentiles)
    {
        var controllerKeys = saves
            .SelectMany(static save => save.ControllerNumbers.Keys
                .Concat(save.ControllerStrings.Keys)
                .Concat(save.ControllerBooleans.Keys))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        var bySave = rows.GroupBy(static row => row.SavePath, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        EnsureDirectory(path);
        var ecologyByPath = ecology.ToDictionary(static item => item.Path, StringComparer.Ordinal);
        var lineageByPath = lineage.ToDictionary(static item => item.Path, StringComparer.Ordinal);
        var percentileColumns = includePercentiles
            ? columns.Where(column => TimelinePercentileColumns.Contains(column, StringComparer.Ordinal)).ToArray()
            : [];
        using var writer = new StreamWriter(path);
        writer.Write("save,time,tick,creatures,analyzed,food,corpses,food_sources");
        writer.Write(",food.soft,food.tough,food.toxic,food.corpse");
        writer.Write(",patch.count,patch.radius,patch.coverage");
        writer.Write(",origin.founderShare,origin.meanGeneration,origin.maxGeneration");
        writer.Write(",access.foodInPickupShare,access.foodInSenseShare,access.foodIn300Share");
        writer.Write(",access.meanFoodInSense,access.meanFoodIn300,access.nearestFoodP50,access.nearestSourceP50");
        writer.Write(",diet.living.soft,diet.living.tough,diet.living.toxic,diet.living.corpseOfSoft");
        writer.Write(",diet.experienced.specialistShare60,diet.experienced.entropy");
        writer.Write(",diet.controller.soft,diet.controller.tough,diet.controller.toxic,diet.controller.corpseOfSoft");
        writer.Write(",lineage.count,lineage.founderLineages,lineage.founderLineageShare,lineage.gen0Share");
        writer.Write(",lineage.largestShare,lineage.entropy,lineage.new,lineage.lost,lineage.newGen0");
        writer.Write(",lineage.priorSurvival,lineage.turnover");
        writer.Write(",delta.population,delta.births,delta.kills,delta.enrichmentSpawns");
        writer.Write(",delta.deathsEstimated,delta.predationShareEstimated");
        foreach (var key in controllerKeys)
            writer.Write(",{0}", Csv("controller." + key));
        foreach (var column in columns)
            writer.Write(",{0}", Csv(column + ".mean"));
        foreach (var column in percentileColumns)
            foreach (var suffix in new[] { "p50", "p90", "p95" })
                writer.Write(",{0}", Csv(column + "." + suffix));
        writer.WriteLine();
        for (var index = 0; index < saves.Count; index++)
        {
            var save = saves[index];
            var saveEcology = ecologyByPath[save.Path];
            var saveLineage = lineageByPath[save.Path].Summary;
            var saveRows = bySave.GetValueOrDefault(save.Path, []);
            writer.Write(Csv(save.Id));
            writer.Write(',');
            writer.Write(save.Time.ToString("0.###", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(save.Tick.ToString("R", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(save.Creatures.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(saveRows.Length.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(save.Food.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(save.Corpses.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(save.FoodSources.ToString(CultureInfo.InvariantCulture));
            WriteCsvNumber(writer, saveEcology.Food.Soft);
            WriteCsvNumber(writer, saveEcology.Food.Tough);
            WriteCsvNumber(writer, saveEcology.Food.Toxic);
            WriteCsvNumber(writer, saveEcology.Food.Corpse);
            WriteCsvNumber(writer, saveEcology.LivePatchCount);
            WriteCsvNumber(writer, saveEcology.PatchRadius);
            WriteCsvNumber(writer, saveEcology.PatchCoverage);
            WriteCsvNumber(writer, saveEcology.FounderShare);
            WriteCsvNumber(writer, saveEcology.MeanGeneration);
            WriteCsvNumber(writer, saveEcology.MaxGeneration);
            WriteCsvNumber(writer, saveEcology.Access.FractionInPickup);
            WriteCsvNumber(writer, saveEcology.Access.FractionInSense);
            WriteCsvNumber(writer, saveEcology.Access.FractionIn300);
            WriteCsvNumber(writer, saveEcology.Access.MeanFoodInSense);
            WriteCsvNumber(writer, saveEcology.Access.MeanFoodIn300);
            WriteCsvNumber(writer, saveEcology.Access.NearestFoodP50);
            WriteCsvNumber(writer, saveEcology.Access.NearestSourceP50);
            WriteCsvNumber(writer, saveEcology.LivingDiet.Soft);
            WriteCsvNumber(writer, saveEcology.LivingDiet.Tough);
            WriteCsvNumber(writer, saveEcology.LivingDiet.Toxic);
            WriteCsvNumber(writer, saveEcology.LivingDiet.CorpseOfSoft);
            WriteCsvNumber(writer, saveEcology.SpecialistShare60);
            WriteCsvNumber(writer, saveEcology.MeanExperiencedEntropy);
            WriteCsvNumber(writer, saveEcology.ControllerDiet?.Soft);
            WriteCsvNumber(writer, saveEcology.ControllerDiet?.Tough);
            WriteCsvNumber(writer, saveEcology.ControllerDiet?.Toxic);
            WriteCsvNumber(writer, saveEcology.ControllerDiet?.CorpseOfSoft);
            WriteCsvNumber(writer, saveLineage.Lineages);
            WriteCsvNumber(writer, saveLineage.FounderLineages);
            WriteCsvNumber(writer, saveLineage.FounderLineageShare);
            WriteCsvNumber(writer, saveLineage.Gen0Share);
            WriteCsvNumber(writer, saveLineage.LargestLineageShare);
            WriteCsvNumber(writer, saveLineage.LineageEntropy);
            WriteCsvNumber(writer, saveLineage.NewLineages);
            WriteCsvNumber(writer, saveLineage.LostLineages);
            WriteCsvNumber(writer, saveLineage.NewGen0Lineages);
            WriteCsvNumber(writer, saveLineage.PriorLineageSurvival);
            WriteCsvNumber(writer, saveLineage.LineageTurnover);
            WriteDeltaColumns(writer, saves, index);
            foreach (var key in controllerKeys)
            {
                writer.Write(',');
                if (save.ControllerNumbers.TryGetValue(key, out var number))
                    writer.Write(number.ToString("R", CultureInfo.InvariantCulture));
                else if (save.ControllerStrings.TryGetValue(key, out var text))
                    writer.Write(Csv(text));
                else if (save.ControllerBooleans.TryGetValue(key, out var boolean))
                    writer.Write(boolean ? "true" : "false");
            }

            foreach (var column in columns)
            {
                writer.Write(',');
                var values = saveRows
                    .Select(row => row.Values.GetValueOrDefault(column, double.NaN))
                    .Where(double.IsFinite)
                    .ToArray();
                if (values.Length > 0)
                    writer.Write(values.Average().ToString("0.####", CultureInfo.InvariantCulture));
            }

            foreach (var column in percentileColumns)
            {
                var values = saveRows
                    .Select(row => row.Values.GetValueOrDefault(column, double.NaN))
                    .Where(double.IsFinite)
                    .OrderBy(static value => value)
                    .ToArray();
                foreach (var fraction in new[] { 0.50d, 0.90d, 0.95d })
                {
                    writer.Write(',');
                    if (values.Length > 0)
                        writer.Write(Percentile(values, fraction).ToString("0.####", CultureInfo.InvariantCulture));
                }
            }

            writer.WriteLine();
        }

        var percentileSummary = includePercentiles
            ? $", {percentileColumns.Length * 3} percentile columns"
            : string.Empty;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"wrote {path} ({saves.Count} saves, {controllerKeys.Length} controller counters, {columns.Length} feature means{percentileSummary})"));
    }

    private static void WriteDeltaColumns(
        TextWriter writer, IReadOnlyList<SaveSummary> saves, int index)
    {
        if (index == 0)
        {
            writer.Write(",,,,,,");
            return;
        }

        var previous = saves[index - 1];
        var current = saves[index];
        var births = TryDelta(current.ControllerNumbers, previous.ControllerNumbers, "evo.births");
        var kills = TryDelta(current.ControllerNumbers, previous.ControllerNumbers, "evo.spikeKills");
        var enrichment = TryDelta(current.ControllerNumbers, previous.ControllerNumbers, "evo.enrichmentSpawns");
        var population = current.Creatures - previous.Creatures;
        writer.Write(',');
        writer.Write(population.ToString(CultureInfo.InvariantCulture));
        WriteCsvValue(writer, births?.ToString("0.###", CultureInfo.InvariantCulture));
        WriteCsvValue(writer, kills?.ToString("0.###", CultureInfo.InvariantCulture));
        WriteCsvValue(writer, enrichment?.ToString("0.###", CultureInfo.InvariantCulture));
        if (births is { } birthDelta && kills is { } killDelta)
        {
            var recruits = birthDelta + (enrichment ?? 0d);
            var deaths = recruits - population;
            WriteCsvValue(writer, deaths.ToString("0.###", CultureInfo.InvariantCulture));
            WriteCsvValue(writer, deaths > 0d
                ? (killDelta / deaths).ToString("0.######", CultureInfo.InvariantCulture)
                : string.Empty);
        }
        else
        {
            writer.Write(",,");
        }
    }

    private static double? TryDelta(
        IReadOnlyDictionary<string, double> current,
        IReadOnlyDictionary<string, double> previous,
        string key) =>
        current.TryGetValue(key, out var now)
            ? now - (previous.TryGetValue(key, out var before) ? before : 0d)
            : null;

    private static void WriteLineageCsv(
        string path, LineageAnalysis analysis, IReadOnlyList<SaveSummary> saves)
    {
        EnsureDirectory(path);
        var displayIds = saves.ToDictionary(static save => save.Path, static save => save.Id, StringComparer.Ordinal);
        using var writer = new StreamWriter(path);
        writer.WriteLine("save,time,lineage,creatures,share,founderCreatures,minGeneration,maxGeneration,isNew,isNewGen0");
        foreach (var detail in analysis.Details
            .OrderBy(static item => item.Time)
            .ThenBy(static item => item.SaveId, StringComparer.Ordinal)
            .ThenByDescending(static item => item.Creatures)
            .ThenBy(static item => item.Lineage))
        {
            writer.Write(Csv(displayIds.GetValueOrDefault(detail.Path, detail.SaveId)));
            writer.Write(',');
            writer.Write(detail.Time.ToString("0.###", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(detail.Lineage.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(detail.Creatures.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(detail.Share.ToString("0.######", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(detail.FounderCreatures.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(detail.MinGeneration.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(detail.MaxGeneration.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(detail.IsNew ? "1" : "0");
            writer.Write(',');
            writer.WriteLine(detail.IsNewGen0 ? "1" : "0");
        }
    }

    private static void WriteCsvNumber(TextWriter writer, int? value) =>
        WriteCsvValue(writer, value?.ToString(CultureInfo.InvariantCulture));

    private static void WriteCsvNumber(TextWriter writer, double? value) =>
        WriteCsvValue(writer, value is { } number && double.IsFinite(number)
            ? number.ToString("0.######", CultureInfo.InvariantCulture)
            : string.Empty);

    private static void WriteCsvValue(TextWriter writer, string? value)
    {
        writer.Write(',');
        writer.Write(value ?? string.Empty);
    }

    private static double Percentile(double[] sorted, double fraction)
    {
        var position = (sorted.Length - 1) * fraction;
        var lower = (int)System.Math.Floor(position);
        var upper = System.Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }

    private static void WriteFeaturesCsv(string path, IReadOnlyList<FeatureRow> rows, KMeans.Result clustering)
    {
        EnsureDirectory(path);
        var columns = rows.SelectMany(static row => row.Values.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        using var writer = new StreamWriter(path);
        writer.Write("name,save,time,lineage,generation,cluster");
        foreach (var column in columns)
            writer.Write(",{0}", Csv(column));
        writer.WriteLine();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            writer.Write(Csv(row.Name));
            writer.Write(',');
            writer.Write(Csv(row.SavePath));
            writer.Write(',');
            writer.Write(row.SaveTime.ToString("0.###", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(row.Lineage.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(row.Generation.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(clustering.Labels[index].ToString(CultureInfo.InvariantCulture));
            foreach (var column in columns)
            {
                writer.Write(',');
                writer.Write(row.Values.GetValueOrDefault(column, double.NaN)
                    .ToString("R", CultureInfo.InvariantCulture));
            }
            writer.WriteLine();
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"wrote {path} ({rows.Count} creatures, {columns.Length} feature columns)"));
    }

    private static void WriteEnrichmentCsv(string path, EnrichmentAnalysis[] enrichment)
    {
        EnsureDirectory(path);
        using var writer = new StreamWriter(path);
        writer.WriteLine("cluster,signature,cluster_with,cluster_total,rest_with,rest_total,odds,p_value,q_value");
        var rows = enrichment
            .SelectMany(static analysis => analysis.Signatures)
            .OrderBy(static result => result.Cluster)
            .ThenBy(static result => result.QValue)
            .ThenBy(static result => result.PValue)
            .ThenBy(static result => result.Signature, StringComparer.Ordinal);
        var count = 0;
        foreach (var result in rows)
        {
            writer.Write(string.Create(CultureInfo.InvariantCulture,
                $"{result.Cluster},{Csv(result.Signature)},{result.ClusterWith},{result.ClusterTotal},{result.RestWith},{result.RestTotal},{result.OddsRatio:R},{result.PValue:R},{result.QValue:R}"));
            writer.WriteLine();
            count++;
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"wrote {path} ({count} tested signatures across {enrichment.Length} clusters)"));
    }

    private static void WriteClustersCsv(string path, IReadOnlyList<FeatureRow> rows, KMeans.Result clustering)
    {
        EnsureDirectory(path);
        using var writer = new StreamWriter(path);
        writer.WriteLine("name,save,lineage,generation,cluster");
        for (var index = 0; index < rows.Count; index++)
            writer.WriteLine(string.Join(',',
                Csv(rows[index].Name), Csv(rows[index].SavePath), rows[index].Lineage,
                rows[index].Generation, clustering.Labels[index]));
    }

    private static void WriteFile(string path, string content)
    {
        EnsureDirectory(path);
        File.WriteAllText(path, content);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    private static double RequireValue(FeatureRow row, string column)
    {
        if (!row.Values.TryGetValue(column, out var value))
            throw new InvalidDataException(
                $"Creature '{row.Name}' in save '{row.SavePath}' has no feature value for column '{column}'.");
        if (!double.IsFinite(value))
            throw new InvalidDataException(
                $"Creature '{row.Name}' in save '{row.SavePath}' has a non-finite value for feature '{column}'.");
        return value;
    }

    private static bool MatchesMode(string column, string mode) => mode switch
    {
        "all" => true,
        "genotype" => column.StartsWith("gene.", StringComparison.Ordinal)
            || column.StartsWith("organ.", StringComparison.Ordinal)
            || column.StartsWith("neural.", StringComparison.Ordinal)
            || column.StartsWith("ports.", StringComparison.Ordinal)
            || column.StartsWith("energy.", StringComparison.Ordinal),
        "phenotype" => column.StartsWith("body.", StringComparison.Ordinal)
            || column.StartsWith("trait.", StringComparison.Ordinal)
            || column.StartsWith("ports.", StringComparison.Ordinal)
            || column.StartsWith("organ.", StringComparison.Ordinal),
        "behavior" => column.StartsWith("behavior.", StringComparison.Ordinal)
            || column.StartsWith("diet.", StringComparison.Ordinal)
            || column.StartsWith("access.", StringComparison.Ordinal)
            || column.StartsWith("neutral.", StringComparison.Ordinal),
        _ => throw new InvalidOperationException($"Unknown mode '{mode}'."),
    };

    private static void ValidateChoice(string value, string[] valid, string option)
    {
        if (!valid.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Unknown {option} '{value}'; expected {string.Join(", ", valid)}.");
    }

    private static int ParseInt(string text, string option) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"{option} requires an integer, got '{text}'.");

    private static double ParseDouble(string text, string option) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            ? value
            : throw new InvalidOperationException($"{option} requires a finite number, got '{text}'.");

    private static string FormatValue(double? value, string format) =>
        value is { } number && double.IsFinite(number)
            ? number.ToString(format, CultureInfo.InvariantCulture)
            : "n/a";

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal)
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;

    private static string Csv(string text) =>
        text.AsSpan().IndexOfAny(",\"\n\r") >= 0
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  genome --save <save-dir-or-json> [--save ...] [--out report.html] [options]");
        Console.WriteLine("  perf --profile <profile.jsonl> [--out perf.html] [--segment all|last|<index>] [--from <simsec>] [--to <simsec>]");
        Console.WriteLine("  compare --baseline <baseline.jsonl> --current <current.jsonl> [--out compare.html] [options]");
        Console.WriteLine("Run 'genome --help', 'perf --help', or 'compare --help' for the command options.");
    }

    private static void PrintPerfUsage()
    {
        Console.WriteLine("Usage: perf --profile <profile.jsonl> [--out perf-report.html] [options]");
        Console.WriteLine();
        Console.WriteLine("  --profile <path>         JSONL profile written by SimulationProfiler (schema 2).");
        Console.WriteLine("  --out <path>             HTML report (default perf-report.html).");
        Console.WriteLine("  --segment all|last|<n>   simulation segment selection, 0-based (default last).");
        Console.WriteLine("                           A new segment starts when simTime decreases; 'all' pools them.");
        Console.WriteLine("  --from <simsec>          inclusive lower simTime bound within the selected segment.");
        Console.WriteLine("  --to <simsec>            inclusive upper simTime bound within the selected segment.");
        Console.WriteLine("  -h, --help               Show this help.");
    }

    private static void PrintCompareUsage()
    {
        Console.WriteLine("Usage: compare --baseline <baseline.jsonl> --current <current.jsonl> [options]");
        Console.WriteLine();
        Console.WriteLine("  --baseline <path>        baseline JSONL profile (schema 2).");
        Console.WriteLine("  --current <path>         current JSONL profile (schema 2).");
        Console.WriteLine("  --out <path>             HTML report (default perf-compare.html).");
        Console.WriteLine("  --segment all|last|<n>   simulation segment selection for both profiles, 0-based (default last).");
        Console.WriteLine("  --from <simsec>          inclusive lower simTime bound within the selected segment.");
        Console.WriteLine("  --to <simsec>            inclusive upper simTime bound within the selected segment.");
        Console.WriteLine("  -h, --help               Show this help.");
    }

    private static void PrintGenomeUsage()
    {
        Console.WriteLine("Usage: genome --save <save-dir-or-json> [--save ...] [options]");
        Console.WriteLine();
        Console.WriteLine("  --save <path>            v7 save root, scene directory, or snd_scene.json; repeat to pool.");
        Console.WriteLine("  --pool                   allow one --save directory to expand to several save scenes.");
        Console.WriteLine("  --out <path>             HTML report (default genome-report.html).");
        Console.WriteLine("                           A sibling .clusters.csv is always written.");
        Console.WriteLine("  --max <n>                Per-save analyzed row cap; 0 analyzes every creature (default 5000).");
        Console.WriteLine("  --seed <n>               Seed for sampling, k-means/Jaccard restarts, and bootstrap (default 1).");
        Console.WriteLine("  --k <n>                  Fixed cluster count; 0 auto-selects k (default 0).");
        Console.WriteLine("  --mode all|genotype|phenotype|behavior   Feature selection (default all).");
        Console.WriteLine("  --method kmeans|jaccard|dbscan           Clustering method (default kmeans).");
        Console.WriteLine("  --stability <n>          True bootstrap ARI rounds, kmeans only (default 5; 0 = n/a).");
        Console.WriteLine("  --eps <x>                DBSCAN epsilon; 0 estimates it from k-distances.");
        Console.WriteLine("  --min-points <n>         DBSCAN minimum points, counting the point itself (default 5).");
        Console.WriteLine("  --min-cluster-size <n>   Auto-k validity floor; default max(2, ceil(0.01 * rows)).");
        Console.WriteLine("  --allow-skips            Analyze the rest when creatures have undecodable genomes.");
        Console.WriteLine("  --timeline-csv <path>    One row per save: population, counters, feature means.");
        Console.WriteLine("  --metrics-json <path>    Machine-readable metrics (same numbers as the HTML report).");
        Console.WriteLine("  --enrichment-csv <path>  All tested signatures with p/q values for FDR auditing.");
        Console.WriteLine("  --features-csv <path>    One row per analyzed creature with every feature value and cluster.");
        Console.WriteLine("  --lineages-csv <path>    One row per living lineage per save with turnover flags.");
        Console.WriteLine("  --timeline-percentiles   Add p50/p90/p95 columns for key features to the timeline CSV.");
        Console.WriteLine("  -h, --help               Show this help.");
    }

    private sealed record GenomeOptionValidation(
        int SaveCount,
        int MaxCreatures,
        int RequestedK,
        string Method,
        string Mode,
        int StabilityRounds,
        double Eps,
        int MinPoints,
        int MinClusterSize,
        bool ExplicitK,
        bool ExplicitStability,
        bool ExplicitEps,
        bool ExplicitMinPoints,
        bool ExplicitMinClusterSize);
}
