using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.Math;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Builds a synthetic <see cref="GenomeReportInput" /> for report/JSON tests.</summary>
internal static class ReportFixture
{
    internal static GenomeReportInput Create(
        int clusterCount = 3,
        int rowsPerCluster = 3,
        bool withAutoK = true,
        bool withNoise = false,
        int testedSignatureCount = 0,
        int stabilityRounds = 5,
        string commandLine = "genome --save save_a --seed 1",
        bool sampled = false)
    {
        var rows = new List<FeatureRow>();
        var labels = new List<int>();
        for (var cluster = 0; cluster < clusterCount; cluster++)
            for (var point = 0; point < rowsPerCluster; point++)
            {
                var values = new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["f.one"] = cluster + (point * 0.1d),
                    ["f.two"] = (cluster * 10d) + point,
                    ["constant"] = 7d,
                };
                var signatures = new HashSet<string>(["sig.all", $"sig.cluster{cluster}"], StringComparer.Ordinal);
                for (var tested = 0; tested < testedSignatureCount; tested++)
                    signatures.Add($"sig.tested{tested}");
                rows.Add(new FeatureRow("save_a", 10d, $"c{cluster}_{point}", 0, 1, values, signatures));
                labels.Add(cluster);
            }

        if (withNoise)
        {
            for (var noise = 0; noise < 2; noise++)
            {
                rows.Add(new FeatureRow("save_a", 10d, $"noise_{noise}", 0, 1,
                    new Dictionary<string, double>(StringComparer.Ordinal)
                    {
                        ["f.one"] = 99d + noise,
                        ["f.two"] = 99d,
                        ["constant"] = 7d,
                    },
                    new HashSet<string>(["sig.all"], StringComparer.Ordinal)));
                labels.Add(-1);
            }
        }

        var noiselessClusterCount = clusterCount;
        var candidates = withAutoK
            ? Enumerable.Range(2, 4).Select(k => new ClusterCandidate(
                k,
                Inertia: 1000d / k,
                Silhouette: k == clusterCount ? 0.6d : 0.2d,
                Sizes: [.. Enumerable.Range(0, k).Select(cluster => cluster == k - 1 ? rows.Count - (k - 1) : 1)],
                Flags: k == clusterCount
                    ? ["accepted", "selected"]
                    : ["rejected", "small-clusters"]))
                .ToArray()
            : [];

        var saves = new List<SaveSummary>
        {
            new("save_c", "/saves/save_c/level_pond/snd_scene.json", 300d, 3000d, 50, 10, 1, 2,
                new Dictionary<string, double>(StringComparer.Ordinal) { ["evo.tick"] = 3000d },
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal)),
            new("save_a", "/saves/save_a/level_pond/snd_scene.json", 10d, 100d, 20, 4, 2, 2,
                new Dictionary<string, double>(StringComparer.Ordinal) { ["evo.tick"] = 100d },
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal)),
            new("save_b", "/saves/save_b/level_pond/snd_scene.json", 20d, 200d, 30, 5, 0, 2,
                new Dictionary<string, double>(StringComparer.Ordinal) { ["evo.tick"] = 200d },
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal)),
        };
        var ecology = saves.Select(save => new SaveEcology(
            save.Id,
            save.Path,
            save.Time,
            save.Tick,
            new FoodTypeCounts(4, 2, 2, 1),
            2,
            60d,
            1000d,
            2d * 60d * 60d / (1000d * 1000d),
            new FoodAccessSummary(20, 0.25d, 0.5d, 0.75d, 2d, 5d, 120d, 300d),
            0d,
            1d,
            10,
            new DietShares(0.4d, 0.3d, 0.3d, 0.2d, 10),
            new DietShares(0.4d, 0.3d, 0.3d, 0.2d, 10),
            new DietShares(0.5d, 0.25d, 0.25d, 0.3d, 100),
            5,
            0.2d,
            0.8d,
            [])).ToArray();
        var lineage = new LineageAnalysis(
            [.. saves.Select(save => new SaveLineage(
                save.Id,
                save.Path,
                save.Time,
                save.Tick,
                new LineageSummary(2, 1, 0.5d, 10, 0.5d, 0.6d, 0.9d, 0, 0, 0, null, 0d),
                new LineageSummary(2, 1, 0.5d, 10, 0.5d, 0.6d, 0.9d, 0, 0, 0, null, 0d),
                new SpawnOriginCounts(0, 20, 0, 0, 0, 0)))],
            [],
            new LineageSequenceSummary(2, 2, 2, 0, 0, 0, null, null),
            new LineageSequenceSummary(2, 2, 2, 0, 0, 0, null, null),
            new SpawnOriginCounts(0, 60, 0, 0, 0, 0));
        var mortality = new MortalityAnalysis(
            [.. saves.Select(save => new SaveMortality(
                save.Id, save.Path, save.Time, save.Tick, new DeathCounts(2, 3, 1)))],
            new MortalitySequenceSummary(new DeathCounts(6, 9, 3), 0.5d, 1 / 3d, 1 / 6d));
        var finalLabels = labels.ToArray();
        var clustering = new KMeans.Result(
            finalLabels,
            [.. Enumerable.Range(0, noiselessClusterCount).Select(_ => new double[2])],
            Inertia: 321.5d,
            noiselessClusterCount,
            Silhouette: 0.4567d);
        var stability = stabilityRounds > 0
            ? new ClusterStability.BootstrapResult(0.9d, 0.05d, stabilityRounds)
            : null;
        var enrichment = Enumerable.Range(0, noiselessClusterCount)
            .Select(cluster => GeneEnrichment.Analyze(rows, finalLabels, cluster))
            .ToArray();
        var pca = new PcaResult(
            [.. rows.Select(row => new[] { row.Values["f.one"], row.Values["f.two"] })],
            [[0.7071, 0.7071], [0.7071, -0.7071]],
            [0.6d, 0.25d]);
        return new GenomeReportInput(
            commandLine,
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            new GenomeReportOptions(1, 5000, "all", "kmeans", 0, stabilityRounds, 0d, 5, 10, false),
            saves,
            ecology,
            lineage,
            mortality,
            rows,
            100,
            100,
            [],
            [],
            ["f.one", "f.two", "constant"],
            2,
            ["constant"],
            pca,
            clustering,
            candidates,
            withAutoK ? 5 : 0,
            NoValidCandidate: false,
            stability,
            Silhouette.EuclideanSampleSize,
            enrichment,
            sampled);
    }
}
