using System;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Snd;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     Single entry point for <see cref="GodotSndManager" /> setup: runtime dependencies must bind before context.
/// </summary>
public static class GodotSndBootstrap
{
    public static void BindRuntimeAndContext(
        GodotSndManager manager,
        SndWorld world,
        ILogger logger,
        ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);

        manager.BindRuntimeDependencies(world, logger);
        manager.BindContext(context);
    }
}
