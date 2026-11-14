using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Origo.EvolutionDemo.Analysis.IO;

/// <summary>Reads a v6 SND save scene and exposes creature records for analysis.</summary>
public static class SaveSnapshotReader
{
    public static IReadOnlyList<SaveSnapshot> Read(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                files.AddRange(Directory.GetFiles(path, "snd_scene.json", SearchOption.AllDirectories));
            else if (File.Exists(path))
                files.Add(path);
            else
                throw new FileNotFoundException("Save path not found.", path);
        }

        return [.. files.Distinct(StringComparer.Ordinal).OrderBy(static p => p, StringComparer.Ordinal).Select(ReadSnapshot)];
    }

    private static SaveSnapshot ReadSnapshot(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var entities = new List<EntityRecord>();
        var controller = new Dictionary<string, double>(StringComparer.Ordinal);
        var controllerStrings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entity in document.RootElement.EnumerateArray())
            ReadEntity(entity, entities, controller, controllerStrings);

        if (!controller.TryGetValue("evo.genomeVersion", out var version)
            || System.Math.Abs(version - 6d) > 1e-9)
            throw new InvalidDataException(
                $"Save '{path}' is not a v6 save (evo.genomeVersion='{version}'). " +
                "The analysis tool supports only the current save schema.");
        var creatures = entities
            .Where(static entity => entity.Kind == "creature")
            .Select(static entity => CreatureRecord.From(entity))
            .Where(static creature => creature is not null)
            .Select(static creature => creature!)
            .ToArray();
        return new SaveSnapshot(path, controller.GetValueOrDefault("evo.time"),
            controller.GetValueOrDefault("evo.tick"), creatures,
            entities.Count(static entity => entity.Kind == "food"),
            entities.Count(static entity => entity.Kind == "corpse"),
            entities.Count(static entity => entity.Kind == "food_source"));
    }

    private static void ReadEntity(
        JsonElement entity,
        List<EntityRecord> entities,
        Dictionary<string, double> controllerNumbers,
        Dictionary<string, string> controllerStrings)
    {
        if (!entity.TryGetProperty("name", out var nameElement)
            || !entity.TryGetProperty("data", out var data)
            || !data.TryGetProperty("pairs", out var pairs))
            return;
        var name = nameElement.GetString() ?? string.Empty;
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        foreach (var pair in pairs.EnumerateObject())
            values[pair.Name] = ReadValue(pair.Value);
        var kind = values.TryGetValue("evo.kind", out var kindValue) ? kindValue.String : string.Empty;
        if (kind == "controller")
        {
            foreach (var (key, value) in values)
            {
                if (value.Number is { } number)
                    controllerNumbers[key] = number;
                else if (value.String is not null)
                    controllerStrings[key] = value.String;
            }

            return;
        }

        if (string.IsNullOrEmpty(kind))
            return;
        entities.Add(new EntityRecord(name, kind, values));
    }

    private static DataValue ReadValue(JsonElement wrapper)
    {
        var data = wrapper.TryGetProperty("data", out var value) ? value : default;
        return data.ValueKind switch
        {
            JsonValueKind.Number => new DataValue(data.GetDouble(), null),
            JsonValueKind.String => new DataValue(null, data.GetString()),
            _ => new DataValue(null, null),
        };
    }

    internal sealed record DataValue(double? Number, string? String);
}

/// <summary>One save snapshot with simulation metadata and creature rows.</summary>
public sealed record SaveSnapshot(
    string Path,
    double Time,
    double Tick,
    IReadOnlyList<CreatureRecord> Creatures,
    int Food,
    int Corpses,
    int FoodSources);

/// <summary>Flattened creature record from a v6 save.</summary>
public sealed record CreatureRecord(
    string Name,
    string Genome,
    double X,
    double Y,
    double PrevX,
    double PrevY,
    double Age,
    double Energy,
    double MaxEnergy,
    double Drive,
    double Turn,
    double SpikeDrive,
    double Aim,
    double SprintDrive,
    int Lineage,
    int Generation,
    int Children,
    int Kills,
    int CorpseMeals,
    int OrganMask,
    int SpikeCount,
    double MoveSpeed,
    double TurnSpeed,
    double SenseRange,
    double SpikeRadius,
    double BasalMetabolism,
    double MaxAge,
    double BodyRadius)
{
    internal static CreatureRecord? From(EntityRecord entity)
    {
        var v = entity.Values;
        var genome = v.TryGetValue("evo.bodyGenome", out var g) ? g.String : null;
        if (string.IsNullOrWhiteSpace(genome))
            return null;
        double Number(string key, double fallback = 0) =>
            v.TryGetValue(key, out var value) && value.Number is { } number ? number : fallback;
        int Integer(string key, int fallback = 0) =>
            v.TryGetValue(key, out var value) && value.Number is { } number ? (int)number : fallback;
        return new CreatureRecord(
            entity.Name, genome,
            Number("evo.x"), Number("evo.y"), Number("evo.prevX"), Number("evo.prevY"),
            Number("evo.age"), Number("evo.energy"), Number("evo.maxEnergy", 1),
            Number("evo.drive"), Number("evo.turn"), Number("evo.spikeDrive"),
            Number("evo.aim"), Number("evo.sprintDrive"),
            Integer("evo.lineage"), Integer("evo.generation"), Integer("evo.children"),
            Integer("evo.kills"), Integer("evo.creatureCorpseMeals"),
            Integer("evo.organMask"), Integer("evo.spikeCount", 1),
            Number("evo.moveSpeed"), Number("evo.turnSpeed"), Number("evo.senseRange"),
            Number("evo.spikeRadius"), Number("evo.basalMetabolism"), Number("evo.maxAge"),
            Number("evo.bodyRadius"));
    }
}

internal sealed record EntityRecord(string Name, string Kind, IReadOnlyDictionary<string, SaveSnapshotReader.DataValue> Values);
