<!-- docsync-pair: Origo.Core/Abstractions/StateMachine/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# StateMachine (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: StateMachine](../../StateMachine/README.en.md)

## Overview
Defines the string-stack state machine system. The state machine only stores strings; Push/Pop semantics are by associated strategy hooks. Also defines `IStateMachineContext` and `IStateMachineContainer`.

## Included Files

| File | Responsibility |
|------|------|
| `IStateMachine.cs` | String-stack state machine: Push/Pop/Peek/Snapshot + persistence |
| `IStateMachineContext.cs` | Strategy hook runtime context: blackboard + scene + deferred queue |
| `IStateMachineContainer.cs` | Container: CreateOrGet/TryGet/Remove/Clear |

## IStateMachine Members

| Method | Description |
|------|------|
| `MachineKey` | Unique identifier |
| `PushStrategyIndex` / `PopStrategyIndex` | Associated strategy indices |
| `Push(value)` | Push onto stack |
| `TryPopRuntime(out popped)` | Runtime pop: first triggers the pop strategy's `OnPopRuntime` hook, then pops |
| `TryPopOnQuit(out popped)` | Exit pop: first triggers the pop strategy's `OnPopBeforeQuit` hook, then pops |
| `Peek()` | View stack top |
| `Snapshot()` | Bottom-to-top snapshot |
| `FlushAfterLoad()` | Replay Push AfterLoad hooks in insertion order |
| `RestoreStackWithoutHooks(list)` | Restore from save, no hooks triggered (`internal` — reserved for the framework's deserialization path; business code must use `Push`) |

## IStateMachineContext Members

> Members inherited from [ISndBlackboardAccess](../Snd/README.en.md) and [ISndDeferredActions](../Snd/README.en.md) are not re-listed.

| Member | Description |
|------|------|
| `SystemBlackboard` | System-level blackboard (cross-process), inherited from [ISndBlackboardAccess](../Snd/README.en.md) |
| `ProgressBlackboard` | Progress-level blackboard; null when no active progress (inherited from [ISndBlackboardAccess](../Snd/README.en.md)) |
| `EnqueueBusinessDeferred(action)` | Enqueue a business logic deferred action (inherited from [ISndDeferredActions](../Snd/README.en.md)) |
| `FlushDeferredActionsForCurrentFrame()` | Flush the deferred queue (inherited from [ISndDeferredActions](../Snd/README.en.md)) |
| `GetPendingPersistenceRequestCount()` | Pending persistence request count (inherited from [ISndDeferredActions](../Snd/README.en.md)) |
| `SessionBlackboard` | Session-level blackboard; null when no active session (own) |
| `SceneAccess` | Current session SND read-only scene access (`ISndSceneReadAccess`: GetEntities / FindByName) |

## Design Decisions

### Why the state machine only stores strings rather than state objects
State logic is delegated to `StateMachineStrategyBase` (see [StateMachine implementation](../../StateMachine/README.en.md)); the state machine itself only maintains a stack of identifiers. This keeps the state machine lightweight and stateless, concentrates all business logic in strategies, and makes testing and reuse easy.

### Why separate TryPopRuntime and TryPopOnQuit
The two pop paths trigger different strategy hook semantics. Runtime pop triggers `OnPopRuntime` (normal state transition); exit pop triggers `OnPopBeforeQuit` (cleanup when the state machine is destroyed). Merging them into one method would force callers to pass extra parameters to distinguish the semantics, increasing misuse risk.

### Why SessionStateMachineContext is internal and not in the Abstractions layer
`SessionStateMachineContext` is a concrete implementation class defined in the `Origo.Core.Runtime.Lifecycle` namespace, not part of the Abstractions-layer interface contract. External code only needs to depend on the `IStateMachineContext` interface. Session-level context binding is performed internally by `SessionManager` when constructing `SessionRun`.

### Why IStateMachineContainer is needed
`StateMachineContainer` is a concrete type (in `Runtime.StateMachine`); if `ISessionRun.GetSessionStateMachines()` returned it directly, `ISessionRun` (Abstractions layer) would depend on a Runtime-layer concrete type — violating the dependency direction. Introducing the `IStateMachineContainer` interface:

- Strategy code obtaining the container via `ISessionRun` depends only on the Abstractions layer
- The concrete implementation `StateMachineContainer` keeps its internal methods (`FlushAllAfterLoad`, `SerializeToNode`) for Runtime-layer internal code
- External strategies can create and look up state machines via `CreateOrGet`/`TryGet` without being aware of the container's concrete implementation
- `Remove` throws `InvalidOperationException` for a key that does not exist (fail-fast, consistent with the strategy managers' remove contracts)

---
[↑ Back to Abstractions](../README.en.md)
