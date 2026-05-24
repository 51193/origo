using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     挂载在 SND 实体上的策略基类，提供实体生命周期钩子。
/// </summary>
public abstract class EntityStrategyBase : BaseStrategy
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
