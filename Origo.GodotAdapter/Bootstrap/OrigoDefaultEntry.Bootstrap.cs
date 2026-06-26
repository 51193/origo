using System;
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

        _sndContext = new SndContext(new SndContextParameters(
            Runtime,
            SharedDataSourceIo,
            SharedMetaAccess,
            SharedPathResolver,
            SaveRootPath,
            InitialSaveRootPath,
            ConfigPath)
        {
            AutoDiscoverStrategies = AutoDiscoverStrategies,
            DiscoverySkipPrefixes = AutoDiscoverStrategies ? GodotSkipPrefixes : null,
            SceneAliasMapPath = SceneAliasMapPath,
            SndTemplateMapPath = SndTemplateMapPath,
            ConfigureConverters = RegisterCustomConverters,
        });

        SndManager.BindContext(_sndContext);
        ConfigureSaveMetadataContributors(_sndContext);

        // 委托 Core 执行完整启动流程：策略发现 → 别名/模板加载 → 入口存档
        _sndContext.Bootstrap();
    }

    private void RegisterConsoleCommandHandlers()
    {
        if (Runtime.Console is null)
            throw new InvalidOperationException(
                "Runtime.Console is not available. Ensure OrigoRuntime is fully initialized before calling Bootstrap.");

        Runtime.Console.RegisterHandler(new PressButtonCommandHandler(Runtime));
        Runtime.Console.RegisterHandler(new TreeDebugCommandHandler(Runtime));
    }
}
