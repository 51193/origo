using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;

namespace Origo.TestSupport;

/// <summary>Initializes an entity <c>count</c> value and increments it per frame.</summary>
public abstract class SharedFrameCounterStrategy : LifecycleStrategyBase
{
    /// <inheritdoc/>
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
        entity.SetData("count", 0);

    /// <inheritdoc/>
    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var count = entity.GetData<int>("count");
        entity.SetData("count", count + 1);
    }
}

/// <summary>Active strategy that doubles integer inputs and echoes other inputs.</summary>
public abstract class SharedEchoActiveStrategy : ActiveStrategyBase
{
    /// <inheritdoc/>
    public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) =>
        input is int i ? i * 2 : input;
}

/// <summary>
///     Lifecycle strategy that records BeforeDead calls into an AsyncLocal
///     event list for isolated test assertions.
/// </summary>
public abstract class SharedKillProbeStrategy : LifecycleStrategyBase
{
    private static readonly AsyncLocal<List<string>?> _events = new();

    /// <summary>Current test-local event list; assign before each test.</summary>
    public static List<string>? Events
    {
        get => _events.Value;
        set => _events.Value = value;
    }

    /// <inheritdoc/>
    public override void BeforeDead(ISndEntity entity, ISndContext ctx) =>
        Events?.Add("before_dead");
}

/// <summary>Lifecycle strategy whose hooks intentionally do nothing.</summary>
public abstract class SharedNoopLifecycleStrategy : LifecycleStrategyBase
{
}

/// <summary>State machine strategy whose hooks intentionally do nothing.</summary>
public abstract class SharedNoopStateMachineStrategy : StateMachineStrategyBase
{
}
