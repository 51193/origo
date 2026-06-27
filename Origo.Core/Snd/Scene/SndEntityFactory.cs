using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

public static class SndEntityFactory
{
    public static ISndEntity Spawn(ISndSceneHost host, SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(meta);
        var entity = host.CreateEntity(meta);
        if (entity is IEntityLifecycle lifecycle)
            lifecycle.FireAfterSpawnHooks();
        return entity;
    }

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
