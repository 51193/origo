using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.Math;

namespace Origo.EvolutionDemo.Analysis.Reports;

/// <summary>
///     Renders the self-contained dark-theme HTML genome report from a
///     <see cref="GenomeReportInput" />. The same input feeds
///     <see cref="GenomeMetricsJson" /> so the HTML and the machine-readable
///     output cannot disagree.
/// </summary>
public static class GenomeHtmlReport
{
    /// <summary>Builds the HTML document.</summary>
    /// <exception cref="ArgumentNullException">The input or one of its required parts is null.</exception>
    public static string Build(GenomeReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Saves);
        ArgumentNullException.ThrowIfNull(input.Ecology);
        ArgumentNullException.ThrowIfNull(input.Rows);
        ArgumentNullException.ThrowIfNull(input.RawColumns);
        ArgumentNullException.ThrowIfNull(input.Pca);
        ArgumentNullException.ThrowIfNull(input.Clustering);
        ArgumentNullException.ThrowIfNull(input.Candidates);
        ArgumentNullException.ThrowIfNull(input.DroppedColumns);
        ArgumentNullException.ThrowIfNull(input.SkippedCreatures);
        ArgumentNullException.ThrowIfNull(input.Enrichment);
        ArgumentNullException.ThrowIfNull(input.Lineage);
        ArgumentNullException.ThrowIfNull(input.Mortality);
        ArgumentNullException.ThrowIfNull(input.Options);
        var rows = input.Rows;
        var clustering = input.Clustering;
        var index = Enumerable.Range(0, rows.Count).Where(row => clustering.Labels[row] >= 0).ToArray();
        var noiseCount = rows.Count - index.Length;
        var testedCount = input.Enrichment.Count == 0 ? 0 : input.Enrichment.Max(static analysis => analysis.TestedCount);
        var html = new StringBuilder();
        html.Append("""
<!doctype html><html><head><meta charset="utf-8">
<title>Evolution Genome Analysis</title>
<style>
body{font:14px system-ui,sans-serif;margin:24px;background:#0b1220;color:#dbeafe}
h1,h2{color:#7dd3fc}h1{font-size:24px}h2{font-size:18px;margin-top:32px}
table{border-collapse:collapse;margin:8px 0 20px}th,td{padding:4px 8px;border-bottom:1px solid #1e3a5f;text-align:left}
th{color:#93c5fd}code{color:#fbbf24}.muted{color:#94a3b8}.warn{color:#fca5a5}.note{color:#cbd5e1;max-width:1100px}
.card{display:inline-block;background:#111c33;border:1px solid #1e3a5f;border-radius:8px;padding:10px 16px;margin:4px 8px 4px 0}
.na{color:#94a3b8}
</style></head><body><h1>Evolution Genome Analysis</h1>
""");
        AppendProvenance(html, input);
        AppendCards(html, input, noiseCount, testedCount);
        AppendWarnings(html, input);
        AppendEcology(html, input);
        AppendLineage(html, input);
        AppendMortality(html, input);
        if (input.Candidates.Count > 0)
            AppendCandidates(html, input);
        html.Append("<h2>PCA scatter</h2>");
        html.Append(Scatter(input, index));
        AppendPcaTable(html, input);
        html.Append("<h2>Cluster behavior profile</h2>");
        AppendClusterProfile(html, input);
        html.Append("<h2>Top differentiating features</h2>");
        AppendDifferentiating(html, input);
        html.Append("<h2>Clusters by save</h2>");
        AppendSaveClusters(html, input);
        html.Append("<h2>Gene-signature enrichment</h2>");
        AppendEnrichment(html, input);
        html.Append("<h2>Saves</h2>");
        AppendSaves(html, input);
        AppendControllerCounters(html, input);
        html.Append("<h2>Feature distribution</h2>");
        AppendFeatureDistribution(html, input);
        AppendDefinitions(html, input, noiseCount);
        html.Append("</body></html>");
        return html.ToString();
    }

    private static void AppendProvenance(StringBuilder html, GenomeReportInput input)
    {
        var options = input.Options;
        var k = options.Method switch
        {
            "dbscan" => string.Create(CultureInfo.InvariantCulture, $"k={input.Clustering.K} (DBSCAN)"),
            _ when options.RequestedK == 0 => string.Create(
                CultureInfo.InvariantCulture,
                $"k={input.Clustering.K} (auto, searched 2..{input.AutoKMax}, min cluster size {options.MinClusterSize})"),
            _ => string.Create(CultureInfo.InvariantCulture, $"k={input.Clustering.K} (fixed)"),
        };
        var minClusterSize = string.Equals(options.Method, "dbscan", StringComparison.Ordinal)
            ? "n/a"
            : options.MinClusterSize.ToString(CultureInfo.InvariantCulture);
        html.Append(CultureInfo.InvariantCulture,
            $"<p class=\"muted\">generated {input.GeneratedAtUtc:u}; command <code>{Escape(input.CommandLine)}</code></p>");
        html.Append(CultureInfo.InvariantCulture,
            $"<p class=\"muted\">seed={options.Seed}; max={options.MaxCreatures}; mode={Esc(input.Options.Mode)}; "
            + $"method={Esc(options.Method)}; {k}; stability rounds={options.StabilityRounds}; "
            + $"eps={(options.Eps <= 0 ? "estimated" : options.Eps.ToString("0.###", CultureInfo.InvariantCulture))}; "
            + $"min-points={options.MinPoints}; min-cluster-size={minClusterSize}; allow-skips={options.AllowSkips}; "
            + $"saves={input.Saves.Count}; analyzed={input.Rows.Count}/{input.TotalCreatures}; "
            + $"features used/raw={input.UsedFeatureCount}/{input.RawColumns.Count}</p>");
    }

    private static void AppendCards(StringBuilder html, GenomeReportInput input, int noiseCount, int testedCount)
    {
        var clustering = input.Clustering;
        html.Append("<div>");
        Card(html, "saves", input.Saves.Count.ToString(CultureInfo.InvariantCulture));
        Card(html, "population analyzed/total", string.Create(CultureInfo.InvariantCulture,
            $"{input.Rows.Count}/{input.TotalCreatures}"));
        Card(html, "features used/raw", string.Create(CultureInfo.InvariantCulture,
            $"{input.UsedFeatureCount}/{input.RawColumns.Count}"));
        Card(html, "clusters", clustering.K.ToString(CultureInfo.InvariantCulture));
        Card(html, "silhouette", FormatMetric(clustering.Silhouette, "0.000"));
        Card(html, "inertia", FormatMetric(clustering.Inertia, "0.0"));
        Card(html, "bootstrap ARI", input.Stability is { } stability
            ? string.Create(CultureInfo.InvariantCulture, $"{stability.Mean:0.000} ± {stability.StandardDeviation:0.000} ({stability.Rounds} rounds)")
            : "n/a");
        if (string.Equals(input.Options.Method, "dbscan", StringComparison.Ordinal))
            Card(html, "noise", string.Create(CultureInfo.InvariantCulture,
                $"{noiseCount} ({100d * noiseCount / System.Math.Max(1, input.Rows.Count):0.0}%)"));
        if (input.SkippedCreatures.Count > 0)
            Card(html, "skipped creatures", input.SkippedCreatures.Count.ToString(CultureInfo.InvariantCulture));
        Card(html, "signatures tested", testedCount.ToString(CultureInfo.InvariantCulture));
        html.Append("</div>");
    }

    private static void Card(StringBuilder html, string label, string value) =>
        html.Append(CultureInfo.InvariantCulture, $"<span class=card>{Escape(label)} <b>{Escape(value)}</b></span>");

    private static void AppendWarnings(StringBuilder html, GenomeReportInput input)
    {
        var warnings = GenomeWarnings.Build(input);
        if (warnings.Count == 0)
            return;
        html.Append("<ul>");
        foreach (var warning in warnings)
            html.Append("<li class=\"warn\">").Append(Escape(warning)).Append("</li>");
        html.Append("</ul>");
    }

    private static void AppendEcology(StringBuilder html, GenomeReportInput input)
    {
        if (input.Ecology.Count == 0)
            return;
        html.Append("<h2>Food, access, and origin</h2>");
        html.Append("<p class=\"muted\">Computed from every parsed creature and positioned food entity in each save, not from the optional --max clustering sample. "
            + "Nearest-food distance is capped at 600 world units; diet.living includes corpse food in soft meals.</p>");
        html.Append("<table><tr><th>save</th><th>time</th><th>food soft/tough/toxic/corpse</th>"
            + "<th>patch n / radius / coverage</th><th>food in sense</th><th>food in 300</th>"
            + "<th>nearest food p50</th><th>founder share</th><th>mean generation</th>"
            + "<th>living diet S/T/X</th><th>specialist &gt;60%</th></tr>");
        foreach (var ecology in input.Ecology
            .OrderBy(static item => item.Time)
            .ThenBy(static item => item.SaveId, StringComparer.Ordinal))
        {
            var diet = ecology.LivingDiet;
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(ecology.SaveId)}</code></td><td>{ecology.Time:0.###}</td>"
                + $"<td>{ecology.Food.Soft}/{ecology.Food.Tough}/{ecology.Food.Toxic}/{ecology.Food.Corpse}</td>"
                + $"<td>{FormatMetric(ecology.LivePatchCount, "0")} / {FormatMetric(ecology.PatchRadius, "0.##")} / "
                + $"{(ecology.PatchCoverage is { } coverage ? $"{coverage:0.0%}" : "n/a")}</td>"
                + $"<td>{ecology.Access.FractionInSense:0.0%}</td><td>{ecology.Access.FractionIn300:0.0%}</td>"
                + $"<td>{FormatMetric(ecology.Access.NearestFoodP50, "0.#")}</td>"
                + $"<td>{ecology.FounderShare:0.0%}</td><td>{ecology.MeanGeneration:0.0}</td>"
                + $"<td>{diet.Soft:0.00}/{diet.Tough:0.00}/{diet.Toxic:0.00}</td>"
                + $"<td>{ecology.SpecialistShare60:0.0%}</td></tr>");
        }

        html.Append("</table>");
    }

    private static void AppendLineage(StringBuilder html, GenomeReportInput input)
    {
        if (input.Lineage.Saves.Count == 0)
            return;
        html.Append("<h2>Lineages and founder cohorts</h2>");
        html.Append("<p class=\"muted\">Living lineage structure per save. New gen0 lineages are a proxy for injected founders: they include enrichment and organ-innovation founders, but not ordinary births. Prior survival, lost, and turnover compare consecutive saves.</p>");
        html.Append("<table><tr><th>save</th><th>time</th><th>lineages</th><th>founder lineages</th>"
            + "<th>largest</th><th>entropy</th><th>new / lost</th><th>new gen0</th>"
            + "<th>prior survival</th><th>turnover</th></tr>");
        foreach (var lineage in input.Lineage.Saves
            .OrderBy(static item => item.Time)
            .ThenBy(static item => item.SaveId, StringComparer.Ordinal))
        {
            var summary = lineage.Summary;
            var displayId = input.Saves.FirstOrDefault(save =>
                string.Equals(save.Path, lineage.Path, StringComparison.Ordinal))?.Id ?? lineage.SaveId;
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(displayId)}</code></td><td>{lineage.Time:0.###}</td>"
                + $"<td>{summary.Lineages}</td><td>{summary.FounderLineages} ({summary.FounderLineageShare:0%})</td>"
                + $"<td>{summary.LargestLineageShare:0%}</td><td>{summary.LineageEntropy:0.000}</td>"
                + $"<td>{summary.NewLineages} / {summary.LostLineages}</td><td>{summary.NewGen0Lineages}</td>"
                + $"<td>{(summary.PriorLineageSurvival is { } survival ? $"{survival:0%}" : "n/a")}</td>"
                + $"<td>{summary.LineageTurnover:0.000}</td></tr>");
        }

        html.Append("</table>");
        html.Append("<h3>Root lineages</h3>");
        html.Append("<p class=\"muted\">Root ids are inherited through mutation-lineage changes, so this table is the stable ancestry view. New root gen0 ids are injected founders.</p>");
        html.Append("<table><tr><th>save</th><th>time</th><th>roots</th><th>founder roots</th>"
            + "<th>largest</th><th>entropy</th><th>new / lost</th><th>new root gen0</th>"
            + "<th>prior survival</th><th>turnover</th></tr>");
        foreach (var lineage in input.Lineage.Saves
            .OrderBy(static item => item.Time)
            .ThenBy(static item => item.SaveId, StringComparer.Ordinal))
        {
            var root = lineage.RootSummary;
            var displayId = input.Saves.FirstOrDefault(save =>
                string.Equals(save.Path, lineage.Path, StringComparison.Ordinal))?.Id ?? lineage.SaveId;
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(displayId)}</code></td><td>{lineage.Time:0.###}</td>"
                + $"<td>{root.Lineages}</td><td>{root.FounderLineages} ({root.FounderLineageShare:0%})</td>"
                + $"<td>{root.LargestLineageShare:0%}</td><td>{root.LineageEntropy:0.000}</td>"
                + $"<td>{root.NewLineages} / {root.LostLineages}</td><td>{root.NewGen0Lineages}</td>"
                + $"<td>{(root.PriorLineageSurvival is { } survival ? $"{survival:0%}" : "n/a")}</td>"
                + $"<td>{root.LineageTurnover:0.000}</td></tr>");
        }

        html.Append("</table><h3>Spawn origins</h3>");
        html.Append("<table><tr><th>save</th><th>time</th><th>founder</th><th>child</th>"
            + "<th>enrichment</th><th>innovation</th><th>overgrowth</th><th>unknown</th></tr>");
        foreach (var lineage in input.Lineage.Saves
            .OrderBy(static item => item.Time)
            .ThenBy(static item => item.SaveId, StringComparer.Ordinal))
        {
            var origin = lineage.Origins;
            var displayId = input.Saves.FirstOrDefault(save =>
                string.Equals(save.Path, lineage.Path, StringComparison.Ordinal))?.Id ?? lineage.SaveId;
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(displayId)}</code></td><td>{lineage.Time:0.###}</td>"
                + $"<td>{origin.Founders}</td><td>{origin.Children}</td><td>{origin.Enrichment}</td>"
                + $"<td>{origin.Innovation}</td><td>{origin.Overgrowth}</td><td>{origin.Unknown}</td></tr>");
        }

        html.Append("</table>");
    }

    private static void AppendMortality(StringBuilder html, GenomeReportInput input)
    {
        var saves = input.Mortality.Saves
            .Where(static item => item.Counts is not null)
            .OrderBy(static item => item.Time)
            .ThenBy(static item => item.SaveId, StringComparer.Ordinal)
            .ToArray();
        var hasRoots = input.Mortality.RootDeaths is { Count: > 0 };
        if (saves.Length == 0 && !hasRoots)
            return;
        html.Append("<h2>Death causes</h2>");
        if (saves.Length > 0)
        {
            html.Append("<p class=\"muted\">Cumulative controller counters for starvation (energy <= 0), predation (lethal spike hit), and aging (max age reached). Counters are absent in older saves.</p>");
            html.Append("<table><tr><th>save</th><th>time</th><th>starvation</th><th>predation</th>"
                + "<th>aging</th><th>total</th></tr>");
            foreach (var save in saves)
            {
                var counts = save.Counts!;
                var displayId = input.Saves.FirstOrDefault(item =>
                    string.Equals(item.Path, save.Path, StringComparison.Ordinal))?.Id ?? save.SaveId;
                html.Append(CultureInfo.InvariantCulture,
                    $"<tr><td><code>{Escape(displayId)}</code></td><td>{save.Time:0.###}</td>"
                    + $"<td>{counts.Starvation}</td><td>{counts.Predation}</td>"
                    + $"<td>{counts.Aging}</td><td>{counts.Total}</td></tr>");
            }

            html.Append("</table>");
        }

        if (input.Mortality.RootDeaths is { Count: > 0 } roots)
        {
            var finalPath = input.Lineage.Saves.Count == 0 ? null : input.Lineage.Saves[^1].Path;
            var living = input.Lineage.RootDetails
                .Where(item => string.Equals(item.Path, finalPath, StringComparison.Ordinal))
                .Select(static item => item.RootLineage)
                .ToHashSet();
            html.Append("<h3>Deaths by root lineage</h3>");
            html.Append("<p class=\"muted\">Latest cumulative totals; living means the root still had a creature in the final save. Only the ten largest roots are shown.</p>");
            html.Append("<table><tr><th>root</th><th>starvation</th><th>predation</th>"
                + "<th>aging</th><th>total</th><th>living</th></tr>");
            foreach (var (root, tally) in roots.OrderByDescending(static pair => pair.Value.Total)
                .ThenBy(static pair => pair.Key)
                .Take(10))
            {
                html.Append(CultureInfo.InvariantCulture,
                    $"<tr><td><code>{root}</code></td><td>{tally.Starvation}</td>"
                    + $"<td>{tally.Predation}</td><td>{tally.Aging}</td><td>{tally.Total}</td>"
                    + $"<td>{(living.Contains(root) ? "yes" : "no")}</td></tr>");
            }

            html.Append("</table>");
        }
    }

    private static void AppendCandidates(StringBuilder html, GenomeReportInput input)
    {
        var method = input.Options.Method;
        if (!string.Equals(method, "kmeans", StringComparison.Ordinal)
            && !string.Equals(method, "jaccard", StringComparison.Ordinal))
            return;
        var jaccard = string.Equals(method, "jaccard", StringComparison.Ordinal);
        var criterion = jaccard ? "maximum-silhouette solution" : "min-inertia solution";
        var inertiaHeader = jaccard ? "inertia (n/a)" : "inertia (SSE)";
        html.Append(CultureInfo.InvariantCulture,
            $"<h2>Auto-k candidates (k={input.AutoKMax} cap, restarts per k, {criterion}, minimum cluster size {input.Options.MinClusterSize})</h2>");
        html.Append(CultureInfo.InvariantCulture,
            $"<table><tr><th>k</th><th>{inertiaHeader}</th><th>silhouette</th><th>cluster sizes</th><th>flags</th></tr>");
        foreach (var candidate in input.Candidates)
        {
            var selected = candidate.Flags.Contains("selected", StringComparer.Ordinal);
            html.Append(CultureInfo.InvariantCulture,
                $"<tr{(selected ? " style=\"font-weight:600\"" : string.Empty)}><td>{candidate.K}</td>"
                + $"<td>{FormatMetric(candidate.Inertia, "0.0")}</td>"
                + $"<td>{FormatMetric(candidate.Silhouette, "0.000")}</td>"
                + $"<td>{string.Join(" / ", candidate.Sizes)}</td>"
                + $"<td>{Escape(string.Join(", ", candidate.Flags))}</td></tr>");
        }

        html.Append("</table>");
    }

    private static void AppendPcaTable(StringBuilder html, GenomeReportInput input)
    {
        if (input.Pca.ExplainedVarianceRatios.Count == 0)
        {
            html.Append("<p class=\"muted\">PCA was not computed for this run.</p>");
            return;
        }

        html.Append("<table><tr><th>component</th><th>explained variance</th><th>cumulative</th></tr>");
        var cumulative = 0d;
        for (var component = 0; component < input.Pca.ExplainedVarianceRatios.Count; component++)
        {
            var ratio = input.Pca.ExplainedVarianceRatios[component];
            cumulative += ratio;
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td>PC{component + 1}</td><td>{100 * ratio:0.00}%</td><td>{100 * System.Math.Min(1d, cumulative):0.00}%</td></tr>");
        }

        html.Append("</table>");
    }

    private static string Scatter(GenomeReportInput input, int[] clusteredIndexes)
    {
        var projection = input.Pca.Projection;
        if (projection.Length != input.Rows.Count || projection.Length == 0 || projection[0].Length == 0)
            return "<p class=\"muted\">No PCA projection available.</p>";
        const int width = 900;
        const int height = 460;
        const int pad = 48;
        var components = projection[0].Length;
        var xs = projection.Select(point => point[0]).ToArray();
        var ys = components > 1 ? [.. projection.Select(point => point[1])] : new double[projection.Length];
        var minX = xs.Min();
        var maxX = xs.Max();
        var minY = ys.Min();
        var maxY = ys.Max();
        double X(double value) => pad + ((value - minX) / System.Math.Max(1e-9, maxX - minX)) * (width - (2 * pad));
        double Y(double value) => height - pad - ((value - minY) / System.Math.Max(1e-9, maxY - minY)) * (height - (2 * pad));
        var ratios = input.Pca.ExplainedVarianceRatios;
        var xLabel = ratios.Count > 0
            ? string.Create(CultureInfo.InvariantCulture, $"PC1 ({100 * ratios[0]:0.00}% explained variance)")
            : "PC1";
        var yLabel = ratios.Count > 1
            ? string.Create(CultureInfo.InvariantCulture, $"PC2 ({100 * ratios[1]:0.00}% explained variance)")
            : "PC2";
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg width={width} height={height} style=\"background:#0f172a;border:1px solid #1e3a5f\">");
        svg.Append(CultureInfo.InvariantCulture, $"<text x={pad} y=20 fill=#94a3b8 font-size=12>{Escape(xLabel)}</text>");
        svg.Append(CultureInfo.InvariantCulture,
            $"<text x={pad} y={height - 12} fill=#94a3b8 font-size=12>horiz: {Escape(xLabel)}; vert: {Escape(yLabel)}</text>");
        foreach (var row in clusteredIndexes)
        {
            var label = input.Clustering.Labels[row];
            var color = PaletteColor(label);
            svg.Append(CultureInfo.InvariantCulture,
                $"<circle cx={X(xs[row]):0.0} cy={Y(ys[row]):0.0} r=3 fill=\"{color}\" opacity=0.65><title>{Escape(input.Rows[row].Name)} cluster {label}</title></circle>");
        }

        for (var row = 0; row < projection.Length; row++)
        {
            if (input.Clustering.Labels[row] >= 0)
                continue;
            svg.Append(CultureInfo.InvariantCulture,
                $"<circle cx={X(xs[row]):0.0} cy={Y(ys[row]):0.0} r=3 fill=\"#64748b\" opacity=0.5><title>{Escape(input.Rows[row].Name)} noise</title></circle>");
        }

        var legendRows = System.Math.Min(20, System.Math.Max(1, input.Clustering.K));
        for (var cluster = 0; cluster < input.Clustering.K; cluster++)
        {
            var column = cluster / legendRows;
            var row = cluster % legendRows;
            var left = width - 150 - (column * 110);
            svg.Append(CultureInfo.InvariantCulture,
                $"<circle cx={left} cy={24 + row * 20} r=4 fill=\"{PaletteColor(cluster)}\"/>"
                + $"<text x={left + 12} y={28 + row * 20} fill=#93c5fd font-size=12>cluster {cluster}</text>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void AppendClusterProfile(StringBuilder html, GenomeReportInput input)
    {
        var columns = SelectedFeatures.Where(input.RawColumns.Contains).ToArray();
        if (columns.Length == 0)
        {
            html.Append("<p class=\"muted\">No selected profile feature is active in this mode.</p>");
            return;
        }

        html.Append("<table><tr><th>cluster</th><th>n</th>");
        foreach (var column in columns)
            html.Append(CultureInfo.InvariantCulture, $"<th>{Escape(column)}</th>");
        html.Append("</tr>");
        for (var cluster = 0; cluster < input.Clustering.K; cluster++)
        {
            var indexes = ClusterIndexes(input, cluster);
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td style=\"color:{PaletteColor(cluster)}\">{cluster}</td><td>{indexes.Length}</td>");
            foreach (var column in columns)
            {
                var values = indexes.Select(row => input.Rows[row].Values.GetValueOrDefault(column, double.NaN))
                    .Where(double.IsFinite).ToArray();
                html.Append(CultureInfo.InvariantCulture,
                    $"<td>{(values.Length == 0 ? "-" : values.Average().ToString("0.###", CultureInfo.InvariantCulture))}</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</table>");
    }

    private static void AppendDifferentiating(StringBuilder html, GenomeReportInput input)
    {
        if (input.Clustering.K < 2)
        {
            html.Append("<p class=\"muted\">A single cluster has no differentiating features.</p>");
            return;
        }

        var clustered = Enumerable.Range(0, input.Rows.Count)
            .Where(row => input.Clustering.Labels[row] >= 0).ToArray();
        var globalMeans = input.RawColumns.Select(column =>
            clustered.Select(row => input.Rows[row].Values.GetValueOrDefault(column, double.NaN))
                .Where(double.IsFinite).DefaultIfEmpty(0).Average()).ToArray();
        var globalStd = input.RawColumns.Select((column, columnIndex) =>
        {
            var values = clustered.Select(row => input.Rows[row].Values.GetValueOrDefault(column, double.NaN))
                .Where(double.IsFinite).ToArray();
            return values.Length < 2
                ? 0
                : System.Math.Sqrt(values.Sum(value => (value - globalMeans[columnIndex]) * (value - globalMeans[columnIndex])) / values.Length);
        }).ToArray();
        html.Append("<table><tr><th>cluster</th><th>n</th><th>top differentiating features (cluster mean vs all clustered rows, z-score)</th></tr>");
        for (var cluster = 0; cluster < input.Clustering.K; cluster++)
        {
            var indexes = ClusterIndexes(input, cluster);
            var ranked = Enumerable.Range(0, input.RawColumns.Count)
                .Where(column => globalStd[column] > 1e-12)
                .Select(column =>
                {
                    var values = indexes.Select(row => input.Rows[row].Values.GetValueOrDefault(input.RawColumns[column], double.NaN))
                        .Where(double.IsFinite).ToArray();
                    var mean = values.Length == 0 ? globalMeans[column] : values.Average();
                    return (Name: input.RawColumns[column], Mean: mean, Z: (mean - globalMeans[column]) / globalStd[column]);
                })
                .OrderByDescending(static item => System.Math.Abs(item.Z))
                .ThenBy(static item => item.Name, StringComparer.Ordinal)
                .Take(6)
                .ToArray();
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td style=\"color:{PaletteColor(cluster)}\">{cluster}</td><td>{indexes.Length}</td><td>");
            html.Append(string.Join(" · ", ranked.Select(item => string.Create(CultureInfo.InvariantCulture,
                $"{Escape(item.Name)} {item.Mean:0.###} (z={item.Z:+0.0;-0.0})"))));
            html.Append("</td></tr>");
        }

        html.Append("</table>");
    }

    private static void AppendSaveClusters(StringBuilder html, GenomeReportInput input)
    {
        var byPath = input.Rows
            .Select(static (row, index) => (row.SavePath, index))
            .GroupBy(static entry => entry.SavePath, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Select(static entry => entry.index).ToArray(), StringComparer.Ordinal);
        var hasNoise = input.Clustering.Labels.Any(static label => label < 0);
        html.Append("<p class=\"muted\">analyzed = rows analyzed in this run (after --max sampling); the saves table below lists the full save population.</p>");
        html.Append("<table><tr><th>save</th><th>time</th><th>analyzed</th>");
        for (var cluster = 0; cluster < input.Clustering.K; cluster++)
            html.Append(CultureInfo.InvariantCulture, $"<th style=\"color:{PaletteColor(cluster)}\">c{cluster}</th>");
        if (hasNoise)
            html.Append("<th>noise</th>");
        html.Append("</tr>");
        foreach (var save in TimeOrderedSaves(input))
        {
            var indexes = byPath.GetValueOrDefault(save.Path, []);
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(save.Id)}</code></td><td>{save.Time:0.###}</td><td>{indexes.Length}</td>");
            for (var cluster = 0; cluster < input.Clustering.K; cluster++)
                html.Append(CultureInfo.InvariantCulture, $"<td>{indexes.Count(row => input.Clustering.Labels[row] == cluster)}</td>");
            if (hasNoise)
                html.Append(CultureInfo.InvariantCulture, $"<td>{indexes.Count(row => input.Clustering.Labels[row] < 0)}</td>");
            html.Append("</tr>");
        }

        html.Append("</table>");
    }

    private static void AppendEnrichment(StringBuilder html, GenomeReportInput input)
    {
        var tested = input.Enrichment.Count == 0 ? 0 : input.Enrichment.Max(static analysis => analysis.TestedCount);
        html.Append(CultureInfo.InvariantCulture,
            $"<p class=\"note\">Two-sided Fisher exact per cluster vs rest, Benjamini-Hochberg step-up over all m={tested} tested signatures (support &gt;= {GeneEnrichment.MinimumSupport} rows overall). Each cluster row below shows at most 10 of the tested signatures with q &lt; 0.10; the complete tested set is written only by --enrichment-csv (--metrics-json keeps the capped display rows).</p>");
        if (tested == 0)
        {
            html.Append(input.Clustering.K < 2
                ? "<p class=\"muted\">No cluster-versus-rest enrichment is possible with fewer than two clusters.</p>"
                : "<p class=\"muted\">No signature passed the support filter for this clustering.</p>");
            return;
        }

        html.Append("<table><tr><th>cluster</th><th>signature</th><th>cluster with</th><th>rest with</th><th>odds</th><th>p</th><th>BH q</th></tr>");
        for (var cluster = 0; cluster < input.Clustering.K; cluster++)
        {
            var analysis = input.Enrichment.FirstOrDefault(item => item.Cluster == cluster)
                ?? new EnrichmentAnalysis(cluster, 0, []);
            var displayed = analysis.Signatures
                .Where(static result => result.QValue < 0.10)
                .Take(10)
                .ToArray();
            var significantCount = analysis.Signatures.Count(static result => result.QValue < 0.10);
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td colspan=7 class=\"muted\">cluster {cluster}: m={analysis.TestedCount} tested, showing {displayed.Length} of {significantCount} with q &lt; 0.10</td></tr>");
            if (displayed.Length == 0)
            {
                html.Append(CultureInfo.InvariantCulture,
                    $"<tr><td style=\"color:{PaletteColor(cluster)}\">{cluster}</td><td colspan=6 class=\"muted\">no gene signature with BH q &lt; 0.10</td></tr>");
                continue;
            }

            foreach (var result in displayed)
                html.Append(CultureInfo.InvariantCulture,
                    $"<tr><td style=\"color:{PaletteColor(cluster)}\">{cluster}</td><td><code>{Escape(result.Signature)}</code></td>"
                    + $"<td>{result.ClusterWith}/{result.ClusterTotal}</td><td>{result.RestWith}/{result.RestTotal}</td>"
                    + $"<td>{result.OddsRatio:0.00}</td><td>{FormatProbability(result.PValue)}</td><td>{FormatProbability(result.QValue)}</td></tr>");
        }

        html.Append("</table>");
    }

    private static void AppendSaves(StringBuilder html, GenomeReportInput input)
    {
        html.Append("<p class=\"muted\">saves ordered by simulation time; id is the save directory name.</p>");
        html.Append("<table><tr><th>save id</th><th>time</th><th>tick</th><th>creatures</th><th>food</th><th>corpses</th><th>sources</th></tr>");
        foreach (var save in TimeOrderedSaves(input))
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(save.Id)}</code></td><td>{save.Time:0.###}</td><td>{save.Tick:0.###}</td>"
                + $"<td>{save.Creatures}</td><td>{save.Food}</td><td>{save.Corpses}</td><td>{save.FoodSources}</td></tr>");
        html.Append("</table>");
    }

    private static void AppendControllerCounters(StringBuilder html, GenomeReportInput input)
    {
        if (input.Saves.Count == 0)
            return;
        var latest = input.Saves
            .OrderBy(static save => save.Time)
            .ThenBy(static save => save.Id, StringComparer.Ordinal)
            .Last();
        var keys = latest.ControllerNumbers.Keys
            .Concat(latest.ControllerStrings.Keys)
            .Concat(latest.ControllerBooleans.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        html.Append("<h2>Controller counters (latest save)</h2>");
        html.Append(CultureInfo.InvariantCulture,
            $"<p class=\"muted\">Save <code>{Escape(latest.Id)}</code> at simulation time {latest.Time:0.###} s, tick {latest.Tick:R}.</p>");
        if (keys.Length == 0)
        {
            html.Append("<p class=\"muted\">The latest save has no controller values.</p>");
            return;
        }

        html.Append("<table><tr><th>key</th><th>value</th></tr>");
        foreach (var key in keys)
        {
            var value = latest.ControllerNumbers.TryGetValue(key, out var number)
                ? number.ToString("R", CultureInfo.InvariantCulture)
                : latest.ControllerStrings.TryGetValue(key, out var text)
                    ? text
                    : latest.ControllerBooleans.TryGetValue(key, out var boolean) && boolean ? "true" : "false";
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(key)}</code></td><td>{Escape(value)}</td></tr>");
        }

        html.Append("</table>");
    }

    private static void AppendFeatureDistribution(StringBuilder html, GenomeReportInput input)
    {
        html.Append("<table><tr><th>feature</th><th>mean</th><th>std</th><th>min</th><th>max</th></tr>");
        foreach (var column in input.RawColumns)
        {
            var values = input.Rows
                .Select(row => row.Values.GetValueOrDefault(column, double.NaN))
                .Where(double.IsFinite).ToArray();
            if (values.Length == 0)
                continue;
            var mean = values.Average();
            var std = values.Length < 2
                ? 0
                : System.Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / values.Length);
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td><code>{Escape(column)}</code></td><td>{mean:0.###}</td><td>{std:0.###}</td><td>{values.Min():0.###}</td><td>{values.Max():0.###}</td></tr>");
        }

        html.Append("</table>");
        if (input.DroppedColumns.Count > 0)
            html.Append(CultureInfo.InvariantCulture,
                $"<p class=\"muted\">dropped constant feature columns ({input.DroppedColumns.Count}): {Escape(string.Join(", ", input.DroppedColumns))}</p>");
        else
            html.Append("<p class=\"muted\">no feature column was dropped as constant.</p>");
    }

    private static void AppendDefinitions(StringBuilder html, GenomeReportInput input, int noiseCount)
    {
        html.Append("<h2>Metric definitions</h2><ul class=\"note\">");
        AppendDefinition(html,
            "Silhouette (standard Rousseeuw): s(i) = (b(i) - a(i)) / max(a(i), b(i)), where a is the mean distance to the other rows of the "
            + "same cluster and b is the mean distance to the nearest other cluster over all of its rows (not the nearest point). A row whose "
            + $"cluster is a singleton scores 0. Rows are scored on the deterministic systematic sample of at most {input.SilhouetteSampleSize} rows "
            + "(indexes floor(i * n / sampleSize)); DBSCAN noise rows are excluded.");
        if (string.Equals(input.Options.Method, "kmeans", StringComparison.Ordinal))
            AppendDefinition(html,
                "k-means: the reported solution is the minimum-inertia solution over the documented restart budget; inertia is the sum of squared "
                + "distances to the assigned centers. Auto-k evaluates every k in 2..min(8, n-1) with the same restart budget and selects the "
                + $"maximum-silhouette candidate whose clusters are all at least the minimum cluster size ({input.Options.MinClusterSize}).");
        if (string.Equals(input.Options.Method, "jaccard", StringComparison.Ordinal))
            AppendDefinition(html,
                "Jaccard k-medoids: no inertia is defined. Auto-k evaluates every k in 2..min(6, n-1) and selects the maximum-silhouette "
                + $"candidate whose clusters are all at least the minimum cluster size ({input.Options.MinClusterSize}).");
        AppendDefinition(html,
            "Living lineage metrics: lineage entropy is normalized Shannon entropy over living lineage sizes; largest share is the largest "
            + "lineage divided by all living creatures. New/lost compare lineage ids with the previous save; new gen0 counts new ids containing "
            + "a generation-0 creature and is a founder-injection proxy (enrichment or organ innovation), not an enrichment-only marker. "
            + "Lineage ids are mutation clusters, not stable family ids, so new/lost/turnover mostly measure mutation-lineage churn. "
            + "Prior survival is the share of previous ids still present; turnover = (new + lost) / (previous + current).");
        AppendDefinition(html,
            "Bootstrap ARI (k-means only): each round resamples n rows with replacement, fits k-means centers on the resample, assigns every "
            + "original row to its nearest resampled center, and compares that labeling with the reference labeling by adjusted Rand index; "
            + "the report shows the mean and population standard deviation over the rounds. Non-k-means methods and k=1 show n/a.");
        AppendDefinition(html,
            "Gene-signature enrichment: two-sided Fisher exact test per cluster versus the rest, Benjamini-Hochberg step-up over the full "
            + $"tested set (signatures with support >= {GeneEnrichment.MinimumSupport} rows overall): q(i) = min over j >= i of p(j) * m / j, so q >= p and q <= 1. The tested count m is shown; the display limits rows, never the correction.");
        AppendDefinition(html,
            "Diet features: behavior.softMeals/toughMeals/toxicMeals/corpseMeals are per-creature persisted meal counters. "
            + "diet.totalMeals = soft+tough+toxic (corpse food is a soft subset reported separately); diet.*Share are shares of that total, "
            + "diet.dominance is the largest share, and diet.entropy is normalized Shannon entropy over the three food types (0 = one type, "
            + "1 = equal shares). Older saves without the counters read as zero.");
        AppendDefinition(html,
            "Food and access: food.soft/tough/toxic count food types in the save; food.corpse counts food released by corpses (a soft subset). "
            + "origin.founderShare is the share of parsed creatures at generation 0. access.foodInPickup/foodInSense/foodIn300/foodIn600 count "
            + "positioned food inside the creature's pickup radius, sense range, or fixed 300/600 units; access.nearestFoodDistance is capped at "
            + "600 units and access.nearestSourceDistance is capped at 10,000 units. Save-level access shares and diet.living/experienced/controller "
            + "are computed from all parsed creatures; the optional --max sample affects only the clustering rows and feature means.");
        AppendDefinition(html,
            "Diet population semantics: diet.*Share is 0 for a creature with no meals, so the mean over all creatures is age-structure sensitive "
            + "and is not a valid population diet. Use the timeline diet.living.* (sum over living creatures) or diet.controller.* (cumulative "
            + "controller counters); diet.experienced.* restricts the summary to creatures with at least 10 meals. diet.corpseOfSoftShare is the "
            + "corpse share of soft meals; corpse food is already included in soft meals.");
        AppendDefinition(html,
            "Feature semantics: organ.* is the expressed organ count for every OrganKindV4 (0 = absent); trait.* is the effective life-history "
            + "value after the same fallback defaults as the runtime phenotype adapter. Constant columns are dropped and listed; feature counts "
            + "show used/raw. A non-finite feature value is a hard error.");
        AppendDefinition(html,
            "PCA: components are orthonormal eigenvectors of the standardized covariance matrix; the explained-variance ratio of a component "
            + "is its eigenvalue share of the covariance trace, so the reported ratios sum to at most 1.");
        if (string.Equals(input.Options.Method, "dbscan", StringComparison.Ordinal))
            AppendDefinition(html,
                $"DBSCAN: noise keeps label -1; the reported cluster count and silhouette exclude noise, and min-points counts the point itself. {noiseCount} rows are noise in this run.");
        html.Append("</ul>");
    }

    private static void AppendDefinition(StringBuilder html, string text) =>
        html.Append("<li>").Append(Escape(text)).Append("</li>");

    private static int[] ClusterIndexes(GenomeReportInput input, int cluster) =>
        [.. Enumerable.Range(0, input.Rows.Count).Where(row => input.Clustering.Labels[row] == cluster)];

    private static string FormatMetric(double? value, string format) =>
        value is { } number && double.IsFinite(number)
            ? number.ToString(format, CultureInfo.InvariantCulture)
            : "n/a";

    private static string FormatProbability(double value)
    {
        if (!double.IsFinite(value))
            return "n/a";
        if (value < 0.0001)
            return "<0.0001";
        return value.ToString("0.0000", CultureInfo.InvariantCulture);
    }

    /// <summary>Deterministic palette color for any cluster index (negative = noise grey).</summary>
    public static string PaletteColor(int cluster)
    {
        if (cluster < 0)
            return "#64748b";
        var hue = (cluster * 137.508) % 360;
        return HslToHex(hue, 0.65, 0.55);
    }

    private static string HslToHex(double hue, double saturation, double lightness)
    {
        var chroma = (1 - System.Math.Abs((2 * lightness) - 1)) * saturation;
        var x = chroma * (1 - System.Math.Abs(((hue / 60) % 2) - 1));
        var m = lightness - (chroma / 2);
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        return string.Create(CultureInfo.InvariantCulture,
            $"#{ToByte(red + m):x2}{ToByte(green + m):x2}{ToByte(blue + m):x2}");
    }

    private static int ToByte(double value) =>
        (int)System.Math.Clamp(System.Math.Round(value * 255), 0, 255);

    private static IReadOnlyList<SaveSummary> TimeOrderedSaves(GenomeReportInput input) =>
        [.. input.Saves
            .OrderBy(static save => save.Time)
            .ThenBy(static save => save.Id, StringComparer.Ordinal)
            .ThenBy(static save => save.Path, StringComparer.Ordinal)];

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string Esc(string value) => Escape(value);

    private static readonly string[] SelectedFeatures =
    [
        "behavior.drive", "behavior.displacement", "neutral.move", "behavior.corpseMeals",
        "behavior.softMeals", "behavior.toughMeals", "behavior.toxicMeals",
        "diet.totalMeals", "diet.softShare", "diet.toughShare", "diet.toxicShare",
        "diet.corpseShare", "diet.dominance", "diet.entropy",
        "behavior.kills", "behavior.children", "behavior.age", "behavior.energyRatio",
        "gene.count", "body.organs", "neural.nodes", "neural.edges",
    ];
}
