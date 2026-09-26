using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Base class for observer strategies. Observers are stateless,
///     poolable strategy instances that react to data changes on a target
///     entity. Override <see cref="OnMounted" />, <see cref="OnDataChanged" />,
///     and/or <see cref="OnUnmounted" /> to implement observation logic.
/// </summary>
public abstract class ObserverStrategyBase : BaseStrategy
{
    /// <summary>Called after the observer strategy is mounted onto the target entity.</summary>
    public virtual void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
    {
    }

    /// <summary>Called when a subscribed data key on the target entity changes.</summary>
    public virtual void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
    }

    /// <summary>Called after the observer strategy is unmounted from the target entity.</summary>
    public virtual void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
    {
    }
}
