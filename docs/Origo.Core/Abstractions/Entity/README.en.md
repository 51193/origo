<!-- docsync-pair: Origo.Core/Abstractions/Entity/README -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Entity (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd/Entity](../../Snd/Entity/README.en.md)

## Overview
Defines the abstract interface system for SND entities following ISP. Five capabilities (data, nodes, passive strategies, active strategies, observer strategies) are split into independent interfaces, composed by `ISndEntity`. `IEntityLifecycle` is an `internal` interface defined separately for framework/adapter-layer shared implementation.

## Included Files

| File | Responsibility |
|------|------|
| `ISndDataAccess.cs` | Data write, safe read, strong-assertion read |
| `ISndNodeAccess.cs` | Node query and enumeration |
| `ISndStrategyAccess.cs` | Passive strategy add/remove |
| `ISndActiveStrategyAccess.cs` | Active strategy add/remove/invoke |
| `ISndObserverStrategyAccess.cs` | Observer strategy mount/unmount (self and cross-entity) |
| `ISndEntity.cs` | Composite: inherits five capability interfaces + `Name` + `IsPendingKill` + `OwningSession` |
| `IEntityLifecycle.cs` | `internal` framework-internal lifecycle: phased recovery/hook/teardown methods. Implemented by `SndEntity` (Core in-memory entity) and adapter-layer entities (bridging to an inner `SndEntity`); adapter and test projects access it via `InternalsVisibleTo` |

## Interface Details

### ISndDataAccess

| Member | Description |
|------|------|
| `SetData<T>(name, value)` | Write named data; skips notification if value unchanged |
| `TryGetData<T>(name)` | Safe read, returns `(found, value?)`. For keys that may be absent or of unknown type |
| `TryGetData<T>(name, out value)` | Out-parameter variant of the safe read, supporting the `if (TryGetData("hp", out var hp))` idiom without discarding the found flag |
| `GetData<T>(name)` | Strong-assertion read; throws `InvalidOperationException` if missing/type-mismatch. For callers that know the data must exist (fail-fast) |

> Data change observation is not on this interface. Reacting to data changes goes through `ObserverStrategyBase.OnDataChanged`, mounted via `ISndObserverStrategyAccess`. See [Snd/Strategy](../../Snd/Strategy/README.en.md).

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

Unified entry point for entity observation. Observer strategies (`ObserverStrategyBase`) mount onto target entities by strategy index; the framework auto-wires the target's data changes and calls back `OnMounted` / `OnUnmounted` on mount/unmount. Self-observation (`targetName == entity.Name`) and cross-entity observation use the same API and the same binding topology format; bindings are persisted via `StrategyMetaData.ObserverIndices` and automatically restored on load.

| Member | Description |
|------|------|
| `MountObserverStrategy(targetName, observerIndex)` | Mount by name. `targetName == own Name` is self-observation; cross-entity name resolution needs the scene host, so use the `ISndEntity` overload instead |
| `UnmountObserverStrategy(targetName, observerIndex)` | Unmount by name, triggers `OnUnmounted` |
| `MountObserverStrategy(target, observerIndex)` | Mount with resolved target entity (preferred for cross-entity; the target is obtained via `entity.OwningSession.FindByName(name)`) |
| `UnmountObserverStrategy(target, observerIndex)` | Unmount by target entity, triggers `OnUnmounted` |

### ISndEntity

`ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess, ISndActiveStrategyAccess, ISndObserverStrategyAccess`

Composite interface with own members:

| Member | Description |
|------|------|
| `Name { get; }` | Stable entity identifier |
| `OwningSession { get; }` | The entity's owning `ISessionRun` (non-null, fail-fast): auto-bound by the scene host at creation (`FullMemorySndSceneHost.CreateEntity` / `GodotSndManager.CreateEntity` / `RecoverFromMetaList` paths). Strategies reach their own session directly (same-session operations) and other sessions via `OwningSession.SessionManager`. Accessing it before binding throws `InvalidOperationException` |
| `IsPendingKill { get; }` | Marked pending destruction; the framework destroys the entity at end of frame (after the business deferred queue, before the system deferred queue). Strategies should check this flag before operating on an entity |

### IEntityLifecycle

**`internal` framework-internal interface** for two-phase batch orchestration by `SndEntityFactory` and `SessionRun`, also implemented by adapter-layer entities (such as `GodotSndEntity`) for bridge delegation. Business code must not call it directly and cannot reference it outside the Core assembly (adapter and test projects access it via `InternalsVisibleTo`).

| Method | Phase | Description |
|------|------|------|
| `RecoverForLifecycle(meta)` | Phase 1 | Recover Name + Data + Node + EntityStrategy + ActiveStrategy; does not trigger any hooks. On failure, rolls back atomically across phases: acquired strategy references are returned to the pool and created nodes are freed before the exception propagates |
| `FireAfterSpawnHooks()` | Phase 2 | Trigger AfterSpawn |
| `FireAfterLoadHooks()` | Phase 2 | Trigger AfterLoad |
| `FireBeforeSaveHooks()` | Phase 2 | Trigger BeforeSave |
| `FireBeforeQuitHooks()` | Phase 2 | Trigger BeforeQuit |
| `FireBeforeDeadHooks()` | Phase 2 | Trigger BeforeDead |
| `ReleaseStrategiesOnly()` | Phase 3 | Release EntityStrategy + ActiveStrategy references |
| `TeardownOnly()` | Phase 3 | Release Node + Data resources |
| `TeardownObserverBindings()` | Phase 3 | Unload all observer bindings of this entity via the host `ObserverTopology`: unsubscribe target data channels, fire `OnUnmounted`, and release strategy-pool references |
| `BuildMetaData()` | Serialization | Build metadata (no BeforeSave) |

Implementers: `SndEntity` (Core in-memory entity) and adapter-layer entities (such as `GodotSndEntity`, which bridges by delegating to an inner `SndEntity`).

`ISndEntityRawSubscription` (`Origo.Core/Snd/Entity/`) provides the raw `TypedData`-level data subscription interface — `SubscribeDataRaw`, `UnsubscribeDataRaw`. It is used by the framework-internal `ObserverTopology` to wire observer strategies into a target entity's data changes and is not exposed to business strategy code.

## Design Decisions

### Why split into five access interfaces

The five sub-interfaces (`ISndDataAccess`, `ISndNodeAccess`, `ISndStrategyAccess`, `ISndActiveStrategyAccess`, `ISndObserverStrategyAccess`) form the composition contract of `ISndEntity`, aiming for clear responsibility separation and internal testability. External code depends on `ISndEntity` directly without referencing the sub-interfaces; the ISP split serves framework-internal implementation clarity and fine-grained test mock control.

### Why active strategies separated into ISndActiveStrategyAccess

Active and passive strategies share the `BaseStrategy` and `SndStrategyPool` infrastructure but keep fully independent containers: active uses a Dictionary for O(1) index lookup without frame iteration; passive uses a sorted list iterated by priority every frame. The interface separation keeps consumers from coupling to unnecessary strategy types.

### Why observation unified as observer strategies

Data change responses and lifecycle observation converge into a single first-class strategy type, `ObserverStrategyBase`, mounted by strategy index through `ISndObserverStrategyAccess` instead of exposing delegate subscription APIs on the entity. Benefits: observation logic is stateless and poolable like any strategy; binding topology serializes alongside entities (`ObserverIndices`) and auto-restores on load, so no manual reconnection in `AfterLoad`; wiring, unwiring, and persistence are governed centrally by `ObserverTopology`, eliminating error-prone delegate identity matching and manual unsubscription.

### Why IEntityLifecycle defined separately

Lifecycle hook triggering is controlled by the framework layer. Putting `RecoverForLifecycle`, `FireXxxHooks`, `ReleaseStrategiesOnly`, `TeardownOnly` in a separate interface:
- keeps `ISndEntity` (business-facing) free of lifecycle orchestration capability
- lets `SndEntityFactory` and `SessionRun` orchestrate batches uniformly through `IEntityLifecycle`
- lets Godot adapter's `GodotSndEntity` implement the interface by delegating to an inner `SndEntity`

### Why TryGetData uses found/value tuple

See the [same design decision for Blackboard](../Blackboard/README.en.md#why-generic-tryget-instead-of-object).

### Why observer hook signatures include target parameter

Strategy instances are shared across entities via `SndStrategyPool` and must not hold entity references in instance fields. The `OnMounted` / `OnDataChanged` / `OnUnmounted` callbacks receive both `entity` (the observer) and `target` (the observed), letting stateless strategies distinguish the two without holding references. Self-observation: `entity == target`.

---
[↑ Back to Abstractions](../README.en.md)
