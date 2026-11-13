using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>
///     Process-independent meta progression. Unlike pond saves, this profile
///     deliberately survives New Game and application restarts, which is the
///     expected scope for idle unlocks.
/// </summary>
internal static class GameplayProfileStore
{
    private const string FileName = "gameplay_profile.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private static string Path => System.IO.Path.Combine(OS.GetUserDataDir(), FileName);

    public static bool TryLoad(GameplayRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (!File.Exists(Path))
            return false;
        var profile = JsonSerializer.Deserialize<ProfileDto>(File.ReadAllText(Path), Options);
        if (profile is null || profile.SchemaVersion != 1)
            return false;
        runtime.Progression.Points = profile.Points;
        runtime.Progression.LifetimeSeconds = profile.LifetimeSeconds;
        runtime.Progression.UnlockedIds.Clear();
        foreach (var id in profile.UnlockedIds)
            runtime.Progression.UnlockedIds.Add(id);
        runtime.Progression.CompletedMilestones.Clear();
        foreach (var id in profile.CompletedMilestones)
            runtime.Progression.CompletedMilestones.Add(id);
        return true;
    }

    public static void Save(GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        var profile = new ProfileDto(
            1,
            progression.Points,
            progression.LifetimeSeconds,
            progression.UnlockedIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            progression.CompletedMilestones.OrderBy(static id => id, StringComparer.Ordinal).ToArray());
        File.WriteAllText(Path, JsonSerializer.Serialize(profile, Options));
    }

    private sealed record ProfileDto(
        int SchemaVersion,
        long Points,
        double LifetimeSeconds,
        string[] UnlockedIds,
        string[] CompletedMilestones);
}
