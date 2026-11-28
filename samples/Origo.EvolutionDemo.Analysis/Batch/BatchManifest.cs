using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Origo.EvolutionDemo.Analysis.Batch;

/// <summary>Value kind for a manifest-selected metric.</summary>
public enum BatchMetricKind
{
    /// <summary>A finite number.</summary>
    Number,

    /// <summary>A boolean; group summary reports a proportion with a Wilson interval.</summary>
    Boolean,
}

/// <summary>Contract for one externally configured metric extraction.</summary>
public interface IBatchMetricSelector
{
    /// <summary>Output name.</summary>
    string Name { get; }

    /// <summary>Restricted JSON dot path.</summary>
    string JsonPath { get; }

    /// <summary>Value kind.</summary>
    BatchMetricKind Kind { get; }
}

/// <summary>Contract for one configured run source and its experiment labels.</summary>
public interface IBatchRunSource
{
    /// <summary>Metrics JSON path.</summary>
    string MetricsPath { get; }

    /// <summary>Arbitrary labels used for grouping.</summary>
    IReadOnlyDictionary<string, string> Labels { get; }
}

/// <summary>One metric selected from each run's metrics JSON.</summary>
/// <param name="Name">Output column name.</param>
/// <param name="JsonPath">Dot path with optional array indexes, e.g. saves[-1].creatures.</param>
/// <param name="Kind">Value kind.</param>
public sealed record BatchMetricDefinition(string Name, string JsonPath, BatchMetricKind Kind = BatchMetricKind.Number) : IBatchMetricSelector;

/// <summary>One run source and arbitrary experiment labels.</summary>
/// <param name="MetricsPath">Metrics JSON path, resolved relative to the manifest.</param>
/// <param name="Labels">Experiment labels; the batch can group by any subset.</param>
public sealed record BatchRunSource(string MetricsPath, IReadOnlyDictionary<string, string> Labels) : IBatchRunSource;

/// <summary>Versioned experiment manifest decoupling batch output from game mechanics.</summary>
/// <param name="SchemaVersion">Must be 1.</param>
/// <param name="Name">Experiment name.</param>
/// <param name="Protocol">Free-form protocol note copied into outputs.</param>
/// <param name="Metrics">Reusable metric definitions.</param>
/// <param name="Runs">Runs and their labels.</param>
public sealed record BatchManifest(
    int SchemaVersion,
    string Name,
    string? Protocol,
    IReadOnlyList<BatchMetricDefinition> Metrics,
    IReadOnlyList<BatchRunSource> Runs);

/// <summary>One run's extracted metric values.</summary>
/// <param name="Source">Run source and labels.</param>
/// <param name="Numbers">Numeric metric values by name.</param>
/// <param name="Booleans">Boolean metric values by name.</param>
public sealed record ManifestRunResult(
    BatchRunSource Source,
    IReadOnlyDictionary<string, double?> Numbers,
    IReadOnlyDictionary<string, bool?> Booleans);

/// <summary>Aggregate of one metric over one arbitrary label group.</summary>
/// <param name="Name">Metric name.</param>
/// <param name="Count">Finite observations.</param>
/// <param name="Min">Minimum numeric value; null for boolean metrics.</param>
/// <param name="Median">Median numeric value; null for boolean metrics.</param>
/// <param name="Max">Maximum numeric value; null for boolean metrics.</param>
/// <param name="Proportion">True proportion; null for numeric metrics.</param>
/// <param name="WilsonLow">Wilson 95% lower bound; null unless proportion is present.</param>
/// <param name="WilsonHigh">Wilson 95% upper bound; null unless proportion is present.</param>
public sealed record BatchMetricStat(
    string Name,
    int Count,
    double? Min,
    double? Median,
    double? Max,
    double? Proportion,
    double? WilsonLow,
    double? WilsonHigh);

/// <summary>One arbitrary label group with all metric aggregates.</summary>
/// <param name="Labels">Group label values.</param>
/// <param name="Runs">Runs in the group.</param>
/// <param name="Metrics">Metric aggregates.</param>
public sealed record BatchGroupSummary(
    IReadOnlyDictionary<string, string> Labels,
    int Runs,
    IReadOnlyList<BatchMetricStat> Metrics);

/// <summary>Full generalized batch result.</summary>
/// <param name="Manifest">Source manifest.</param>
/// <param name="GroupBy">Labels used for grouping.</param>
/// <param name="Runs">Extracted per-run values.</param>
/// <param name="Groups">Aggregated arbitrary groups.</param>
public sealed record ManifestBatchReport(
    BatchManifest Manifest,
    IReadOnlyList<string> GroupBy,
    IReadOnlyList<ManifestRunResult> Runs,
    IReadOnlyList<BatchGroupSummary> Groups);

/// <summary>Generalized manifest-driven run comparison.</summary>
public static class BatchManifestAnalyzer
{
    /// <summary>Reads a manifest and extracts metrics into arbitrary groups.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="manifestPath" /> is null.</exception>
    /// <exception cref="InvalidDataException">The manifest or a metrics file violates the schema.</exception>
    public static ManifestBatchReport Read(string manifestPath, IReadOnlyList<string>? groupBy = null)
    {
        ArgumentNullException.ThrowIfNull(manifestPath);
        var fullPath = Path.GetFullPath(manifestPath);
        var manifest = ReadManifest(fullPath);
        var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var labels = ResolveGroupBy(manifest, groupBy);
        var runs = new List<ManifestRunResult>(manifest.Runs.Count);
        foreach (var source in manifest.Runs)
        {
            var metricsPath = Path.IsPathRooted(source.MetricsPath)
                ? source.MetricsPath
                : Path.Combine(directory, source.MetricsPath);
            using var document = JsonDocument.Parse(File.ReadAllText(metricsPath));
            var numbers = new Dictionary<string, double?>(StringComparer.Ordinal);
            var booleans = new Dictionary<string, bool?>(StringComparer.Ordinal);
            foreach (var metric in manifest.Metrics)
            {
                var value = BatchJsonPath.Resolve(document.RootElement, metric.JsonPath, metricsPath);
                if (metric.Kind == BatchMetricKind.Boolean)
                {
                    if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new InvalidDataException(
                            $"Metric '{metric.Name}' at '{metric.JsonPath}' in '{metricsPath}' is not a boolean.");
                    booleans[metric.Name] = value.GetBoolean();
                }
                else
                {
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number)
                        || !double.IsFinite(number))
                        throw new InvalidDataException(
                            $"Metric '{metric.Name}' at '{metric.JsonPath}' in '{metricsPath}' is not a finite number.");
                    numbers[metric.Name] = number;
                }
            }

            runs.Add(new ManifestRunResult(source, numbers, booleans));
        }

        var groups = runs.GroupBy(
                run => GroupKey(run.Source.Labels, labels),
                StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group => new BatchGroupSummary(
                labels.ToDictionary(
                    static label => label,
                    label => group.First().Source.Labels[label],
                    StringComparer.Ordinal),
                group.Count(),
                [.. manifest.Metrics.Select(metric => Stat(metric, [.. group]))]))
            .ToArray();
        return new ManifestBatchReport(manifest, labels, runs, groups);
    }

    /// <summary>Writes one row per group and metric.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="report" /> is null.</exception>
    public static void WriteCsv(string path, ManifestBatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        EnsureDirectory(path);
        using var writer = new StreamWriter(path);
        writer.Write("group");
        foreach (var label in report.GroupBy)
            writer.Write(",{0}", Csv(label));
        writer.WriteLine(",runs,metric,count,min,median,max,proportion,wilsonLow,wilsonHigh");
        foreach (var group in report.Groups)
        {
            foreach (var metric in group.Metrics)
            {
                writer.Write(Csv(GroupValue(group.Labels, report.GroupBy)));
                writer.Write(',');
                writer.Write(group.Runs.ToString(CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(Csv(metric.Name));
                writer.Write(',');
                writer.Write(metric.Count.ToString(CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(Number(metric.Min));
                writer.Write(',');
                writer.Write(Number(metric.Median));
                writer.Write(',');
                writer.Write(Number(metric.Max));
                writer.Write(',');
                writer.Write(Number(metric.Proportion));
                writer.Write(',');
                writer.Write(Number(metric.WilsonLow));
                writer.Write(',');
                writer.WriteLine(Number(metric.WilsonHigh));
            }
        }
    }

    /// <summary>Writes the full generalized report as JSON.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="report" /> is null.</exception>
    public static void WriteJson(string path, ManifestBatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        EnsureDirectory(path);
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", report.Manifest.SchemaVersion);
        writer.WriteString("name", report.Manifest.Name);
        if (report.Manifest.Protocol is { } protocol)
            writer.WriteString("protocol", protocol);
        writer.WriteStartArray("groupBy");
        foreach (var label in report.GroupBy)
            writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("runs");
        foreach (var run in report.Runs)
        {
            writer.WriteStartObject();
            writer.WriteString("metrics", run.Source.MetricsPath);
            writer.WriteStartObject("labels");
            foreach (var (key, value) in run.Source.Labels.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                writer.WriteString(key, value);
            writer.WriteEndObject();
            writer.WriteStartObject("values");
            foreach (var metric in report.Manifest.Metrics)
            {
                if (metric.Kind == BatchMetricKind.Boolean)
                {
                    if (run.Booleans[metric.Name] is { } boolean)
                        writer.WriteBoolean(metric.Name, boolean);
                    else
                        writer.WriteNull(metric.Name);
                }
                else
                {
                    WriteNullable(writer, metric.Name, run.Numbers[metric.Name]);
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("groups");
        foreach (var group in report.Groups)
        {
            writer.WriteStartObject();
            writer.WriteStartObject("labels");
            foreach (var (key, value) in group.Labels.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                writer.WriteString(key, value);
            writer.WriteEndObject();
            writer.WriteNumber("runs", group.Runs);
            writer.WriteStartArray("metrics");
            foreach (var metric in group.Metrics)
            {
                writer.WriteStartObject();
                writer.WriteString("name", metric.Name);
                writer.WriteNumber("count", metric.Count);
                writer.WritePropertyName("min");
                WriteNumber(writer, metric.Min);
                writer.WritePropertyName("median");
                WriteNumber(writer, metric.Median);
                writer.WritePropertyName("max");
                WriteNumber(writer, metric.Max);
                writer.WritePropertyName("proportion");
                WriteNumber(writer, metric.Proportion);
                writer.WritePropertyName("wilsonLow");
                WriteNumber(writer, metric.WilsonLow);
                writer.WritePropertyName("wilsonHigh");
                WriteNumber(writer, metric.WilsonHigh);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Builds a standalone HTML report.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="report" /> is null.</exception>
    public static string BuildHtml(ManifestBatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Manifest batch</title></head><body>");
        html.Append(CultureInfo.InvariantCulture,
            $"<h1>{Escape(report.Manifest.Name)}</h1><p>{report.Runs.Count} runs; groups: {string.Join(", ", report.GroupBy)}.</p>");
        if (report.Manifest.Protocol is { } protocol)
            html.Append(CultureInfo.InvariantCulture, $"<p>{Escape(protocol)}</p>");
        html.Append("<h2>Groups</h2><table><tr><th>group</th><th>runs</th>");
        foreach (var metric in report.Manifest.Metrics)
            html.Append(CultureInfo.InvariantCulture, $"<th>{Escape(metric.Name)}</th>");
        html.Append("</tr>");
        foreach (var group in report.Groups)
        {
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td>{Escape(GroupValue(group.Labels, report.GroupBy))}</td><td>{group.Runs}</td>");
            foreach (var metric in group.Metrics)
            {
                var value = metric.Proportion is { } proportion
                    ? $"{proportion:0.0%} (95% {metric.WilsonLow:0.0%}-{metric.WilsonHigh:0.0%})"
                    : $"{metric.Median:0.###} [{metric.Min:0.###}, {metric.Max:0.###}]";
                html.Append(CultureInfo.InvariantCulture, $"<td>{value}</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</table><h2>Runs</h2><table><tr><th>metrics file</th>");
        foreach (var label in report.GroupBy)
            html.Append(CultureInfo.InvariantCulture, $"<th>{Escape(label)}</th>");
        foreach (var metric in report.Manifest.Metrics)
            html.Append(CultureInfo.InvariantCulture, $"<th>{Escape(metric.Name)}</th>");
        html.Append("</tr>");
        foreach (var run in report.Runs)
        {
            html.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{Escape(run.Source.MetricsPath)}</code></td>");
            foreach (var label in report.GroupBy)
                html.Append(CultureInfo.InvariantCulture, $"<td>{Escape(run.Source.Labels[label])}</td>");
            foreach (var metric in report.Manifest.Metrics)
            {
                var value = metric.Kind == BatchMetricKind.Boolean
                    ? (run.Booleans[metric.Name] is { } boolean ? (boolean ? "true" : "false") : "n/a")
                    : Number(run.Numbers[metric.Name]);
                html.Append(CultureInfo.InvariantCulture, $"<td>{value}</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</table></body></html>");
        return html.ToString();
    }

    private static BatchManifest ReadManifest(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var schema = root.TryGetProperty("schemaVersion", out var schemaValue) && schemaValue.TryGetInt32(out var version)
            ? version : 0;
        if (schema != 1)
            throw new InvalidDataException($"Manifest '{path}' needs schemaVersion 1.");
        var name = root.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"Manifest '{path}' needs a non-empty name.");
        var protocol = root.TryGetProperty("protocol", out var protocolValue) ? protocolValue.GetString() : null;
        if (!root.TryGetProperty("metrics", out var metricsValue) || metricsValue.ValueKind != JsonValueKind.Array
            || metricsValue.GetArrayLength() == 0)
            throw new InvalidDataException($"Manifest '{path}' needs a non-empty metrics array.");
        var metrics = new List<BatchMetricDefinition>();
        foreach (var metric in metricsValue.EnumerateArray())
        {
            var metricName = metric.TryGetProperty("name", out var metricNameValue) ? metricNameValue.GetString() : null;
            var jsonPath = metric.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(metricName) || string.IsNullOrWhiteSpace(jsonPath))
                throw new InvalidDataException($"Manifest '{path}' has a metric without name/path.");
            var kind = metric.TryGetProperty("kind", out var kindValue) && kindValue.GetString() == "boolean"
                ? BatchMetricKind.Boolean : BatchMetricKind.Number;
            metrics.Add(new BatchMetricDefinition(metricName, jsonPath, kind));
        }

        if (metrics.Select(static metric => metric.Name).Distinct(StringComparer.Ordinal).Count() != metrics.Count)
            throw new InvalidDataException($"Manifest '{path}' has duplicate metric names.");
        if (!root.TryGetProperty("runs", out var runsValue) || runsValue.ValueKind != JsonValueKind.Array
            || runsValue.GetArrayLength() == 0)
            throw new InvalidDataException($"Manifest '{path}' needs a non-empty runs array.");
        var runs = new List<BatchRunSource>();
        foreach (var run in runsValue.EnumerateArray())
        {
            var metricsPath = run.TryGetProperty("metrics", out var metricsPathValue) ? metricsPathValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(metricsPath))
                throw new InvalidDataException($"Manifest '{path}' has a run without metrics.");
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            if (run.TryGetProperty("labels", out var labelsValue) && labelsValue.ValueKind == JsonValueKind.Object)
                foreach (var label in labelsValue.EnumerateObject())
                    labels[label.Name] = label.Value.ValueKind == JsonValueKind.String
                        ? label.Value.GetString() ?? string.Empty
                        : label.Value.ToString();
            runs.Add(new BatchRunSource(metricsPath, labels));
        }

        return new BatchManifest(1, name, protocol, metrics, runs);
    }

    private static List<string> ResolveGroupBy(BatchManifest manifest, IReadOnlyList<string>? requested)
    {
        List<string> labels = requested is { Count: > 0 }
            ? [.. requested]
            : [.. manifest.Runs.SelectMany(static run => run.Labels.Keys).Distinct(StringComparer.Ordinal).OrderBy(static label => label, StringComparer.Ordinal)];
        foreach (var label in labels)
        {
            if (manifest.Runs.Any(run => !run.Labels.ContainsKey(label)))
                throw new InvalidDataException($"Group-by label '{label}' is missing from at least one run.");
        }

        return labels;
    }

    private static BatchMetricStat Stat(BatchMetricDefinition metric, IReadOnlyList<ManifestRunResult> runs)
    {
        if (metric.Kind == BatchMetricKind.Boolean)
        {
            var values = runs.Select(run => run.Booleans[metric.Name]).Where(static value => value is not null)
                .Select(static value => value!.Value).ToArray();
            var trueCount = values.Count(static value => value);
            var proportion = values.Length == 0 ? (double?)null : trueCount / (double)values.Length;
            var (low, high) = Wilson(trueCount, values.Length);
            return new BatchMetricStat(metric.Name, values.Length, null, null, null, proportion, low, high);
        }

        var numbers = runs.Select(run => run.Numbers[metric.Name]).Where(static value => value is not null)
            .Select(static value => value!.Value).OrderBy(static value => value).ToArray();
        return new BatchMetricStat(
            metric.Name,
            numbers.Length,
            numbers.Length == 0 ? null : numbers[0],
            Median(numbers),
            numbers.Length == 0 ? null : numbers[^1],
            null,
            null,
            null);
    }

    private static string GroupKey(IReadOnlyDictionary<string, string> labels, IReadOnlyList<string> groupBy) =>
        string.Join('\u001f', groupBy.Select(label => labels[label]));

    private static string GroupValue(IReadOnlyDictionary<string, string> labels, IReadOnlyList<string> groupBy) =>
        string.Join(", ", groupBy.Select(label => $"{label}={labels[label]}"));

    private static double? Median(double[] values)
    {
        if (values.Length == 0)
            return null;
        return values.Length % 2 == 1
            ? values[values.Length / 2]
            : (values[(values.Length / 2) - 1] + values[values.Length / 2]) / 2d;
    }

    private static (double Low, double High) Wilson(int successes, int samples)
    {
        if (samples == 0)
            return (0d, 0d);
        const double z = 1.959963984540054;
        var p = successes / (double)samples;
        var denominator = 1d + ((z * z) / samples);
        var center = (p + ((z * z) / (2d * samples))) / denominator;
        var margin = z * System.Math.Sqrt(((p * (1d - p)) / samples) + ((z * z) / (4d * samples * samples))) / denominator;
        return (System.Math.Max(0d, center - margin), System.Math.Min(1d, center + margin));
    }

    private static string Number(double? value) =>
        value is { } number ? number.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty;

    private static string Csv(string value) =>
        value.ContainsAny([',', '"', '\n'])
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is { } number)
            writer.WriteNumber(name, number);
        else
            writer.WriteNull(name);
    }

    private static void WriteNumber(Utf8JsonWriter writer, double? value)
    {
        if (value is { } number)
            writer.WriteNumberValue(number);
        else
            writer.WriteNullValue();
    }
}

/// <summary>Resolves a restricted JSON dot path used by batch metric definitions.</summary>
public static class BatchJsonPath
{
    /// <summary>Resolves object properties and non-negative/negative array indexes.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> is null or empty.</exception>
    /// <exception cref="InvalidDataException">The path does not exist in the document.</exception>
    public static JsonElement Resolve(JsonElement root, string path, string source)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));
        var current = root;
        foreach (var segment in path.TrimStart('$', '.').Split('.'))
        {
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);
            var property = bracket < 0 ? segment : segment[..bracket];
            if (property.Length > 0)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(property, out current))
                    throw new InvalidDataException($"Path '{path}' missing '{property}' in '{source}'.");
            }

            if (bracket < 0)
                continue;
            if (segment[^1] != ']')
                throw new InvalidDataException($"Path '{path}' has an unterminated index in '{source}'.");
            var indexText = segment[(bracket + 1)..^1];
            if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                throw new InvalidDataException($"Path '{path}' has a non-integer index in '{source}'.");
            if (current.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Path '{path}' indexes a non-array in '{source}'.");
            if (index < 0)
                index = current.GetArrayLength() + index;
            if (index < 0 || index >= current.GetArrayLength())
                throw new InvalidDataException($"Path '{path}' index {indexText} outside array in '{source}'.");
            current = current[index];
        }

        return current;
    }
}
