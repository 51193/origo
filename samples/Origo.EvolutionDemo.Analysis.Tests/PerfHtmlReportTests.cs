using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Origo.EvolutionDemo.Analysis.Perf;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for the perf HTML report: tables, shares, GC labels, percentiles, metadata.</summary>
public sealed partial class PerfHtmlReportTests
{
    [Fact]
    public void Build_RendersSystemAndSubphaseTablesWithParent()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 3));
        var html = PerfHtmlReport.Build(aggregate);

        Assert.Equal(2, SectionRowCount(html, "System breakdown"));
        Assert.Equal(2, SectionRowCount(html, "Subphases"));
        Assert.Contains("<tr><td><code>sys.alpha.child</code></td><td><code>sys.alpha</code></td>", html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ShareUsesSystemMeanTotalOnly()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 3));
        var html = PerfHtmlReport.Build(aggregate);
        var rows = SectionRows(html, "System breakdown");

        Assert.Equal(2, rows.Length);
        var alpha = Assert.Single(rows, static row => row[0] == "sys.alpha");
        var beta = Assert.Single(rows, static row => row[0] == "sys.beta");
        Assert.Equal("80.0%", alpha[6]);
        Assert.Equal("20.0%", beta[6]);
        Assert.Contains("share denominator = sum of system means = 15.0000 ms", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sys.alpha.child</code></td><td>6.0000", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_LabelsGcSampledSumAndRunEstimate()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 100, tickInterval: 10));
        var html = PerfHtmlReport.Build(aggregate);

        Assert.Contains("<span class=card>GC 0/1/2 Σ(100 samples) <b>100/200/0</b></span>", html,
            StringComparison.Ordinal);
        Assert.Contains("<span class=card>GC 0/1/2 est./run <b>1000/2000/0</b></span>", html,
            StringComparison.Ordinal);
        Assert.Contains("tickInterval=10", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RendersNaWhenSamplingIntervalIsUnknown()
    {
        var aggregate = new PerfAggregator([PerfFixture.Tick(10, 1d, gc0: 5, phases: PerfFixture.StandardPhases())]);
        var html = PerfHtmlReport.Build(aggregate);

        Assert.Contains("tickInterval=n/a", html, StringComparison.Ordinal);
        Assert.Contains("<span class=card>GC 0/1/2 est./run <b>n/a/n/a/n/a</b></span>", html,
            StringComparison.Ordinal);
        Assert.Contains("<span class=card>GC 0/1/2 Σ(1 samples) <b>5/0/0</b></span>", html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UsesType7Percentiles()
    {
        TickProfile[] ticks =
        [
            PerfFixture.Tick(10, 1d, wallMs: 1d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(20, 2d, wallMs: 2d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(30, 3d, wallMs: 3d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(40, 4d, wallMs: 4d, phases: PerfFixture.StandardPhases()),
        ];
        var html = PerfHtmlReport.Build(new PerfAggregator(ticks));

        Assert.Contains("<span class=card>wall P50 <b>2.50 ms</b></span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=card>wall P95 <b>3.85 ms</b></span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=card>wall P99 <b>3.97 ms</b></span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ReportsPopulationFoodAndCorpsesP50()
    {
        var aggregate = new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 3));
        var html = PerfHtmlReport.Build(aggregate);

        Assert.Contains("population P50", html, StringComparison.Ordinal);
        Assert.Contains("food P50", html, StringComparison.Ordinal);
        Assert.Contains("corpses P50", html, StringComparison.Ordinal);
        Assert.Contains("Population, food, corpses per sample", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_IncludesSegmentAndWindowMetadata()
    {
        var selection = ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", from: 0d, to: 12d);
        var aggregate = new PerfAggregator(selection.Ticks);
        var html = PerfHtmlReport.Build(aggregate, selection,
            ["profile has 2 simulation segments; aggregated the last one (12 of 111 samples)."]);

        Assert.Contains("profile=profile.jsonl", html, StringComparison.Ordinal);
        Assert.Contains("segments=2", html, StringComparison.Ordinal);
        Assert.Contains("segment=last", html, StringComparison.Ordinal);
        Assert.Contains("window=0..12", html, StringComparison.Ordinal);
        Assert.Contains("simulation segments", html, StringComparison.Ordinal);
        Assert.Contains("<div class=warnings>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithoutSelectionOmitsProfileMetadata()
    {
        var html = PerfHtmlReport.Build(new PerfAggregator(PerfFixture.SampledSeries(sampleCount: 3)));

        Assert.DoesNotContain("profile=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("segments=", html, StringComparison.Ordinal);
        Assert.Contains("samples=3", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RendersWarningsAndEscapesNames()
    {
        var ticks = new[]
        {
            PerfFixture.Tick(10, 1d, phases: [("sys.<script>", 1d, 0L)]),
        };
        var aggregate = new PerfAggregator(ticks);
        var html = PerfHtmlReport.Build(aggregate, warnings: ["plain warning"]);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("sys.&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("<li>plain warning</li>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ThrowsOnNullAggregate()
    {
        Assert.Throws<ArgumentNullException>(() => PerfHtmlReport.Build(null!));
    }

    private static int SectionRowCount(string html, string section) => SectionRows(html, section).Length;

    private static string[][] SectionRows(string html, string section)
    {
        var start = html.IndexOf($"<h2>{section}</h2>", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Section '{section}' not found.");
        var table = html[start..];
        table = table[..table.IndexOf("</table>", StringComparison.Ordinal)];
        return
        [
            .. RowRegex().Matches(table).Skip(1).Select(row =>
                CellRegex().Matches(row.Groups[1].Value)
                    .Select(cell => TagRegex().Replace(cell.Groups[1].Value, string.Empty))
                    .ToArray()),
        ];
    }

    [GeneratedRegex("<tr>(.*?)</tr>")]
    private static partial Regex RowRegex();

    [GeneratedRegex("<td[^>]*>(.*?)</td>")]
    private static partial Regex CellRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
