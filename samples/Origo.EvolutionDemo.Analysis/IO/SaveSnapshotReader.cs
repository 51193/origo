using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Origo.EvolutionDemo.Analysis.IO;

/// <summary>
///     Reads v7 SND save scenes and exposes creature and controller records for
///     analysis. Paths are canonicalized before deduplication; a directory request
///     that resolves to more than one save scene is rejected unless
///     <c>allowPool</c> is set, and malformed entities or creatures are counted
///     instead of being silently ignored.
/// </summary>
public static class SaveSnapshotReader
{
    /// <summary>The v7 genome schema version accepted by this reader.</summary>
    public const double SupportedGenomeVersion = 7d;

    /// <summary>Reads one snapshot per path, rejecting multi-scene directories.</summary>
    /// <param name="paths">Save roots, level directories, or snd_scene.json files.</param>
    /// <exception cref="FileNotFoundException">A requested path does not exist.</exception>
    /// <exception cref="InvalidOperationException">A directory resolves to several scenes without pooling.</exception>
    public static IReadOnlyList<SaveSnapshot> Read(params string[] paths) => Read(paths, allowPool: false);

    /// <summary>Reads one snapshot per resolved save scene.</summary>
    /// <param name="paths">Save roots, level directories, or snd_scene.json files.</param>
    /// <param name="allowPool">
    ///     When true, a directory that contains several save scenes is analyzed as
    ///     one pooled input; when false it is a hard error.
    /// </param>
    /// <exception cref="ArgumentException">The path list is empty or contains a blank entry.</exception>
    /// <exception cref="FileNotFoundException">A requested path does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">A directory contains no snd_scene.json.</exception>
    /// <exception cref="InvalidOperationException">A directory resolves to several scenes without pooling.</exception>
    public static IReadOnlyList<SaveSnapshot> Read(IReadOnlyList<string> paths, bool allowPool)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
            throw new ArgumentException("At least one save path is required.", nameof(paths));
        var files = new List<string>();
        foreach (var requested in paths)
        {
            if (string.IsNullOrWhiteSpace(requested))
                throw new ArgumentException("Save paths must not be blank.", nameof(paths));
            var fullPath = Path.GetFullPath(requested);
            if (Directory.Exists(fullPath))
            {
                var scenes = Directory.GetFiles(fullPath, "snd_scene.json", SearchOption.AllDirectories)
                    .Select(Path.GetFullPath)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToArray();
                if (scenes.Length == 0)
                    throw new DirectoryNotFoundException(
                        $"No snd_scene.json was found under save path '{fullPath}'.");
                if (scenes.Length > 1 && !allowPool)
                    throw new InvalidOperationException(
                        $"Save path '{fullPath}' contains {scenes.Length} save scenes. "
                        + "Pass each save slot or scene explicitly (repeat --save) or pass --pool to "
                        + $"analyze them as one pooled input. Scenes: {string.Join(", ", scenes)}.");
                files.AddRange(scenes);
            }
            else if (File.Exists(fullPath))
            {
                files.Add(fullPath);
            }
            else
            {
                throw new FileNotFoundException("Save path not found.", fullPath);
            }
        }

        return [.. files.Distinct(StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal)
            .Select(ReadSnapshot)];
    }

    private static SaveSnapshot ReadSnapshot(string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Save '{path}' is not valid JSON (line {exception.LineNumber + 1}, "
                + $"byte {exception.BytePositionInLine + 1}): {exception.Message}", exception);
        }

        using (document)
        {
            var entities = new List<EntityRecord>();
            var controllerNumbers = new Dictionary<string, double>(StringComparer.Ordinal);
            var controllerStrings = new Dictionary<string, string>(StringComparer.Ordinal);
            var controllerBooleans = new Dictionary<string, bool>(StringComparer.Ordinal);
            var controllerSeen = false;
            var malformedEntities = 0;
            var unknownKindEntities = 0;
            foreach (var entity in document.RootElement.EnumerateArray())
            {
                if (!TryReadEntity(entity, out var record))
                {
                    malformedEntities++;
                    continue;
                }

                if (record.Kind == "controller")
                {
                    controllerSeen = true;
                    foreach (var (key, value) in record.Values)
                    {
                        if (value.Number is { } number)
                            controllerNumbers[key] = number;
                        else if (value.String is not null)
                            controllerStrings[key] = value.String;
                        else if (value.Boolean is { } boolean)
                            controllerBooleans[key] = boolean;
                    }

                    continue;
                }

                if (!IsKnownEntityKind(record.Kind))
                {
                    unknownKindEntities++;
                    continue;
                }

                entities.Add(record);
            }

            if (!controllerSeen)
                throw new InvalidDataException(
                    $"Save '{path}' has no controller entity; the analysis tool requires a current v7 "
                    + "Origo Evolution Demo save.");
            if (!controllerNumbers.TryGetValue("evo.genomeVersion", out var version))
                throw new InvalidDataException(
                    $"Save '{path}' has no evo.genomeVersion controller value; the analysis tool requires "
                    + $"a current v{SupportedGenomeVersion:0} Evolution Demo save.");
            if (System.Math.Abs(version - SupportedGenomeVersion) > 1e-9)
                throw new InvalidDataException(
                    $"Save '{path}' is not a v{SupportedGenomeVersion:0} save "
                    + $"(evo.genomeVersion='{version}'). The analysis tool supports only the current save schema.");
            if (!controllerNumbers.TryGetValue("evo.time", out _) || !controllerNumbers.TryGetValue("evo.tick", out _))
                throw new InvalidDataException(
                    $"Save '{path}' has no evo.time/evo.tick controller value; time-series analysis "
                    + "requires a current v7 Evolution Demo save.");

            var skipped = new List<CreatureSkip>();
            var creatures = new List<CreatureRecord>();
            var rawCreatureCount = 0;
            foreach (var entity in entities.Where(static entity => entity.Kind == "creature"))
            {
                rawCreatureCount++;
                if (CreatureRecord.TryCreate(entity, out var creature, out var reason))
                    creatures.Add(creature);
                else
                    skipped.Add(new CreatureSkip(entity.Name, reason));
            }

            var warnings = BuildWarnings(path, malformedEntities, unknownKindEntities, skipped.Count);
            return new SaveSnapshot(
                ResolveId(path),
                path,
                controllerNumbers["evo.time"],
                controllerNumbers["evo.tick"],
                creatures,
                rawCreatureCount,
                skipped,
                entities.Count(static entity => entity.Kind == "food"),
                entities.Count(static entity => entity.Kind == "corpse"),
                entities.Count(static entity => entity.Kind == "food_source"),
                malformedEntities,
                unknownKindEntities,
                controllerNumbers,
                controllerStrings,
                controllerBooleans,
                warnings);
        }
    }

    private static List<string> BuildWarnings(
        string path,
        int malformedEntities,
        int unknownKindEntities,
        int skippedCreatures)
    {
        var warnings = new List<string>();
        if (malformedEntities > 0)
            warnings.Add(
                $"Save '{path}' contains {malformedEntities} malformed top-level entit(y/ies) "
                + "without name/data/pairs; they were ignored.");
        if (unknownKindEntities > 0)
            warnings.Add(
                $"Save '{path}' contains {unknownKindEntities} entit(y/ies) with an unknown evo.kind; "
                + "they were ignored.");
        if (skippedCreatures > 0)
            warnings.Add(
                $"Save '{path}' contains {skippedCreatures} creature(s) without a usable evo.bodyGenome; "
                + "they are reported as skipped and fail the run unless --allow-skips is passed.");
        var saveRoot = ResolveSaveRoot(path);
        if (saveRoot is not null)
        {
            if (File.Exists(Path.Combine(saveRoot, ".write_in_progress")))
                warnings.Add(
                    $"Save root '{saveRoot}' contains a .write_in_progress marker; it may be a live or "
                    + "half-written save rather than a snapshot.");
            if (!File.Exists(Path.Combine(saveRoot, ".payload.sha")))
                warnings.Add(
                    $"Save root '{saveRoot}' has no .payload.sha integrity file; the payload cannot be "
                    + "cross-checked against the save writer's hash.");
        }

        return warnings;
    }

    private static string? ResolveSaveRoot(string scenePath)
    {
        var levelDirectory = Path.GetDirectoryName(scenePath);
        if (levelDirectory is null)
            return null;
        var parent = Path.GetDirectoryName(levelDirectory);
        if (parent is null)
            return null;
        return Path.GetFileName(levelDirectory).StartsWith("level_", StringComparison.Ordinal)
            ? parent
            : levelDirectory;
    }

    private static string ResolveId(string scenePath)
    {
        var levelDirectory = Path.GetDirectoryName(scenePath) ?? scenePath;
        if (Path.GetFileName(levelDirectory).StartsWith("level_", StringComparison.Ordinal))
        {
            var parent = Path.GetDirectoryName(levelDirectory);
            if (parent is not null)
                return Path.GetFileName(parent);
        }

        return Path.GetFileName(levelDirectory);
    }

    private static bool IsKnownEntityKind(string kind) =>
        kind is "creature" or "food" or "corpse" or "food_source";

    private static bool TryReadEntity(JsonElement entity, out EntityRecord record)
    {
        record = null!;
        if (!entity.TryGetProperty("name", out var nameElement)
            || !entity.TryGetProperty("data", out var data)
            || !data.TryGetProperty("pairs", out var pairs))
            return false;
        var name = nameElement.GetString() ?? string.Empty;
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        foreach (var pair in pairs.EnumerateObject())
            values[pair.Name] = ReadValue(pair.Value);
        var kind = values.TryGetValue("evo.kind", out var kindValue) ? kindValue.String : null;
        if (string.IsNullOrWhiteSpace(kind))
            return false;
        record = new EntityRecord(name, kind, values);
        return true;
    }

    private static DataValue ReadValue(JsonElement wrapper)
    {
        var data = wrapper.TryGetProperty("data", out var value) ? value : default;
        return data.ValueKind switch
        {
            JsonValueKind.Number => new DataValue(data.GetDouble(), null, null),
            JsonValueKind.String => new DataValue(null, data.GetString(), null),
            JsonValueKind.True => new DataValue(null, null, true),
            JsonValueKind.False => new DataValue(null, null, false),
            _ => new DataValue(null, null, null),
        };
    }

    internal sealed record DataValue(double? Number, string? String, bool? Boolean);
}

/// <summary>A creature the reader could not turn into a record.</summary>
/// <param name="Name">Entity name reported by the save.</param>
/// <param name="Reason">Why the creature cannot be analyzed.</param>
public sealed record CreatureSkip(string Name, string Reason);

/// <summary>One save snapshot with simulation metadata and creature rows.</summary>
/// <param name="Id">Short save identifier (save directory name, or level directory name).</param>
/// <param name="Path">Canonical scene file path.</param>
/// <param name="Time">Simulation time in seconds.</param>
/// <param name="Tick">Simulation tick.</param>
/// <param name="Creatures">Creatures that carry a usable genome.</param>
/// <param name="RawCreatureCount">Creature entities in the save, including skipped ones.</param>
/// <param name="SkippedCreatures">Creatures without a usable genome.</param>
/// <param name="Food">Food entities.</param>
/// <param name="Corpses">Corpse entities.</param>
/// <param name="FoodSources">Food-source entities.</param>
/// <param name="MalformedEntityCount">Top-level entities without name/data/pairs.</param>
/// <param name="UnknownKindEntityCount">Entities whose evo.kind is not a known save kind.</param>
/// <param name="ControllerNumbers">Numeric controller values.</param>
/// <param name="ControllerStrings">String controller values.</param>
/// <param name="ControllerBooleans">Boolean controller values.</param>
/// <param name="Warnings">Reader warnings (integrity markers, ignored entities, skipped creatures).</param>
public sealed record SaveSnapshot(
    string Id,
    string Path,
    double Time,
    double Tick,
    IReadOnlyList<CreatureRecord> Creatures,
    int RawCreatureCount,
    IReadOnlyList<CreatureSkip> SkippedCreatures,
    int Food,
    int Corpses,
    int FoodSources,
    int MalformedEntityCount,
    int UnknownKindEntityCount,
    IReadOnlyDictionary<string, double> ControllerNumbers,
    IReadOnlyDictionary<string, string> ControllerStrings,
    IReadOnlyDictionary<string, bool> ControllerBooleans,
    IReadOnlyList<string> Warnings);

/// <summary>Flattened creature record from a v7 save.</summary>
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
    int SoftMeals,
    int ToughMeals,
    int ToxicMeals,
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
    internal static bool TryCreate(EntityRecord entity, out CreatureRecord creature, out string reason)
    {
        creature = null!;
        var v = entity.Values;
        var genome = v.TryGetValue("evo.bodyGenome", out var g) ? g.String : null;
        if (string.IsNullOrWhiteSpace(genome))
        {
            reason = "missing or blank evo.bodyGenome";
            return false;
        }

        double Number(string key, double fallback = 0) =>
            v.TryGetValue(key, out var value) && value.Number is { } number ? number : fallback;
        int Integer(string key, int fallback = 0) =>
            v.TryGetValue(key, out var value) && value.Number is { } number ? (int)number : fallback;
        reason = string.Empty;
        creature = new CreatureRecord(
            entity.Name, genome,
            Number("evo.x"), Number("evo.y"), Number("evo.prevX"), Number("evo.prevY"),
            Number("evo.age"), Number("evo.energy"), Number("evo.maxEnergy", 1),
            Number("evo.drive"), Number("evo.turn"), Number("evo.spikeDrive"),
            Number("evo.aim"), Number("evo.sprintDrive"),
            Integer("evo.lineage"), Integer("evo.generation"), Integer("evo.children"),
            Integer("evo.kills"), Integer("evo.creatureCorpseMeals"),
            Integer("evo.creatureSoftMeals"), Integer("evo.creatureToughMeals"),
            Integer("evo.creatureToxicMeals"),
            Integer("evo.organMask"), Integer("evo.spikeCount", 1),
            Number("evo.moveSpeed"), Number("evo.turnSpeed"), Number("evo.senseRange"),
            Number("evo.spikeRadius"), Number("evo.basalMetabolism"), Number("evo.maxAge"),
            Number("evo.bodyRadius"));
        return true;
    }
}

internal sealed record EntityRecord(
    string Name,
    string Kind,
    IReadOnlyDictionary<string, SaveSnapshotReader.DataValue> Values);
