using System;
using System.Linq;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Bootstrap;

namespace ShellPackageConsumer;

/// <summary>
///     Package-consumption demo entry point. It uses only the stable shell
///     surface: Origo.Core and Origo.GodotAdapter, a consumer-authored level
///     scene and lifecycle strategy, and the public save/load APIs.
/// </summary>
public partial class ConsumerEntry : OrigoDefaultEntry
{
    private const string PlayerName = "demo_player";
    private const string SaveId = "demo_slot";

    /// <inheritdoc/>
    public override void _Ready()
    {
        ConfigPath = "res://Origo/entry/entry.json";
        SceneAliasMapPath = "res://Origo/maps/scene_aliases.map";
        SndTemplateMapPath = "res://Origo/maps/snd_templates.map";
        InitialSaveRootPath = "res://Origo/initial";
        SaveRootPath = "user://consumer_smoke_saves";
        base._Ready();

        try
        {
            // The generated TypedData accessor must arrive through the
            // Contracts package restore, not through a project reference.
            _ = default(TypedData).TryGetString(out _);

            PumpUntil(() => Runtime.SessionManager.ForegroundSession is not null
                && Context.Lifecycle.IsBootstrapCompleted
                && Context.Deferred.IsPersistenceIdle,
                "the main-menu entry did not complete bootstrap");
            if (!Runtime.SndWorld.IsStrategyRegistered("demo.tick"))
                throw new InvalidOperationException("The consumer strategy was not discovered and registered.");

            var foreground = Runtime.SessionManager.ForegroundSession!;
            if (!string.Equals(foreground.LevelId, "demo_title", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"The configured main-menu level ID was not used (found '{foreground.LevelId}').");
            var initial = RequirePlayer(foreground);
            if (!initial.GetNodeNames().Contains("main", StringComparer.Ordinal))
                throw new InvalidOperationException("The authored Godot node was not instantiated.");
            if (!initial.TryGetData<string>("node_name").found)
                throw new InvalidOperationException(
                    "The strategy-facing node lookup did not record the authored Godot node.");
            var (hpFound, hp) = initial.TryGetData<int>("hp");
            if (!hpFound || hp != 42)
                throw new InvalidOperationException(
                    $"The authored level entity did not restore hp=42 (found={hpFound}, value={hp}).");

            // Save the authored state, mutate it, then load the snapshot back.
            Context.Save.RequestSaveGame(SaveId);
            if (Context.Deferred.IsPersistenceIdle)
                throw new InvalidOperationException("The save request completed before the frame boundary.");
            PumpUntil(() => Context.Deferred.IsPersistenceIdle,
                "the save request did not become idle");
            if (!Context.Save.ListSaves().Contains(SaveId, StringComparer.Ordinal))
                throw new InvalidOperationException($"Save '{SaveId}' was not listed after saving.");

            initial.SetData("hp", 1);
            Context.Save.RequestLoadGame(SaveId);
            PumpUntil(() => Context.Deferred.IsPersistenceIdle,
                "the load request did not become idle");

            var restored = RequirePlayer(Runtime.SessionManager.ForegroundSession!);
            var (restoredFound, restoredHp) = restored.TryGetData<int>("hp");
            if (!restoredFound || restoredHp != 42)
                throw new InvalidOperationException(
                    $"The load did not restore hp=42 (found={restoredFound}, value={restoredHp}).");
            if (!restored.TryGetData<string>("spawned").found
                || !restored.TryGetData<string>("saved_by_hook").found
                || !restored.TryGetData<string>("loaded_by_hook").found
                || !restored.TryGetData<string>("node_name_after_load").found)
                throw new InvalidOperationException(
                    "The demo strategy hooks or strategy-facing node lookup did not persist/restore.");
            if (!restored.GetNodeNames().Contains("main", StringComparer.Ordinal))
                throw new InvalidOperationException("The restored entity lost its authored Godot node.");

            var saveCount = Context.Save.ListSaves().Count;
            var kernelLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                string.Equals(assembly.GetName().Name, "Origo.Core.Kernel", StringComparison.Ordinal));
            Context.Shutdown();
            GD.Print($"DEMO_CONSUMER_OK player={PlayerName} hp={restoredHp} saves={saveCount}");
            GD.Print($"SHELL_CONSUMER_STARTUP_OK kernel={kernelLoaded} foreground=True");
            GetTree().Quit(kernelLoaded ? 0 : 3);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SHELL_CONSUMER_STARTUP_FAILED {ex.GetType().Name}: {ex.Message}");
            GetTree().Quit(4);
        }
    }

    private static ISndEntity RequirePlayer(Origo.Core.Abstractions.Lifecycle.ISessionRun session)
    {
        return session.FindByName(PlayerName)
            ?? throw new InvalidOperationException($"Entity '{PlayerName}' is missing from the foreground session.");
    }

    private void PumpUntil(Func<bool> condition, string failureMessage, int maxFrames = 600)
    {
        for (var frame = 0; frame < maxFrames; frame++)
        {
            Runtime.DriveFrame(0.016);
            if (condition())
                return;
        }
        throw new InvalidOperationException(failureMessage);
    }
}
