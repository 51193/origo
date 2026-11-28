using System;
using System.Collections.Generic;
using System.IO;
using Origo.EvolutionDemo.Analysis.Batch;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Cross-run batch summary tests.</summary>
public sealed class BatchTests
{
    [Fact]
    public void Read_ExtractsRegimeAndTopRoot()
    {
        using var directory = new TemporaryDirectory();
        var high = Path.Combine(directory.Path, "high.json");
        var low = Path.Combine(directory.Path, "low.json");
        File.WriteAllText(high, Metrics(seed: 1, population: 2497, roots: 407, enrichment: 335,
            total: 1000, predation: 0.1, topRoot: 4, topDeaths: 500, topLiving: true));
        File.WriteAllText(low, Metrics(seed: 2, population: 300, roots: 90, enrichment: 40,
            total: 800, predation: 0.5, topRoot: 6, topDeaths: 120, topLiving: false));

        var report = BatchAnalyzer.Read([high, low]);

        Assert.Equal(2, report.Runs.Count);
        Assert.Equal("high", report.Runs[0].Regime);
        Assert.Equal("low", report.Runs[1].Regime);
        Assert.Equal(0.5d, report.Runs[0].TopRootShare!.Value, 9);
        Assert.True(report.Runs[0].TopRootLiving);
        Assert.False(report.Runs[1].TopRootLiving);
        var html = BatchAnalyzer.BuildHtml(report);
        Assert.Contains("Run comparison", html, StringComparison.Ordinal);
        Assert.Contains("By regime", html, StringComparison.Ordinal);
    }

    private static string Metrics(
        int seed, int population, int roots, int enrichment,
        int total, double predation, int topRoot, int topDeaths, bool topLiving) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            saves = new[]
            {
                new
                {
                    creatures = population,
                    lineage = new { count = 100 },
                    rootLineage = new { count = roots, newGen0 = 20 },
                    spawnOrigins = new { enrichment },
                    controller = new { numbers = new Dictionary<string, int> { ["evo.seed"] = seed } },
                },
            },
            mortalitySequence = new
            {
                counts = new { total },
                predationShare = predation,
                starvationShare = 0.8,
                agingShare = 0.2,
            },
            rootMortalitySequence = new { roots = 3 },
            rootMortality = new[]
            {
                new { rootLineage = topRoot, total = topDeaths, living = topLiving },
            },
        });
}
