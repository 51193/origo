using System;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
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
    ///     Godot lifecycle entry: creates the <see cref="SndContext" />, binds it to the
    ///     manager, then delegates the full startup flow to <see cref="SndContext.Bootstrap" />.
    /// </summary>
    public override void _Ready()
    {
        base._Ready();

        RegisterConsoleCommandHandlers();

        var sndContext = new SndContext(new SndContextParameters(
            Runtime,
            SharedDataSourceIo,
            SharedMetaAccess,
            SharedPathResolver,
            SaveRootPath,
            InitialSaveRootPath,
            ConfigPath)
        {
            AutoDiscoverStrategies = AutoDiscoverStrategies,
            DiscoverySkipPrefixes = AutoDiscoverStrategies ? _godotSkipPrefixes : null,
            SceneAliasMapPath = SceneAliasMapPath,
            SndTemplateMapPath = SndTemplateMapPath,
            ConfigureConverters = RegisterCustomConverters,
        });

        ((ISndContextAttachableSceneHost)SndManager).BindContext(sndContext);
        ConfigureSaveMetadataContributors(sndContext);

        // Delegate to Core to execute the complete startup flow: strategy discovery → alias/template loading → entry save
        sndContext.Bootstrap();
    }

    private void RegisterConsoleCommandHandlers()
    {
        if (Runtime.Console is null)
            throw new InvalidOperationException(
                "Runtime.Console is not available. Ensure OrigoRuntime is fully initialized before calling Bootstrap.");

        Runtime.Console.RegisterHandler(new PressButtonCommandHandler(Runtime));
        Runtime.Console.RegisterHandler(new TreeDebugCommandHandler(Runtime));
        Runtime.Console.RegisterHandler(new CameraViewCommandHandler(Runtime));
    }
}
