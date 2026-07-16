using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Strategy index lists associated with an SND entity.
///     Grouped by strategy type into three categories: lifecycle strategies, active strategies,
///     and observer strategies, managed by SndStrategyManager, ActiveStrategyManager,
///     and ObserverTopology respectively.
/// </summary>
public sealed class StrategyMetaData
{
    /// <summary>Lifecycle strategy index list (LifecycleStrategyBase subclasses).</summary>
    public List<string> LifecycleIndices { get; set; } = [];

    /// <summary>Active strategy index list (ActiveStrategyBase subclasses).</summary>
    public List<string> ActiveIndices { get; set; } = [];

    /// <summary>
    ///     Observer strategy index list. Grouped by target entity;
    ///     each binding records the observed target entity name and the list of observer strategy
    ///     indices mounted on that entity. When observing itself, Target equals the entity's own name.
    /// </summary>
    public List<ObserverBinding> ObserverIndices { get; set; } = [];

    /// <summary>
    ///     An observer strategy index binding entry.
    /// </summary>
    public sealed class ObserverBinding
    {
        /// <summary>The name of the observed target entity.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>The list of observer strategy indices mounted on the target entity.</summary>
        public List<string> ObserverIndices { get; set; } = [];
    }
}
