using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

public sealed class SndRuntime
{
    public SndRuntime(SndWorld world, ISndSceneHost sceneHost)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sceneHost);
        World = world;
        SceneHost = sceneHost;
    }

    public SndWorld World { get; }

    public ISndSceneHost SceneHost { get; }

    public ISndEntity Spawn(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        if (string.IsNullOrWhiteSpace(metaData.Name))
            throw new ArgumentException("SndMetaData.Name cannot be null or whitespace.", nameof(metaData));
        if (SceneHost.FindByName(metaData.Name) is not null)
            throw new InvalidOperationException($"Snd entity name '{metaData.Name}' already exists.");

        return SpawnCore(SceneHost, metaData);
    }

    public void SpawnMany(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);

        foreach (var meta in metaList)
        {
            if (string.IsNullOrWhiteSpace(meta.Name))
                throw new ArgumentException("SndMetaData.Name cannot be null or whitespace.", nameof(metaList));
            if (SceneHost.FindByName(meta.Name) is not null)
                throw new InvalidOperationException($"Snd entity name '{meta.Name}' already exists.");
        }

        SpawnManyCore(SceneHost, metaList);
    }

    internal static ISndEntity SpawnCore(ISndSceneHost host, SndMetaData meta)
    {
        var entity = host.CreateEntity(meta);
        if (entity is IEntityLifecycle lifecycle)
            lifecycle.FireAfterSpawnHooks();
        return entity;
    }

    internal static void SpawnManyCore(ISndSceneHost host, IEnumerable<SndMetaData> metaList)
    {
        var staged = new List<ISndEntity>();
        foreach (var meta in metaList)
            staged.Add(host.CreateEntity(meta));

        foreach (var entity in staged)
            if (entity is IEntityLifecycle lifecycle)
                lifecycle.FireAfterSpawnHooks();
    }

    public IReadOnlyList<SndMetaData> BuildMetaList() => SceneHost.BuildMetaList();

    public void ClearAll()
    {
        var entities = SceneHost.GetEntities();

        foreach (var entity in entities)
            if (entity is SndEntity se)
                TeardownOutgoingObserverBindings(se, entities);

        foreach (var entity in entities)
            if (entity is IEntityLifecycle lifecycle)
            {
                lifecycle.FireBeforeQuitHooks();
                lifecycle.ReleaseStrategiesOnly();
                lifecycle.TeardownOnly();
            }

        SceneHost.RemoveAllEntities();
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => SceneHost.GetEntities();

    public ISndEntity? FindByName(string name) => SceneHost.FindByName(name);

    public void KillPendingEntities()
    {
        var entities = SceneHost.GetEntities();
        var pending = new List<ISndEntity>();
        foreach (var e in entities)
            if (e.IsPendingKill)
                pending.Add(e);

        foreach (var e in pending)
            if (e is SndEntity se)
                TeardownOutgoingObserverBindings(se, entities);

        foreach (var e in pending)
            foreach (var other in entities)
                if (other is SndEntity otherSe && otherSe != e && otherSe.HasObserverBindingTargeting(e.Name))
                    TeardownIncomingObserverBindings(otherSe, e);

        foreach (var e in pending)
            if (e is IEntityLifecycle lifecycle)
                lifecycle.FireBeforeDeadHooks();

        foreach (var e in pending)
        {
            if (e is IEntityLifecycle lifecycle)
            {
                lifecycle.ReleaseStrategiesOnly();
                lifecycle.TeardownOnly();
            }

            SceneHost.RemoveEntity(e.Name);
        }
    }

    private static void TeardownOutgoingObserverBindings(SndEntity entity, IReadOnlyCollection<ISndEntity> entities)
    {
        entity.TeardownOutgoingObserverBindings(targetName =>
        {
            foreach (var other in entities)
                if (other.Name == targetName)
                    return other;
            return null;
        });
    }

    private static void TeardownIncomingObserverBindings(SndEntity observer, ISndEntity target)
    {
        observer.RemoveAllObserverBindingsTargeting(target.Name);
    }

    public void ProcessAll(double delta)
    {
        SceneHost.ProcessAll(delta);
    }
}
