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

    /// <summary>
    ///     SND 世界实例，包含策略池、类型映射、编解码器和模板配置。
    /// </summary>
    public SndWorld World { get; }

    /// <summary>
    ///     SND 场景宿主，由具体引擎适配层实现。负责实体的物理创建、挂载与管理。
    /// </summary>
    public ISndSceneHost SceneHost { get; }

    /// <summary>
    ///     按元数据在场景中生成一个 SND 实体。若名称已存在则抛出异常。
    /// </summary>
    public ISndEntity Spawn(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        if (string.IsNullOrWhiteSpace(metaData.Name))
            throw new ArgumentException("SndMetaData.Name cannot be null or whitespace.", nameof(metaData));
        if (SceneHost.FindByName(metaData.Name) is not null)
            throw new InvalidOperationException($"Snd entity name '{metaData.Name}' already exists.");

        return SceneHost.Spawn(metaData);
    }

    /// <summary>
    ///     批量生成多个 SND 实体，逐个调用 Spawn。
    /// </summary>
    public void SpawnMany(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        foreach (var meta in metaList) Spawn(meta);
    }

    /// <summary>
    ///     序列化当前场景中所有实体的元数据列表。
    /// </summary>
    public IReadOnlyList<SndMetaData> SerializeMetaList() => SceneHost.SerializeMetaList();

    /// <summary>
    ///     清除场景中所有 SND 实体。
    /// </summary>
    public void ClearAll() => SceneHost.ClearAll();

    /// <summary>
    ///     获取场景中所有 SND 实体集合。
    /// </summary>
    public IReadOnlyCollection<ISndEntity> GetEntities() => SceneHost.GetEntities();

    /// <summary>
    ///     按名称查找 SND 实体，未找到时返回 null。
    /// </summary>
    public ISndEntity? FindByName(string name) => SceneHost.FindByName(name);
}
