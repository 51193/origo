using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;

namespace Origo.Core.Tests;

internal abstract class SharedFrameCounterStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
        entity.SetData("count", 0);

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var count = entity.GetData<int>("count");
        entity.SetData("count", count + 1);
    }
}

internal abstract class SharedEchoActiveStrategy : ActiveStrategyBase
{
    public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) =>
        input is int i ? i * 2 : input;
}

internal abstract class SharedKillProbeStrategy : LifecycleStrategyBase
{
    private static readonly AsyncLocal<List<string>?> _events = new();

    public static List<string>? Events
    {
        get => _events.Value;
        set => _events.Value = value;
    }

    public override void BeforeDead(ISndEntity entity, ISndContext ctx) =>
        Events?.Add("before_dead");
}

internal abstract class SharedNoopLifecycleStrategy : LifecycleStrategyBase
{
}

internal abstract class SharedNoopStateMachineStrategy : StateMachineStrategyBase
{
}
