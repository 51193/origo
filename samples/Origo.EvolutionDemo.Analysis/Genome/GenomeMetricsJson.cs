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
        ArgumentNullException.ThrowIfNull(input.Lineage);
        ArgumentNullException.ThrowIfNull(input.Mortality);
        ArgumentNullException.ThrowIfNull(input.Births);
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
            WriteLineageSequence(writer, "lineageSequence", input.Lineage.Sequence);
            WriteLineageSequence(writer, "rootLineageSequence", input.Lineage.RootSequence);
            WriteOrigins(writer, input.Lineage.Origins);
            WriteMortalitySequence(writer, input.Mortality.Sequence);
            WriteRootMortality(writer, input.Mortality, input.Lineage);
            WriteRootBirths(writer, input.Births, input.Lineage);
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
            var ecology = input.Ecology.FirstOrDefault(item =>
                string.Equals(item.Path, save.Path, StringComparison.Ordinal));
            if (ecology is not null)
                WriteEcology(writer, ecology);
            var lineage = input.Lineage.Saves.FirstOrDefault(item =>
                string.Equals(item.Path, save.Path, StringComparison.Ordinal));
            if (lineage is not null)
            {
                WriteLineage(writer, "lineage", lineage.Summary);
                WriteLineage(writer, "rootLineage", lineage.RootSummary);
                WriteOrigins(writer, lineage.Origins);
            }
            var mortality = input.Mortality.Saves.FirstOrDefault(item =>
                string.Equals(item.Path, save.Path, StringComparison.Ordinal));
            if (mortality?.Counts is { } deaths)
                WriteDeaths(writer, deaths);
            if (ecology is not null)
                WriteController(writer, save);

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteEcology(Utf8JsonWriter writer, SaveEcology ecology)
    {
        writer.WriteStartObject("foodInventory");
        writer.WriteNumber("soft", ecology.Food.Soft);
        writer.WriteNumber("tough", ecology.Food.Tough);
        writer.WriteNumber("toxic", ecology.Food.Toxic);
        writer.WriteNumber("corpse", ecology.Food.Corpse);
        writer.WriteNumber("total", ecology.Food.Total);
        writer.WriteEndObject();
        writer.WriteStartObject("patch");
        WriteNullableNumber(writer, "count", ecology.LivePatchCount);
        WriteNullableNumber(writer, "radius", ecology.PatchRadius);
        WriteNullableNumber(writer, "pondRadius", ecology.PondRadius);
        WriteNullableNumber(writer, "coverage", ecology.PatchCoverage);
        writer.WriteEndObject();
        writer.WriteStartObject("origin");
        writer.WriteNumber("founderShare", ecology.FounderShare);
        writer.WriteNumber("meanGeneration", ecology.MeanGeneration);
        writer.WriteNumber("maxGeneration", ecology.MaxGeneration);
        writer.WriteEndObject();
        writer.WriteStartObject("access");
        writer.WriteNumber("creatures", ecology.Access.Creatures);
        writer.WriteNumber("foodInPickupShare", ecology.Access.FractionInPickup);
        writer.WriteNumber("foodInSenseShare", ecology.Access.FractionInSense);
        writer.WriteNumber("foodIn300Share", ecology.Access.FractionIn300);
        writer.WriteNumber("meanFoodInSense", ecology.Access.MeanFoodInSense);
        writer.WriteNumber("meanFoodIn300", ecology.Access.MeanFoodIn300);
        WriteNullableNumber(writer, "nearestFoodP50", ecology.Access.NearestFoodP50);
        WriteNullableNumber(writer, "nearestSourceP50", ecology.Access.NearestSourceP50);
        writer.WriteEndObject();
        writer.WriteStartObject("diet");
        WriteDiet(writer, "living", ecology.LivingDiet);
        WriteDiet(writer, "experienced", ecology.ExperiencedDiet);
        if (ecology.ControllerDiet is { } controller)
            WriteDiet(writer, "controller", controller);
        else
            writer.WriteNull("controller");
        writer.WriteNumber("experiencedCreatures", ecology.ExperiencedCreatures);
        writer.WriteNumber("specialistShare60", ecology.SpecialistShare60);
        WriteNullableNumber(writer, "meanExperiencedEntropy", ecology.MeanExperiencedEntropy);
        writer.WriteEndObject();
    }

    private static void WriteDiet(Utf8JsonWriter writer, string name, DietShares diet)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("soft", diet.Soft);
        writer.WriteNumber("tough", diet.Tough);
        writer.WriteNumber("toxic", diet.Toxic);
        writer.WriteNumber("corpseOfSoft", diet.CorpseOfSoft);
        writer.WriteNumber("totalMeals", diet.TotalMeals);
        writer.WriteEndObject();
    }

    private static void WriteLineageSequence(
        Utf8JsonWriter writer, string name, LineageSequenceSummary sequence)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("distinctLineages", sequence.DistinctLineages);
        writer.WriteNumber("peakLivingLineages", sequence.PeakLivingLineages);
        writer.WriteNumber("finalLivingLineages", sequence.FinalLivingLineages);
        writer.WriteNumber("totalNewLineages", sequence.TotalNewLineages);
        writer.WriteNumber("totalNewGen0Lineages", sequence.TotalNewGen0Lineages);
        writer.WriteNumber("lostLineages", sequence.LostLineages);
        WriteNullableNumber(writer, "meanPriorLineageSurvival", sequence.MeanPriorLineageSurvival);
        WriteNullableNumber(writer, "meanLineageTurnover", sequence.MeanLineageTurnover);
        writer.WriteEndObject();
    }

    private static void WriteLineage(Utf8JsonWriter writer, string name, LineageSummary lineage)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("count", lineage.Lineages);
        writer.WriteNumber("founderLineages", lineage.FounderLineages);
        writer.WriteNumber("founderLineageShare", lineage.FounderLineageShare);
        writer.WriteNumber("gen0Individuals", lineage.Gen0Individuals);
        writer.WriteNumber("gen0Share", lineage.Gen0Share);
        writer.WriteNumber("largestShare", lineage.LargestLineageShare);
        writer.WriteNumber("entropy", lineage.LineageEntropy);
        writer.WriteNumber("new", lineage.NewLineages);
        writer.WriteNumber("newGen0", lineage.NewGen0Lineages);
        writer.WriteNumber("lost", lineage.LostLineages);
        WriteNullableNumber(writer, "priorSurvival", lineage.PriorLineageSurvival);
        writer.WriteNumber("turnover", lineage.LineageTurnover);
        writer.WriteEndObject();
    }

    private static void WriteOrigins(Utf8JsonWriter writer, SpawnOriginCounts origins)
    {
        writer.WriteStartObject("spawnOrigins");
        writer.WriteNumber("founders", origins.Founders);
        writer.WriteNumber("children", origins.Children);
        writer.WriteNumber("enrichment", origins.Enrichment);
        writer.WriteNumber("innovation", origins.Innovation);
        writer.WriteNumber("overgrowth", origins.Overgrowth);
        writer.WriteNumber("unknown", origins.Unknown);
        writer.WriteEndObject();
    }

    private static void WriteMortalitySequence(Utf8JsonWriter writer, MortalitySequenceSummary sequence)
    {
        writer.WriteStartObject("mortalitySequence");
        if (sequence.Counts is { } counts)
        {
            writer.WriteStartObject("counts");
            WriteDeathCounts(writer, counts);
            writer.WriteEndObject();
            WriteNullableNumber(writer, "predationShare", sequence.PredationShare);
            WriteNullableNumber(writer, "starvationShare", sequence.StarvationShare);
            WriteNullableNumber(writer, "agingShare", sequence.AgingShare);
        }
        else
        {
            writer.WriteNull("counts");
        }

        writer.WriteEndObject();
    }

    private static void WriteRootMortality(
        Utf8JsonWriter writer, MortalityAnalysis mortality, LineageAnalysis lineage)
    {
        var finalPath = lineage.Saves.Count == 0 ? null : lineage.Saves[^1].Path;
        var livingRoots = lineage.RootDetails
            .Where(item => string.Equals(item.Path, finalPath, StringComparison.Ordinal))
            .Select(static item => item.RootLineage)
            .ToHashSet();
        writer.WriteStartObject("rootMortalitySequence");
        if (mortality.RootSummary is { } summary)
        {
            writer.WriteNumber("roots", summary.Roots);
            writer.WriteStartObject("counts");
            WriteDeathCounts(writer, summary.Counts);
            writer.WriteEndObject();
            WriteNullableNumber(writer, "predationShare", summary.PredationShare);
            WriteNullableNumber(writer, "starvationShare", summary.StarvationShare);
            WriteNullableNumber(writer, "agingShare", summary.AgingShare);
            if (mortality.RootDeaths is { Count: > 0 } top)
            {
                var (root, tally) = top.MaxBy(static pair => pair.Value.Total);
                writer.WriteNumber("topRootLineage", root);
                writer.WriteNumber("topRootDeaths", tally.Total);
                writer.WriteNumber("topRootShare", tally.Total / (double)summary.Counts.Total);
                writer.WriteNumber("topRootPredation", tally.Predation);
                writer.WriteBoolean("topRootLiving", livingRoots.Contains(root));
            }
        }
        else
        {
            writer.WriteNull("summary");
        }

        writer.WriteEndObject();
        writer.WriteStartArray("rootMortality");
        if (mortality.RootDeaths is not null)
            foreach (var (root, tally) in mortality.RootDeaths.OrderByDescending(static pair => pair.Value.Total)
                .ThenBy(static pair => pair.Key))
            {
                writer.WriteStartObject();
                writer.WriteNumber("rootLineage", root);
                writer.WriteNumber("starvation", tally.Starvation);
                writer.WriteNumber("predation", tally.Predation);
                writer.WriteNumber("aging", tally.Aging);
                writer.WriteNumber("total", tally.Total);
                writer.WriteBoolean("living", livingRoots.Contains(root));
                writer.WriteEndObject();
            }

        writer.WriteEndArray();
    }

    private static void WriteRootBirths(
        Utf8JsonWriter writer, BirthsAnalysis births, LineageAnalysis lineage)
    {
        var finalPath = lineage.Saves.Count == 0 ? null : lineage.Saves[^1].Path;
        var livingRoots = lineage.RootDetails
            .Where(item => string.Equals(item.Path, finalPath, StringComparison.Ordinal))
            .Select(static item => item.RootLineage)
            .ToHashSet();
        writer.WriteStartObject("rootBirthsSequence");
        if (births.Summary is { } summary)
        {
            writer.WriteNumber("roots", summary.Roots);
            writer.WriteNumber("totalBirths", summary.TotalBirths);
            if (summary.TopRoot is { } topRoot) writer.WriteNumber("topRootLineage", topRoot); else writer.WriteNull("topRootLineage");
            if (summary.TopRootBirths is { } topBirths) writer.WriteNumber("topRootBirths", topBirths); else writer.WriteNull("topRootBirths");
            WriteNullableNumber(writer, "topRootShare", summary.TopRootShare);
            if (summary.TopRoot is { } root) writer.WriteBoolean("topRootLiving", livingRoots.Contains(root));
        }
        else
        {
            writer.WriteNull("summary");
        }

        writer.WriteEndObject();
        writer.WriteStartArray("rootBirths");
        if (births.Latest is not null)
            foreach (var (root, count) in births.Latest.OrderByDescending(static pair => pair.Value)
                .ThenBy(static pair => pair.Key))
            {
                writer.WriteStartObject();
                writer.WriteNumber("rootLineage", root);
                writer.WriteNumber("births", count);
                writer.WriteBoolean("living", livingRoots.Contains(root));
                writer.WriteEndObject();
            }

        writer.WriteEndArray();
    }

    private static void WriteDeaths(Utf8JsonWriter writer, DeathCounts counts)
    {
        writer.WriteStartObject("deaths");
        WriteDeathCounts(writer, counts);
        writer.WriteEndObject();
    }

    private static void WriteDeathCounts(Utf8JsonWriter writer, DeathCounts counts)
    {
        writer.WriteNumber("starvation", counts.Starvation);
        writer.WriteNumber("predation", counts.Predation);
        writer.WriteNumber("aging", counts.Aging);
        writer.WriteNumber("total", counts.Total);
    }

    private static void WriteController(Utf8JsonWriter writer, SaveSummary save)
    {
        writer.WriteStartObject("controller");
        writer.WriteStartObject("numbers");
        foreach (var (key, value) in save.ControllerNumbers.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            writer.WriteNumber(key, value);
        writer.WriteEndObject();
        writer.WriteStartObject("strings");
        foreach (var (key, value) in save.ControllerStrings.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            writer.WriteString(key, value);
        writer.WriteEndObject();
        writer.WriteStartObject("booleans");
        foreach (var (key, value) in save.ControllerBooleans.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            writer.WriteBoolean(key, value);
        writer.WriteEndObject();
        writer.WriteEndObject();
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

    private static void WriteNullableNumber(Utf8JsonWriter writer, string property, int? value)
    {
        if (value is { } number)
            writer.WriteNumber(property, number);
        else
            writer.WriteNull(property);
    }
}
