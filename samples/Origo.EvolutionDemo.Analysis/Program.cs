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
    private static int Main(string[] args)
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

    private static int GenomeCommand(string[] args)
    {
        var saves = new List<string>();
        var output = "genome-report.html";
        var maxCreatures = 5000;
        var seed = 1;
        var requestedK = 0;
        var mode = "all";
        var method = "kmeans";
        var stabilityRounds = 5;
        var dbscanEps = 0d;
        var dbscanMinPoints = 5;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--save" when i + 1 < args.Length:
                    saves.Add(args[++i]);
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--max" when i + 1 < args.Length:
                    maxCreatures = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--seed" when i + 1 < args.Length:
                    seed = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--k" when i + 1 < args.Length:
                    requestedK = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--mode" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--method" when i + 1 < args.Length:
                    method = args[++i];
                    break;
                case "--stability" when i + 1 < args.Length:
                    stabilityRounds = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--eps" when i + 1 < args.Length:
                    dbscanEps = double.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--min-points" when i + 1 < args.Length:
                    dbscanMinPoints = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown genome argument '{args[i]}'.");
            }
        }

        if (saves.Count == 0)
            throw new InvalidOperationException("genome requires at least one --save <path>.");
        var snapshots = SaveSnapshotReader.Read([.. saves]);
        var rows = FeatureExtractor.Extract(snapshots, maxCreatures, seed);
        if (rows.Count < 3)
            throw new InvalidOperationException("Fewer than three analyzable creatures.");
        var columns = rows.SelectMany(static row => row.Values.Keys)
            .Distinct(StringComparer.Ordinal)
            .Where(column => MatchesMode(column, mode))
            .OrderBy(static name => name, StringComparer.Ordinal).ToArray();
        if (columns.Length == 0)
            throw new InvalidOperationException($"No features match mode '{mode}'.");
        var raw = new FeatureMatrix(columns, [.. rows.Select(row =>
            columns.Select(column => row.Values.GetValueOrDefault(column, double.NaN)).ToArray())]);
        var (standardized, _, _, _) = Standardizer.Fit(raw);
        var projection = Pca.Project(standardized, 3, seed);
        var clustering = method switch
        {
            "jaccard" => JaccardClustering.Fit(
                rows.Select(static row => row.GeneSignatures).ToArray(), requestedK, seed),
            "dbscan" => Dbscan.Fit(standardized, dbscanEps, dbscanMinPoints, seed),
            _ => KMeans.Fit(standardized, requestedK, seed),
        };
        var stability = string.Equals(method, "kmeans", StringComparison.Ordinal)
            ? ClusterStability.Bootstrap(standardized, clustering.K, stabilityRounds, seed)
            : 0d;
        var report = GenomeHtmlReport.Build(snapshots, rows, columns, projection, clustering,
            stability, method);
        File.WriteAllText(output, report);
        WriteCsv(Path.ChangeExtension(output, ".clusters.csv"), rows, clustering);
        Console.WriteLine($"wrote {output} ({rows.Count} creatures, k={clustering.K}, silhouette={clustering.Silhouette:0.000})");
        return 0;
    }

    private static int CompareCommand(string[] args)
    {
        string? baseline = null;
        string? current = null;
        var output = "perf-compare.html";
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline" when i + 1 < args.Length:
                    baseline = args[++i];
                    break;
                case "--current" when i + 1 < args.Length:
                    current = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                default:
                    throw new InvalidOperationException($"Unknown compare argument '{args[i]}'.");
            }
        }

        if (baseline is null || current is null)
            throw new InvalidOperationException("compare requires --baseline and --current.");
        var previous = new PerfAggregator(ProfileReader.Read(baseline));
        var aggregate = new PerfAggregator(ProfileReader.Read(current));
        var deltas = PerfComparison.Compare(previous, aggregate);
        File.WriteAllText(output, PerfCompareHtmlReport.Build(baseline, current, deltas));
        Console.WriteLine($"wrote {output} ({deltas.Count} metrics)");
        return 0;
    }

    private static int PerfCommand(string[] args)
    {
        string? profile = null;
        var output = "perf-report.html";
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--profile" when i + 1 < args.Length:
                    profile = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                default:
                    throw new InvalidOperationException($"Unknown perf argument '{args[i]}'.");
            }
        }

        if (profile is null)
            throw new InvalidOperationException("perf requires --profile <profile.jsonl>.");
        var aggregate = new PerfAggregator(ProfileReader.Read(profile));
        File.WriteAllText(output, PerfHtmlReport.Build(aggregate));
        Console.WriteLine($"wrote {output} ({aggregate.Ticks.Count} samples)");
        return 0;
    }

    private static void WriteCsv(string path, IReadOnlyList<FeatureRow> rows, KMeans.Result clustering)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("name,save,lineage,generation,cluster");
        for (var i = 0; i < rows.Count; i++)
            writer.WriteLine(string.Join(',',
                Csv(rows[i].Name), Csv(rows[i].SavePath), rows[i].Lineage,
                rows[i].Generation, clustering.Labels[i]));
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
            || column.StartsWith("neutral.", StringComparison.Ordinal),
        _ => throw new InvalidOperationException(
            $"Unknown mode '{mode}'; expected all, genotype, phenotype, or behavior."),
    };

    private static string Csv(string text) =>
        text.Contains(',', StringComparison.Ordinal) ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  genome --save <save-dir-or-json> [--save ...] [--out report.html]");
        Console.WriteLine("         [--max 5000] [--seed 1] [--k 0(auto)]");
        Console.WriteLine("  perf --profile <profile.jsonl> [--out perf.html]");
        Console.WriteLine("  compare --baseline <baseline.jsonl> --current <current.jsonl> [--out compare.html]");
    }
}
