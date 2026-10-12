# StateMachine

> [↑ Back to Origo.Core.Kernel](../README.en.md) · [↔ Related Tests: StateMachine](../../Origo.Core.Tests/StateMachine.en.md)

## Overview

The complete implementation of the `IStateMachine` interface: the string-stack state machine (`StackStateMachine`) and its persistence model. The state machine itself only stores a stack of string identifiers; specific behavior is defined by associated Push/Pop strategies. The strategy base class `StateMachineStrategyBase` and the context struct `StateMachineStrategyContext` live in [Origo.Core.Contracts/StateMachine](../../Origo.Core.Contracts/StateMachine/README.en.md).

## Included Files

| File | Responsibility |
|------|------|
| `StackStateMachine.cs` | String-stack state machine complete implementation, supporting Push/Pop/Peek/persistence |
| `StateMachinePersistenceModels.cs` | Persistence model: StateMachineContainerPayload |

## Implementation Details

### StackStateMachine

Core state machine; maintains `List<string> _stack` and holds Push/Pop strategy references through the injected `SndStrategyPool`. Key behavior:

| Operation | Push Strategy Hook | Pop Strategy Hook | Timing |
|-----------|-------------------|-------------------|--------|
| `Push(value)` | `OnPushRuntime` | - | After runtime push |
| `TryPopRuntime()` | - | `OnPopRuntime` | Before runtime pop |
| `TryPopOnQuit()` | - | `OnPopBeforeQuit` | Before exit-flow pop |
| `FlushAfterLoad()` | `OnPushAfterLoad` | - | After load recovery, bottom to top |

Each hook receives `(MachineKey, BeforeTop, AfterTop)` context, explicitly informing the strategy of stack top changes before and after the operation.

### StateMachineStrategyBase

Inherits from `BaseStrategy`, provides 4 virtual method hooks. All method parameters are `StateMachineStrategyContext` + `IStateMachineContext`; foreground and background share the same abstraction.

### StateMachineStrategyContext

`readonly struct`, immutable value type. Contains:
- `MachineKey`: The state machine's identifier within the container
- `BeforeTop`: Stack top before the operation (null if empty stack)
- `AfterTop`: Stack top after the operation (null if stack became empty)

### Persistence Model

`StateMachineContainerPayload` contains a set of `StateMachineEntryPayload` (each entry: key + pushIndex + popIndex + stack snapshot list).

## Design Decisions

### Why StackStateMachine holds both Push and Pop strategy references

Both strategies are fetched from the pool simultaneously at construction time; each gets ref count +1. `Dispose()` decrements each by 1. This ensures strategies are not recycled during the state machine's lifetime, and ref counting precisely matches the pool's reuse mechanism.

### Why RestoreStackWithoutHooks + FlushAfterLoad are two steps

Save recovery requires two phases: (1) restore stack content without hooks (preventing side effects during recovery), (2) after stack structure adjustment is complete, replay AfterLoad hooks in order. If merged into one step, hooks would fire while the stack is incomplete, causing strategies to receive incorrect context. `RestoreStackWithoutHooks` is an `internal` member of `IStateMachine`, invoked only by the framework's deserialization path (`StateMachineContainer`) — business code must modify the stack via `Push`/`TryPop*` so strategy hooks stay in sync with stack contents.

### Why strategy hook parameters include BeforeTop and AfterTop

Strategies may need to perceive state transition direction (e.g., performing initialization when switching from "menu" to "gameplay"). Simply informing of the current stack value is insufficient; "where from, where to" transition information is needed.

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
