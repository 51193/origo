using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>Everything the compare report renders: both sides, their metadata, and warnings.</summary>
/// <param name="BaselineSelection">Baseline segment/window selection.</param>
/// <param name="Baseline">Baseline aggregate.</param>
/// <param name="CurrentSelection">Current segment/window selection.</param>
/// <param name="Current">Current aggregate.</param>
/// <param name="Comparison">Comparison rows and comparison-level warnings.</param>
/// <param name="Warnings">All warnings to render (CLI selection warnings plus comparison warnings).</param>
public sealed record PerfCompareReportInput(
    ProfileSelection BaselineSelection,
    PerfAggregator Baseline,
    ProfileSelection CurrentSelection,
    PerfAggregator Current,
    PerfComparisonResult Comparison,
    IReadOnlyList<string> Warnings);

/// <summary>HTML table and metadata for a profile-to-profile comparison.</summary>
public static class PerfCompareHtmlReport
{
    /// <summary>Renders the comparison report.</summary>
    public static string Build(PerfCompareReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var html = new StringBuilder();
        html.Append("""
<!doctype html><html><head><meta charset="utf-8"><title>Performance Compare</title>
<style>
body{font:14px system-ui,sans-serif;margin:24px;background:#0b1220;color:#dbeafe}
h1{color:#7dd3fc}table{border-collapse:collapse;margin:8px 0}th,td{padding:5px 10px;border-bottom:1px solid #1e3a5f;text-align:right}
th:first-child,td:first-child{text-align:left}th{color:#93c5fd}code{color:#fbbf24}.up{color:#fca5a5}.down{color:#86efac}
.muted{color:#94a3b8}.warnings{background:#3b1d1d;border:1px solid #7f1d1d;border-radius:8px;padding:10px 16px;margin:8px 0}
</style></head><body><h1>Performance Compare</h1>
""");
        html.Append("<p>baseline: <code>").Append(Escape(input.BaselineSelection.Path)).Append("</code></p>");
        html.Append("<p class=\"muted\">").Append(Describe(input.Baseline, input.BaselineSelection)).Append("</p>");
        html.Append("<p>current: <code>").Append(Escape(input.CurrentSelection.Path)).Append("</code></p>");
        html.Append("<p class=\"muted\">").Append(Describe(input.Current, input.CurrentSelection)).Append("</p>");
        if (input.Warnings.Count > 0)
        {
            html.Append("<div class=warnings><b>Warnings</b><ul>");
            foreach (var warning in input.Warnings)
                html.Append("<li>").Append(Escape(warning)).Append("</li>");
            html.Append("</ul></div>");
        }

        html.Append("<table><tr><th>metric</th><th>baseline</th><th>current</th><th>delta</th></tr>");
        foreach (var delta in input.Comparison.Rows)
        {
            var className = delta.DeltaPercent switch
            {
                > 5 => "up",
                < -5 => "down",
                _ => string.Empty,
            };
            var deltaText = delta.DeltaPercent is { } percent
                ? percent.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%"
                : "n/a";
            html.Append("<tr><td><code>").Append(Escape(delta.Metric)).Append("</code></td><td>")
                .Append(FormatValue(delta.Baseline, delta.Unit)).Append("</td><td>")
                .Append(FormatValue(delta.Current, delta.Unit)).Append("</td><td class=\"")
                .Append(className).Append("\">").Append(Escape(deltaText)).Append("</td></tr>");
        }

        html.Append("</table>");
        html.Append(
            "<p class=\"muted\">Rows are the symmetric union of both profiles; n/a means the metric exists in "
            + "only one profile or the baseline is zero. GC rows compare the interval-normalized estimated run "
            + "totals, not raw sampled sums. Compare profiles measured with the same scenario, seed, sampling "
            + "interval, and duration for a meaningful delta.</p>");
        html.Append("</body></html>");
        return html.ToString();
    }

    private static string Describe(PerfAggregator aggregate, ProfileSelection selection)
    {
        var html = new StringBuilder();
        html.Append(CultureInfo.InvariantCulture,
            $"samples={aggregate.SampleCount}; simTime={aggregate.SimTimeStart:0.0}..{aggregate.SimTimeEnd:0.0}; ");
        if (aggregate.TickInterval is { } interval)
            html.Append(CultureInfo.InvariantCulture, $"tickInterval={interval}");
        else
            html.Append("tickInterval=n/a");
        html.Append(CultureInfo.InvariantCulture, $"; segments={selection.SegmentCount}");
        if (selection.SegmentCount > 1)
            html.Append("; segment=").Append(selection.SegmentLabel);
        if (selection.HasWindow)
            html.Append("; window=").Append(selection.WindowLabel);
        html.Append("; gcSampled=").Append(aggregate.Gc0SampledSum.ToString(CultureInfo.InvariantCulture))
            .Append('/').Append(aggregate.Gc1SampledSum.ToString(CultureInfo.InvariantCulture))
            .Append('/').Append(aggregate.Gc2SampledSum.ToString(CultureInfo.InvariantCulture));
        html.Append("; gcEst=").Append(FormatCount(aggregate.Gc0RunEstimate)).Append('/')
            .Append(FormatCount(aggregate.Gc1RunEstimate)).Append('/')
            .Append(FormatCount(aggregate.Gc2RunEstimate));
        return html.ToString();
    }

    private static string FormatValue(double? value, PerfMetricUnit unit)
    {
        if (value is not { } number)
            return "n/a";
        return unit switch
        {
            PerfMetricUnit.Milliseconds => number.ToString("0.000", CultureInfo.InvariantCulture),
            PerfMetricUnit.Mebibytes => number.ToString("0.000", CultureInfo.InvariantCulture),
            PerfMetricUnit.Collections => number.ToString("0", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unknown metric unit '{unit}'."),
        };
    }

    private static string FormatCount(long? value) =>
        value is { } count ? count.ToString(CultureInfo.InvariantCulture) : "n/a";

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
