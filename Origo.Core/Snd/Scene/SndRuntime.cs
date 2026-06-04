using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     面向上层的 SND 运行时门面。
///     将 SndWorld（策略池与 JSON 配置）与具体场景宿主 ISndSceneHost 组合在一起，
///     提供统一的 Spawn / 导出入口。
///     Spawn/SpawnMany 委托给 SceneHost（宿主内部完成钩子触发），
///     KillPendingEntities/ClearAll 则在此编排两阶段批处理。
/// </summary>
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

        return SceneHost.Spawn(metaData);
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

        SceneHost.SpawnMany(metaList);
    }

    public IReadOnlyList<SndMetaData> BuildMetaList() => SceneHost.BuildMetaList();

    public void ClearAll()
    {
        foreach (var entity in GetEntities())
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
            if (e is IEntityLifecycle lifecycle)
                lifecycle.FireBeforeDeadHooks();

        foreach (var e in pending)
            SceneHost.TeardownEntity(e.Name);
    }
}
