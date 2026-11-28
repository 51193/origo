using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Origo.EvolutionDemo.Analysis.Batch;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Manifest-driven, mechanism-agnostic batch tests.</summary>
public sealed class BatchManifestTests
{
    [Fact]
    public void Read_GroupsByArbitraryLabelsAndAggregatesMetrics()
    {
        using var directory = new TemporaryDirectory();
        var metricsA1 = Path.Combine(directory.Path, "a1.json");
        var metricsA2 = Path.Combine(directory.Path, "a2.json");
        var metricsB1 = Path.Combine(directory.Path, "b1.json");
        File.WriteAllText(metricsA1, Metrics(100, 3, 0.2, true));
        File.WriteAllText(metricsA2, Metrics(200, 5, 0.4, false));
        File.WriteAllText(metricsB1, Metrics(300, 7, 0.6, true));
        var manifest = Path.Combine(directory.Path, "experiment.json");
        File.WriteAllText(manifest, Manifest(
            "a1.json", "mechanic-a", "a2.json", "mechanic-a", "b1.json", "mechanic-b"));

        var report = BatchManifestAnalyzer.Read(manifest, ["mechanic"]);

        Assert.Equal(3, report.Runs.Count);
        Assert.Equal(2, report.Groups.Count);
        var a = Assert.Single(report.Groups, group => group.Labels["mechanic"] == "mechanic-a");
        Assert.Equal(2, a.Runs);
        Assert.Equal(150d, Assert.Single(a.Metrics, metric => metric.Name == "population").Median!.Value, 9);
        Assert.Equal(0.5d, Assert.Single(a.Metrics, metric => metric.Name == "alive").Proportion!.Value, 9);
        Assert.Equal(36.6666666666667d,
            Assert.Single(a.Metrics, metric => metric.Name == "perRoot").Median!.Value, 9);
        var b = Assert.Single(report.Groups, group => group.Labels["mechanic"] == "mechanic-b");
        Assert.Equal(300d, Assert.Single(b.Metrics, metric => metric.Name == "population").Median!.Value, 9);
        Assert.Equal(1d, Assert.Single(b.Metrics, metric => metric.Name == "alive").Proportion!.Value, 9);
    }

    [Fact]
    public void Read_MissingPathFailsExplicitly()
    {
        using var directory = new TemporaryDirectory();
        var metrics = Path.Combine(directory.Path, "a1.json");
        File.WriteAllText(metrics, Metrics(100, 3, 0.2, true));
        var manifest = Path.Combine(directory.Path, "experiment.json");
        File.WriteAllText(manifest, """
        {
          "schemaVersion": 1,
          "name": "bad",
          "metrics": [{"name":"missing","path":"saves[-1].nope"}],
          "runs": [{"metrics":"a1.json","labels":{"mechanic":"a"}}]
        }
        """);

        var exception = Assert.Throws<InvalidDataException>(() => BatchManifestAnalyzer.Read(manifest));

        Assert.Contains("nope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidSchemaFailsExplicitly()
    {
        using var directory = new TemporaryDirectory();
        var manifest = Path.Combine(directory.Path, "experiment.json");
        File.WriteAllText(manifest, """{"schemaVersion":2,"name":"bad","metrics":[],"runs":[]}""");

        Assert.Throws<InvalidDataException>(() => BatchManifestAnalyzer.Read(manifest));
    }

    [Fact]
    public void JsonPath_ResolvesNegativeIndexesAndFailsOutOfRange()
    {
        using var document = JsonDocument.Parse("""{"saves":[{"creatures":1},{"creatures":2}]}""");
        Assert.Equal(2, BatchJsonPath.Resolve(document.RootElement, "saves[-1].creatures", "test").GetInt32());
        Assert.Throws<InvalidDataException>(() =>
            BatchJsonPath.Resolve(document.RootElement, "saves[9].creatures", "test"));
    }

    private static string Metrics(int population, int roots, double predation, bool alive) =>
        JsonSerializer.Serialize(new
        {
            saves = new[]
            {
                new
                {
                    creatures = population,
                    rootLineage = new { count = roots },
                },
            },
            mortalitySequence = new { predationShare = predation },
            rootMortalitySequence = new { topRootLiving = alive },
        });

    private static string Manifest(
        string metricsA1, string labelA1, string metricsA2, string labelA2, string metricsB1, string labelB1) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            name = "arbitrary-mechanic-test",
            protocol = "synthetic",
            metrics = new object[]
            {
                new { name = "population", path = "saves[-1].creatures", kind = "number" },
                new { name = "roots", path = "saves[-1].rootLineage.count", kind = "number" },
                new { name = "predation", path = "mortalitySequence.predationShare", kind = "number" },
                new { name = "perRoot", expression = "population / roots", kind = "number" },
                new { name = "alive", path = "rootMortalitySequence.topRootLiving", kind = "boolean" },
            },
            runs = new[]
            {
                Run(metricsA1, labelA1),
                Run(metricsA2, labelA2),
                Run(metricsB1, labelB1),
            },
        });

    private static object Run(string metrics, string label) => new
    {
        metrics,
        labels = new Dictionary<string, string> { ["mechanic"] = label },
    };
}
