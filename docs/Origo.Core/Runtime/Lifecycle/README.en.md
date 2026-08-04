<!-- docsync-pair: Origo.Core/Runtime/Lifecycle/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Lifecycle

> [↑ Back to Runtime](../README.en.md)

## Overview

The implementation layer of the runtime's four-layer lifecycle. Defines the complete startup, run, and shutdown flow from the system level down to the session level. All types are constructed through structured parameter objects; dependencies flow strictly one-way downward.

## Included Files

| File | Responsibility |
|------|------|
| `SystemParameters.cs` | System-layer construction parameters (including `AdapterSceneHost`) |
| `SystemRuntime.cs` | System-level runtime container: holds SystemRun, SystemBlackboard, SndWorld |
| `SystemRun.cs` | System-layer startup: creates SndWorld → constructs ProgressRuntime |
| `ProgressParameters.cs` | Progress-layer construction parameters |
| `ProgressRuntime.cs` | Progress-level runtime container: holds ProgressRun, ProgressBlackboard, SaveContext |
| `ProgressRun.cs` | Progress-layer main logic: level switching, save read/write, session lifecycle orchestration |
| `ProgressRun.Persistence.cs` | Progress-layer persistence delegation (via SaveCoordinator) |
| `ProgressRun.SessionLoading.cs` | Progress-layer session loading branch (partial class) |
| `SessionParameters.cs` | Session-layer construction parameters |
| `SessionManager.cs` | Session manager full implementation (implements `ISessionManager`) |
| `SessionManagerRuntime.cs` | Session manager runtime container |
| `SessionRun.cs` | Session-level runtime implementation (implements `ISessionRun`) |
| `SessionTopologyCodec.cs` | Session topology codec (foreground + background session relations) |
| `SessionStateMachineContext.cs` | internal: session-level state machine context adapter binding SessionBlackboard/SceneAccess to the current session |
| `EmptySessionManager.cs` | No-op session manager (tests / empty scenarios) |
| `RunStateScope.cs` | Runtime state scope utility |
| `TopologyInvariant.cs` | internal — topology invariant validation utility |

> The `ISessionManager` and `ISessionRun` interfaces are defined in the `Origo.Core.Abstractions.Lifecycle` namespace so the Abstractions layer does not depend on the Runtime layer. This layer holds the concrete implementations.

## Four-Layer Container Model

```
SystemRun (constructed and held by SndContext)
├── SystemRuntime (holds SndWorld forwarding, SystemBlackboard, scheduler)
│   └── ProgressRun (created by SndContext)
│       ├── ProgressRuntime (holds SndWorld, SndContext, StorageService)
│       └── SessionManager (held by ProgressRun)
│           ├── SessionManagerRuntime (holds SndWorld, SndContext, ProgressBlackboard)
│           └── SessionRun (foreground + background)
```

Each layer container holds its layer's core object references and public access points:
- `SndContext` is the global/progress-level aggregator: constructs `SystemRun` and creates/destroys `ProgressRun` on every progress lifecycle transition (load/save/level switch)
- `SystemRuntime` holds `SndWorld` forwarding, `ConverterRegistry`, `AdapterSceneHost`, and the scheduler (the scheduler instance is actually held by `OrigoRuntime` and injected via `IScheduler`)
- `ProgressRuntime` holds `Logger`, `StorageService`, `SndWorld`, `AdapterSceneHost`, `StateMachineContext`, `SndContext`, `SavePathPolicy` (the progress blackboard and state machine container are held layer-wise by `SessionManager`/`ProgressRun`; SaveContext is a transient object created on demand)
- `SessionManagerRuntime` holds `SndWorld`, `SndContext`, `ProgressBlackboard` and other runtime dependencies (`ISessionManager` is held by `ProgressRun`)
- `SessionManager` reads and stores `AdapterSceneHost` at construction for creating foreground sessions
- `SessionRun` holds `SessionBlackboard`, the internal `ISndSceneHost` (`internal`, framework-internal use), `StateMachineContainer`, and the entity operation facade

## Key Lifecycle Flows

### Startup

1. `SndContext` construction creates `SystemRun` (`OrigoRuntime` already created `SndWorld` at construction, passed via `SystemParameters`)
2. `SndContext.Bootstrap()` (or `RequestLoadMainMenuEntrySave`) creates `ProgressRun` → `ProgressRun` creates `SessionManager` → `SessionRun` (foreground + background)

### Run

- Every frame: `IScheduler.Tick()` → execute deferred queue → `SessionManager.ProcessAllSessions()` → `SessionManager.KillPendingAllSessions()` → system queue → console
- Console commands route to `OrigoConsole.ProcessPending()`

### Persistence

- `SaveCoordinator`: an independent class (`Origo.Core.Save.SaveCoordinator`) for building save payloads, persisting progress state, and managing metadata, enabling isolated testing and clear responsibility separation.
- `ISndSaveOperations.RequestSaveGame` → `SaveCoordinator.BuildSavePayload` → `SavePayloadWriter.WriteToCurrent(handle, ...)` → snapshot
- `ISndSaveOperations.RequestLoadGame` → `SavePayloadReader.ReadFromCurrent(handle, ...)` / `ReadFromSnapshot(handle, ...)` → restore blackboard + scene
- `SaveFileHandle`: unified I/O context (`Origo.Core.Save.Storage.SaveFileHandle`) wrapping `IFileMetaAccess` + `IDataSourceIoGateway` + `IPathResolver` + `saveRootPath` + `ISavePathPolicy`. All Writer/Reader methods receive dependencies through `SaveFileHandle`, eliminating multi-parameter overload chains.
- `PersistProgress`: serializes the progress blackboard and the full session topology (foreground + all backgrounds) to `current/progress.json`. Without a mounted foreground session it throws `InvalidOperationException` rather than silently writing partial data.
- `SessionRun.BuildLevelPayload`: batch-triggers BeforeSave hooks (`FireBeforeSaveHooks`) on all entities first, then builds scene metadata via `SaveContext.BuildSndScene`. This gives every strategy a final chance to flush in-memory state into entity Data before saving. The full-save path (`SaveCoordinator.BuildSavePayload`) also batch-triggers `FireBeforeSaveHooks` before serializing the foreground scene, matching the background session semantics. Hooks that overwrite framework-managed blackboard keys (such as `SessionTopology`) are overridden by the framework-computed value before serialization, so such writes take no effect.
- `SessionRun.LoadFromPayload`: first restores all entity data/strategies/nodes via `SaveContext.RecoverSndScene`, then batch-triggers AfterLoad hooks (`FireAfterLoadHooks`), finally flushes state machine AfterLoad. This ensures every entity and ActiveStrategy is fully recovered before any strategy's AfterLoad fires, enabling loading-order-independent cross-entity interoperability. Observer bindings are then restored via the host topology's `RecoverBindingsFor`; an archived observer_indices reference to an entity missing from the recovered scene throws `InvalidOperationException` (fail-fast, strict-read contract).

### Level Switching

`SwitchForeground(newLevelId)` is a composite save-destroy-load operation:
1. `PersistForegroundLevelState()` **explicitly** persists the old foreground level data to `current/` (via `SessionManager.PersistSession`)
2. `PersistAndDestroyBackgroundIfExists(newLevelId)` persists and destroys a background session holding the target `levelId`, if any
3. `ResetForeground(true)` destroys the current foreground (Dispose does not implicitly persist)
4. `LoadAndMountForeground(newLevelId)` creates and mounts the new foreground (`CreateForegroundSession` → resolves level data from `current/`)
5. `PersistProgress()` writes the new full topology to `current/progress.json`

After the switch, `WriteForegroundTopology` writes the new foreground and all surviving background sessions into the progress blackboard topology; a subsequent `PersistProgress()` lands this topology together with the progress state machines to disk.

### Shutdown

- Dispose cascade: SessionRun → SessionManager → ProgressRun → SystemRun
- `SessionRun.Dispose` uses a two-phase flag: `_disposing` is set first (re-entrancy guard), BeforeQuit hooks run while session resources are still accessible, strategies are released, then `try/finally` guarantees the scene collection is cleared and the blackboard cleared, and only then is `_disposed` set (external access formally forbidden)
- Cleanup operations in Dispose do not catch exceptions: if `StateMachines.Clear()`, `ReleaseAllEntities`, `RemoveAllEntities`, or `Blackboard.Clear()` throws, the exception propagates directly to the caller — no `firstError` accumulation and no `AggregateException` wrapping
- Exceptions from `SessionManager.Clear()` and `DeleteCurrentDirectory()` in `ProgressRun.Dispose` likewise propagate directly and are not silently swallowed
- Pre-shutdown data saving is the application layer's explicit responsibility via `RequestSaveGame`; the `current/` directory is a temporary work area safely cleaned up on exit

## Design Decisions

### Why ProgressRun uses partial classes for persistence and session loading

`ProgressRun` splits persistence logic (`SaveCoordinator`) and session loading logic (topology codec, background session creation) into separate files via partial classes, keeping the main file focused on core orchestration. `SaveCoordinator` is an independent class `Origo.Core.Save.SaveCoordinator` (not a nested class of `ProgressRun`), making save orchestration independently unit-testable.

### Why Dispose does not auto-persist

Persistence is entirely the caller's explicit responsibility; `SessionRun.Dispose` and `ProgressRun.Dispose` do not trigger auto-persist. If Dispose auto-wrote to disk:
- it would write to `current/` only to be deleted by `DeleteCurrentDirectory()` — wasted I/O
- `BeforeSave` hooks would run on entities about to be destroyed — wrong semantics with side-effect risk

Therefore:
- User saves: `RequestSaveGame` → `BuildSavePayload` → `WriteSavePayloadToCurrentThenSnapshot`
- Level switch: `SwitchForeground` **explicitly** calls `PersistForegroundLevelState` before destroying the old foreground
- Exit/destroy: cleanup only, no persistence

This ensures every persistence path has explicit semantics and a traceable call chain.

### Why loading a save does not roll back ProgressRun's in-memory state on failure

`ProgressRun.LoadFromPayload` operates on a brand-new `ProgressRun` just created by `SndContext` (create-then-load: `CreateProgressRun` then `LoadFromPayload`), and the on-disk `current/` has already received the complete payload before deserialization. So if deserialization or session mounting fails midway, the half-deserialized state belongs to a `ProgressRun` not yet in service: it does not pollute existing runtime state and the disk data stays intact. After failure that `ProgressRun` has no foreground session, so any later `PersistProgress`/`BuildSavePayload` immediately throws `InvalidOperationException` for the missing foreground session (fail-fast) instead of silently landing a half state; `LoadFromPayload` itself is idempotent and can be re-run to overwrite. On these three grounds the load-failure path does not roll back in-memory state.

### Why foreground session key fixed as `__foreground__`

Foreground and background sessions share the same `ISessionRun` interface; the only differences are the internal implementation (how `ISndSceneHost` is injected) and the key name. A fixed key eliminates the "find the foreground" branch — look it up by constant key directly from the SessionManager.

### Why runtime containers are separated by layer

Each layer container (`SystemRuntime`, `ProgressRuntime`, etc.) exposes only its own and lower layers' capabilities; upper layers cannot reach lower-layer implementation details. For example, strategies can only operate on sessions through `ISessionRun` and cannot access `ProgressRun` internals.

### Why PersistProgress and WriteForegroundTopology write the full session topology

The session topology records the complete relation of foreground and all background session keys, levels, and sync modes. Writing only foreground info would leave the topology string in the progress blackboard without background sessions, so `progress.json` would lose background session markers after a switch — the background sessions survive in memory, but a crash-restart cannot restore them. Writing the full topology keeps the progress blackboard a restorable snapshot of the current runtime state.

### Why RequestSwitchForegroundLevel executes in the system deferred queue

Level switching is a composite save-destroy-load operation that should run after business logic, FIFO with Save operations. Placing it in the System Deferred queue ensures: the same frame's Save request writes `current/` first, and the subsequent Switch's `LoadAndMountForeground` finds the data when resolving from `current/`. If Switch ran in the Business Deferred queue, it would try to load the target level before Save had executed, falling back to an empty load when `current/` has no data.

### Why levelId must be globally unique

Each levelId maps to a `current/level_{id}/` directory and a key in `SaveGamePayload.Levels`. If two sessions hold the same levelId, the later writer overwrites the former's data on persist, and both read the same overwritten payload on load. Therefore `SessionManager` validates levelId uniqueness when creating sessions — a conflict throws `InvalidOperationException` immediately.

`SwitchForeground` automatically detects whether a background session holds the target `levelId` before creating the new foreground. On conflict it calls `PersistSession` to save the background data, then `DestroySession` to destroy that background, so `LoadAndMountForeground` can create the new foreground without conflict. Callers need no manual conflict cleanup.

---
[↑ Back to Runtime](../README.en.md)
