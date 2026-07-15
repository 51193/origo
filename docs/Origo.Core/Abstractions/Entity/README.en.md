<!-- docsync-pair: Origo.Core/Abstractions/Entity/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Entity (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd/Entity](../../Snd/Entity/README.en.md)

## Overview
Defines the abstract interface system for SND entities following ISP. Five capabilities (data, nodes, passive strategies, active strategies, observer strategies) are split into independent interfaces, composed by `ISndEntity`. `IEntityLifecycle` is defined separately for framework/adapter-layer shared implementation.

## Included Files

| File | Responsibility |
|------|------|
| `ISndDataAccess.cs` | Data write, safe read, strong-assertion read |
| `ISndNodeAccess.cs` | Node query and enumeration |
| `ISndStrategyAccess.cs` | Passive strategy add/remove |
| `ISndActiveStrategyAccess.cs` | Active strategy add/remove/invoke |
| `ISndObserverStrategyAccess.cs` | Observer strategy mount/unmount (self and cross-entity) |
| `ISndEntity.cs` | Composite: inherits five capability interfaces + `Name` + `IsPendingKill` + `OwningSession` |
| `IEntityLifecycle.cs` | Framework-internal lifecycle: phased recovery/hook/teardown methods |

## Interface Details

### ISndDataAccess

| Member | Description |
|------|------|
| `SetData<T>(name, value)` | Write named data; skips notification if value unchanged |
| `TryGetData<T>(name)` | Safe read, returns `(found, value?)` |
| `GetData<T>(name)` | Strong-assertion read; throws `InvalidOperationException` if missing/type-mismatch |

### ISndNodeAccess

| Member | Description |
|------|------|
| `GetNode(name)` | Get node handle by name |
| `GetNodeNames()` | Enumerate all mounted node names |

### ISndStrategyAccess

| Member | Description |
|------|------|
| `AddStrategy(index)` | Dynamically add strategy |
| `RemoveStrategy(index)` | Remove strategy |

### ISndActiveStrategyAccess

| Member | Description |
|------|------|
| `AddActiveStrategy(index)` | Add active strategy |
| `RemoveActiveStrategy(index)` | Remove active strategy |
| `InvokeStrategy(strategyIndex, input?)` | Invoke strategy and return result |

### ISndObserverStrategyAccess
Unified entry for entity observation. Observer strategies mounted by index; framework auto-wires data changes and callbacks. Self-observation and cross-entity use shared API and binding topology format.

| Member | Description |
|------|------|
| `MountObserverStrategy(targetName, observerIndex)` | Mount by name |
| `UnmountObserverStrategy(targetName, observerIndex)` | Unmount, triggers `OnUnmounted` |
| `MountObserverStrategy(target, observerIndex)` | Mount with resolved target entity (preferred for cross-entity) |
| `UnmountObserverStrategy(target, observerIndex)` | Unmount by target entity |

### ISndEntity
Composite interface with own members:

| Member | Description |
|------|------|
| `Name { get; }` | Stable entity identifier |
| `OwningSession { get; }` | Belonging ISessionRun (non-null, fail-fast). Auto-bound on creation |
| `IsPendingKill { get; }` | Marked pending destruction. Strategies check before operating |

### IEntityLifecycle
**Framework-internal interface** for two-phase batch orchestration. Business code must not call directly.

| Method | Phase | Description |
|------|------|------|
| `RecoverForLifecycle(meta)` | Phase 1 | Recover Name + Data + Node + Strategies, no hooks |
| `FireAfterSpawnHooks()` | Phase 2 | Trigger AfterSpawn |
| `FireAfterLoadHooks()` | Phase 2 | Trigger AfterLoad |
| `FireBeforeSaveHooks()` | Phase 2 | Trigger BeforeSave |
| `FireBeforeQuitHooks()` | Phase 2 | Trigger BeforeQuit |
| `FireBeforeDeadHooks()` | Phase 2 | Trigger BeforeDead |
| `ReleaseStrategiesOnly()` | Phase 3 | Release EntityStrategy + ActiveStrategy + ObserverStrategy references |
| `TeardownOnly()` | Phase 3 | Release Node + Data resources |
| `BuildMetaData()` | Serialization | Build metadata (no BeforeSave) |

## Design Decisions

### Why split into five access interfaces
Clear responsibility separation and internal testability. External code depends on `ISndEntity` directly.

### Why active strategies separated into ISndActiveStrategyAccess
Containers are independent: active uses Dictionary for O(1) lookup (no frame iteration); passive uses sorted list per priority.

### Why observation unified as observer strategies
Stateless, poolable, with binding topology serializable alongside entities and auto-restored on load. No manual reconnect in AfterLoad needed.

### Why IEntityLifecycle defined separately
Keeps `ISndEntity` business-facing without lifecycle orchestration. Allows framework and adapter entities to share the same interface.

### Why TryGetData uses found/value tuple
See Blackboard design decision.

### Why observer hook signatures include target parameter
Stateless strategies receive both `entity` (observer) and `target` (observed) without holding references. Self-observation: `entity == target`.

---
[↑ Back to Abstractions](../README.en.md)
