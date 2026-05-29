using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.GodotAdapter.Console;

namespace Origo.GodotAdapter.Bootstrap;

public partial class OrigoDefaultEntry
{
    public override void _Ready()
    {
        base._Ready();

        RegisterConsoleCommandHandlers();

        if (AutoDiscoverStrategies)
            OrigoAutoInitializer.DiscoverAndRegisterStrategies(
                Runtime.SndWorld, Runtime.Logger, GodotSkipPrefixes);

        _sndContext = new SndContext(new SndContextParameters(
            Runtime,
            SharedFileSystem,
            SaveRootPath,
            InitialSaveRootPath,
            ConfigPath));

        // Runtime dependencies are already bound in OrigoAutoHost.CreateRuntime(); only bind lifecycle context here.
        SndManager.BindContext(_sndContext);

        ConfigureSaveMetadataContributors(_sndContext);

        Runtime.SndWorld.LoadSceneAliases(SharedFileSystem, SceneAliasMapPath, Runtime.Logger);
        Runtime.SndWorld.LoadTemplates(SharedFileSystem, SndTemplateMapPath, Runtime.Logger);

        _sndContext.RequestLoadMainMenuEntrySave();
        _sndContext.FlushDeferredActionsForCurrentFrame();
    }

    private void RegisterConsoleCommandHandlers() =>
        Runtime.Console!.RegisterHandler(new PressButtonCommandHandler(Runtime));
}
