<!-- docsync-pair: usage/state-machine -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# State Machine

> [↑ Back to usage](README.en.md)

## Overview

Origo's state machine is a **string-stack** state machine. The stack stores only string identifiers; the specific semantics of Push/Pop are implemented by associated strategy hooks. This keeps the state machine itself lightweight, with all business logic concentrated in strategies.

## Core Interface

```csharp
public interface IStateMachine
{
    string MachineKey { get; }           // State machine unique identifier
    string PushStrategyIndex { get; }    // Index of the Push strategy
    string PopStrategyIndex { get; }     // Index of the Pop strategy

    void Push(string value);             // Push at runtime
    bool TryPopRuntime(out string? popped);  // Pop at runtime
    bool TryPopOnQuit(out string? popped);   // Pop on exit
    (bool found, string? top) Peek();    // Peek at stack top
    IReadOnlyList<string> Snapshot();    // Stack snapshot
    void FlushAfterLoad();               // Replay after loading
    // internal: RestoreStackWithoutHooks (used only by the framework's deserialization path; business code modifies the stack via Push/TryPop*)
}
```

## Strategy Hooks

### StateMachineStrategyBase

```csharp
public abstract class StateMachineStrategyBase : BaseStrategy
{
    // Called during a runtime Push attempt; the Push is rolled back when this hook throws
    public virtual void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) { }

    // After load recovery — called for each layer from bottom to top
    public virtual void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx) { }

    // Called before a Pop at runtime
    public virtual void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) { }

    // Called before a Pop during the exit flow
    public virtual void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) { }
}
```

### Hook Context

```csharp
public readonly struct StateMachineStrategyContext
{
    public string MachineKey { get; }   // State machine identifier
    public string? BeforeTop { get; }   // Stack top before the operation
    public string? AfterTop { get; }    // Stack top after the operation
}
```

## Usage Examples

### Defining Push/Pop Strategies

```csharp
// Push strategy: logic executed on Push
[StrategyIndex("my_game.menu_push")]
public sealed class MenuPushStrategy : StateMachineStrategyBase
{
    public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
        // context.AfterTop is the new stack top (the just-pushed value)
        if (context.AfterTop == "main_menu")
            ctx.SessionBlackboard?.SetValue("active_menu", context.AfterTop);
    }
}

// Pop strategy: logic executed on Pop
[StrategyIndex("my_game.menu_pop")]
public sealed class MenuPopStrategy : StateMachineStrategyBase
{
    public override void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
        // context.BeforeTop is the stack top before the pop
        var nextMenu = context.AfterTop;
        // Switch to next menu state...
    }
}
```

### Creating and Using a State Machine

```csharp
// Get the state machine container in ProgressRun or SessionRun
var container = sessionRun.GetSessionStateMachines();

// Create a state machine (returns existing instance if already present)
var sm = container.CreateOrGet(
    machineKey: "main_fsm",
    pushStrategyIndex: "my_game.menu_push",
    popStrategyIndex: "my_game.menu_pop");

// Runtime operations
sm.Push("main_menu");
sm.Push("settings");
sm.TryPopRuntime(out var popped);  // popped == "settings"
```

## Load Recovery (Two-Phase)

```
1. Deserialize → RestoreStackWithoutHooks(stack) (internal, invoked by the framework's load pipeline)
   → Restore stack content; trigger no strategy hooks

2. FlushAfterLoad()
   → Call OnPushAfterLoad for each layer from bottom to top
   → Replay initial state logic
```

The two-phase separation ensures that hook-triggering during stack structure adjustment (which would cause incomplete state) is avoided, and a unified replay happens after the adjustment is complete.

### Push Failure Rollback

`Push` first places the value on top of the stack and then invokes `OnPushRuntime`; if the hook throws, the pushed value is removed, the stack returns to its pre-Push state, and the exception propagates unchanged. A failed Push therefore never leaves the stack changed while the business logic did not run.

## Serialization Format

```json
{
  "machines": [
    {
      "key": "main_fsm",
      "pushIndex": "my_game.menu_push",
      "popIndex": "my_game.menu_pop",
      "stack": ["main_menu", "settings"]
    }
  ]
}
```

## Container Operations

> The first three rows below are `IStateMachineContainer` interface methods; `FlushAllAfterLoad`, `PopAllRuntime`, and `PopAllOnQuit` are internal methods of the concrete `StateMachineContainer` class (non-interface).

| Operation | Description | Source |
|-----------|-------------|--------|
| `CreateOrGet(key, pushIdx, popIdx)` | Create or get (throws if index differs) | Interface |
| `TryGet(key)` | Look up an existing state machine | Interface |
| `Remove(key)` | Remove and Dispose | Interface |
| `Clear()` | Release all state machines | Interface |
| `FlushAllAfterLoad()` | Replay all machines in insertion order | Implementation |
| `PopAllRuntime()` | Pop each machine empty in insertion order | Implementation |
| `PopAllOnQuit()` | Exit flow: pop each machine empty in insertion order | Implementation |

## Comparison with Entity Strategies

| Trait | Entity Strategy (LifecycleStrategyBase) | State Machine Strategy (StateMachineStrategyBase) |
|-------|------|------|
| Mount location | Entity (SndEntity) | State machine (StackStateMachine) |
| Data access | `entity.SetData/TryGetData` | `ctx.SessionBlackboard` |
| Lifecycle | 8 hooks (Spawn→Dead) | 4 hooks (Push/Pop) |
| Applicable scenario | Entity behavior (movement, health, rendering) | Flow control (menu stack, level switching) |

## Related Documents

- [SND Entity Model](snd-entity-model.en.md) — Entity strategies
- [Session Model](session-model.en.md) — Session-level state machines

---
[↑ Back to usage](README.en.md)
