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
            "--seed", "1");

        Assert.Equal(0, ExitCode);
        Assert.True(File.Exists(Path.Combine(outputRoot, "report.html")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "report.clusters.csv")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "timeline.csv")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "metrics.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "enrichment.csv")));
        Assert.Contains("wrote ", Output, StringComparison.Ordinal);
        using var metrics = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputRoot, "metrics.json")));
        Assert.Equal(JsonValueKind.Object, metrics.RootElement.ValueKind);
        Assert.True(metrics.RootElement.GetProperty("clustering").GetProperty("k").GetInt32() >= 1);
        Assert.Contains("save,time,tick,creatures,analyzed,food,corpses,food_sources",
            File.ReadLines(Path.Combine(outputRoot, "timeline.csv")).First(), StringComparison.Ordinal);
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
        Assert.Contains("undecodable", Error, StringComparison.Ordinal);
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
        var first = SaveFixture.CreateV7Save(directory, "save_t1", [("c0", genome), ("c1", genome)], time: 10d);
        var second = SaveFixture.CreateV7Save(
            directory, "save_t2", [("c2", genome), ("c3", genome)], time: 20d,
            extraControllerPairs: new Dictionary<string, object>
            {
                ["demo.paused"] = true,
                ["demo.phase"] = "running",
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
        return SaveFixture.CreateV7Save(directory, "save_cli", creatures, time: 10d, food: 3, corpses: 1, foodSources: 2);
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
