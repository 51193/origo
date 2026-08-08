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
    ///     If the hook throws, the entity is rolled back (removed from the host,
    ///     observer bindings torn down, strategies and nodes released) before the
    ///     exception propagates.
    /// </summary>
    public static ISndEntity Spawn(ISndSceneHost host, SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(meta);
        var entity = host.CreateEntity(meta);
        if (entity is IEntityLifecycle lifecycle)
        {
            try
            {
                lifecycle.FireAfterSpawnHooks();
            }
            catch
            {
                Rollback(host, entity, lifecycle);
                throw;
            }
        }

        return entity;
    }

    /// <summary>
    ///     Stages all entities on the host first, then fires AfterSpawn hooks in a
    ///     second pass so hooks can assume a fully-constructed scene graph. If the
    ///     staging itself fails (a host rejects a meta), every entity staged so far
    ///     is rolled back — none of them fired AfterSpawn, so none may remain as
    ///     half-staged entities that are registered on the host but never spawned.
    ///     If a hook throws, every entity that was created but did not complete its
    ///     AfterSpawn hook (the failing one and all not-yet-fired ones) is rolled
    ///     back; entities whose hooks already fired are left fully spawned.
    /// </summary>
    public static void SpawnMany(ISndSceneHost host, params SndMetaData[] metaList)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(metaList);
        var staged = new List<ISndEntity>();
        try
        {
            foreach (var meta in metaList)
                staged.Add(host.CreateEntity(meta));
        }
        catch
        {
            // The failing entity is rolled back by the host itself; the
            // already-staged ones never fired AfterSpawn and must not stay
            // registered on the host.
            foreach (var entity in staged)
                if (entity is IEntityLifecycle rollback)
                    Rollback(host, entity, rollback);
            throw;
        }

        for (var i = 0; i < staged.Count; i++)
        {
            if (staged[i] is not IEntityLifecycle lifecycle)
                continue;
            try
            {
                lifecycle.FireAfterSpawnHooks();
            }
            catch
            {
                for (var j = i; j < staged.Count; j++)
                    if (staged[j] is IEntityLifecycle rollback)
                        Rollback(host, staged[j], rollback);
                throw;
            }
        }
    }

    private static void Rollback(ISndSceneHost host, ISndEntity entity, IEntityLifecycle lifecycle)
    {
        // Teardown before removal: adapter wrappers (e.g. GodotSndEntity)
        // detach their backing entity when removed from the host, after which
        // lifecycle delegation would throw. Matches the KillPending / disposal
        // teardown order (hooks/bindings/strategies first, physical removal last).
        lifecycle.TeardownObserverBindings();
        lifecycle.ReleaseStrategiesOnly();
        lifecycle.TeardownOnly();
        host.RemoveEntity(entity.Name);
    }
}
