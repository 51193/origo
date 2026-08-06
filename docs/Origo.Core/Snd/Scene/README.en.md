<!-- docsync-pair: Origo.Core/Snd/Scene/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Scene

> [↑ Back to Snd](../README.en.md)

## Overview

SND scene host implementation layer. Provides two implementations of `ISndSceneHost`: a full in-memory host (for background sessions) and a lightweight stub host (for testing and device-independent offline construction). The scene host is solely responsible for entity container management (create/lookup/remove/frame update) and **does not trigger any strategy lifecycle hooks**. Hook orchestration belongs to higher-level session lifecycle: `SndEntityFactory` is responsible for AfterSpawn after spawn, `SessionRun` for batch hooks during load/save/quit/kill phases, and `SessionManager` drives multi-session frame updates and end-of-frame harvesting.

## Included Files

| File | Responsibility |
|------|---------------|
| `SndEntityFactory.cs` | Public static utility: `Spawn(host, meta)` = `host.CreateEntity` + trigger AfterSpawn; `SpawnMany(host, metas)` = two-phase (create all first, then uniformly trigger AfterSpawn) |
| `FullMemorySndSceneHost.cs` | Full in-memory scene host, creates real SndEntity, holds per-scene-host observer topology, supports owning session binding |
| `StubSndSceneHost.cs` | Lightweight stub scene host, uses simple StubSndEntity (no strategies/nodes), for unit tests and LevelBuilder offline construction |
| `ISndContextAttachableSceneHost.cs` | Interface: allows binding `ISndContext` to the host during session construction (`BindContext`) |
| `IObserverTopologyHost.cs` | `internal` interface: exposes the host's per-scene-host observer topology (`ObserverTopology`), used by `SessionRun`/`SessionManager` to orchestrate cross-entity observer binding teardown and load-time recovery |
| `NullNodeFactory.cs` | In-memory node factory, creates no-op handles |

> The owning session binding interface [`IOwningSessionBindable`](../../Abstractions/Scene/README.en.md) is defined in the Abstractions/Scene layer and implemented by `FullMemorySndSceneHost` (and the adapter layer's `GodotSndManager`).

## Module Details

### Strategy Lifecycle Hook Orchestration Attribution

The scene host only creates/recovers/contains entities; hooks are uniformly triggered in batch by callers at the appropriate phase:

| Phase | Orchestrator | Description |
|-------|-------------|-------------|
| AfterSpawn | `SndEntityFactory.Spawn` / `SpawnMany` | After creation (batch creates all first), uniformly `FireAfterSpawnHooks()` |
| AfterLoad + observer binding recovery | `SessionRun.LoadFromPayload` | First `FireAfterLoadHooks()`, then wire from `ObserverIndices` via host topology `RecoverBindingsFor` |
| BeforeSave | `SessionRun` (`BuildLevelPayload`) | `FireBeforeSaveHooks()` on all entities before serialization |
| Observer bidirectional teardown + BeforeDead + physical removal | `SessionRun.KillPending` | Called by `SessionManager.KillPendingAllSessions` at end of frame for each session |
| BeforeQuit + release + clear | `SessionRun.Dispose` | `FireBeforeQuitHooks()` → `ReleaseStrategiesOnly` + `TeardownOnly` → `SceneHost.RemoveAllEntities()` |
| Frame update | `SessionManager.ProcessAllSessions` | Pass-through `SceneHost.ProcessAll(delta)` for sessions participating in Process |

`SceneHost.CreateEntity` is only responsible for creation and recovery (`RecoverForLifecycle`); it does not trigger any hooks.

### FullMemorySndSceneHost

The default scene host for background sessions. Key characteristics:
- Creates full `SndEntity` via `SndWorld.CreateEntity` (not a simple in-memory entity)
- Implements `IObserverTopologyHost`: creates per-scene-host `ObserverTopology` on `BindWorld` and injects it into every entity it creates; all observer bindings for entities within this host are centralized in this topology
- Implements `IOwningSessionBindable`: during `SessionRun` construction, the owning session is bound via `SetOwningSession`; thereafter, every entity created by `CreateEntity` is auto-bound to that session's `OwningSession` via `entity.BindSession` after `RecoverForLifecycle`
- Implements `ISndContextAttachableSceneHost`: `ISndContext` is injected via `BindContext` during session construction
- Requires deferred binding of `SndWorld` and `ISndContext` (accommodating `OrigoRuntime`'s two-phase construction)
- **Entity container management**: only responsible for creating, finding, and removing entities; does not trigger any strategy hooks
- **CreateEntity**: creates entity, recovers data/strategies/nodes (via `RecoverForLifecycle`), binds owning session; does not trigger AfterSpawn hooks
- **RecoverFromMetaList**: only recovers entity data/strategies/nodes (does not trigger AfterLoad hooks), used for save loading scenarios. First sets entity name via `entity.Name = metaData.Name`, then registers entity in the internal collection, and finally calls `RecoverForLifecycle(meta)`. Therefore, before hooks execute, `FindByName` can find all registered entities.
- **RemoveEntity**: only removes the entity from the collection; does not release strategy references, does not release engine resources, and does not trigger hooks (strategy release and resource reclamation are completed by `SessionRun.KillPending` before this method is called)
- **RemoveAllEntities**: only clears the internal collection
- **ProcessAll**: iterates all alive entities with an index loop (the host container must not be modified during iteration)

### StubSndSceneHost

Lightweight implementation using an embedded `StubSndEntity` class directly. This entity does not support node access or strategy execution (node access throws, strategy/observer operations are silent no-ops); only basic key-value data access is supported. Used for unit tests and `LevelBuilder` offline construction.

> `StubSndSceneHost`'s naming expresses its "stub" semantics — a lightweight placeholder implementation without strategies/nodes, not a full in-memory host.

### NullNodeFactory / NullNodeHandle

Used by `FullMemorySndSceneHost`. `Create()` returns a handle not bound to any engine node; all operations (`Free`, `SetVisible`) are no-ops. Core-layer background sessions do not need actual rendered nodes.

## Design Decisions

### Why the scene host does not trigger strategy hooks

All strategy lifecycle hook triggering is uniformly orchestrated by session lifecycle (`SndEntityFactory` / `SessionRun` / `SessionManager`). The scene host is solely responsible for entity container management. This separation of concerns ensures:

- The Godot adapter layer (`GodotSndManager`) does not participate in strategy lifecycle management
- Batch operations can proceed in two phases: "create/recover all" and then "trigger all hooks"
- During hook triggering, all entities are fully recovered and registered in the lookup collection, enabling loading-order-independent cross-entity interoperability

### Why two scene hosts are needed

`FullMemorySndSceneHost` provides full strategy lifecycle support but requires upstream dependencies of `SndWorld` and `ISndContext`; `StubSndSceneHost` has zero dependencies and is fully self-contained but cannot run strategies. The former is used for background sessions; the latter for testing and offline construction (tests typically only need data flow without strategy execution).

### Why spawn logic is centralized in SndEntityFactory

`SndEntityFactory.Spawn/SpawnMany` is the single authoritative implementation of "create entity + trigger AfterSpawn". `ISessionRun.Spawn/SpawnMany` delegates to it; adapter layers and auto-initializers also reuse it. A single source ensures that adjusting spawn behavior only requires one change, avoiding divergence from multiple spawn logic paths. `SndEntityFactory.SpawnMany` uses a two-phase approach (all created first, then uniformly trigger hooks), making all sibling entities visible during AfterSpawn hooks.

### Why FullMemorySndSceneHost uses deferred binding for World/Context

`OrigoRuntime`'s two-phase construction (first create host, then inject runtime dependencies) requires hosts to support deferred binding. Providing these dependencies in the constructor would create a cycle: `SndWorld` creation requires `OrigoRuntime`, and host creation happens before `SndWorld`.

### Why entities are registered in the lookup collection before hooks are triggered

Strategy hooks may need to reference sibling entities during creation (e.g., using `FindByName` to find dependency entities, mounting cross-entity observer bindings). Registering first then triggering hooks ensures all entities are always retrievable throughout the entire lifecycle. In batch mode, all entities are registered first, then hooks are triggered uniformly, further strengthening this guarantee.

### Why observer topology is per-scene-host

Observer bindings form a session-internal directed graph (target resolution always within a single host's `FindByName` scope). Each host that creates real `SndEntity` instances (`FullMemorySndSceneHost`, `GodotSndManager`) holds an `ObserverTopology` and implements `IObserverTopologyHost`, with the topology sharing the host's lifecycle. `SessionRun` obtains the host topology via this interface, orchestrating kill/clear bidirectional teardown and load-time recovery for **all entity types** — bare `SndEntity` and adapter wrapper entities (e.g., Godot foreground entities) — since teardown resolves bindings by entity name and operates through the `ISndEntityRawSubscription` interface, independent of the concrete entity type. `StubSndSceneHost` does not create real entities and does not implement this interface. Centralizing to host-level topology means entities no longer need to expose their internal observer managers in reverse to accomplish cross-entity wiring, teardown, and recovery.

### Why entities bind to owning session at creation time

Strategy hooks learn which session they belong to via `entity.OwningSession` (rather than reverse-querying a global context). Ownership is determined at entity **creation time**: during `SessionRun` construction, the session binds itself to the host via `IOwningSessionBindable.SetOwningSession`; thereafter, every entity created by the host's `CreateEntity` is bound to that session via `entity.BindSession` after `RecoverForLifecycle`.

Thus, regardless of whether the entity is created through the `SessionManager` orchestration path or **directly spawned into some background session's host** (e.g., pre-building a world in the background before switching to foreground), its `OwningSession` always points to the session that truly owns it; hook attribution will not be misjudged. `StubSndSceneHost` does not create real entities and does not implement this interface.

---

[↑ Back to Snd](../README.en.md)
