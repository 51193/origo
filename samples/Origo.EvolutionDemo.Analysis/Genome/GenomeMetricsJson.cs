using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Origo.EvolutionDemo.Analysis.Math;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>
///     Writes the machine-readable metrics document for one genome-analysis run.
///     This is a public automation surface: keys and their ordering are stable,
///     numbers are invariant-culture, and every value is the same value the HTML
///     report shows. The wall-clock generation time is deliberately absent so the
///     document is deterministic for a given input and seed.
/// </summary>
public static class GenomeMetricsJson
{
    /// <summary>Name of the tool that writes the document.</summary>
    public const string ToolName = "Origo.EvolutionDemo.Analysis";

    /// <summary>Rule used to draw the deterministic silhouette sample.</summary>
    public const string SilhouetteSampleRule = "systematic-stride-floor(i*rowCount/sampleSize)";

    /// <summary>Serializes <paramref name="input" /> as an indented JSON object.</summary>
    /// <exception cref="ArgumentNullException">The input or one of its required parts is null.</exception>
    public static string Build(GenomeReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Options);
        ArgumentNullException.ThrowIfNull(input.Saves);
        ArgumentNullException.ThrowIfNull(input.Rows);
        ArgumentNullException.ThrowIfNull(input.RawColumns);
        ArgumentNullException.ThrowIfNull(input.DroppedColumns);
        ArgumentNullException.ThrowIfNull(input.SkippedCreatures);
        ArgumentNullException.ThrowIfNull(input.Pca);
        ArgumentNullException.ThrowIfNull(input.Clustering);
        ArgumentNullException.ThrowIfNull(input.Candidates);
        ArgumentNullException.ThrowIfNull(input.Enrichment);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("tool", ToolName);
            writer.WriteString("command", "genome");
            writer.WriteString("commandLine", input.CommandLine);
            WriteOptions(writer, input);
            WritePopulation(writer, input);
            WriteSaves(writer, input);
            WriteFeatures(writer, input);
            WriteClustering(writer, input);
            WritePca(writer, input);
            WriteEnrichment(writer, input);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteOptions(Utf8JsonWriter writer, GenomeReportInput input)
    {
        var options = input.Options;
        writer.WriteStartObject("options");
        writer.WriteNumber("seed", options.Seed);
        writer.WriteNumber("max", options.MaxCreatures);
        writer.WriteString("mode", options.Mode);
        writer.WriteString("method", options.Method);
        writer.WriteNumber("requestedK", options.RequestedK);
        writer.WriteNumber("actualK", input.Clustering.K);
        writer.WriteNumber("stabilityRounds", options.StabilityRounds);
        writer.WriteNumber("eps", options.Eps);
        writer.WriteNumber("minPoints", options.MinPoints);
        writer.WriteNumber("minClusterSize", options.MinClusterSize);
        writer.WriteBoolean("allowSkips", options.AllowSkips);
        writer.WriteEndObject();
    }

    private static void WritePopulation(Utf8JsonWriter writer, GenomeReportInput input)
    {
        writer.WriteStartObject("population");
        writer.WriteNumber("total", input.TotalCreatures);
        writer.WriteNumber("analyzed", input.Rows.Count);
        writer.WriteNumber("considered", input.ConsideredCreatures);
        writer.WriteNumber("skipped", input.SkippedCreatures.Count);
        writer.WriteEndObject();
    }

    private static void WriteSaves(Utf8JsonWriter writer, GenomeReportInput input)
    {
        writer.WriteStartArray("saves");
        foreach (var save in input.Saves
            .OrderBy(static save => save.Time)
            .ThenBy(static save => save.Id, StringComparer.Ordinal)
            .ThenBy(static save => save.Path, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", save.Id);
            writer.WriteString("path", save.Path);
            writer.WriteNumber("time", save.Time);
            writer.WriteNumber("tick", save.Tick);
            writer.WriteNumber("creatures", save.Creatures);
            writer.WriteNumber("food", save.Food);
            writer.WriteNumber("corpses", save.Corpses);
            writer.WriteNumber("foodSources", save.FoodSources);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteFeatures(Utf8JsonWriter writer, GenomeReportInput input)
    {
        writer.WriteStartObject("features");
        writer.WriteNumber("raw", input.RawColumns.Count);
        writer.WriteNumber("used", input.UsedFeatureCount);
        writer.WriteStartArray("droppedColumns");
        foreach (var column in input.DroppedColumns)
            writer.WriteStringValue(column);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteClustering(Utf8JsonWriter writer, GenomeReportInput input)
    {
        var clustering = input.Clustering;
        writer.WriteStartObject("clustering");
        writer.WriteString("method", input.Options.Method);
        writer.WriteNumber("k", clustering.K);
        WriteNullableNumber(writer, "inertia", clustering.Inertia);
        WriteNullableNumber(writer, "silhouette", clustering.Silhouette);
        writer.WriteString("silhouetteDefinitionId", Silhouette.DefinitionId);
        writer.WriteNumber("silhouetteSampleSize", input.SilhouetteSampleSize);
        writer.WriteNull("silhouetteSampleSeed");
        writer.WriteString("silhouetteSampleRule", SilhouetteSampleRule);
        if (input.Stability is { } stability)
        {
            writer.WriteStartObject("bootstrapAri");
            writer.WriteNumber("mean", stability.Mean);
            writer.WriteNumber("standardDeviation", stability.StandardDeviation);
            writer.WriteNumber("rounds", stability.Rounds);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("bootstrapAri");
        }

        if (input.Candidates.Count > 0)
        {
            writer.WriteStartObject("autoK");
            writer.WriteNumber("maxK", input.AutoKMax);
            writer.WriteBoolean("noValidCandidate", input.NoValidCandidate);
            writer.WriteStartArray("candidates");
            foreach (var candidate in input.Candidates)
            {
                writer.WriteStartObject();
                writer.WriteNumber("k", candidate.K);
                WriteNullableNumber(writer, "inertia", candidate.Inertia);
                WriteNullableNumber(writer, "silhouette", candidate.Silhouette);
                writer.WriteStartArray("sizes");
                foreach (var size in candidate.Sizes)
                    writer.WriteNumberValue(size);
                writer.WriteEndArray();
                writer.WriteStartArray("flags");
                foreach (var flag in candidate.Flags)
                    writer.WriteStringValue(flag);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("autoK");
        }

        var noise = clustering.Labels.Count(static label => label < 0);
        if (string.Equals(input.Options.Method, "dbscan", StringComparison.Ordinal))
            writer.WriteNumber("dbscanNoise", noise);
        else
            writer.WriteNull("dbscanNoise");
        writer.WriteEndObject();
    }

    private static void WritePca(Utf8JsonWriter writer, GenomeReportInput input)
    {
        writer.WriteStartObject("pca");
        writer.WriteNumber("componentCount", input.Pca.Components.Count);
        writer.WriteStartArray("explainedVarianceRatios");
        foreach (var ratio in input.Pca.ExplainedVarianceRatios)
            writer.WriteNumberValue(ratio);
        writer.WriteEndArray();
        writer.WriteStartArray("components");
        foreach (var component in input.Pca.Components)
        {
            writer.WriteStartArray();
            foreach (var weight in component)
                writer.WriteNumberValue(weight);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteEnrichment(Utf8JsonWriter writer, GenomeReportInput input)
    {
        var testedCount = input.Enrichment.Count == 0
            ? 0
            : input.Enrichment.Max(static analysis => analysis.TestedCount);
        writer.WriteStartObject("enrichment");
        writer.WriteNumber("testedCount", testedCount);
        writer.WriteStartArray("clusters");
        foreach (var cluster in Enumerable.Range(0, input.Clustering.K))
        {
            var analysis = input.Enrichment.FirstOrDefault(item => item.Cluster == cluster);
            writer.WriteStartObject();
            writer.WriteNumber("cluster", cluster);
            writer.WriteNumber("testedCount", analysis?.TestedCount ?? 0);
            writer.WriteStartArray("displayed");
            foreach (var result in (analysis?.Signatures ?? []).Where(static result => result.QValue < 0.10).Take(10))
            {
                writer.WriteStartObject();
                writer.WriteString("signature", result.Signature);
                writer.WriteNumber("p", result.PValue);
                writer.WriteNumber("q", result.QValue);
                writer.WriteNumber("odds", result.OddsRatio);
                writer.WriteNumber("clusterWith", result.ClusterWith);
                writer.WriteNumber("clusterTotal", result.ClusterTotal);
                writer.WriteNumber("restWith", result.RestWith);
                writer.WriteNumber("restTotal", result.RestTotal);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string property, double? value)
    {
        if (value is { } number && double.IsFinite(number))
            writer.WriteNumber(property, number);
        else
            writer.WriteNull(property);
    }
}
