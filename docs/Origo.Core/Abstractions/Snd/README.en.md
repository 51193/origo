<!-- docsync-pair: Origo.Core/Abstractions/Snd/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Snd (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd](../../Snd/README.en.md)

## Overview
Role interface decomposition of ISndContext. 10 narrow interfaces decomposed by responsibility (ISP). ISndContext does not inherit any role interfaces; all capabilities are accessed through typed companion properties.

## Included Files

| File | Responsibility |
|------|------|
| `ISndBlackboardAccess.cs` | System + progress-level blackboard access (2 members) |
| `ISndDeferredActions.cs` | Deferred action queue: enqueue + flush + count (3 members) |
| `ISndTemplateAccess.cs` | Template cloning (1 member) |
| `ISndConsoleAccess.cs` | Console command submit/process/output subscribe (4 members) |
| `ISndStateMachineAccess.cs` | Progress-level state machine container (1 member). Returns `IStateMachineContainer?` |
| `ISndSaveOperations.cs` | Save list/read/write + level switch + continue + meta contributor (8 members) |
| `ISndLifecycleOperations.cs` | Continue/Initial/MainMenu entry points (4 members) |
| `ISndFileAccess.cs` | File access: structured + strongly-typed + exists (5 members). All via IDataSourceIoGateway boundary |
| `ISndArchiveFileAccess.cs` | In-save file access: structured + strongly-typed + exists + delete (6 members) |

## ISndContext Companion Properties

| Companion Property | Type | Responsibility |
|---------------|------|------|
| `Blackboard` | `ISndBlackboardAccess` | System + progress blackboard |
| `Deferred` | `ISndDeferredActions` | Deferred action queue |
| `Template` | `ISndTemplateAccess` | Template cloning |
| `ConsoleAccess` | `ISndConsoleAccess` | Console command submit/process/subscribe |
| `StateMachines` | `ISndStateMachineAccess` | Progress-level state machine container |
| `Save` | `ISndSaveOperations` | Save list/read/write + level switch + meta contributor |
| `Lifecycle` | `ISndLifecycleOperations` | Continue/Initial/MainMenu |
| `FileAccess` | `ISndFileAccess` | Static resource file access |
| `ArchiveFileAccess` | `ISndArchiveFileAccess` | In-save file access |
| `StateMachineContext` | `IStateMachineContext` | State machine context |

## Design Decisions

### Why decompose ISndContext
Narrow interfaces let consumers depend only on what they need. Strategy hooks retain `ISndContext ctx` as first-class citizens.

### Why companion properties rather than interface inheritance
Eliminates naming conflicts between role interfaces (e.g., multiple `Clear()`) and provides clearer call semantics (`ctx.Save.RequestLoadGame(...)`).

### Why SessionManager is not on ISndContext
Strategies access it through `entity.OwningSession.SessionManager` — safer than global context lookup by key.

### Why IStateMachineContext also inherits role interfaces
`SystemBlackboard`, `ProgressBlackboard`, `EnqueueBusinessDeferred` are semantically identical; reuse via inheritance avoids duplicate definitions.

---
[↑ Back to Abstractions](../README.en.md)
