using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>A temporary directory that is deleted after the test.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    internal string Path { get; } = Directory.CreateTempSubdirectory("origo-analysis-tests-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leaked test directory must not fail the suite.
        }
    }
}

/// <summary>
///     Builds minimal but structurally valid v7 SND saves in a temporary directory
///     from the real Logic genome factory and genome codec. No binary fixture is
///     checked in.
/// </summary>
internal static class SaveFixture
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    /// <summary>Creates a save directory and returns the save root path.</summary>
    internal static string CreateV7Save(
        TemporaryDirectory directory,
        string slotName,
        IReadOnlyList<(string Name, string Genome)> creatures,
        double time = 10d,
        double tick = 100d,
        int food = 0,
        int corpses = 0,
        int foodSources = 1,
        double genomeVersion = 7d,
        bool includeController = true,
        bool includeTime = true,
        bool includeVersion = true,
        IReadOnlyDictionary<string, object>? extraControllerPairs = null)
    {
        var saveRoot = Path.Combine(directory.Path, slotName);
        var sceneDirectory = Path.Combine(saveRoot, "level_pond");
        Directory.CreateDirectory(sceneDirectory);
        var entities = new List<Dictionary<string, object>>();
        if (includeController)
        {
            var controllerPairs = new Dictionary<string, object>
            {
                ["evo.kind"] = "controller",
                ["evo.tick"] = tick,
                ["evo.seed"] = 20261101d,
                ["evo.population"] = creatures.Count,
                ["evo.spikeKills"] = 3d,
            };
            if (includeTime)
                controllerPairs["evo.time"] = time;
            if (includeVersion)
                controllerPairs["evo.genomeVersion"] = genomeVersion;
            if (extraControllerPairs is not null)
                foreach (var (key, value) in extraControllerPairs)
                    controllerPairs[key] = value;
            entities.Add(Entity("controller", controllerPairs));
        }

        for (var index = 0; index < creatures.Count; index++)
        {
            var (name, genome) = creatures[index];
            var creaturePairs = new Dictionary<string, object>
            {
                ["evo.kind"] = "creature",
                ["evo.bodyGenome"] = genome,
                ["evo.x"] = 1d + index,
                ["evo.y"] = 2d + index,
                ["evo.prevX"] = 1d + index,
                ["evo.prevY"] = 2d,
                ["evo.age"] = 30d + index,
                ["evo.energy"] = 50d,
                ["evo.maxEnergy"] = 100d,
                ["evo.drive"] = 0.5d,
                ["evo.turn"] = 0.1d,
                ["evo.spikeDrive"] = 0.2d,
                ["evo.aim"] = 0.3d,
                ["evo.sprintDrive"] = 0.4d,
                ["evo.lineage"] = 1d,
                ["evo.generation"] = 2d,
                ["evo.children"] = 1d,
                ["evo.kills"] = 0d,
                ["evo.organMask"] = 1d,
                ["evo.spikeCount"] = 1d,
                ["evo.moveSpeed"] = 45d,
                ["evo.turnSpeed"] = 3d,
                ["evo.senseRange"] = 120d,
                ["evo.spikeRadius"] = 50d,
                ["evo.basalMetabolism"] = 0.8d,
                ["evo.maxAge"] = 200d,
                ["evo.bodyRadius"] = 5d,
            };
            entities.Add(Entity(name, creaturePairs));
        }

        for (var index = 0; index < food; index++)
            entities.Add(Entity($"food_{index}", new Dictionary<string, object> { ["evo.kind"] = "food" }));
        for (var index = 0; index < corpses; index++)
            entities.Add(Entity($"corpse_{index}", new Dictionary<string, object> { ["evo.kind"] = "corpse" }));
        for (var index = 1; index < foodSources; index++)
            entities.Add(Entity($"source_{index}", new Dictionary<string, object> { ["evo.kind"] = "food_source" }));
        if (foodSources > 0)
            entities.Add(Entity("source_0", new Dictionary<string, object> { ["evo.kind"] = "food_source" }));

        var scenePath = Path.Combine(sceneDirectory, "snd_scene.json");
        File.WriteAllText(scenePath, JsonSerializer.Serialize(entities, SerializerOptions));
        return saveRoot;
    }

    /// <summary>Writes an arbitrary raw scene document (for reader failure tests).</summary>
    internal static string WriteRawScene(TemporaryDirectory directory, string slotName, string json)
    {
        var saveRoot = Path.Combine(directory.Path, slotName);
        var sceneDirectory = Path.Combine(saveRoot, "level_pond");
        Directory.CreateDirectory(sceneDirectory);
        File.WriteAllText(Path.Combine(sceneDirectory, "snd_scene.json"), json);
        return saveRoot;
    }

    /// <summary>The default minimal valid genome used by the save fixtures.</summary>
    internal static GenomeV4 DefaultGenome() => GenomeV4Factory.CreateSimpleReflex();

    /// <summary>Encodes a genome in the v7 save storage form.</summary>
    internal static string Encode(GenomeV4 genome) => GenomeV4Codec.ToBase64(genome);

    /// <summary>Builds a list of identical named creatures carrying the same genome.</summary>
    internal static (string Name, string Genome)[] Creatures(GenomeV4 genome, int count) =>
        [.. Enumerable.Range(0, count).Select(index => ($"creature_{index}", Encode(genome)))];

    private static Dictionary<string, object> Entity(string name, Dictionary<string, object> pairs)
    {
        var wrapped = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            wrapped[key] = new Dictionary<string, object>(StringComparer.Ordinal) { ["data"] = value };
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["data"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["pairs"] = wrapped },
        };
    }
}
