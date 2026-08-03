<!-- docsync-pair: Origo.Core/Runtime/Lifecycle/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Lifecycle

> [↑ Back to Runtime](../README.en.md)

## Overview
The implementation layer for the runtime's four-tier lifecycle. Defines the complete startup, running, and shutdown flow from system to session level.

## Included Files

| File | Responsibility |
|------|------|
| `SystemParameters.cs` | System-level construction parameters |
| `SystemRuntime.cs` | System-level runtime container |
| `SystemRun.cs` | System startup: creates SndWorld → ProgressRuntime |
| `ProgressRun.cs` | Process-level main logic: level switching, save/load, session orchestration |
| `SessionManager.cs` | Complete ISessionManager implementation |
| `SessionRun.cs` | ISessionRun implementation |
| `SessionTopologyCodec.cs` | Session topology codec |
| `EmptySessionManager.cs` | No-op session manager (testing) |
| `RunStateScope.cs` | Runtime state scope utility |
| `TopologyInvariant.cs` | internal — Topology invariant validation utility |

## Four-Tier Container Model

```
SystemRuntime → SystemRun → ProgressRuntime → ProgressRun → SessionManager → SessionRun (foreground + background)
```

Each tier container holds core object references for its layer. SystemRuntime holds SndWorld, ConverterRegistry, AdapterSceneHost, and the scheduler (the scheduler instance is actually held by OrigoRuntime and injected via IScheduler). ProgressRuntime holds Logger, StorageService, SndWorld, AdapterSceneHost, StateMachineContext, SndContext, and SavePathPolicy — the progress blackboard and state machine container are held by SessionManager/ProgressRun, and SaveContext is a transient object created on demand. SessionManagerRuntime holds runtime dependencies such as SndWorld, SndContext, and ProgressBlackboard; ISessionManager is held by ProgressRun.

## Key Lifecycle Flows

### Startup
1. `OrigoAutoHost` creates `SystemParameters` → `new SystemRun(...)` constructor
2. `SystemRun` creates `SndWorld` → `ProgressRuntime`
3. Loading initial level: `ProgressRun` → `SessionManager` → `SessionRun`

### Runtime
- Per-frame: `IScheduler.Tick()` → deferred queue → `SessionManager.ProcessAllSessions()` → `KillPendingAllSessions()` → system queue → console

### Persistence
- `SaveCoordinator`: standalone class for building save payloads
- Save: `ISndSaveOperations.RequestSaveGame` → `SaveCoordinator.BuildSavePayload` → two-phase write
- Load: `ISndSaveOperations.RequestLoadGame` → restore blackboard + scene
- BeforeSave hooks batch-triggered by `SessionRun.BuildLevelPayload` before serialization. The full-save path (`SaveCoordinator.BuildSavePayload`) also batch-triggers `FireBeforeSaveHooks` before serializing the foreground scene, matching the background session semantics. This ensures every strategy has a final chance to flush in-memory state into entity Data before saving. Overwrites of framework-managed blackboard keys (such as `SessionTopology`) inside hooks are overridden by the framework-computed value before serialization, so such writes take no effect.
- AfterLoad hooks batch-triggered after recovery; all entities fully restored before any hook fires

### Level Switching
Composite save-destroy-load: persist old foreground → destroy conflicting background → destroy old foreground → create new foreground → persist topology.

### Shutdown
- Dispose cascade: SessionRun → SessionManager → ProgressRun → SystemRun
- Dispose does not auto-persist. Saving must be explicit via `RequestSaveGame`.

## Design Decisions

### Why Dispose does not auto-persist
Previously Dispose triggered auto-persist writes to `current/` immediately followed by deletion, wasting I/O with incorrect semantics. Now persistence is entirely explicit.

### Why loading a save does not roll back on failure
`LoadFromPayload` operates on a freshly-created ProgressRun not yet in service. Disk data remains intact. No pollution of existing runtime state.

### Why foreground session key fixed as `__foreground__`
Eliminates branching logic; foreground and background share the same interface.

---
[↑ Back to Runtime](../README.en.md)
