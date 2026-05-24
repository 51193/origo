using System;
using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     SND 实体在框架层面的元数据聚合。
///     与具体引擎无关，仅包含名称、节点元信息、策略列表与数据。
/// </summary>
public sealed class SndMetaData
{
    /// <summary>实体的唯一名称标识符。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>节点元数据（逻辑名称 → 资源 ID 映射），用于引擎适配层创建节点。</summary>
    public NodeMetaData? NodeMetaData { get; set; }

    /// <summary>策略元数据，包含策略索引列表。</summary>
    public StrategyMetaData? StrategyMetaData { get; set; }

    /// <summary>数据元数据，包含实体键值对数据（TypedData 映射）。</summary>
    public DataMetaData? DataMetaData { get; set; } = new();

    /// <summary>
    ///     克隆元数据容器（新字典与列表；<see cref="TypedData" /> 实例与其中 <c>Data</c> 引用与 JSON 往返深拷贝类似，不递归复制对象图）。
    /// </summary>
    public SndMetaData DeepClone()
    {
        return new SndMetaData
        {
            Name = Name,
            NodeMetaData = NodeMetaData is null
                ? null
                : new NodeMetaData
                {
                    Pairs = new Dictionary<string, string>(NodeMetaData.Pairs, StringComparer.Ordinal)
                },
            StrategyMetaData = StrategyMetaData is null
                ? null
                : new StrategyMetaData { Indices = new List<string>(StrategyMetaData.Indices) },
            DataMetaData = DataMetaData is null
                ? null
                : new DataMetaData
                {
                    Pairs = new Dictionary<string, TypedData>(DataMetaData.Pairs)
                }
        };
    }
}
