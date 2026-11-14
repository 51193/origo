using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Analysis.Math;

namespace Origo.EvolutionDemo.Analysis.Reports;

public static class GenomeHtmlReport
{
    private static readonly string[] Palette = ["#2563eb", "#dc2626", "#059669", "#d97706", "#7c3aed", "#0891b2", "#be185d", "#4d7c0f"];

    public static string Build(
        IReadOnlyList<SaveSnapshot> saves,
        IReadOnlyList<FeatureRow> rows,
        IReadOnlyList<string> columns,
        double[][] projection,
        KMeans.Result clustering)
    {
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(clustering);
        var palette = Palette;
        var html = new StringBuilder();
        html.Append("""
<!doctype html><html><head><meta charset="utf-8">
<title>Evolution Genome Analysis</title>
<style>
body{font:14px system-ui,sans-serif;margin:24px;background:#0b1220;color:#dbeafe}
h1,h2{color:#7dd3fc}h1{font-size:24px}h2{font-size:18px;margin-top:32px}
table{border-collapse:collapse;margin:8px 0 20px}th,td{padding:4px 8px;border-bottom:1px solid #1e3a5f;text-align:left}
th{color:#93c5fd}code{color:#fbbf24}.muted{color:#94a3b8}.card{display:inline-block;background:#111c33;border:1px solid #1e3a5f;border-radius:8px;padding:10px 16px;margin:4px 8px 4px 0}
</style></head><body><h1>Evolution Genome Analysis</h1>
""");
        html.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">Generated {DateTime.UtcNow:u}; saves={saves.Count}; creatures={rows.Count}; features={columns.Count}; k={clustering.K}; silhouette={clustering.Silhouette:0.000}</p>");
        html.Append("<div>");
        html.Append(CultureInfo.InvariantCulture, $"<span class=card>population <b>{rows.Count}</b></span>");
        html.Append(CultureInfo.InvariantCulture, $"<span class=card>clusters <b>{clustering.K}</b></span>");
        html.Append(CultureInfo.InvariantCulture, $"<span class=card>silhouette <b>{clustering.Silhouette:0.000}</b></span>");
        html.Append(CultureInfo.InvariantCulture, $"<span class=card>inertia <b>{clustering.Inertia:0.0}</b></span>");
        html.Append("</div>");
        html.Append("<h2>PCA scatter</h2>");
        html.Append(SvgScatter(projection, clustering.Labels, rows, palette));
        html.Append("<h2>Cluster behavior profile</h2>");
        html.Append(ClusterTable(rows, clustering, columns, palette));
        html.Append("<h2>Top differentiating features</h2>");
        html.Append(DifferentiatingTable(rows, clustering, columns, palette));
        html.Append("<h2>Saves</h2><table><tr><th>save</th><th>time</th><th>creatures</th><th>food</th><th>corpses</th><th>sources</th></tr>");
        foreach (var save in saves)
            html.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{Escape(save.Path)}</code></td><td>{save.Time:0.0}</td><td>{save.Creatures.Count}</td><td>{save.Food}</td><td>{save.Corpses}</td><td>{save.FoodSources}</td></tr>");
        html.Append("</table><h2>Feature distribution</h2><table><tr><th>feature</th><th>mean</th><th>std</th><th>min</th><th>max</th></tr>");
        foreach (var column in columns)
        {
            var values = rows.Select(row => row.Values.GetValueOrDefault(column, double.NaN)).Where(double.IsFinite).ToArray();
            if (values.Length == 0)
                continue;
            var mean = values.Average();
            var std = values.Length < 2 ? 0 : System.Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / values.Length);
            html.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{Escape(column)}</code></td><td>{mean:0.###}</td><td>{std:0.###}</td><td>{values.Min():0.###}</td><td>{values.Max():0.###}</td></tr>");
        }

        html.Append("</table></body></html>");
        return html.ToString();
    }

    private static string SvgScatter(double[][] projection, int[] labels, IReadOnlyList<FeatureRow> rows, string[] palette)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(rows);
        const int width = 760;
        const int height = 420;
        const int pad = 36;
        var xs = projection.Select(static point => point.Length > 0 ? point[0] : 0).ToArray();
        var ys = projection.Select(static point => point.Length > 1 ? point[1] : 0).ToArray();
        var minX = xs.Min();
        var maxX = xs.Max();
        var minY = ys.Min();
        var maxY = ys.Max();
        double X(double value) => pad + ((value - minX) / System.Math.Max(1e-9, maxX - minX)) * (width - 2 * pad);
        double Y(double value) => height - pad - ((value - minY) / System.Math.Max(1e-9, maxY - minY)) * (height - 2 * pad);
        var svg = new StringBuilder($"<svg width={width} height={height} style=\"background:#0f172a;border:1px solid #1e3a5f\">");
        svg.Append(CultureInfo.InvariantCulture, $"<text x=8 y=20 fill=#94a3b8 font-size=12>PC1 / PC2</text>");
        for (var i = 0; i < projection.Length; i++)
        {
            var color = palette[labels[i] % palette.Length];
            svg.Append(CultureInfo.InvariantCulture, $"<circle cx={X(xs[i]):0.0} cy={Y(ys[i]):0.0} r=3 fill=\"{color}\" opacity=0.65><title>{Escape(rows[i].Name)} cluster {labels[i]}</title></circle>");
        }

        for (var i = 0; i < palette.Length; i++)
            svg.Append(CultureInfo.InvariantCulture, $"<circle cx={width - 120} cy={20 + i * 17} r=4 fill=\"{palette[i]}\"/><text x={width - 108} y={24 + i * 17} fill=#93c5fd font-size=12>cluster {i}</text>");
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string DifferentiatingTable(IReadOnlyList<FeatureRow> rows, KMeans.Result clustering,
        IReadOnlyList<string> columns, string[] palette)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(clustering);
        ArgumentNullException.ThrowIfNull(columns);
        var html = new StringBuilder("<table><tr><th>cluster</th><th>n</th><th>top differentiating features (cluster mean vs all)</th></tr>");
        var globalMeans = columns.Select(column =>
            rows.Select(row => row.Values.GetValueOrDefault(column, double.NaN))
                .Where(double.IsFinite).DefaultIfEmpty(0).Average()).ToArray();
        var globalStd = columns.Select((column, index) =>
        {
            var values = rows.Select(row => row.Values.GetValueOrDefault(column, double.NaN))
                .Where(double.IsFinite).ToArray();
            return values.Length < 2
                ? 0
                : System.Math.Sqrt(values.Sum(value => (value - globalMeans[index]) * (value - globalMeans[index])) / values.Length);
        }).ToArray();
        for (var cluster = 0; cluster < clustering.K; cluster++)
        {
            var indexes = Enumerable.Range(0, rows.Count).Where(row => clustering.Labels[row] == cluster).ToArray();
            var ranked = Enumerable.Range(0, columns.Count)
                .Where(index => globalStd[index] > 1e-12)
                .Select(index =>
                {
                    var values = indexes.Select(row => rows[row].Values.GetValueOrDefault(columns[index], double.NaN))
                        .Where(double.IsFinite).ToArray();
                    var mean = values.Length == 0 ? globalMeans[index] : values.Average();
                    return (Name: columns[index], Mean: mean, Z: (mean - globalMeans[index]) / globalStd[index]);
                })
                .OrderByDescending(item => System.Math.Abs(item.Z))
                .Take(6)
                .ToArray();
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td style=\"color:{palette[cluster % palette.Length]}\">{cluster}</td><td>{indexes.Length}</td><td>");
            html.Append(string.Join(" · ", ranked.Select(item =>
                string.Create(CultureInfo.InvariantCulture,
                    $"{Escape(item.Name)} {item.Mean:0.###} (z={item.Z:+0.0;-0.0})"))));
            html.Append("</td></tr>");
        }

        return html.Append("</table>").ToString();
    }

    private static string ClusterTable(IReadOnlyList<FeatureRow> rows, KMeans.Result clustering,
        IReadOnlyList<string> columns, string[] palette)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(clustering);
        ArgumentNullException.ThrowIfNull(columns);
        var selected = SelectedFeatures.Where(columns.Contains).ToArray();
        var html = new StringBuilder("<table><tr><th>cluster</th><th>n</th>");
        foreach (var column in selected)
            html.Append(CultureInfo.InvariantCulture, $"<th>{Escape(column)}</th>");
        html.Append("</tr>");
        for (var cluster = 0; cluster < clustering.K; cluster++)
        {
            var indexes = Enumerable.Range(0, rows.Count).Where(row => clustering.Labels[row] == cluster).ToArray();
            html.Append(CultureInfo.InvariantCulture, $"<tr><td style=\"color:{palette[cluster % palette.Length]}\">{cluster}</td><td>{indexes.Length}</td>");
            foreach (var column in selected)
            {
                var values = indexes.Select(index => rows[index].Values.GetValueOrDefault(column, double.NaN))
                    .Where(double.IsFinite).ToArray();
                html.Append(CultureInfo.InvariantCulture, $"<td>{(values.Length == 0 ? "-" : values.Average().ToString("0.###", CultureInfo.InvariantCulture))}</td>");
            }

            html.Append("</tr>");
        }

        return html.Append("</table>").ToString();
    }

    private static readonly string[] SelectedFeatures =
    [
        "behavior.drive", "behavior.displacement", "neutral.move", "behavior.corpseMeals",
        "behavior.kills", "behavior.children", "behavior.age", "behavior.energyRatio",
        "gene.count", "body.organs", "neural.nodes", "neural.edges",
    ];

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
