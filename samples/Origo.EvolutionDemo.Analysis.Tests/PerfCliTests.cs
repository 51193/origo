using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for the perf/compare CLI: segments, windows, help, validation, and n/a output.</summary>
[Collection(ConsoleSerialGroup.Name)]
public sealed class PerfCliTests
{
    [Fact]
    public void Perf_TwoSegmentProfileDefaultsToLastAndWarns()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());
        var output = Path.Combine(directory.Path, "nested", "perf.html");

        var (exitCode, stdout, stderr) = Run("perf", "--profile", profile, "--out", output);

        Assert.Equal(0, exitCode);
        Assert.Contains("(100 samples", stdout, StringComparison.Ordinal);
        Assert.Contains("segments=2", stdout, StringComparison.Ordinal);
        Assert.Contains("simulation segments", stderr, StringComparison.Ordinal);
        var html = File.ReadAllText(output);
        Assert.Contains("samples=100", html, StringComparison.Ordinal);
        Assert.Contains("segments=2", html, StringComparison.Ordinal);
        Assert.Contains("segment=last", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_SegmentAllPoolsEverySegment()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, stdout, stderr) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"), "--segment", "all");

        Assert.Equal(0, exitCode);
        Assert.Contains("(111 samples", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("simulation segments", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_SegmentIndexSelectsSegmentAndWarnsOnSmallSamples()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, stdout, stderr) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"), "--segment", "0");

        Assert.Equal(0, exitCode);
        Assert.Contains("(11 samples", stdout, StringComparison.Ordinal);
        Assert.Contains("unstable below 100 samples", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2", "out of range")]
    [InlineData("bogus", "--segment expects all, last")]
    [InlineData("-1", "--segment expects all, last")]
    public void Perf_InvalidSegment_ReturnsOne(string segment, string message)
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, _, stderr) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"), "--segment", segment);

        Assert.Equal(1, exitCode);
        Assert.Contains(message, stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_WindowAppliesInsideSelectedSegment()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, _, stderr) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"), "--from", "0", "--to", "12");

        Assert.Equal(0, exitCode);
        var html = File.ReadAllText(Path.Combine(directory.Path, "perf.html"));
        Assert.Contains("samples=12", html, StringComparison.Ordinal);
        Assert.Contains("window=0..12", html, StringComparison.Ordinal);
        Assert.Contains("unstable below 100 samples", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_AllPlusWindowPoolsEverySegment()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, stdout, _) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"), "--segment", "all", "--from", "0", "--to", "12");

        Assert.Equal(0, exitCode);
        Assert.Contains("(23 samples", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_EmptyWindow_ReturnsOne()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, _, stderr) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"), "--from", "500", "--to", "600");

        Assert.Equal(1, exitCode);
        Assert.Contains("window contains no samples", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_FromGreaterThanTo_ReturnsOne()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, _, stderr) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"), "--from", "20", "--to", "10");

        Assert.Equal(1, exitCode);
        Assert.Contains("perf --from must not be greater than --to", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_SingleSampleProfileHasNoIntervalCrash()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "single.jsonl",
            [PerfFixture.Tick(10, 1d, gc0: 5, phases: PerfFixture.StandardPhases())]);

        var (exitCode, _, _) = Run("perf", "--profile", profile, "--out",
            Path.Combine(directory.Path, "perf.html"));

        Assert.Equal(0, exitCode);
        var html = File.ReadAllText(Path.Combine(directory.Path, "perf.html"));
        Assert.Contains("samples=1", html, StringComparison.Ordinal);
        Assert.Contains("tickInterval=n/a", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_Help_ReturnsZeroAndListsSegmentAndWindowOptions()
    {
        var (exitCode, stdout, _) = Run("perf", "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--profile", stdout, StringComparison.Ordinal);
        Assert.Contains("--segment", stdout, StringComparison.Ordinal);
        Assert.Contains("--from", stdout, StringComparison.Ordinal);
        Assert.Contains("--to", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Perf_MissingProfileAndUnknownArgument_ReturnOne()
    {
        var (missingExit, _, missingError) = Run("perf", "--out", "x.html");
        var (unknownExit, _, unknownError) = Run("perf", "--profile", "x.jsonl", "--not-an-option");

        Assert.Equal(1, missingExit);
        Assert.Contains("requires --profile", missingError, StringComparison.Ordinal);
        Assert.Equal(1, unknownExit);
        Assert.Contains("--help", unknownError, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_RequiresBaselineAndCurrent()
    {
        var (exitCode, _, stderr) = Run("compare", "--baseline", "a.jsonl");

        Assert.Equal(1, exitCode);
        Assert.Contains("requires --baseline and --current", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_Help_ReturnsZeroAndListsSegmentAndWindowOptions()
    {
        var (exitCode, stdout, _) = Run("compare", "-h");

        Assert.Equal(0, exitCode);
        Assert.Contains("--baseline", stdout, StringComparison.Ordinal);
        Assert.Contains("--current", stdout, StringComparison.Ordinal);
        Assert.Contains("--segment", stdout, StringComparison.Ordinal);
        Assert.Contains("--from", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_UnknownArgument_ReturnsOne()
    {
        var (exitCode, _, stderr) = Run("compare", "--baseline", "a", "--current", "b", "--nope");

        Assert.Equal(1, exitCode);
        Assert.Contains("compare", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_OneSidedProfilesEmitNaRowsAndWarnings()
    {
        using var directory = new TemporaryDirectory();
        var baseline = PerfFixture.WriteProfile(directory.Path, "baseline.jsonl",
        [
            PerfFixture.Tick(10, 1d, phases: [("sys.alpha", 10d, 100L)]),
            PerfFixture.Tick(20, 2d, phases: [("sys.alpha", 10d, 100L)]),
        ]);
        var current = PerfFixture.WriteProfile(directory.Path, "current.jsonl",
        [
            PerfFixture.Tick(10, 1d, phases: [("sys.alpha", 10d, 100L), ("sys.beta", 5d, 50L)]),
            PerfFixture.Tick(20, 2d, phases: [("sys.alpha", 10d, 100L), ("sys.beta", 5d, 50L)]),
        ]);
        var output = Path.Combine(directory.Path, "compare.html");

        var (exitCode, stdout, stderr) = Run("compare", "--baseline", baseline, "--current", current, "--out", output);

        Assert.Equal(0, exitCode);
        Assert.Contains(" n/a)", stdout, StringComparison.Ordinal);
        Assert.Contains("exists only in the current profile", stderr, StringComparison.Ordinal);
        var html = File.ReadAllText(output);
        Assert.Contains("n/a", html, StringComparison.Ordinal);
        Assert.Contains("samples=2", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_AppliesSegmentAndWindowOptionsToBothProfiles()
    {
        using var directory = new TemporaryDirectory();
        var baseline = PerfFixture.WriteProfile(directory.Path, "baseline.jsonl", PerfFixture.TwoSegmentProfile());
        var current = PerfFixture.WriteProfile(directory.Path, "current.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, _, _) = Run("compare", "--baseline", baseline, "--current", current,
            "--out", Path.Combine(directory.Path, "compare.html"), "--segment", "1", "--from", "0", "--to", "12");

        Assert.Equal(0, exitCode);
        var html = File.ReadAllText(Path.Combine(directory.Path, "compare.html"));
        Assert.Contains("window=0..12", html, StringComparison.Ordinal);
        Assert.Contains("samples=12", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_IntervalMismatchWarnsAndNormalizesGc()
    {
        using var directory = new TemporaryDirectory();
        var intervalOne = PerfFixture.WriteProfile(directory.Path, "interval1.jsonl",
            [.. Enumerable.Range(0, 1000).Select(index =>
                PerfFixture.Tick(index + 1, 1d + index, gc0: 1, phases: PerfFixture.StandardPhases()))]);
        var intervalTen = PerfFixture.WriteProfile(directory.Path, "interval10.jsonl",
            [.. Enumerable.Range(0, 100).Select(index =>
                PerfFixture.Tick((index + 1) * 10, 1d + index, gc0: 1, phases: PerfFixture.StandardPhases()))]);
        var output = Path.Combine(directory.Path, "compare.html");

        var (exitCode, _, stderr) = Run("compare", "--baseline", intervalOne, "--current", intervalTen, "--out", output);

        Assert.Equal(0, exitCode);
        Assert.Contains("different or unknown tick intervals", stderr, StringComparison.Ordinal);
        var html = File.ReadAllText(output);
        Assert.Contains("gc0 est./run", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_FromGreaterThanTo_ReturnsOne()
    {
        using var directory = new TemporaryDirectory();
        var profile = PerfFixture.WriteProfile(directory.Path, "profile.jsonl", PerfFixture.TwoSegmentProfile());

        var (exitCode, _, stderr) = Run("compare", "--baseline", profile, "--current", profile,
            "--out", Path.Combine(directory.Path, "compare.html"), "--from", "20", "--to", "10");

        Assert.Equal(1, exitCode);
        Assert.Contains("compare --from must not be greater than --to", stderr, StringComparison.Ordinal);
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
