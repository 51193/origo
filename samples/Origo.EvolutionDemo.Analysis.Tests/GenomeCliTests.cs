using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Serializes tests that redirect the process-wide console streams.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleSerialGroup
{
    /// <summary>Collection name shared by console-capturing tests.</summary>
    public const string Name = "console";
}

/// <summary>Regression tests for the genome CLI contract: help, validation, outputs, and skips.</summary>
[Collection(ConsoleSerialGroup.Name)]
public sealed class GenomeCliTests
{
    [Fact]
    public void Main_NoArguments_PrintsUsageAndReturnsTwo()
    {
        var (ExitCode, Output, _) = Run();

        Assert.Equal(2, ExitCode);
        Assert.Contains("Usage:", Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_Help_ReturnsZeroForRootAndGenomeCommand()
    {
        var (ExitCode, _, _) = Run("--help");
        var genome = Run("genome", "-h");

        Assert.Equal(0, ExitCode);
        Assert.Equal(0, genome.ExitCode);
        Assert.Contains("--metrics-json", genome.Output, StringComparison.Ordinal);
        Assert.Contains("--allow-skips", genome.Output, StringComparison.Ordinal);
        Assert.Contains("--lineages-csv", genome.Output, StringComparison.Ordinal);
        Assert.Contains("--root-lineages-csv", genome.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_UnknownCommand_ReturnsTwo()
    {
        var (ExitCode, _, Error) = Run("nope");

        Assert.Equal(2, ExitCode);
        Assert.Contains("Unknown command", Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_MissingSave_ReturnsOne()
    {
        var (ExitCode, _, Error) = Run("genome", "--out", "x.html");

        Assert.Equal(1, ExitCode);
        Assert.Contains("requires at least one --save", Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--method", "nope")]
    [InlineData("--mode", "nope")]
    public void Genome_UnknownChoice_ReturnsOne(string option, string value)
    {
        var (ExitCode, _, Error) = Run("genome", "--save", "x", option, value);

        Assert.Equal(1, ExitCode);
        Assert.Contains("Unknown", Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--max", "-1")]
    [InlineData("--k", "-1")]
    [InlineData("--stability", "-1")]
    [InlineData("--eps", "-1")]
    [InlineData("--min-points", "0")]
    [InlineData("--min-cluster-size", "1")]
    [InlineData("--max", "not-a-number")]
    public void Genome_InvalidNumericOption_ReturnsOne(string option, string value)
    {
        var (ExitCode, _, _) = Run("genome", "--save", "x", option, value);

        Assert.Equal(1, ExitCode);
    }

    [Fact]
    public void Genome_IncompatibleOptionCombinations_ReturnOne()
    {
        var (ExitCode, _, _) = Run("genome", "--save", "x", "--method", "dbscan", "--k", "2");
        var epsWithKMeans = Run("genome", "--save", "x", "--method", "kmeans", "--eps", "1");
        var minPointsWithJaccard = Run("genome", "--save", "x", "--method", "jaccard", "--min-points", "4");
        var stabilityWithDbscan = Run("genome", "--save", "x", "--method", "dbscan", "--stability", "3");
        var minClusterSizeWithDbscan = Run("genome", "--save", "x", "--method", "dbscan", "--min-cluster-size", "3");

        Assert.Equal(1, ExitCode);
        Assert.Equal(1, epsWithKMeans.ExitCode);
        Assert.Equal(1, minPointsWithJaccard.ExitCode);
        Assert.Equal(1, stabilityWithDbscan.ExitCode);
        Assert.Equal(1, minClusterSizeWithDbscan.ExitCode);
    }

    [Fact]
    public void Genome_UnknownArgumentAndMissingValue_ReturnOne()
    {
        var (ExitCode, _, Error) = Run("genome", "--save", "x", "--not-an-option");
        var missingValue = Run("genome", "--save");

        Assert.Equal(1, ExitCode);
        Assert.Contains("--help", Error, StringComparison.Ordinal);
        Assert.Equal(1, missingValue.ExitCode);
    }

    [Fact]
    public void Genome_MissingSavePath_ReturnsOne()
    {
        using var directory = new TemporaryDirectory();
        var missing = Path.Combine(directory.Path, "no-such-save");

        var (ExitCode, Output, Error) = Run("genome", "--save", missing, "--out", Path.Combine(directory.Path, "report.html"));

        Assert.Equal(1, ExitCode);
        Assert.Contains("FileNotFoundException", Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_ValidSave_WritesEveryOutputAndCreatesDirectories()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var outputRoot = Path.Combine(directory.Path, "nested", "reports");
        var (ExitCode, Output, Error) = Run(
            "genome", "--save", save,
            "--out", Path.Combine(outputRoot, "report.html"),
            "--timeline-csv", Path.Combine(outputRoot, "timeline.csv"),
            "--metrics-json", Path.Combine(outputRoot, "metrics.json"),
            "--enrichment-csv", Path.Combine(outputRoot, "enrichment.csv"),
            "--lineages-csv", Path.Combine(outputRoot, "lineages.csv"),
            "--seed", "1");

        Assert.Equal(0, ExitCode);
        Assert.True(File.Exists(Path.Combine(outputRoot, "report.html")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "report.clusters.csv")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "timeline.csv")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "metrics.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "enrichment.csv")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "lineages.csv")));
        Assert.Contains("wrote ", Output, StringComparison.Ordinal);
        using var metrics = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputRoot, "metrics.json")));
        Assert.Equal(JsonValueKind.Object, metrics.RootElement.ValueKind);
        Assert.True(metrics.RootElement.GetProperty("clustering").GetProperty("k").GetInt32() >= 1);
        Assert.True(metrics.RootElement.GetProperty("lineageSequence").TryGetProperty("distinctLineages", out _));
        Assert.True(metrics.RootElement.GetProperty("saves")[0].TryGetProperty("lineage", out _));
        Assert.Equal(11, metrics.RootElement.GetProperty("saves")[0].GetProperty("deaths").GetProperty("predation").GetInt32());
        Assert.Contains("Lineages and founder cohorts", File.ReadAllText(Path.Combine(outputRoot, "report.html")), StringComparison.Ordinal);
        Assert.Equal(11, metrics.RootElement.GetProperty("mortalitySequence").GetProperty("counts").GetProperty("predation").GetInt32());
        var timelineHeader = File.ReadLines(Path.Combine(outputRoot, "timeline.csv")).First();
        Assert.Contains("lineage.count", timelineHeader, StringComparison.Ordinal);
        Assert.Contains("death.predation", timelineHeader, StringComparison.Ordinal);
        Assert.Contains("save,time,lineage,creatures,share,founderCreatures",
            File.ReadLines(Path.Combine(outputRoot, "lineages.csv")).First(), StringComparison.Ordinal);
        Assert.Contains("save,time,tick,creatures,analyzed,food,corpses,food_sources",
            File.ReadLines(Path.Combine(outputRoot, "timeline.csv")).First(), StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_TimelineAndEnrichmentCreateTheirOwnParentDirectories()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var reportDir = Path.Combine(directory.Path, "report");
        var timeline = Path.Combine(directory.Path, "timeline", "timeline.csv");
        var enrichment = Path.Combine(directory.Path, "enrichment", "enrichment.csv");

        var (ExitCode, _, Error) = Run(
            "genome", "--save", save,
            "--out", Path.Combine(reportDir, "report.html"),
            "--timeline-csv", timeline,
            "--enrichment-csv", enrichment);

        Assert.Equal(0, ExitCode);
        Assert.True(File.Exists(timeline), Error);
        Assert.True(File.Exists(enrichment), Error);
    }

    [Fact]
    public void Genome_OldSaveWithoutDeathCountersLeavesMortalityBlank()
    {
        using var directory = new TemporaryDirectory();
        var save = SaveFixture.CreateV7Save(
            directory, "old_save",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 4));
        var timeline = Path.Combine(directory.Path, "timeline.csv");
        var metrics = Path.Combine(directory.Path, "metrics.json");
        var report = Path.Combine(directory.Path, "report.html");

        var (exitCode, output, error) = Run(
            "genome", "--save", save, "--out", report,
            "--timeline-csv", timeline, "--metrics-json", metrics);

        Assert.True(exitCode == 0, output + error);
        var header = File.ReadLines(timeline).First().Split(',');
        var data = File.ReadLines(timeline).Skip(1).First().Split(',');
        var starvation = Array.IndexOf(header, "death.starvation");
        Assert.True(starvation >= 0);
        Assert.Equal(string.Empty, data[starvation]);
        using var json = JsonDocument.Parse(File.ReadAllText(metrics));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("mortalitySequence").GetProperty("counts").ValueKind);
        Assert.False(json.RootElement.GetProperty("saves")[0].TryGetProperty("deaths", out _));
        Assert.DoesNotContain("Death causes", File.ReadAllText(report), StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_LineageCsvRowsAndDuplicateSaveIdsAreDisambiguated()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var save1 = SaveFixture.CreateV7Save(
            first, "save_final",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 3),
            time: 10d,
            lineages: [1, 1, 2],
            generations: [0, 1, 2]);
        var save2 = SaveFixture.CreateV7Save(
            second, "save_final",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 2),
            time: 20d,
            lineages: [2, 3],
            generations: [1, 0]);
        var lineagesCsv = Path.Combine(first.Path, "lineages.csv");
        var rootLineagesCsv = Path.Combine(first.Path, "root-lineages.csv");
        var timelineCsv = Path.Combine(first.Path, "timeline.csv");
        var metricsJson = Path.Combine(first.Path, "metrics.json");

        var (exitCode, _, error) = Run(
            "genome",
            "--save", save1,
            "--save", save2,
            "--out", Path.Combine(first.Path, "report.html"),
            "--timeline-csv", timelineCsv,
            "--lineages-csv", lineagesCsv,
            "--root-lineages-csv", rootLineagesCsv,
            "--metrics-json", metricsJson);

        Assert.Equal(0, exitCode);
        var rows = File.ReadLines(lineagesCsv).Skip(1)
            .Select(line => line.Split(','))
            .ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal(2, rows.Select(row => row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, rows.Count(row => row[0] == "save_final#1"));
        Assert.Equal(2, rows.Count(row => row[0] == "save_final#2"));
        Assert.Contains(rows, row => row[0] == "save_final#2" && row[8] == "1" && row[9] == "1");
        var rootRows = File.ReadLines(rootLineagesCsv).Skip(1).Select(line => line.Split(',')).ToArray();
        Assert.Equal(4, rootRows.Length);
        Assert.Contains("save,time,rootLineage,creatures,share,founderCreatures",
            File.ReadLines(rootLineagesCsv).First(), StringComparison.Ordinal);

        var header = File.ReadLines(timelineCsv).First().Split(',');
        var data = File.ReadLines(timelineCsv).Skip(1).First().Split(',');
        Assert.Equal(header.Length, data.Length);
        Assert.Contains("lineage.count", header);
        Assert.Contains("lineage.newGen0", header);
        Assert.Contains("rootLineage.newGen0", header);
        Assert.Contains("origin.enrichment", header);
        var report = File.ReadAllText(Path.Combine(first.Path, "report.html"));
        Assert.Contains("Root lineages", report, StringComparison.Ordinal);
        Assert.Contains("Spawn origins", report, StringComparison.Ordinal);
        var metrics = JsonDocument.Parse(File.ReadAllText(metricsJson));
        Assert.True(metrics.RootElement.GetProperty("rootLineageSequence").TryGetProperty("distinctLineages", out _));
        Assert.True(metrics.RootElement.GetProperty("saves")[0].TryGetProperty("rootLineage", out _));
        Assert.True(metrics.RootElement.GetProperty("saves")[0].GetProperty("spawnOrigins").TryGetProperty("children", out _));
    }

    [Fact]
    public void Genome_SameSeed_ProducesDeterministicMetricsJson()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var output = Path.Combine(directory.Path, "deterministic.html");
        var metrics = Path.Combine(directory.Path, "deterministic.json");
        string[] arguments =
        [
            "genome", "--save", save, "--out", output, "--metrics-json", metrics, "--seed", "3",
        ];

        var (ExitCode, Output, Error) = Run(arguments);
        var firstJson = File.ReadAllText(metrics);
        var second = Run(arguments);
        var secondJson = File.ReadAllText(metrics);

        Assert.Equal(0, ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(firstJson, secondJson);
    }

    [Fact]
    public void Genome_SkippedCreatureWithoutAllowSkips_ReturnsOne()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateSaveWithCorruptGenome(directory);

        var (ExitCode, Output, Error) = Run("genome", "--save", save, "--out", Path.Combine(directory.Path, "report.html"));

        Assert.Equal(1, ExitCode);
        Assert.Contains("could not be analyzed", Error, StringComparison.Ordinal);
        Assert.Contains("--allow-skips", Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_SkippedCreatureWithAllowSkips_WarnsAndSucceeds()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateSaveWithCorruptGenome(directory);

        var (ExitCode, Output, Error) = Run(
            "genome", "--save", save, "--out", Path.Combine(directory.Path, "report.html"), "--allow-skips");

        Assert.Equal(0, ExitCode);
        Assert.Contains("WARNING: analyzed", Error, StringComparison.Ordinal);
        Assert.Contains("skipped", Error, StringComparison.Ordinal);
        Assert.DoesNotContain("sampled 3 of 4", Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_ReaderLevelSkipWithAllowSkips_DoesNotClaimMaxSampling()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateDemoFounder(new Random(24)));
        var save = SaveFixture.CreateV7Save(
            directory, "save_reader_skip",
            [("good_0", genome), ("missing_0", string.Empty), ("good_1", genome), ("good_2", genome)]);

        var (ExitCode, _, Error) = Run(
            "genome", "--save", save, "--out", Path.Combine(directory.Path, "skip.html"), "--allow-skips");

        Assert.Equal(0, ExitCode);
        Assert.Contains("WARNING: analyzed 3 of 4 save creatures", Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--max 5000 sampled", Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_MaxSampling_WarnsOnStderr()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);

        var (ExitCode, Output, Error) = Run(
            "genome", "--save", save, "--out", Path.Combine(directory.Path, "report.html"), "--max", "3");

        Assert.Equal(0, ExitCode);
        Assert.Contains("WARNING: --max 3 sampled 3 of 8", Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_FixedKAndMinClusterSize_AreReportedInProvenance()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var output = Path.Combine(directory.Path, "fixed.html");
        var metrics = Path.Combine(directory.Path, "fixed.json");

        var (ExitCode, Output, Error) = Run(
            "genome", "--save", save, "--out", output, "--metrics-json", metrics,
            "--k", "2", "--min-cluster-size", "2", "--seed", "1");

        Assert.Equal(0, ExitCode);
        using var json = JsonDocument.Parse(File.ReadAllText(metrics));
        var options = json.RootElement.GetProperty("options");
        Assert.Equal(2, options.GetProperty("requestedK").GetInt32());
        Assert.Equal(2, options.GetProperty("actualK").GetInt32());
        Assert.Equal(2, options.GetProperty("minClusterSize").GetInt32());
        Assert.Contains("k=2 (fixed)", File.ReadAllText(output), StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_NonKMeansReportsNullBootstrapAri()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var metrics = Path.Combine(directory.Path, "jaccard.json");

        var (ExitCode, Output, Error) = Run(
            "genome", "--save", save, "--out", Path.Combine(directory.Path, "jaccard.html"),
            "--metrics-json", metrics, "--method", "jaccard", "--k", "2", "--seed", "1");

        Assert.Equal(0, ExitCode);
        using var json = JsonDocument.Parse(File.ReadAllText(metrics));
        Assert.Equal(JsonValueKind.Null,
            json.RootElement.GetProperty("clustering").GetProperty("bootstrapAri").ValueKind);
    }

    [Fact]
    public void Genome_MultiSceneDirectory_RequiresPoolThenPools()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateDemoFounder(new Random(21)));
        SaveFixture.CreateV7Save(directory, "save_a", [("a0", genome), ("a1", genome)], time: 10d);
        SaveFixture.CreateV7Save(directory, "save_b", [("b0", genome), ("b1", genome)], time: 20d);

        var (ExitCode, Output, Error) = Run("genome", "--save", directory.Path, "--out", Path.Combine(directory.Path, "no-pool.html"));
        Assert.Equal(1, ExitCode);
        Assert.Contains("--pool", Error, StringComparison.Ordinal);

        var metrics = Path.Combine(directory.Path, "pooled.json");
        var withPool = Run("genome", "--save", directory.Path, "--pool",
            "--out", Path.Combine(directory.Path, "pooled.html"), "--metrics-json", metrics);
        Assert.Equal(0, withPool.ExitCode);
        using var json = JsonDocument.Parse(File.ReadAllText(metrics));
        Assert.Equal(4, json.RootElement.GetProperty("population").GetProperty("total").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("saves").GetArrayLength());
    }

    [Fact]
    public void Genome_TimelineCsv_HasOneRowPerSaveAndTypedControllerValues()
    {
        using var directory = new TemporaryDirectory();
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateDemoFounder(new Random(22)));
        var first = SaveFixture.CreateV7Save(
            directory, "save_t1", [("c0", genome), ("c1", genome)], time: 10d,
            extraControllerPairs: new Dictionary<string, object>
            {
                ["evo.births"] = 10d,
                ["evo.spikeKills"] = 2d,
            });
        var second = SaveFixture.CreateV7Save(
            directory, "save_t2", [("c2", genome), ("c3", genome), ("c4", genome)], time: 20d,
            extraControllerPairs: new Dictionary<string, object>
            {
                ["demo.paused"] = true,
                ["demo.phase"] = "running",
                ["evo.births"] = 15d,
                ["evo.spikeKills"] = 4d,
                ["evo.enrichmentSpawns"] = 1d,
            });
        var timeline = Path.Combine(directory.Path, "timeline.csv");

        var (ExitCode, _, Error) = Run(
            "genome", "--save", first, "--save", second, "--timeline-csv", timeline,
            "--out", Path.Combine(directory.Path, "timeline.html"));

        Assert.Equal(0, ExitCode);
        var lines = File.ReadAllLines(timeline);
        Assert.Equal(3, lines.Length);
        Assert.Contains("controller.demo.paused", lines[0], StringComparison.Ordinal);
        Assert.Contains("controller.demo.phase", lines[0], StringComparison.Ordinal);
        Assert.Contains("save_t1,10", lines[1], StringComparison.Ordinal);
        Assert.Contains("save_t2,20", lines[2], StringComparison.Ordinal);
        Assert.Contains("true", lines[2], StringComparison.Ordinal);
        Assert.Contains("running", lines[2], StringComparison.Ordinal);
        var header = lines[0].Split(',');
        var row = lines[2].Split(',');
        var deaths = row[Array.IndexOf(header, "delta.deathsEstimated")];
        var predation = row[Array.IndexOf(header, "delta.predationShareEstimated")];
        Assert.Equal("5", deaths);
        Assert.Equal("0.4", predation);
    }

    [Fact]
    public void Genome_FeaturesCsvTimelinePercentilesAndEcologyMetrics_AreWritten()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var timeline = Path.Combine(directory.Path, "timeline.csv");
        var features = Path.Combine(directory.Path, "features.csv");
        var metrics = Path.Combine(directory.Path, "metrics.json");
        var (exitCode, _, _) = Run(
            "genome", "--save", save,
            "--out", Path.Combine(directory.Path, "report.html"),
            "--timeline-csv", timeline,
            "--timeline-percentiles",
            "--features-csv", features,
            "--metrics-json", metrics);

        Assert.Equal(0, exitCode);
        var timelineHeader = File.ReadLines(timeline).First();
        Assert.Contains("food.soft", timelineHeader, StringComparison.Ordinal);
        Assert.Contains("access.foodInSenseShare", timelineHeader, StringComparison.Ordinal);
        Assert.Contains("origin.founderShare", timelineHeader, StringComparison.Ordinal);
        Assert.Contains("delta.deathsEstimated", timelineHeader, StringComparison.Ordinal);
        Assert.Contains("behavior.age.p50", timelineHeader, StringComparison.Ordinal);
        var featuresHeader = File.ReadLines(features).First();
        Assert.Contains("access.foodInSense", featuresHeader, StringComparison.Ordinal);
        Assert.Contains("diet.hasMeals", featuresHeader, StringComparison.Ordinal);
        Assert.Contains("origin.generationZero", featuresHeader, StringComparison.Ordinal);
        Assert.Contains("cluster", featuresHeader, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(File.ReadAllText(metrics));
        var saveJson = json.RootElement.GetProperty("saves")[0];
        Assert.Equal(JsonValueKind.Number, saveJson.GetProperty("food").ValueKind);
        Assert.Equal(3, saveJson.GetProperty("foodInventory").GetProperty("soft").GetInt32());
        Assert.True(saveJson.GetProperty("access").TryGetProperty("foodInSenseShare", out _));
        Assert.True(saveJson.GetProperty("controller").GetProperty("numbers").TryGetProperty("evo.tick", out _));
        Assert.True(saveJson.GetProperty("diet").TryGetProperty("living", out _));
    }

    [Fact]
    public void Genome_BehaviorModeIncludesDietAndAccessFeatures()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var output = Path.Combine(directory.Path, "behavior.html");

        var (exitCode, _, _) = Run("genome", "--save", save, "--mode", "behavior", "--out", output);

        Assert.Equal(0, exitCode);
        var html = File.ReadAllText(output);
        Assert.Contains("diet.hasMeals", html, StringComparison.Ordinal);
        Assert.Contains("access.foodInSense", html, StringComparison.Ordinal);
        Assert.DoesNotContain("origin.generationZero", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Genome_SaveIntegrityWarnings_AreReportedOnStderrAndHtml()
    {
        using var directory = new TemporaryDirectory();
        var save = CreateVariedSave(directory);
        var output = Path.Combine(directory.Path, "warnings.html");

        var (ExitCode, _, Error) = Run("genome", "--save", save, "--out", output);

        Assert.Equal(0, ExitCode);
        Assert.Contains(".payload.sha", Error, StringComparison.Ordinal);
        Assert.Contains(".payload.sha", File.ReadAllText(output), StringComparison.Ordinal);
        Assert.Contains("Controller counters (latest save)", File.ReadAllText(output), StringComparison.Ordinal);
    }

    private static string CreateVariedSave(TemporaryDirectory directory)
    {
        var creatures = Enumerable.Range(0, 8)
            .Select(index => ($"creature_{index}", SaveFixture.Encode(GenomeV4Factory.CreateDemoFounder(new Random(index)))))
            .ToArray();
        return SaveFixture.CreateV7Save(
            directory, "save_cli", creatures, time: 10d, food: 3, corpses: 1, foodSources: 2,
            extraControllerPairs: new Dictionary<string, object>
            {
                ["evo.starvationDeaths"] = 7d,
                ["evo.predationDeaths"] = 11d,
                ["evo.ageDeaths"] = 2d,
            });
    }

    private static string CreateSaveWithCorruptGenome(TemporaryDirectory directory)
    {
        var genome = SaveFixture.Encode(GenomeV4Factory.CreateDemoFounder(new Random(11)));
        return SaveFixture.CreateV7Save(directory, "save_cli_skip",
        [
            ("good_0", genome),
            ("bad_0", "b64:AAAA"),
            ("good_1", SaveFixture.Encode(GenomeV4Factory.CreateDemoFounder(new Random(12)))),
            ("good_2", SaveFixture.Encode(GenomeV4Factory.CreateDemoFounder(new Random(13)))),
        ]);
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] arguments)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = Origo.EvolutionDemo.Analysis.Program.Main(arguments);
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
