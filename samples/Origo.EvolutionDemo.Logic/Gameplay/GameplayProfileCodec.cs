using System;
using System.Linq;
using System.Text.Json;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Serializable account-wide progression profile.</summary>
public sealed record GameplayProfile(
    int SchemaVersion,
    long Points,
    double LifetimeSeconds,
    string[] UnlockedIds,
    string[] CompletedMilestones);

/// <summary>Validated JSON codec for the meta-progression profile.</summary>
public static class GameplayProfileCodec
{
    public const int CurrentSchemaVersion = 1;
    public const string CoreUnlockId = "unlock.core";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static string Serialize(GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        return JsonSerializer.Serialize(new GameplayProfile(
            CurrentSchemaVersion,
            progression.Points,
            progression.LifetimeSeconds,
            [.. progression.UnlockedIds.OrderBy(static id => id, StringComparer.Ordinal)],
            [.. progression.CompletedMilestones.OrderBy(static id => id, StringComparer.Ordinal)]), Options);
    }

    public static bool TryDeserialize(string json, out GameplayProfile? profile)
    {
        profile = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<GameplayProfile>(json, Options);
            if (parsed is null
                || parsed.SchemaVersion != CurrentSchemaVersion
                || parsed.UnlockedIds is null
                || parsed.CompletedMilestones is null)
                return false;
            profile = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static void Apply(GameplayProfile profile, GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(progression);
        progression.Points = profile.Points;
        progression.LifetimeSeconds = profile.LifetimeSeconds;
        progression.UnlockedIds.Clear();
        progression.UnlockedIds.Add(CoreUnlockId);
        foreach (var id in profile.UnlockedIds)
            progression.UnlockedIds.Add(id);
        progression.CompletedMilestones.Clear();
        foreach (var id in profile.CompletedMilestones)
            progression.CompletedMilestones.Add(id);
    }
}
