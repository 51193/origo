using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Origo.EvolutionDemo.Analysis.Perf;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>
///     Acceptance test against the frozen real profile. The profile lives outside the
///     tracked tree, so the test skips when it is absent instead of failing a fresh clone.
/// </summary>
public sealed partial class PerfRealProfileTests
{
    private const string CoreSystem = "evolution.core";

    [Fact]
    public void RealProfile_ClassifiesSystemsAndSubphasesAndMatchesIndependentAggregates()
    {
        var path = FindFrozenProfile();
        Assert.SkipUnless(path is not null,
            "Frozen real profile not found; set ORIGO_ANALYSIS_PROFILE or run from the repository checkout.");
        var ticks = ProfileReader.Read(path!);
        var segments = ProfileSegments.Split(ticks);

        Assert.Equal(2, segments.Count);
        Assert.Equal(11, segments[0].Ticks.Count);
        Assert.Equal(900, segments[1].Ticks.Count);

        var pooled = new PerfAggregator(ProfileSegments.Select(ticks, path!, "all").Ticks);
        Assert.Equal(911, pooled.SampleCount);
        Assert.Equal(8, pooled.SystemSummaries.Count);
        Assert.Equal(5, pooled.SubphaseSummaries.Count);
        Assert.All(pooled.SubphaseSummaries, static summary => Assert.Equal(CoreSystem, summary.Parent));
        var shareSum = pooled.SystemSummaries.Sum(summary => 100d * summary.Stats.Mean / pooled.SystemMeanTotal);
        Assert.InRange(shareSum, 99.9d, 100.1d);
        var coreShare = 100d * pooled.SystemSummaries.Single(static summary => summary.Name == CoreSystem).Stats.Mean
            / pooled.SystemMeanTotal;
        Assert.InRange(coreShare, 71.5d, 71.9d);
        Assert.Equal(74, pooled.Gc0SampledSum);
        Assert.Equal(35, pooled.Gc1SampledSum);
        Assert.Equal(1, pooled.Gc2SampledSum);
        Assert.Equal(10, pooled.TickInterval);
        Assert.Equal(740, pooled.Gc0RunEstimate);
        Assert.Equal(350, pooled.Gc1RunEstimate);
        Assert.Equal(10, pooled.Gc2RunEstimate);
        Assert.InRange(pooled.Wall.P95, 24.4626d - 0.001d, 24.4626d + 0.001d);
        Assert.InRange(pooled.Wall.P99, 27.1571d - 0.001d, 27.1571d + 0.001d);

        var last = new PerfAggregator(ProfileSegments.Select(ticks, path!).Ticks);
        Assert.Equal(900, last.SampleCount);
        Assert.Equal(72, last.Gc0SampledSum);
        Assert.Equal(33, last.Gc1SampledSum);
        Assert.Equal(0, last.Gc2SampledSum);
        Assert.Equal(720, last.Gc0RunEstimate);

        Assert.Equal(99, ProfileSegments.Select(ticks, path!, null, 100d, 200d).Ticks.Count);
        Assert.Equal(11, ProfileSegments.Select(ticks, path!, null, 0d, 12d).Ticks.Count);
        Assert.Equal(22, ProfileSegments.Select(ticks, path!, "all", 0d, 12d).Ticks.Count);

        var html = PerfHtmlReport.Build(pooled, ProfileSegments.Select(ticks, path!, "all"));
        Assert.Equal(8, SectionRowCount(html, "System breakdown"));
        Assert.Equal(5, SectionRowCount(html, "Subphases"));
        Assert.Contains("71.7%", html, StringComparison.Ordinal);
        Assert.Contains("<span class=card>GC 0/1/2 Σ(911 samples) <b>74/35/1</b></span>", html,
            StringComparison.Ordinal);
        Assert.Contains("<span class=card>GC 0/1/2 est./run <b>740/350/10</b></span>", html,
            StringComparison.Ordinal);
    }

    private static int SectionRowCount(string html, string section)
    {
        var start = html.IndexOf($"<h2>{section}</h2>", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Section '{section}' not found.");
        var table = html[start..];
        table = table[..table.IndexOf("</table>", StringComparison.Ordinal)];
        return RowRegex().Count(table);
    }

    private static string? FindFrozenProfile()
    {
        var explicitPath = Environment.GetEnvironmentVariable("ORIGO_ANALYSIS_PROFILE");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return File.Exists(explicitPath) ? explicitPath : null;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, ".scratch", "analysis-fix", "corpus", "profile-review.jsonl");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    [GeneratedRegex("<tr><td><code>")]
    private static partial Regex RowRegex();
}
