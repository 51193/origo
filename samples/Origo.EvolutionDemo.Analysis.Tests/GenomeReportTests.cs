using System;
using System.Linq;
using System.Text.Json;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.Reports;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>
///     Regression tests that the HTML report and the machine-readable metrics JSON
///     expose the documented provenance, population, feature, candidate, PCA,
///     enrichment, and noise information with the same numbers.
/// </summary>
public sealed class GenomeReportTests
{
    [Fact]
    public void Build_HtmlContainsProvenancePopulationAndFeatureAccounting()
    {
        var input = ReportFixture.Create();

        var html = GenomeHtmlReport.Build(input);

        Assert.Contains("command <code>genome --save save_a --seed 1</code>", html, StringComparison.Ordinal);
        Assert.Contains("seed=1", html, StringComparison.Ordinal);
        Assert.Contains("method=kmeans", html, StringComparison.Ordinal);
        Assert.Contains("population analyzed/total <b>9/100</b>", html, StringComparison.Ordinal);
        Assert.Contains("features used/raw <b>2/3</b>", html, StringComparison.Ordinal);
        Assert.Contains("constant", html, StringComparison.Ordinal);
        Assert.Contains("dropped constant feature columns", html, StringComparison.Ordinal);
        Assert.Contains("Silhouette (standard Rousseeuw)", html, StringComparison.Ordinal);
        Assert.Contains("Bootstrap ARI", html, StringComparison.Ordinal);
        Assert.Contains("PC1 (60.00% explained variance)", html, StringComparison.Ordinal);
        Assert.Contains("PC2 (25.00% explained variance)", html, StringComparison.Ordinal);
        Assert.Contains("m=4 tested", html, StringComparison.Ordinal);
        Assert.Contains("Food, access, and origin", html, StringComparison.Ordinal);
        Assert.Contains("food in sense", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_FounderInjectionAndGenotypeEnrichmentProduceWarnings()
    {
        var baseInput = ReportFixture.Create();
        var ecology = baseInput.Ecology.Select(item => item with
        {
            FounderShare = 0.2d,
            Warnings = ["serialized patch count disagrees"],
        }).ToArray();
        var input = baseInput with { Ecology = ecology };

        var warnings = GenomeWarnings.Build(input);

        Assert.Contains(warnings, warning => warning.Contains("generation 0", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("enrichment describes", StringComparison.Ordinal));
        Assert.Contains("serialized patch count disagrees", warnings);
    }

    [Fact]
    public void Build_SavesAreTimeOrderedWithIds()
    {
        var input = ReportFixture.Create();

        var html = GenomeHtmlReport.Build(input);

        var first = html.IndexOf("<code>save_a</code>", StringComparison.Ordinal);
        var second = html.IndexOf("<code>save_b</code>", StringComparison.Ordinal);
        var third = html.IndexOf("<code>save_c</code>", StringComparison.Ordinal);
        Assert.True(first >= 0 && second >= 0 && third >= 0);
        Assert.True(first < second && second < third);
        Assert.Contains("<td>10</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>300</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_LegendListsOnlyActualClusters()
    {
        var input = ReportFixture.Create(clusterCount: 2);

        var html = GenomeHtmlReport.Build(input);

        Assert.Contains(">cluster 1<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">cluster 2<", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AutoKCandidateTableShowsEveryCandidateAndFlags()
    {
        var input = ReportFixture.Create();

        var html = GenomeHtmlReport.Build(input);

        Assert.Contains("Auto-k candidates", html, StringComparison.Ordinal);
        Assert.Contains("rejected, small-clusters", html, StringComparison.Ordinal);
        Assert.Contains("accepted, selected", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DbscanNoiseIsReportedAndExcludedFromClusterTables()
    {
        var baseInput = ReportFixture.Create(clusterCount: 2, withNoise: true);
        var dbscanInput = baseInput with
        {
            Clustering = baseInput.Clustering with { Inertia = null },
            Candidates = [],
            AutoKMax = 0,
            Stability = null,
            Options = baseInput.Options with { Method = "dbscan", RequestedK = 0 },
        };

        var html = GenomeHtmlReport.Build(dbscanInput);

        Assert.Contains("noise <b>2", html, StringComparison.Ordinal);
        Assert.Contains("labelled 2 of 8 rows as noise", html, StringComparison.Ordinal);
        Assert.Contains("inertia <b>n/a</b>", html, StringComparison.Ordinal);
        Assert.Contains("bootstrap ARI <b>n/a</b>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DbscanZeroNoiseStillReportsNoiseAndDbscanDefinitions()
    {
        var baseInput = ReportFixture.Create(clusterCount: 2, withNoise: false);
        var dbscanInput = baseInput with
        {
            Clustering = baseInput.Clustering with { Inertia = null },
            Candidates = [],
            AutoKMax = 0,
            Stability = null,
            Options = baseInput.Options with { Method = "dbscan", RequestedK = 0 },
        };

        var html = GenomeHtmlReport.Build(dbscanInput);
        var json = JsonDocument.Parse(GenomeMetricsJson.Build(dbscanInput)).RootElement;

        Assert.Contains("noise <b>0", html, StringComparison.Ordinal);
        Assert.Contains("DBSCAN:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("searched 2..0", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto-k candidates", html, StringComparison.Ordinal);
        Assert.Contains("min-cluster-size=n/a", html, StringComparison.Ordinal);
        Assert.Equal(0, json.GetProperty("clustering").GetProperty("dbscanNoise").GetInt32());
    }

    [Fact]
    public void Build_JaccardUsesJaccardDefinitionsNotKMeansDefinitions()
    {
        var input = ReportFixture.Create() with
        {
            Options = ReportFixture.Create().Options with { Method = "jaccard", RequestedK = 0 },
        };

        var html = GenomeHtmlReport.Build(input);

        Assert.Contains("Jaccard k-medoids", html, StringComparison.Ordinal);
        Assert.Contains("maximum-silhouette solution", html, StringComparison.Ordinal);
        Assert.DoesNotContain("min-inertia solution", html, StringComparison.Ordinal);
        Assert.DoesNotContain("k-means: the reported solution", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EnrichmentNoteDoesNotClaimMetricsJsonIsComplete()
    {
        var input = ReportFixture.Create(testedSignatureCount: 3);

        var html = GenomeHtmlReport.Build(input);

        Assert.Contains("complete tested set is written only by --enrichment-csv", html, StringComparison.Ordinal);
        Assert.DoesNotContain("full tested set is written by --enrichment-csv and --metrics-json", html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MetricsJson_IsValidAndCarriesTheSameNumbersAsTheHtmlReport()
    {
        var input = ReportFixture.Create();

        var json = JsonDocument.Parse(GenomeMetricsJson.Build(input)).RootElement;
        var html = GenomeHtmlReport.Build(input);

        Assert.Equal("Origo.EvolutionDemo.Analysis", json.GetProperty("tool").GetString());
        Assert.Equal("kmeans", json.GetProperty("clustering").GetProperty("method").GetString());
        Assert.Equal(3, json.GetProperty("clustering").GetProperty("k").GetInt32());
        Assert.Equal(321.5d, json.GetProperty("clustering").GetProperty("inertia").GetDouble(), 6);
        Assert.Equal(0.4567d, json.GetProperty("clustering").GetProperty("silhouette").GetDouble(), 6);
        Assert.Equal(0.9d, json.GetProperty("clustering").GetProperty("bootstrapAri").GetProperty("mean").GetDouble(), 6);
        Assert.Equal(5, json.GetProperty("clustering").GetProperty("bootstrapAri").GetProperty("rounds").GetInt32());
        Assert.Equal(9, json.GetProperty("population").GetProperty("analyzed").GetInt32());
        Assert.Equal(100, json.GetProperty("population").GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("features").GetProperty("used").GetInt32());
        Assert.Equal(3, json.GetProperty("features").GetProperty("raw").GetInt32());
        Assert.Equal("constant", json.GetProperty("features").GetProperty("droppedColumns")[0].GetString());
        Assert.Equal(2, json.GetProperty("pca").GetProperty("componentCount").GetInt32());
        Assert.Equal(4, json.GetProperty("clustering").GetProperty("autoK").GetProperty("candidates").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("clustering").GetProperty("dbscanNoise").ValueKind);
        // The headline numbers printed by the HTML exist verbatim in the JSON document.
        Assert.Contains("silhouette <b>0.457</b>", html, StringComparison.Ordinal);
        Assert.Contains("\"silhouette\": 0.4567", GenomeMetricsJson.Build(input), StringComparison.Ordinal);
        Assert.Contains("inertia <b>321.5</b>", html, StringComparison.Ordinal);
        Assert.Contains("analyzed/total <b>9/100</b>", html, StringComparison.Ordinal);

        var options = json.GetProperty("options");
        Assert.Equal(1, options.GetProperty("seed").GetInt32());
        Assert.Equal(10, options.GetProperty("minClusterSize").GetInt32());
        var firstSave = json.GetProperty("saves")[0];
        Assert.Equal(JsonValueKind.Number, firstSave.GetProperty("food").ValueKind);
        Assert.Equal(4, firstSave.GetProperty("foodInventory").GetProperty("soft").GetInt32());
        Assert.Equal(0.5d, firstSave.GetProperty("access").GetProperty("foodInSenseShare").GetDouble(), 6);
        Assert.True(firstSave.GetProperty("controller").GetProperty("numbers").TryGetProperty("evo.tick", out _));
    }

    [Fact]
    public void MetricsJson_IsDeterministicForTheSameInput()
    {
        var input = ReportFixture.Create();

        var first = GenomeMetricsJson.Build(input);
        var second = GenomeMetricsJson.Build(input);

        Assert.Equal(first, second);
        Assert.DoesNotContain("generatedAt", first, StringComparison.Ordinal);
    }

    [Fact]
    public void MetricsJson_KOneAndNonKMeansUseNullConversions()
    {
        var baseInput = ReportFixture.Create(clusterCount: 1, rowsPerCluster: 4, withAutoK: false, stabilityRounds: 0);

        var json = JsonDocument.Parse(GenomeMetricsJson.Build(baseInput)).RootElement;

        Assert.Equal(JsonValueKind.Null, json.GetProperty("clustering").GetProperty("bootstrapAri").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("clustering").GetProperty("autoK").ValueKind);
        var html = GenomeHtmlReport.Build(baseInput);
        Assert.Contains("bootstrap ARI <b>n/a</b>", html, StringComparison.Ordinal);
        Assert.Contains("A single cluster has no differentiating features", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SkippedCreatureWarningIsShown()
    {
        var baseInput = ReportFixture.Create() with
        {
            SkippedCreatures = [new SkippedCreature("/saves/save_a/level_pond/snd_scene.json", "bad", "InvalidDataException: bad genome")],
            Rows = [],
            TotalCreatures = 5,
            ConsideredCreatures = 5,
        };
        var input = baseInput with
        {
            Clustering = baseInput.Clustering with { Labels = [], K = 0 },
            Candidates = [],
            AutoKMax = 0,
            Enrichment = [],
        };

        var html = GenomeHtmlReport.Build(input);
        var json = JsonDocument.Parse(GenomeMetricsJson.Build(input)).RootElement;

        Assert.Contains("1 skipped (--allow-skips)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("--max 5000 sampled", html, StringComparison.Ordinal);
        Assert.Equal(1, json.GetProperty("population").GetProperty("skipped").GetInt32());
    }
}
