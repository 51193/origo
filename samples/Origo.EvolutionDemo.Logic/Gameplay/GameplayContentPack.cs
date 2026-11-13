using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Serializable content pack. Packs are append-only and versioned.</summary>
public sealed record GameplayContentPack(
    int SchemaVersion,
    IReadOnlyList<GameplayContentEntry> Content,
    IReadOnlyList<UnlockDefinition> Unlocks,
    IReadOnlyList<MechanicVariant> Variants,
    IReadOnlyList<MilestoneDefinition> Milestones);

/// <summary>Loads/validates JSON content packs and builds immutable snapshots.</summary>
public static class GameplayContentPackLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static GameplayContentPack Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var pack = JsonSerializer.Deserialize<GameplayContentPack>(json, Options)
            ?? throw new InvalidDataException("Content pack JSON produced null.");
        if (pack.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported content pack schema {pack.SchemaVersion}.");
        Validate(pack);
        return pack;
    }

    public static GameplayContentSnapshot LoadSnapshot(IEnumerable<string> packJson)
    {
        ArgumentNullException.ThrowIfNull(packJson);
        var registry = new GameplayContentRegistry();
        foreach (var json in packJson)
        {
            var pack = Parse(json);
            foreach (var entry in pack.Content)
                registry.AddContent(entry);
            foreach (var unlock in pack.Unlocks)
                registry.AddUnlock(unlock);
            foreach (var variant in pack.Variants)
                registry.AddVariant(variant);
            foreach (var milestone in pack.Milestones)
                registry.AddMilestone(milestone);
        }

        return registry.BuildSnapshot();
    }

    private static void Validate(GameplayContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack.Content);
        ArgumentNullException.ThrowIfNull(pack.Unlocks);
        ArgumentNullException.ThrowIfNull(pack.Variants);
        ArgumentNullException.ThrowIfNull(pack.Milestones);
    }
}
