using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>Self-contained HTML report for a performance profile.</summary>
public static class PerfHtmlReport
{
    /// <summary>
    ///     Renders the report for <paramref name="aggregate" />. <paramref name="selection" />
    ///     adds profile/segment/window metadata (null for a plain aggregate); the
    ///     optional warnings are rendered in the report and are also expected to be
    ///     printed to stderr by the caller.
    /// </summary>
    public static string Build(
        PerfAggregator aggregate,
        ProfileSelection? selection = null,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var html = new StringBuilder();
        html.Append("""
<!doctype html><html><head><meta charset="utf-8"><title>Evolution Performance</title>
<style>
body{font:14px system-ui,sans-serif;margin:24px;background:#0b1220;color:#dbeafe}
h1,h2{color:#7dd3fc}h1{font-size:24px}h2{font-size:18px;margin-top:28px}
table{border-collapse:collapse;margin:8px 0 20px}th,td{padding:4px 8px;border-bottom:1px solid #1e3a5f;text-align:right}
th:first-child,td:first-child{text-align:left}th{color:#93c5fd}code{color:#fbbf24}
.card{display:inline-block;background:#111c33;border:1px solid #1e3a5f;border-radius:8px;padding:10px 16px;margin:4px 8px 4px 0}
.muted{color:#94a3b8}.warnings{background:#3b1d1d;border:1px solid #7f1d1d;border-radius:8px;padding:10px 16px;margin:8px 0}
</style></head><body><h1>Evolution Performance</h1>
""");
        AppendHeader(html, aggregate, selection);
        AppendWarnings(html, warnings ?? []);
        html.Append("<div>");
        Card(html, "wall P50", FormatMs(aggregate.Wall.P50));
        Card(html, "wall P95", FormatMs(aggregate.Wall.P95));
        Card(html, "wall P99", FormatMs(aggregate.Wall.P99));
        Card(html, "alloc P50", FormatBytes(aggregate.Alloc.P50));
        Card(html, "alloc P95", FormatBytes(aggregate.Alloc.P95));
        Card(html, "GC 0/1/2 Σ(" + aggregate.SampleCount.ToString(CultureInfo.InvariantCulture) + " samples)",
            FormatCounts(aggregate.Gc0SampledSum, aggregate.Gc1SampledSum, aggregate.Gc2SampledSum));
        Card(html, "GC 0/1/2 est./run",
            FormatCounts(aggregate.Gc0RunEstimate, aggregate.Gc1RunEstimate, aggregate.Gc2RunEstimate));
        Card(html, "population P50", FormatInteger(aggregate.Population.P50));
        Card(html, "food P50", FormatInteger(aggregate.Food.P50));
        Card(html, "corpses P50", FormatInteger(aggregate.Corpses.P50));
        html.Append("</div>");
        html.Append(
            "<p class=\"muted\">share = phase mean / sum of one-dot system means; nested subphases are "
            + "measured inside their parent system and are never added. Percentiles use Hyndman–Fan type 7 "
            + "(linear interpolation between order statistics). GC 0/1/2 is the sum of collection counts "
            + "over the sampled ticks; est./run = that sum × tick interval, which estimates the run total "
            + "only when sampling is uniform.</p>");
        AppendSystems(html, aggregate);
        AppendSubphases(html, aggregate);
        html.Append("<h2>Phase p95 bars</h2>");
        html.Append(Bars(aggregate));
        html.Append("<h2>Population, food, corpses per sample</h2>");
        html.Append(PerSampleChart(aggregate));
        html.Append("<h2>Wall time per sample</h2>");
        html.Append(LineChart(aggregate));
        html.Append("</body></html>");
        return html.ToString();
    }

    private static void AppendHeader(StringBuilder html, PerfAggregator aggregate, ProfileSelection? selection)
    {
        html.Append("<p class=\"muted\">");
        if (selection is not null)
            html.Append("profile=").Append(Escape(selection.Path)).Append("; ");
        html.Append(CultureInfo.InvariantCulture,
            $"samples={aggregate.SampleCount}; simTime={aggregate.SimTimeStart:0.0}..{aggregate.SimTimeEnd:0.0}; ");
        if (aggregate.TickInterval is { } interval)
            html.Append(CultureInfo.InvariantCulture, $"tickInterval={interval}; ");
        else
            html.Append("tickInterval=n/a; ");
        if (SimInterval(aggregate.Ticks) is { } simInterval)
            html.Append(CultureInfo.InvariantCulture, $"simInterval≈{simInterval:0.0}s");
        else
            html.Append("simInterval=n/a");
        if (selection is not null)
        {
            html.Append(CultureInfo.InvariantCulture, $"; segments={selection.SegmentCount}");
            if (selection.SegmentCount > 1)
                html.Append("; segment=").Append(selection.SegmentLabel);
            if (selection.HasWindow)
                html.Append("; window=").Append(selection.WindowLabel);
        }

        html.Append("</p>");
        if (selection is { ImplicitLast: true })
            html.Append(string.Create(CultureInfo.InvariantCulture,
                $"<p class=\"muted\">The profile contains {selection.SegmentCount} simulation segments; this report aggregates the last one ({aggregate.SampleCount} samples). Use --segment all to pool segments or --segment &lt;index&gt; to select one.</p>"));
        if (selection is { HasWindow: true })
            html.Append(string.Create(CultureInfo.InvariantCulture,
                $"<p class=\"muted\">The window {selection.WindowLabel} is applied to the recorded simTime inside the selected segment; both bounds are inclusive.</p>"));
    }

    private static void AppendWarnings(StringBuilder html, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
            return;
        html.Append("<div class=warnings><b>Warnings</b><ul>");
        foreach (var warning in warnings)
            html.Append("<li>").Append(Escape(warning)).Append("</li>");
        html.Append("</ul></div>");
    }

    private static void AppendSystems(StringBuilder html, PerfAggregator aggregate)
    {
        html.Append("<h2>System breakdown</h2>");
        html.Append(
            "<table><tr><th>system</th><th>mean ms</th><th>p50</th><th>p95</th><th>p99</th><th>max</th>"
            + "<th>share</th><th>alloc mean</th><th>alloc p95</th></tr>");
        var total = System.Math.Max(1e-9, aggregate.SystemMeanTotal);
        foreach (var summary in aggregate.SystemSummaries)
        {
            html.Append("<tr><td><code>").Append(Escape(summary.Name)).Append("</code></td>");
            NumberCell(html, summary.Stats.Mean, "0.0000");
            NumberCell(html, summary.Stats.P50, "0.0000");
            NumberCell(html, summary.Stats.P95, "0.0000");
            NumberCell(html, summary.Stats.P99, "0.0000");
            NumberCell(html, summary.Stats.Max, "0.0000");
            NumberCell(html, 100 * summary.Stats.Mean / total, "0.0", "%");
            NumberCell(html, summary.Alloc.Mean / 1024d / 1024d, "0.00", " MiB");
            NumberCell(html, summary.Alloc.P95 / 1024d / 1024d, "0.00", " MiB");
            html.Append("</tr>");
        }

        html.Append("</table>");
        html.Append(string.Create(CultureInfo.InvariantCulture,
            $"<p class=\"muted\">share denominator = sum of system means = {aggregate.SystemMeanTotal:0.0000} ms (wall mean {aggregate.Wall.Mean:0.0000} ms).</p>"));
    }

    private static void AppendSubphases(StringBuilder html, PerfAggregator aggregate)
    {
        html.Append("<h2>Subphases</h2>");
        html.Append(
            "<table><tr><th>subphase</th><th>parent</th><th>mean ms</th><th>p50</th><th>p95</th><th>p99</th>"
            + "<th>max</th><th>alloc mean</th><th>alloc p95</th></tr>");
        foreach (var summary in aggregate.SubphaseSummaries)
        {
            html.Append("<tr><td><code>").Append(Escape(summary.Name)).Append("</code></td><td><code>")
                .Append(Escape(summary.Parent ?? string.Empty)).Append("</code></td>");
            NumberCell(html, summary.Stats.Mean, "0.0000");
            NumberCell(html, summary.Stats.P50, "0.0000");
            NumberCell(html, summary.Stats.P95, "0.0000");
            NumberCell(html, summary.Stats.P99, "0.0000");
            NumberCell(html, summary.Stats.Max, "0.0000");
            NumberCell(html, summary.Alloc.Mean / 1024d / 1024d, "0.00", " MiB");
            NumberCell(html, summary.Alloc.P95 / 1024d / 1024d, "0.00", " MiB");
            html.Append("</tr>");
        }

        html.Append("</table>");
    }

    private static void NumberCell(StringBuilder html, double value, string format, string suffix = "")
    {
        html.Append("<td>").Append(value.ToString(format, CultureInfo.InvariantCulture)).Append(suffix).Append("</td>");
    }

    private static void Card(StringBuilder html, string label, string value)
    {
        html.Append("<span class=card>").Append(Escape(label)).Append(" <b>").Append(Escape(value)).Append("</b></span>");
    }

    private static string Bars(PerfAggregator aggregate)
    {
        const int width = 760;
        const int row = 24;
        var systems = aggregate.SystemSummaries;
        var max = System.Math.Max(1e-9, systems.Max(static summary => summary.Stats.P95));
        var html = new StringBuilder($"<svg width={width} height={systems.Count * row + 20}>");
        for (var index = 0; index < systems.Count; index++)
        {
            var summary = systems[index];
            var bar = (summary.Stats.P95 / max) * (width - 300);
            html.Append(string.Create(CultureInfo.InvariantCulture,
                $"<text x=0 y={18 + index * row} fill=#93c5fd font-size=12>{Escape(summary.Name)}</text><rect x=190 y={6 + index * row} width={bar:0.0} height=12 fill=#2563eb/><text x={198 + bar:0.0} y={18 + index * row} fill=#dbeafe font-size=12>{summary.Stats.P95:0.000} ms</text>"));
        }

        return html.Append("</svg>").ToString();
    }

    private static string PerSampleChart(PerfAggregator aggregate)
    {
        const int width = 760;
        const int height = 280;
        const int pad = 32;
        var ticks = aggregate.Ticks;
        if (ticks.Count < 2)
            return "<p class=\"muted\">not enough samples for a per-sample chart</p>";
        var html = new StringBuilder(
            $"<svg width={width} height={height} style=\"background:#0f172a;border:1px solid #1e3a5f\">"
            + "<text x=8 y=16 fill=#94a3b8 font-size=12>each series scaled to its own range; x = sample index</text>"
            + "<circle cx=8 cy=34 r=4 fill=#22d3ee/><text x=18 y=38 fill=#93c5fd font-size=12>population</text>"
            + "<circle cx=118 cy=34 r=4 fill=#fbbf24/><text x=128 y=38 fill=#93c5fd font-size=12>food</text>"
            + "<circle cx=178 cy=34 r=4 fill=#f87171/><text x=188 y=38 fill=#93c5fd font-size=12>corpses</text>");
        AppendSeries(html, ticks, static tick => tick.Population, "#22d3ee", pad, width, height);
        AppendSeries(html, ticks, static tick => tick.Food, "#fbbf24", pad, width, height);
        AppendSeries(html, ticks, static tick => tick.Corpses, "#f87171", pad, width, height);
        html.Append("</svg>");
        return html.ToString();
    }

    private static void AppendSeries(
        StringBuilder html,
        IReadOnlyList<TickProfile> ticks,
        Func<TickProfile, double> selector,
        string color,
        int pad,
        int width,
        int height)
    {
        var values = new double[ticks.Count];
        var min = double.MaxValue;
        var max = double.MinValue;
        for (var index = 0; index < ticks.Count; index++)
        {
            values[index] = selector(ticks[index]);
            min = System.Math.Min(min, values[index]);
            max = System.Math.Max(max, values[index]);
        }

        var span = System.Math.Max(1e-9, max - min);
        var points = string.Join(' ', Enumerable.Range(0, ticks.Count).Select(index =>
            string.Create(CultureInfo.InvariantCulture,
                $"{pad + ((index / (double)System.Math.Max(1, ticks.Count - 1)) * (width - (2 * pad))):0.0},"
                + $"{height - pad - (((values[index] - min) / span) * (height - (2 * pad))):0.0}")));
        html.Append("<polyline fill=none stroke=").Append(color).Append(" stroke-width=1.5 points=\"")
            .Append(points).Append("\"/>");
    }

    private static string LineChart(PerfAggregator aggregate)
    {
        const int width = 760;
        const int height = 260;
        const int pad = 32;
        var values = aggregate.Ticks.Select(static tick => tick.WallMs).ToArray();
        var min = values.Min();
        var max = values.Max();
        var points = string.Join(' ', values.Select((value, index) =>
            string.Create(CultureInfo.InvariantCulture,
                $"{pad + ((index / (double)System.Math.Max(1, values.Length - 1)) * (width - (2 * pad))):0.0},"
                + $"{height - pad - (((value - min) / System.Math.Max(1e-9, max - min)) * (height - (2 * pad))):0.0}")));
        return $"<svg width={width} height={height} style=\"background:#0f172a;border:1px solid #1e3a5f\">"
            + "<text x=8 y=18 fill=#94a3b8 font-size=12>wall ms / sample</text>"
            + $"<polyline fill=none stroke=#22d3ee stroke-width=1.5 points=\"{points}\"/></svg>";
    }

    private static double? SimInterval(IReadOnlyList<TickProfile> ticks)
    {
        var deltas = new List<double>(System.Math.Max(0, ticks.Count - 1));
        for (var index = 1; index < ticks.Count; index++)
        {
            var delta = ticks[index].SimTime - ticks[index - 1].SimTime;
            if (delta > 0)
                deltas.Add(delta);
        }

        if (deltas.Count == 0)
            return null;
        deltas.Sort();
        return deltas[deltas.Count / 2];
    }

    private static string FormatMs(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.00} ms");

    private static string FormatBytes(double value) => string.Create(CultureInfo.InvariantCulture,
        $"{value / 1024d / 1024d:0.0} MiB");

    private static string FormatInteger(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static string FormatCounts(long? first, long? second, long? third) =>
        $"{FormatCount(first)}/{FormatCount(second)}/{FormatCount(third)}";

    private static string FormatCount(long? value) =>
        value is { } count ? count.ToString(CultureInfo.InvariantCulture) : "n/a";

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
