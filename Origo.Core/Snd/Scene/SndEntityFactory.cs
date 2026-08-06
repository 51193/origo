using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     Static factory that wraps <see cref="ISndSceneHost.CreateEntity" />
///     with automatic AfterSpawn hook dispatch. Single-entity spawn fires
///     hooks immediately; batch spawn stages all entities first, then fires
///     hooks in a second pass so that hooks can assume a fully-constructed
///     scene graph.
/// </summary>
public static class SndEntityFactory
{
    /// <summary>
    ///     Creates an entity on the host and fires its AfterSpawn hook immediately.
    /// </summary>
    public static ISndEntity Spawn(ISndSceneHost host, SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(meta);
        var entity = host.CreateEntity(meta);
        if (entity is IEntityLifecycle lifecycle)
            lifecycle.FireAfterSpawnHooks();
        return entity;
    }

    /// <summary>
    ///     Stages all entities on the host first, then fires AfterSpawn hooks in a
    ///     second pass so hooks can assume a fully-constructed scene graph.
    /// </summary>
    public static void SpawnMany(ISndSceneHost host, params SndMetaData[] metaList)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(metaList);
        var staged = new List<ISndEntity>();
        foreach (var meta in metaList)
            staged.Add(host.CreateEntity(meta));
        foreach (var entity in staged)
            if (entity is IEntityLifecycle lifecycle)
                lifecycle.FireAfterSpawnHooks();
    }
}
