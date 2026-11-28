using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Origo.EvolutionDemo.Analysis.Batch;

/// <summary>Discovers run directories and writes a portable batch manifest.</summary>
public static class BatchManifestWriter
{
    /// <summary>Convenience metric set for current Evolution Demo metrics JSON.</summary>
    public static IReadOnlyList<BatchMetricDefinition> StandardMetrics { get; } =
    [
        new("population", "saves[-1].creatures"),
        new("roots", "saves[-1].rootLineage.count"),
        new("rootGen0", "saves[-1].rootLineage.newGen0"),
        new("extinctRoots", "rootLineageSequence.lostLineages"),
        new("deaths", "mortalitySequence.counts.total"),
        new("predationShare", "mortalitySequence.predationShare"),
        new("rootBirths", "rootBirthsSequence.totalBirths"),
        new("topRootShare", "rootMortalitySequence.topRootShare"),
        new("topRootLiving", "rootMortalitySequence.topRootLiving", BatchMetricKind.Boolean),
    ];

    /// <summary>Builds a manifest from every metrics.json under a root directory.</summary>
    /// <param name="root">Directory scanned recursively.</param>
    /// <param name="manifestPath">Output manifest path; run paths become relative to its directory.</param>
    /// <param name="name">Experiment name; defaults to the root directory name.</param>
    /// <param name="protocol">Optional protocol note.</param>
    /// <param name="metrics">Metric definitions; defaults to <see cref="StandardMetrics" />.</param>
    /// <param name="fixedLabels">Labels applied to every run.</param>
    /// <param name="sidecarName">Per-run JSON object with extra labels.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root" /> or <paramref name="manifestPath" /> is null.</exception>
    /// <exception cref="InvalidDataException">No metrics files, or a malformed sidecar.</exception>
    public static BatchManifest Discover(
        string root,
        string manifestPath,
        string? name = null,
        string? protocol = null,
        IReadOnlyList<BatchMetricDefinition>? metrics = null,
        IReadOnlyDictionary<string, string>? fixedLabels = null,
        string sidecarName = "run.json")
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(manifestPath);
        var fullRoot = Path.GetFullPath(root);
        var fullManifest = Path.GetFullPath(manifestPath);
        if (!Directory.Exists(fullRoot))
            throw new InvalidDataException($"Manifest root '{fullRoot}' does not exist.");
        var metricsFiles = Directory.EnumerateFiles(fullRoot, "metrics.json", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (metricsFiles.Length == 0)
            throw new InvalidDataException($"No metrics.json files under '{fullRoot}'.");
        var manifestDirectory = Path.GetDirectoryName(fullManifest) ?? Directory.GetCurrentDirectory();
        var runs = new List<BatchRunSource>(metricsFiles.Length);
        foreach (var metricsFile in metricsFiles)
        {
            var runDirectory = Path.GetDirectoryName(metricsFile) ?? fullRoot;
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            if (fixedLabels is not null)
                foreach (var (key, value) in fixedLabels)
                    labels[key] = value;
            var sidecar = Path.Combine(runDirectory, sidecarName);
            if (File.Exists(sidecar))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(sidecar));
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException($"Sidecar '{sidecar}' must be a JSON object.");
                foreach (var property in document.RootElement.EnumerateObject())
                    labels[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.ToString();
            }

            labels.TryAdd("run", Path.GetFileName(runDirectory));
            runs.Add(new BatchRunSource(
                Path.GetRelativePath(manifestDirectory, metricsFile),
                labels));
        }

        return new BatchManifest(
            1,
            string.IsNullOrWhiteSpace(name) ? Path.GetFileName(fullRoot) : name,
            protocol,
            metrics ?? StandardMetrics,
            runs);
    }

    /// <summary>Writes a manifest JSON file.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="manifest" /> is null.</exception>
    public static void Write(string path, BatchManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
        writer.WriteString("name", manifest.Name);
        if (manifest.Protocol is { } protocol)
            writer.WriteString("protocol", protocol);
        writer.WriteStartArray("metrics");
        foreach (var metric in manifest.Metrics)
        {
            writer.WriteStartObject();
            writer.WriteString("name", metric.Name);
            if (metric.Expression is not null)
                writer.WriteString("expression", metric.Expression);
            else
                writer.WriteString("path", metric.JsonPath);
            writer.WriteString("kind", metric.Kind == BatchMetricKind.Boolean ? "boolean" : "number");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("runs");
        foreach (var run in manifest.Runs)
        {
            writer.WriteStartObject();
            writer.WriteString("metrics", run.MetricsPath);
            writer.WriteStartObject("labels");
            foreach (var (key, value) in run.Labels.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                writer.WriteString(key, value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
