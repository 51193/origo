using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Origo.EvolutionDemo.Analysis.Batch;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Automatic manifest discovery tests.</summary>
public sealed class BatchManifestWriterTests
{
    [Fact]
    public void Discover_FindsRunsMergesLabelsAndWritesPortablePaths()
    {
        using var directory = new TemporaryDirectory();
        var runA = Path.Combine(directory.Path, "run-a");
        var runB = Path.Combine(directory.Path, "run-b");
        Directory.CreateDirectory(runA);
        Directory.CreateDirectory(runB);
        File.WriteAllText(Path.Combine(runA, "metrics.json"), Metrics(100));
        File.WriteAllText(Path.Combine(runA, "run.json"), """{"variant":"A"}""");
        File.WriteAllText(Path.Combine(runB, "metrics.json"), Metrics(200));
        File.WriteAllText(Path.Combine(runB, "run.json"), """{"variant":"B","seed":"2"}""");
        var manifestPath = Path.Combine(directory.Path, "experiment.json");

        var manifest = BatchManifestWriter.Discover(
            directory.Path, manifestPath,
            name: "discovered",
            protocol: "test",
            metrics: [new BatchMetricDefinition("population", "saves[-1].creatures")],
            fixedLabels: new Dictionary<string, string> { ["policy"] = "planned" });

        Assert.Equal(2, manifest.Runs.Count);
        Assert.Equal("discovered", manifest.Name);
        Assert.Equal("run-a", manifest.Runs[0].Labels["run"]);
        Assert.Equal("planned", manifest.Runs[0].Labels["policy"]);
        Assert.Equal("B", manifest.Runs[1].Labels["variant"]);
        Assert.Equal(Path.Combine("run-a", "metrics.json"), manifest.Runs[0].MetricsPath);
        BatchManifestWriter.Write(manifestPath, manifest);
        var report = BatchManifestAnalyzer.Read(manifestPath, ["policy", "variant"]);
        Assert.Equal(2, report.Groups.Count);
    }

    [Fact]
    public void Discover_NoMetricsFailsExplicitly()
    {
        using var directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() =>
            BatchManifestWriter.Discover(directory.Path, Path.Combine(directory.Path, "experiment.json")));
    }

    private static string Metrics(int population) =>
        JsonSerializer.Serialize(new { saves = new[] { new { creatures = population } } });
}
