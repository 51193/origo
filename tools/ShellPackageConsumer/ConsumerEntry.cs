using System;
using System.Linq;
using Godot;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Bootstrap;

namespace ShellPackageConsumer;

/// <summary>
///     Package-consumption entry point. Uses only the stable shell surface:
///     the two package references are Origo.Core and Origo.GodotAdapter, and
///     the code never references Origo.Core.Kernel or a test helper.
/// </summary>
public partial class ConsumerEntry : OrigoDefaultEntry
{
    /// <inheritdoc/>
    public override void _Ready()
    {
        AutoDiscoverStrategies = false;
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

            for (var frame = 0; frame < 3; frame++)
                Runtime.DriveFrame(0.016);

            var kernelLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                string.Equals(assembly.GetName().Name, "Origo.Core.Kernel", StringComparison.Ordinal));
            var foreground = Runtime.SessionManager.ForegroundSession is not null;
            _ = Context.Save.ListSaves();

            GD.Print($"SHELL_CONSUMER_STARTUP_OK kernel={kernelLoaded} foreground={foreground}");
            GetTree().Quit(kernelLoaded && foreground ? 0 : 3);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SHELL_CONSUMER_STARTUP_FAILED {ex.GetType().Name}: {ex.Message}");
            GetTree().Quit(4);
        }
    }
}
