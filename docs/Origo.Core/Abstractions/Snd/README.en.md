<!-- docsync-pair: Origo.Core/Abstractions/Snd/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Snd (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd](../../Snd/README.en.md)

## Overview
Role interface decomposition of ISndContext. 9 Snd role interfaces + `IStateMachineContext` (from the StateMachine module) decomposed by responsibility (ISP). ISndContext does not inherit any role interfaces; all capabilities are accessed through typed companion properties.

## Included Files

| File | Responsibility |
|------|------|
| `ISndBlackboardAccess.cs` | System + progress-level blackboard access (2 members) |
| `ISndDeferredActions.cs` | Deferred action queue: enqueue + pending-persistence count (2 members) |
| `ISndTemplateAccess.cs` | Template cloning (1 member) |
| `ISndConsoleAccess.cs` | Console command submit/process/output subscribe (4 members) |
| `ISndStateMachineAccess.cs` | Progress-level state machine container (1 member). Returns `IStateMachineContainer?` |
| `ISndSaveOperations.cs` | Save list/read/write + level switch + continue + meta contributor (8 members) |
| `ISndLifecycleOperations.cs` | Continue/Initial/MainMenu entry points (4 members) |
| `ISndFileAccess.cs` | File access: structured + strongly-typed + exists (5 members). All via IDataSourceIoGateway boundary |
| `ISndArchiveFileAccess.cs` | In-save file access: structured + strongly-typed + exists + delete (6 members) |

## ISndContext Companion Properties

Beyond its 10 companion properties, ISndContext directly exposes the following members:

| Member | Description |
|------|------|
| `Bootstrap()` | Entry point: strategy discovery → alias/template loading → entry save loading |
| `SaveRootPath` | Current save root path |
| `InitialSaveRootPath` | Initial save root path |
| `EntryConfigPath` | Entry configuration file path |

ISndContext does not inherit any role interfaces; all capabilities are accessed through 10 companion properties:

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

## Relationship with IStateMachineContext

`IStateMachineContext` inherits the two shared role interfaces `ISndBlackboardAccess` and `ISndDeferredActions`, avoiding duplicate member definitions between the interfaces:

```
IStateMachineContext : ISndBlackboardAccess + ISndDeferredActions
                     + SessionBlackboard + SceneAccess
```

## Design Decisions

### Why decompose ISndContext

Decomposing the interface into narrow interfaces (9 Snd role interfaces + IStateMachineContext), each consumer can depend only on the narrow interfaces they need:

- Code that only needs blackboard access depends on `ISndBlackboardAccess`
- Code that only needs deferred queue depends on `ISndDeferredActions`
- Code that only needs save operations depends on `ISndSaveOperations`
- etc.

Strategy hooks (`LifecycleStrategyBase`'s 8 virtual methods) retain `ISndContext ctx` as a full parameter — strategies as first-class citizens should be able to access all framework capabilities.

### Why companion properties rather than interface inheritance
Eliminates naming conflicts between role interfaces (e.g., multiple `Clear()`) and provides clearer call semantics (`ctx.Save.RequestLoadGame(...)`).

### Why SessionManager is not on ISndContext
Strategies access it through `entity.OwningSession.SessionManager` — safer than global context lookup by key.

### Why ISndContext does not provide session and entity-destruction members

- Session access: strategies use `entity.OwningSession` to reach their session; the entity knows its session and no global context key lookup is needed.
- Front-session detection: `IsFrontSession` is a convenience property derivable from `SessionManager.ForegroundSession`.
- Entity destruction: always goes through `entity.OwningSession.RequestKillEntity(name)` or `ISessionRun.RequestKillEntity(name)`; no global context entry point exists.

### Why GetProgressStateMachines() returns IStateMachineContainer

Abstractions-layer return values must not reference Runtime-layer concrete types. `IStateMachineContainer` lives in `Origo.Core.Abstractions.StateMachine`; returning this abstract interface instead of the concrete `StateMachineContainer` keeps `ISndStateMachineAccess` consumers free of transitive dependencies on Runtime internals (`StackStateMachine`, `SndStrategyPool`, etc.).

### Why IStateMachineContext also inherits role interfaces
`SystemBlackboard`, `ProgressBlackboard`, `EnqueueBusinessDeferred` are semantically identical; reuse via inheritance avoids duplicate definitions.

### Why ISndFileAccess exposes DataSourceNode rather than raw file text

All file operations go through three base interfaces — `IDataSourceIoGateway` (content I/O), `IFileMetaAccess` (file metadata), `IPathResolver` (path arithmetic). `ISndFileAccess` methods delegate to the corresponding interface:

- `ReadFile` / `WriteFile` → `IDataSourceIoGateway.ReadTree` / `WriteTree` → structured `DataSourceNode` tree
- `ReadObject<T>` / `WriteObject<T>` → Gateway plus `DataSourceConverterRegistry` → strongly-typed objects
- `FileExists` → `IFileMetaAccess.FileExists`

Strategies must not call `IFileSystem` directly (fully internalized) or parse raw JSON/Map text themselves — suffix routing, codec policy, and I/O error semantics are governed on the Gateway side. Path concatenation (`CombinePath`, `GetParentDirectory`) and directory checks (`DirectoryExists`) come from the framework-internal `IPathResolver` and `IFileMetaAccess`, and are not exposed to strategies through `ISndFileAccess`.

### Why WriteFile does not restrict paths

`ISndFileAccess.WriteFile` is a deliberately open file-write capability: strategies are trusted game code, and the framework does not adjudicate which paths business code may write. Framework save writes never go through this path (they are orchestrated by `SaveCoordinator` / `SaveStorageFacade` with write markers and atomic swapping), so the open write path cannot bypass the framework's persistence guarantees — but a strategy that writes framework-owned paths such as `current/*` is responsible for the consequences itself. Scoped write access (e.g. the in-save `extra/` directory) is provided by `ISndArchiveFileAccess`.

---
[↑ Back to Abstractions](../README.en.md)
