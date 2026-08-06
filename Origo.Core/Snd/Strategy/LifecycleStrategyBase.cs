using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Base class for lifecycle strategies attached to SND entities, providing entity lifecycle hooks.
/// </summary>
public abstract class LifecycleStrategyBase : BaseStrategy
{
    /// <summary>Per-frame update, called once per frame while the entity is alive and processing.</summary>
    public virtual void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
    }

    /// <summary>Called once after the entity is spawned and fully present in the scene.</summary>
    public virtual void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
    }

    /// <summary>Called once after the entity is restored from a save.</summary>
    public virtual void AfterLoad(ISndEntity entity, ISndContext ctx)
    {
    }

    /// <summary>Called after the strategy is dynamically mounted on the entity via <c>AddStrategy</c>.</summary>
    public virtual void AfterAdd(ISndEntity entity, ISndContext ctx)
    {
    }

    /// <summary>Called before the strategy is dynamically removed from the entity via <c>RemoveStrategy</c>.</summary>
    public virtual void BeforeRemove(ISndEntity entity, ISndContext ctx)
    {
    }

    /// <summary>Called before the entity's state is serialized to a save; use it to flush in-memory state into entity data.</summary>
    public virtual void BeforeSave(ISndEntity entity, ISndContext ctx)
    {
    }

    /// <summary>Called when the entity's session is quitting, before the entity is torn down.</summary>
    public virtual void BeforeQuit(ISndEntity entity, ISndContext ctx)
    {
    }

    /// <summary>Called when the entity is killed, before its strategies and resources are released.</summary>
    public virtual void BeforeDead(ISndEntity entity, ISndContext ctx)
    {
    }
}
