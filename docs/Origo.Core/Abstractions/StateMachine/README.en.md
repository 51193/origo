<!-- docsync-pair: Origo.Core/Abstractions/StateMachine/README -->
<!-- docsync-revision: 2 -->
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
| `TryPopRuntime(out popped)` | Pop at runtime (triggers BeforeRemove) |
| `TryPopOnQuit(out popped)` | Pop on exit (triggers BeforeQuit) |
| `Peek()` | View stack top |
| `Snapshot()` | Bottom-to-top snapshot |
| `FlushAfterLoad()` | Replay Push AfterLoad hooks in insertion order |
| `RestoreStackWithoutHooks(list)` | Restore from save, no hooks triggered |

## IStateMachineContext Members

| Member | Description |
|------|------|
| `SystemBlackboard` | System-level (inherited from ISndBlackboardAccess) |
| `ProgressBlackboard` | Progress-level; null when none active (inherited from ISndBlackboardAccess) |
| `EnqueueBusinessDeferred(action)` | Enqueue business deferred action (inherited from ISndDeferredActions) |
| `FlushDeferredActionsForCurrentFrame()` | Flush deferred queue (inherited from ISndDeferredActions) |
| `GetPendingPersistenceRequestCount()` | Pending persistence requests (inherited from ISndDeferredActions) |
| `SessionBlackboard` | Session-level; null when none active (own) |
| `SceneAccess` | Current session SND scene access (own) |

## Design Decisions

### Why state machine only stores strings
All business logic is in strategies; the state machine stays lightweight and stateless.

### Why separate TryPopRuntime and TryPopOnQuit
Different hook semantics: runtime pop triggers BeforeRemove, exit pop triggers BeforeQuit. Separation prevents misuse.

### Why IStateMachineContainer in Abstractions
Keeps `ISessionRun.GetSessionStateMachines()` dependent only on Abstractions, not Runtime concrete types.

---
[↑ Back to Abstractions](../README.en.md)
