using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Origo.EvolutionDemo.Analysis.Batch;

/// <summary>Comparable summary of one analyzed run's metrics JSON.</summary>
/// <param name="Source">Source metrics file.</param>
/// <param name="Seed">Simulation seed.</param>
/// <param name="FinalPopulation">Final living creatures.</param>
/// <param name="Regime">High above 1,000, otherwise low.</param>
/// <param name="MutationLineages">Final living mutation lineages.</param>
/// <param name="RootLineages">Final living root lineages.</param>
/// <param name="RootGen0">Final living generation-0 root lineages.</param>
/// <param name="Enrichment">Final living enrichment founders.</param>
/// <param name="Deaths">Tracked cumulative deaths.</param>
/// <param name="PredationShare">Predation share of deaths.</param>
/// <param name="StarvationShare">Starvation share of deaths.</param>
/// <param name="AgingShare">Aging share of deaths.</param>
/// <param name="RootsWithDeaths">Roots with at least one death.</param>
/// <param name="TopRoot">Root with the most deaths.</param>
/// <param name="TopRootDeaths">Deaths under the top root.</param>
/// <param name="TopRootShare">Top-root deaths divided by all deaths.</param>
/// <param name="TopRootLiving">Whether the top root was alive at final.</param>
public sealed record BatchRun(
    string Source,
    int Seed,
    int FinalPopulation,
    string Regime,
    int MutationLineages,
    int RootLineages,
    int RootGen0,
    int Enrichment,
    int Deaths,
    double? PredationShare,
    double? StarvationShare,
    double? AgingShare,
    int RootsWithDeaths,
    int? TopRoot,
    int? TopRootDeaths,
    double? TopRootShare,
    bool? TopRootLiving);

/// <summary>Run summaries and grouped regime aggregates.</summary>
/// <param name="Runs">One row per metrics file.</param>
public sealed record BatchReport(IReadOnlyList<BatchRun> Runs);

/// <summary>Cross-run metrics reader and batch report writer.</summary>
public static class BatchAnalyzer
{
    /// <summary>Reads metrics JSON files into comparable run summaries.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="paths" /> is null.</exception>
    /// <exception cref="InvalidDataException">A metrics file lacks required fields.</exception>
    public static BatchReport Read(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var runs = new List<BatchRun>(paths.Count);
        foreach (var path in paths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (!root.TryGetProperty("saves", out var saves) || saves.ValueKind != JsonValueKind.Array
                || saves.GetArrayLength() == 0)
                throw new InvalidDataException($"Metrics '{path}' has no saves.");
            var last = saves[saves.GetArrayLength() - 1];
            var population = last.GetProperty("creatures").GetInt32();
            var seed = ReadSeed(last);
            var lineage = last.GetProperty("lineage");
            var rootLineage = last.GetProperty("rootLineage");
            var origins = last.GetProperty("spawnOrigins");
            var mortality = root.GetProperty("mortalitySequence");
            var counts = mortality.GetProperty("counts");
            var total = counts.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
            var rootMortality = root.GetProperty("rootMortality");
            var rootsWithDeaths = root.TryGetProperty("rootMortalitySequence", out var sequence)
                && sequence.TryGetProperty("roots", out var rootCount) ? rootCount.GetInt32() : 0;
            int? topRoot = null;
            int? topDeaths = null;
            double? topShare = null;
            bool? topLiving = null;
            if (rootMortality.ValueKind == JsonValueKind.Array && rootMortality.GetArrayLength() > 0)
            {
                var top = rootMortality.EnumerateArray().MaxBy(static item => item.GetProperty("total").GetInt32());
                topRoot = top.GetProperty("rootLineage").GetInt32();
                topDeaths = top.GetProperty("total").GetInt32();
                topShare = total > 0 ? topDeaths / (double)total : null;
                topLiving = top.TryGetProperty("living", out var living) && living.GetBoolean();
            }

            runs.Add(new BatchRun(
                path,
                seed,
                population,
                population > 1000 ? "high" : "low",
                lineage.GetProperty("count").GetInt32(),
                rootLineage.GetProperty("count").GetInt32(),
                rootLineage.GetProperty("newGen0").GetInt32(),
                origins.GetProperty("enrichment").GetInt32(),
                total,
                ReadShare(mortality, "predationShare"),
                ReadShare(mortality, "starvationShare"),
                ReadShare(mortality, "agingShare"),
                rootsWithDeaths,
                topRoot,
                topDeaths,
                topShare,
                topLiving));
        }

        return new BatchReport(runs);
    }

    /// <summary>Writes the per-run CSV.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="report" /> is null.</exception>
    public static void WriteCsv(string path, BatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using var writer = new StreamWriter(path);
        writer.WriteLine("source,seed,finalPopulation,regime,mutationLineages,rootLineages,rootGen0,enrichment,deaths,deathsPerLiving,predationShare,starvationShare,agingShare,rootsWithDeaths,topRoot,topRootDeaths,topRootShare,topRootLiving");
        foreach (var run in report.Runs.OrderBy(static item => item.Seed).ThenBy(static item => item.Source, StringComparer.Ordinal))
        {
            writer.Write(Csv(run.Source));
            writer.Write(',');
            writer.Write(run.Seed.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.FinalPopulation.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.Regime);
            writer.Write(',');
            writer.Write(run.MutationLineages.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.RootLineages.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.RootGen0.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.Enrichment.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.Deaths.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.FinalPopulation > 0 ? (run.Deaths / (double)run.FinalPopulation).ToString("0.####", CultureInfo.InvariantCulture) : string.Empty);
            writer.Write(',');
            writer.Write(Number(run.PredationShare));
            writer.Write(',');
            writer.Write(Number(run.StarvationShare));
            writer.Write(',');
            writer.Write(Number(run.AgingShare));
            writer.Write(',');
            writer.Write(run.RootsWithDeaths.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(run.TopRoot?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            writer.Write(',');
            writer.Write(run.TopRootDeaths?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            writer.Write(',');
            writer.Write(Number(run.TopRootShare));
            writer.Write(',');
            writer.WriteLine(run.TopRootLiving is { } living ? (living ? "1" : "0") : string.Empty);
        }
    }

    /// <summary>Writes a compact JSON summary.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="report" /> is null.</exception>
    public static void WriteJson(string path, BatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteStartArray("runs");
        foreach (var run in report.Runs.OrderBy(static item => item.Seed).ThenBy(static item => item.Source, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("source", run.Source);
            writer.WriteNumber("seed", run.Seed);
            writer.WriteNumber("finalPopulation", run.FinalPopulation);
            writer.WriteString("regime", run.Regime);
            writer.WriteNumber("mutationLineages", run.MutationLineages);
            writer.WriteNumber("rootLineages", run.RootLineages);
            writer.WriteNumber("rootGen0", run.RootGen0);
            writer.WriteNumber("enrichment", run.Enrichment);
            writer.WriteNumber("deaths", run.Deaths);
            WriteNullable(writer, "predationShare", run.PredationShare);
            WriteNullable(writer, "starvationShare", run.StarvationShare);
            WriteNullable(writer, "agingShare", run.AgingShare);
            writer.WriteNumber("rootsWithDeaths", run.RootsWithDeaths);
            if (run.TopRoot is { } root) writer.WriteNumber("topRoot", root); else writer.WriteNull("topRoot");
            if (run.TopRootDeaths is { } deaths) writer.WriteNumber("topRootDeaths", deaths); else writer.WriteNull("topRootDeaths");
            WriteNullable(writer, "topRootShare", run.TopRootShare);
            if (run.TopRootLiving is { } living) writer.WriteBoolean("topRootLiving", living); else writer.WriteNull("topRootLiving");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("groups");
        foreach (var group in Group(report.Runs))
        {
            writer.WriteStartObject();
            writer.WriteString("regime", group.Regime);
            writer.WriteNumber("runs", group.Count);
            writer.WriteNumber("medianPopulation", group.MedianPopulation);
            writer.WriteNumber("medianRoots", group.MedianRoots);
            WriteNullable(writer, "medianPredationShare", group.MedianPredationShare);
            WriteNullable(writer, "medianTopRootShare", group.MedianTopRootShare);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Builds a standalone HTML comparison.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="report" /> is null.</exception>
    public static string BuildHtml(BatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Run comparison</title></head><body>");
        html.Append("<h1>Run comparison</h1><p>High means final population above 1,000. Top-root share uses tracked deaths.</p>");
        html.Append("<h2>By regime</h2><table><tr><th>regime</th><th>runs</th><th>median population</th><th>median roots</th><th>median predation</th><th>median top-root share</th></tr>");
        foreach (var group in Group(report.Runs))
        {
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td>{group.Regime}</td><td>{group.Count}</td><td>{group.MedianPopulation:0}</td>"
                + $"<td>{group.MedianRoots:0}</td><td>{Percent(group.MedianPredationShare)}</td>"
                + $"<td>{Percent(group.MedianTopRootShare)}</td></tr>");
        }

        html.Append("</table><h2>Runs</h2><table><tr><th>seed</th><th>regime</th><th>population</th>"
            + "<th>mutation lineages</th><th>roots</th><th>enrichment</th><th>deaths</th>"
            + "<th>predation</th><th>starvation</th><th>top root share</th><th>top root living</th></tr>");
        foreach (var run in report.Runs.OrderBy(static item => item.Seed).ThenBy(static item => item.Source, StringComparer.Ordinal))
        {
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td>{run.Seed}</td><td>{run.Regime}</td><td>{run.FinalPopulation}</td>"
                + $"<td>{run.MutationLineages}</td><td>{run.RootLineages}</td><td>{run.Enrichment}</td>"
                + $"<td>{run.Deaths}</td><td>{Percent(run.PredationShare)}</td><td>{Percent(run.StarvationShare)}</td>"
                + $"<td>{Percent(run.TopRootShare)}</td><td>{(run.TopRootLiving is { } living ? (living ? "yes" : "no") : "n/a")}</td></tr>");
        }

        html.Append("</table></body></html>");
        return html.ToString();
    }

    private static int ReadSeed(JsonElement last)
    {
        if (last.TryGetProperty("controller", out var controller)
            && controller.TryGetProperty("numbers", out var numbers)
            && numbers.TryGetProperty("evo.seed", out var seed))
            return (int)seed.GetDouble();
        return 0;
    }

    private static double? ReadShare(JsonElement mortality, string name) =>
        mortality.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble() : null;

    private static void WriteNullable(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is { } number) writer.WriteNumber(name, number); else writer.WriteNull(name);
    }

    private static string Csv(string value) =>
        value.ContainsAny([',', '"', '\n'])
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    private static string Number(double? value) =>
        value is { } number ? number.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty;

    private static string Percent(double? value) =>
        value is { } number ? number.ToString("0.0%", CultureInfo.InvariantCulture) : "n/a";

    private static IEnumerable<GroupSummary> Group(IReadOnlyList<BatchRun> runs) =>
        runs.GroupBy(static run => run.Regime, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group =>
            {
                var items = group.ToArray();
                return new GroupSummary(
                    group.Key,
                    items.Length,
                    Median(items.Select(static item => (double)item.FinalPopulation)),
                    Median(items.Select(static item => (double)item.RootLineages)),
                    Median(items.Where(static item => item.PredationShare is not null)
                        .Select(static item => item.PredationShare!.Value)),
                    Median(items.Where(static item => item.TopRootShare is not null)
                        .Select(static item => item.TopRootShare!.Value)));
            });

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(static value => value).ToArray();
        if (ordered.Length == 0)
            return 0d;
        return ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[(ordered.Length / 2) - 1] + ordered[ordered.Length / 2]) / 2d;
    }

    private sealed record GroupSummary(
        string Regime, int Count, double MedianPopulation, double MedianRoots,
        double? MedianPredationShare, double? MedianTopRootShare);
}
