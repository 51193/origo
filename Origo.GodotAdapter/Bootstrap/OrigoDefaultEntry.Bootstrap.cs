using System;
using Origo.Core.Kernel.Ports;
using Origo.Core.Snd;
using Origo.GodotAdapter.Console;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     Bootstrap partial for <see cref="OrigoDefaultEntry" />:
///     registers Godot-specific console command handlers (tree_debug,
///     press_button, camera_view) after the runtime and SndContext
///     are initialized.
/// </summary>
public partial class OrigoDefaultEntry
{
    /// <summary>
    ///     Godot lifecycle entry: creates the <see cref="ISndContext" /> through the adapter
    ///     kernel port, binds it to the manager, then delegates the full startup flow to
    ///     <see cref="ISndContext.Bootstrap" />.
    /// </summary>
    public override void _Ready()
    {
        try
        {
            base._Ready();

            ConfigureStrategies(Runtime.SndWorld);

            RegisterConsoleCommandHandlers();

            var context = CreateSndContext(new AdapterContextOptions(
                SaveRootPath,
                InitialSaveRootPath,
                ConfigPath,
                AutoDiscoverStrategies,
                AutoDiscoverStrategies ? _godotSkipPrefixes : null,
                SceneAliasMapPath,
                SndTemplateMapPath,
                RegisterCustomConverters));

            Context = context;
            ConfigureSaveMetadataContributors(context);

            // Delegate to Core to execute the complete startup flow: strategy discovery → alias/template loading → entry save
            context.Bootstrap();
        }
        catch
        {
            MarkBootstrapFailed();
            throw;
        }
    }

    private void RegisterConsoleCommandHandlers()
    {
        RegisterConsoleCommandHandler(new PressButtonCommandHandler(Runtime));
        RegisterConsoleCommandHandler(new TreeDebugCommandHandler(Runtime));
        RegisterConsoleCommandHandler(new CameraViewCommandHandler(Runtime));
    }
}
