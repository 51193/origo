using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Strategy;

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
