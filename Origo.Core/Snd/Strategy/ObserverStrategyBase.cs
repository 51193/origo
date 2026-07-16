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
    public virtual void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
    {
    }

    public virtual void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
    }

    public virtual void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
    {
    }
}
