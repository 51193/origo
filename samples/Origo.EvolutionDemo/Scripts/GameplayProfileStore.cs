using System;
using System.IO;
using System.Linq;
using Godot;
using Origo.EvolutionDemo.Logic.Gameplay;
using System.Globalization;

namespace Origo.EvolutionDemo;

/// <summary>
///     Process-independent meta progression. Unlike pond saves, this profile
///     deliberately survives New Game and application restarts, which is the
///     expected scope for idle unlocks.
/// </summary>
internal static class GameplayProfileStore
{
    private const string FileName = "gameplay_profile.json";

    private static string Path => System.IO.Path.Combine(OS.GetUserDataDir(), FileName);

    public static bool TryLoad(GameplayRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (!File.Exists(Path))
            return false;
        string json;
        try
        {
            json = File.ReadAllText(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning("Gameplay profile could not be read; using placeholder defaults.");
            return false;
        }

        if (!GameplayProfileCodec.TryDeserialize(json, out var profile))
        {
            QuarantineCorrupt();
            return false;
        }

        GameplayProfileCodec.Apply(profile!, runtime.Progression);
        return true;
    }

    public static void Save(GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        var temp = Path + ".tmp";
        var json = GameplayProfileCodec.Serialize(progression);
        try
        {
            File.WriteAllText(temp, json);
            File.Move(temp, Path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Gameplay profile save failed: {ex.Message}");
        }
    }

    private static void QuarantineCorrupt()
    {
        var quarantine = Path + "." +
            DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) +
            ".corrupt";
        try
        {
            File.Move(Path, quarantine, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A quarantined profile is a warning, not a second launch blocker.
        }
    }
}
