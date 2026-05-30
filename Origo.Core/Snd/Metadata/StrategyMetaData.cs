using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     与某个 SND 实体关联的策略索引列表。
///     按策略类型分为实体策略（被动）和主动策略两组，分别由
///     SndStrategyManager 和 ActiveStrategyManager 管理。
/// </summary>
public sealed class StrategyMetaData
{
    /// <summary>实体策略索引列表（EntityStrategyBase 子类）。</summary>
    public List<string> EntityIndices { get; set; } = new();

    /// <summary>主动策略索引列表（ActiveStrategyBase 子类）。</summary>
    public List<string> ActiveIndices { get; set; } = new();
}
