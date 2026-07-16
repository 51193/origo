using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Base class for lifecycle strategies attached to SND entities, providing entity lifecycle hooks.
/// </summary>
public abstract class LifecycleStrategyBase : BaseStrategy
{
    public virtual void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
    }

    public virtual void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
    }

    public virtual void AfterLoad(ISndEntity entity, ISndContext ctx)
    {
    }

    public virtual void AfterAdd(ISndEntity entity, ISndContext ctx)
    {
    }

    public virtual void BeforeRemove(ISndEntity entity, ISndContext ctx)
    {
    }

    public virtual void BeforeSave(ISndEntity entity, ISndContext ctx)
    {
    }

    public virtual void BeforeQuit(ISndEntity entity, ISndContext ctx)
    {
    }

    public virtual void BeforeDead(ISndEntity entity, ISndContext ctx)
    {
    }
}
