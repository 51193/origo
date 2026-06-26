using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     与某个 SND 实体关联的策略索引列表。
///     按策略类型分为生命周期策略、主动策略和观察者策略三组，分别由
///     SndStrategyManager、ActiveStrategyManager 和 ObserverStrategyManager 管理。
/// </summary>
public sealed class StrategyMetaData
{
    /// <summary>生命周期策略索引列表（LifecycleStrategyBase 子类）。</summary>
    public List<string> LifecycleIndices { get; set; } = new();

    /// <summary>主动策略索引列表（ActiveStrategyBase 子类）。</summary>
    public List<string> ActiveIndices { get; set; } = new();

    /// <summary>
    ///     观察者策略索引列表。按目标实体分组；每个绑定记录被观察目标实体名与该实体上挂载的观察者策略索引列表。
    ///     自观察时 Target 等于自身实体名。
    /// </summary>
    public List<ObserverBinding> ObserverIndices { get; set; } = new();

    /// <summary>
    ///     观察者策略索引绑定项。
    /// </summary>
    public sealed class ObserverBinding
    {
        /// <summary>被观察的目标实体名。</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>该目标实体上挂载的观察者策略索引列表。</summary>
        public List<string> ObserverIndices { get; set; } = new();
    }
}
