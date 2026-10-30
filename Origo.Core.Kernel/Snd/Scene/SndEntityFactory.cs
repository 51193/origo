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
internal static class SndEntityFactory
{
    /// <summary>
    ///     Bounded number of incoming-binding cleanup attempts during rollback.
    ///     A user <c>OnUnmounted</c> hook can mount a fresh binding onto the
    ///     dying target while the previous one is being removed; retrying a
    ///     bounded number of times keeps the rollback finite. If a pathological
    ///     hook keeps remounting after the last attempt, cleanup gives up and
    ///     the surviving observer keeps a live binding to the removed entity
    ///     (recorded in its persisted observer metadata; strict save recovery
    ///     rejects it on load). Entity teardown afterwards only removes outgoing
    ///     bindings, so it cannot repair that last edge.
    /// </summary>
    private const int _maxIncomingBindingCleanupAttempts = 3;

    /// <summary>
    ///     Creates an entity on the host and fires its AfterSpawn hook immediately.
    ///     If the hook throws, the entity is rolled back (removed from the host,
    ///     observer bindings torn down, strategies and nodes released) before the
    ///     exception propagates. Incoming observer-binding cleanup is bounded
    ///     best-effort: a hook that remounts onto the dying entity forever can
    ///     leave one persisted survivor-to-dying edge behind, which strict save
    ///     recovery rejects on the next load (see <c>RemoveIncomingObserverBindings</c>).
    /// </summary>
    public static ISndEntity Spawn(ISndSceneHost host, SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(meta);
        SndEntityNamePolicy.EnsureAvailable(host, meta);
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
        SndEntityNamePolicy.EnsureUniqueBatch(host, metaList);
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
        // SpawnMany stages every entity before firing AfterSpawn hooks, so a
        // hook on an already-fired sibling may have mounted an observer
        // binding onto this entity. Remove those incoming bindings first,
        // while this entity's data and strategy resources are still alive:
        // the surviving observer must not keep (and persist) a binding that
        // references an entity the scene no longer contains — the strict read
        // rejects such a dangling observer binding on the next load.
        //
        // The incoming cleanup runs twice: once up front, and once after the
        // outgoing teardown below, because an outgoing binding's OnUnmounted
        // hook can itself mount a fresh survivor -> dying binding. Discovery
        // and cleanup are best-effort: an adapter scene host whose observer
        // topology is unavailable (unbound) throws from the topology accessor,
        // and that must neither mask the original AfterSpawn failure nor skip
        // the teardown steps below.
        TryRemoveIncomingObserverBindings(host, entity);

        // Teardown before removal: adapter wrappers (e.g. GodotSndEntity)
        // detach their backing entity when removed from the host, after which
        // lifecycle delegation would throw. Matches the KillPending / disposal
        // teardown order (hooks/bindings/strategies first, physical removal last).
        //
        // Each step is independently protected so a throwing step (e.g. an
        // OnUnmounted hook inside TeardownObserverBindings) cannot mask the
        // original AfterSpawn failure nor skip the remaining teardown: the
        // remaining steps still run. The rollback steps run without a logger
        // to report to, so their failures are intentionally not surfaced —
        // the original exception propagated by the caller carries the failure
        // context, and a failing teardown step (including a failing physical
        // removal) is best-effort rather than guaranteed.
        try
        {
            lifecycle.TeardownObserverBindings();
        }
        catch (Exception)
        {
            // Best-effort teardown; continue with the remaining steps.
        }

        // Outgoing teardown fires OnUnmounted hooks, and one of them may have
        // mounted a new survivor -> dying binding. Clean incoming bindings
        // again before the target's data/strategy resources are released.
        TryRemoveIncomingObserverBindings(host, entity);

        try
        {
            lifecycle.ReleaseStrategiesOnly();
        }
        catch (Exception)
        {
            // Best-effort teardown; continue with the remaining steps.
        }

        try
        {
            lifecycle.TeardownOnly();
        }
        catch (Exception)
        {
            // Best-effort teardown; continue with the remaining steps.
        }

        try
        {
            host.RemoveEntity(entity.Name);
        }
        catch (Exception)
        {
            // Best-effort teardown; the caller's original exception wins.
        }
    }

    /// <summary>
    ///     Best-effort wrapper around <c>RemoveIncomingObserverBindings</c>:
    ///     discovery or cleanup failures must neither mask the original AfterSpawn
    ///     failure nor skip the remaining rollback steps.
    /// </summary>
    private static void TryRemoveIncomingObserverBindings(ISndSceneHost host, ISndEntity entity)
    {
        try
        {
            RemoveIncomingObserverBindings(host, entity);
        }
        catch (Exception)
        {
            // Best-effort binding cleanup; the caller's original exception wins.
        }
    }

    /// <summary>
    ///     Removes every observer binding in the scene host's topology that
    ///     targets the entity being rolled back. The observers are surviving
    ///     entities whose persisted outgoing metadata would otherwise reference
    ///     an entity that no longer exists. Discovery and per-binding cleanup are
    ///     both best-effort: the caller wraps this call so an unavailable
    ///     topology accessor cannot mask the original exception, and each
    ///     binding cleanup runs independently with failures swallowed like the
    ///     other rollback steps (the caller's original exception is
    ///     authoritative).
    ///     <para>
    ///     A user <c>OnUnmounted</c> hook may mount a new binding onto the dying
    ///     target while its previous binding is being removed. The cleanup is
    ///     therefore retried a bounded number of times while the incoming index
    ///     for the entity is non-empty. If a pathological hook keeps remounting
    ///     after the last attempt, the cleanup gives up: the surviving observer
    ///     keeps a live binding to the removed entity, which strict save
    ///     recovery rejects when that save is loaded. The bound keeps the
    ///     rollback finite; the caller's original AfterSpawn exception still
    ///     wins.
    ///     </para>
    /// </summary>
    private static void RemoveIncomingObserverBindings(ISndSceneHost host, ISndEntity entity)
    {
        if (host is not IObserverTopologyHost topologyHost)
            return;

        var topology = topologyHost.ObserverTopology;
        for (var attempt = 0; attempt < _maxIncomingBindingCleanupAttempts; attempt++)
        {
            var observerNames = topology.GetObserverNamesTargeting(entity.Name);
            if (observerNames.Count == 0)
                return;

            foreach (var observerName in observerNames)
            {
                var observer = host.FindByName(observerName);
                if (observer is null)
                    continue;

                try
                {
                    topology.RemoveBindingsTargetingFor(observer, entity.Name);
                }
                catch (Exception)
                {
                    // Best-effort teardown; continue with the remaining observations.
                }
            }
        }
    }
}
