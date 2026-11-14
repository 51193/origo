using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Origo.EvolutionDemo.Analysis.Perf;

/// <summary>HTML table for a profile-to-profile comparison.</summary>
public static class PerfCompareHtmlReport
{
    public static string Build(string baselinePath, string currentPath, IReadOnlyList<PerfDelta> deltas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        ArgumentNullException.ThrowIfNull(deltas);
        var html = new StringBuilder();
        html.Append("""
<!doctype html><html><head><meta charset="utf-8"><title>Performance Compare</title>
<style>
body{font:14px system-ui,sans-serif;margin:24px;background:#0b1220;color:#dbeafe}
h1{color:#7dd3fc}table{border-collapse:collapse;margin:8px 0}th,td{padding:5px 10px;border-bottom:1px solid #1e3a5f;text-align:right}
th:first-child,td:first-child{text-align:left}th{color:#93c5fd}code{color:#fbbf24}.up{color:#fca5a5}.down{color:#86efac}
</style></head><body><h1>Performance Compare</h1>
""");
        html.Append("<p>baseline: <code>").Append(Escape(baselinePath)).Append("</code></p>");
        html.Append("<p>current: <code>").Append(Escape(currentPath)).Append("</code></p>");
        html.Append("<table><tr><th>metric</th><th>baseline</th><th>current</th><th>delta</th></tr>");
        foreach (var delta in deltas)
        {
            var className = delta.DeltaPercent > 5 ? "up" : delta.DeltaPercent < -5 ? "down" : string.Empty;
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(delta.Metric)}</code></td><td>{delta.Baseline:0.000}</td><td>{delta.Current:0.000}</td><td class=\"{className}\">{delta.DeltaPercent:+0.0;-0.0;0.0}%</td></tr>");
        }

        html.Append("</table></body></html>");
        return html.ToString();
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
