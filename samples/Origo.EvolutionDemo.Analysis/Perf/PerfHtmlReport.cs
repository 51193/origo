using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>Self-contained HTML report for a performance profile.</summary>
public static class PerfHtmlReport
{
    public static string Build(PerfAggregator aggregate)
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
.muted{color:#94a3b8}
</style></head><body><h1>Evolution Performance</h1>
""");
        html.Append(CultureInfo.InvariantCulture,
            $"<p class=\"muted\">samples={aggregate.Ticks.Count}; simTime={aggregate.Ticks[0].SimTime:0.0}..{aggregate.Ticks[^1].SimTime:0.0}; interval≈{(aggregate.Ticks.Count > 1 ? aggregate.Ticks[1].SimTime - aggregate.Ticks[0].SimTime : 0):0.0}s</p>");
        html.Append("<div>");
        Card(html, "wall P50", FormatMs(aggregate.Wall.P50));
        Card(html, "wall P95", FormatMs(aggregate.Wall.P95));
        Card(html, "wall P99", FormatMs(aggregate.Wall.P99));
        Card(html, "alloc P95", FormatBytes(aggregate.Alloc.P95));
        Card(html, "GC 0/1/2", $"{aggregate.Gc0}/{aggregate.Gc1}/{aggregate.Gc2}");
        Card(html, "population", $"{aggregate.Population.P50:0}");
        Card(html, "food", $"{aggregate.Food.P50:0}");
        Card(html, "alloc P50", FormatBytes(aggregate.Alloc.P50));
        html.Append("</div><h2>System breakdown</h2>");
        html.Append("<table><tr><th>system</th><th>mean ms</th><th>p50</th><th>p95</th><th>p99</th><th>max</th><th>share</th><th>alloc mean</th><th>alloc p95</th></tr>");
        var total = System.Math.Max(1e-9, aggregate.SystemSummaries.Sum(static summary => summary.Stats.Mean));
        foreach (var summary in aggregate.SystemSummaries)
        {
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(summary.System)}</code></td><td>{summary.Stats.Mean:0.0000}</td><td>{summary.Stats.P50:0.0000}</td><td>{summary.Stats.P95:0.0000}</td><td>{summary.Stats.P99:0.0000}</td><td>{summary.Stats.Max:0.0000}</td><td>{100 * summary.Stats.Mean / total:0.0}%</td><td>{summary.Alloc.Mean / 1024d / 1024d:0.00} MiB</td><td>{summary.Alloc.P95 / 1024d / 1024d:0.00} MiB</td></tr>");
        }

        html.Append("</table><h2>Subphases</h2>");
        html.Append("<table><tr><th>subphase</th><th>mean ms</th><th>p95</th><th>alloc mean</th></tr>");
        foreach (var summary in aggregate.SubphaseSummaries)
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(summary.System)}</code></td><td>{summary.Stats.Mean:0.000}</td><td>{summary.Stats.P95:0.000}</td><td>{summary.Alloc.Mean / 1024d / 1024d:0.00} MiB</td></tr>");
        html.Append("</table><h2>Phase p95 bars</h2>");
        html.Append(Bars(aggregate));
        html.Append("<h2>Wall time per sample</h2>");
        html.Append(LineChart(aggregate));
        html.Append("</body></html>");
        return html.ToString();
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
        for (var i = 0; i < systems.Count; i++)
        {
            var summary = systems[i];
            var bar = (summary.Stats.P95 / max) * (width - 300);
            html.Append(CultureInfo.InvariantCulture,
                $"<text x=0 y={18 + i * row} fill=#93c5fd font-size=12>{Escape(summary.System)}</text><rect x=190 y={6 + i * row} width={bar:0.0} height=12 fill=#2563eb/><text x={198 + bar:0.0} y={18 + i * row} fill=#dbeafe font-size=12>{summary.Stats.P95:0.000} ms</text>");
        }

        return html.Append("</svg>").ToString();
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
                $"{pad + (index / (double)System.Math.Max(1, values.Length - 1)) * (width - 2 * pad):0.0},{height - pad - ((value - min) / System.Math.Max(1e-9, max - min)) * (height - 2 * pad):0.0}")));
        return $"<svg width={width} height={height} style=\"background:#0f172a;border:1px solid #1e3a5f\"><text x=8 y=18 fill=#94a3b8 font-size=12>wall ms / sample</text><polyline fill=none stroke=#22d3ee stroke-width=1.5 points=\"{points}\"/></svg>";
    }

    private static string FormatMs(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.00} ms");

    private static string FormatBytes(double value) => string.Create(CultureInfo.InvariantCulture,
        $"{value / 1024d / 1024d:0.0} MiB");

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
