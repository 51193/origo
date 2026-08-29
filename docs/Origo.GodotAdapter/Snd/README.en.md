<!-- docsync-pair: Origo.GodotAdapter/Snd/README -->
<!-- docsync-revision: 22 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Snd

> [↑ Back to Origo.GodotAdapter](../README.en.md) · [↔ Core: Snd](../../Origo.Core/Snd/README.en.md)

## Overview

The concrete implementation of the SND entity system in the Godot engine. Bridges Core's abstract `ISndEntity` / `INodeHandle` / `INodeFactory` / `ISndSceneHost` with Godot's `Node` / `PackedScene` lifecycle.

## Files

| File | Responsibility |
|------|------|
| `GodotSndManager.cs` | Godot scene host: manages GodotSndEntity collection, implements ISndSceneHost. Entity frame processing is driven uniformly by Core's `SessionManager.ProcessAllSessions` via `SceneHost.ProcessAll` through `IOrigoFrameDriver.DriveFrame` |
| `GodotSndEntity.cs` | Godot entity: binds Core SndEntity to Godot Node lifecycle, delegates all ISndEntity calls |
| `GodotPackedSceneNodeFactory.cs` | INodeFactory implementation: creates Godot Nodes via PackedScene.Instantiate |
| `GodotNodeHandle.cs` | INodeHandle implementation: wraps Godot.Node, provides Free / SetVisible / UnsafeGetNode |
| `SndEntityNodeExtensions.cs` | Adapter-layer convenience extensions: `GetNativeNode()` (extracts Godot Node from INodeHandle), `GetNodeFromSnd<T>()` (resolves via the SND node registry by logical name and casts). Physically located at project root `Origo.GodotAdapter/SndEntityNodeExtensions.cs` (not in Snd/ subdirectory), namespace belongs to `Origo.GodotAdapter` |
| `SndEntityCollection.cs` | internal — pure C# entity collection: entity add/remove, batch recovery rollback, kill marking, frame processing orchestration; no Godot dependency, covered directly by unit tests |

## Module Details

### GodotSndManager

The adapter layer's core entry point node (`[GlobalClass]`), mounted directly in the Godot scene tree:

- **Implements ISndSceneHost (internal)**: CreateEntity / RecoverFromMetaList / RemoveAllEntities (framework-internal lifecycle operation) / RequestKillEntity / RemoveEntity / ProcessAll are **explicit interface implementations**, and `ISndSceneHost` / `ISndSceneAccess` are `internal` — business code can neither call them on the concrete type nor cast through the interfaces to bypass orchestration. Public reads go through the public `ISndSceneReadAccess` (`GetEntities` / `FindByName`). `RemoveAllEntities()` uses `Free()` (immediate release) rather than `QueueFree()`, since Core guarantees it is called at a safe lifecycle point.
- **Implements ISndContextAttachableSceneHost (internal)**: `BindContext` is an **explicit interface implementation** and the interface is `internal` — context binding is a framework-orchestrated startup write path (driven by `SessionRun` construction / the bootstrap flow); business code cannot rebind the context on the concrete type
- **Startup wiring sealed**: `BindRuntimeDependencies` is `internal` — runtime dependency binding (World + Logger) is also framework-orchestrated startup wiring (driven by `OrigoAutoHost` in the bootstrap flow); business code cannot rebind runtime dependencies on the concrete type
- **Implements IObserverTopologyHost** (internal): Exposes the per-scene-host `ObserverTopology` for Core observer mount/unmount orchestration
- **Implements IOwningSessionBindable** (internal): `SetOwningSession` binds a session to the host for the Core session-creation flow
- **Collection logic delegated**: entity add/remove, batch recovery rollback, kill marking, and frame processing orchestration live in pure C# `SndEntityCollection<T>` (internal, no Godot dependency) and are covered by unit tests directly; GodotSndManager only bridges the collection to the Godot node tree (`AddChild` / `RemoveChild` / `Free` injected via the `DetachAndFree` callback)
- **`_ExitTree` out-of-contract fallback**: When this manager node leaves the scene tree without the framework's session teardown (a scene switch, or business code calling `RemoveChild`/`Free` directly), `_ExitTree` releases the Core-side state: for every entity it tears down observer bindings (unsubscribe + `OnUnmounted` + pool reference return) and releases strategies, then clears the entity collection (the engine already handles the physical node-tree teardown). Hook failures only log a warning and never interrupt the cleanup (this path exists for out-of-contract use); the framework path empties the collection before freeing nodes, so `_ExitTree` is idempotent and no-ops there.
- **Rollback mechanism**: In `RecoverFromMetaList`, if an entity fails to load, the collection rolls back and releases all already-created entities (via the staged list in `SndEntityCollection`)
- **GetEntities()**: Returns a **snapshot** (not a live view) — consistent with the Core hosts' contract: iterating while the host is mutated does not throw "collection was modified", and the snapshot cannot be downcast to the mutable backing list (no manual mutations bypassing collection management)
- **BuildMetaList()**: Calls entities' `BuildSndMetaData()` to collect metadata
- **ProcessAll(delta)**: Implements the unified entry for `ISndSceneHost.ProcessAll`, called by Core's `SessionManager.ProcessAllSessions`, driving frame processing for every entity in the collection. Like `FullMemorySndSceneHost`, a container mutation during processing (e.g. a strategy calling `Spawn` inside `Process`) throws `InvalidOperationException` instead of silently skipping or double-processing entities

### GodotSndEntity

A Godot wrapper for Core `SndEntity` (`[GlobalClass]`):

> **`[GlobalClass]` limitation**: `GodotSndEntity`'s only constructor is internal (five-parameter dependency injection) and there is no parameterless constructor — it cannot be created manually in the editor or instantiated from a `.tscn`. Entities must be created via `GodotSndManager` (`CreateEntity`). This is deliberate: `GodotSndEntity`'s dependencies (`SndWorld`, `ISndContext`, logger, observer topology) can only be injected by the framework; `[GlobalClass]` only makes the type recognizable to the Godot editor (exported properties / type registration).

- **Lazy initialization**: `_entity` is created on first access via `SndWorld.CreateEntity`
- **Lifecycle separation**: `DetachFromManager()` sets the released flag and nulls the entity reference; engine-level release (`RemoveChild`/`Free`) is performed by the GodotSndManager `DetachAndFree` callback (the `SndEntityCollection` "engine work delegated via callback" contract)
- **BuildSndMetaData()**: Public wrapper for `BuildMetaData()`, used by GodotSndManager to collect metadata
- **IEntityLifecycle implementation**: Each method includes an `EnsureEntity()` guard (creates before use)
- **StableName**: Independently stores the entity's stable name (Godot Node's Name may be auto-modified with suffixes due to name conflicts)
- **GetNodeFromSnd<TNode>**: Godot-specific extension — looks up by logical name in the entity's SND node registry and casts to a concrete Godot type

### GodotPackedSceneNodeFactory

- **Create**: `ResourceLoader.Load<PackedScene>(resourceId)` → `Instantiate<Node>()` → `parent.AddChild(node)` → returns GodotNodeHandle
- resourceId is resolved on the Core side (`SndWorld` passes the `SndMappings.ResolveSceneAlias` delegate when creating entities), so the factory always receives the final path
- Loaded `PackedScene` instances are cached to avoid repeated disk I/O when the same resource is instantiated multiple times

### GodotNodeHandle

- **Name**: The node name cached at construction time, unaffected by Godot releasing the original node
- **Free()** → checks `IsInstanceValid(_node)` then calls `_node.Free()` if valid
- **SetVisible(bool)** → first checks `IsInstanceValid(_node)`, then sets the appropriate Visible property based on node type (`CanvasItem` or `Node3D`); for other node types without a `Visible` property it throws `InvalidOperationException` (fail-fast, no silent no-op)
- **UnsafeGetNode()** → `internal` — returns the underlying `Godot.Node` reference, used only by `SndEntityNodeExtensions.GetNativeNode()`

### SndEntityNodeExtensions

- **GetNativeNode(this INodeHandle)** → safely converts `INodeHandle` to a native `Godot.Node`. Only works when the handle is a `GodotNodeHandle`; returns null otherwise
- **GetNodeFromSnd<TNode>(this ISndEntity, string)** → resolves a node by logical name in the entity's SND node registry and casts to the specified type; an unregistered name throws `InvalidOperationException`, a non-Godot handle or type mismatch returns null. Only works when the entity is a `GodotSndEntity`

## Design Decisions

### Why GodotSndManager does not own a _Process loop

Entity frame processing is a Core orchestration responsibility. If `GodotSndManager` held its own `_Process` loop iterating entities and calling `ProcessSnd(delta)`, it would duplicate Core's frame processing logic and bypass the formal processing pipeline. Therefore frame processing is uniformly executed by Core's `SessionManager.ProcessAllSessions(delta)` via `SceneHost.ProcessAll(delta)`, through `IOrigoFrameDriver.DriveFrame(delta)`. `ProcessSnd` is `internal` — lifecycle orchestration can only be triggered via Core's `ISessionRun` and the batch hook pipeline; external code must not call it through the concrete `GodotSndEntity` type.

### Why GodotSndEntity uses lazy creation for the Core Entity

Core `SndEntity` requires `INodeFactory` injection, and `INodeFactory` needs the GodotSndEntity itself as the parent node. This is a construction ordering problem — GodotSndEntity is created first, then `INodeFactory` is constructed with itself as the parameter, then the Core Entity is created through the factory. Lazy creation resolves this circular dependency.

### Why StableName is independent of Godot Node.Name

If nodes with the same name exist in Godot's scene tree, Godot automatically appends suffixes like `@2`, `@3` to the Name. SND entity lookups depend on stable names and cannot use Godot-modified Names. `StableName` is set at Spawn/Load time and is unaffected by Godot's automatic renaming of `Node.Name`.

### Why RecoverFromMetaList uses a rollback mechanism

If loading 100 entities and the 50th fails, the first 49 created entities are in an incomplete state (AfterLoad hooks may have fired but the scene is incomplete). Rollback releases all of them to prevent residual corrupted entities from contaminating subsequent operations.

### Why DetachFromManager must remove from list before releasing

If the node is directly released during iteration over `_entities`, Godot's node tree changes could cause subsequent iterations to skip entities or double-process. Remove first, release later, ensures safe list iteration.

### Adapter layer entity bridging: Why GodotSndEntity must hand-write forwarding

The code in `GodotSndEntity` (~238 lines) can be broken down into three categories:

| Category | Lines | Share | Notes |
|------|------|------|------|
| Pure forwarding boilerplate | ~120 | 60% | `ISndEntity` (~20 methods), `IEntityLifecycle` (10 methods), `ISndEntityRawSubscription` (2 methods) — all follow the pattern `Entity.Foo(...)`, each method 1–6 lines |
| Engine-specific logic | ~60 | 21% | `StableName` ↔ `Node.Name` sync, `Free()` cleanup, `GetNodeFromSnd<TNode>()` Godot node escape, `EnsureEntity()` lazy creation |
| Infrastructure | ~100 | 34% | Field declarations, constructors, guard methods, usings |

#### Why it cannot be extracted to a base class or auto-generated

**C# single inheritance is the fundamental constraint.** `GodotSndEntity` must inherit `Godot.Node` (`[GlobalClass]` requirement) to be mounted in the Godot scene tree. If Core provided an abstract base class `SndEntityBridge`, the Godot adapter could not simultaneously inherit both the base class and `Node`. The same applies to Unity (must inherit `MonoBehaviour`).

Other technical approaches also lack sufficient ROI:

- **Default interface methods (DIMs)**: Require modifying the contract design of core interfaces like `ISndEntity`, adding abstract properties as indirection access points, which contradicts the current ISP decomposition direction. `IEntityLifecycle`'s explicit interface implementation semantics also conflict with DIMs.
- **Source generator auto-forwarding**: To save ~130 lines of one-time boilerplate, would require a new Roslyn incremental source generator (~200-300 lines) and its ongoing maintenance burden. Entity bridging accounts for only about 5% of total engine adapter effort; the real cost is in the scene host, file system, serialization, and other components.
- **Standalone delegate object** (`SndEntityProxy`): Moving forwarding code from the adapter to a delegate class still requires the adapter to implement `ISndEntity` and forward calls to the delegate object — no boilerplate reduction.

In summary, under the C# single-inheritance constraint, the current hand-written forwarding approach is already optimal and not worth code changes for further optimization.

#### Reference for future adapter layer authors

When a new engine adapter needs to implement its own entity bridge, the following pure forwarding boilerplate can be directly copied (method signatures and implementations are identical, only type names need replacement):

```
ISndEntity:
  SetData / GetData / TryGetData
  GetNode / GetNodeNames
  AddStrategy / RemoveStrategy
  AddActiveStrategy / RemoveActiveStrategy / InvokeStrategy
  MountObserverStrategy / UnmountObserverStrategy (two overload groups)

IEntityLifecycle (explicit interface implementation):
  RecoverForLifecycle / FireAfterSpawnHooks / FireAfterLoadHooks
  FireBeforeSaveHooks / FireBeforeQuitHooks / FireBeforeDeadHooks
  ReleaseStrategiesOnly / TeardownOnly / TeardownObserverBindings / BuildMetaData

ISndEntityRawSubscription (explicit interface implementation):
  SubscribeDataRaw / UnsubscribeDataRaw
```

The following must be rewritten in each adapter (engine-specific parts):

| Logic | Godot Implementation | Unity Replacement |
|------|-----------|---------------|
| Engine base class | `: Node` | `: MonoBehaviour` |
| Entity cleanup | `Free()` | `Destroy(gameObject)` |
| Name sync | `StableName` / `Node.Name` | Equivalent concept (`gameObject.name`) |
| Node access | `GetNodeFromSnd<TNode>()` returns `Godot.Node` | Returns `UnityEngine.GameObject` / `Component` |
| Node factory | `GodotPackedSceneNodeFactory`, with self as parent | `UnityPrefabNodeFactory`, instantiate under own Transform |
| Lazy creation | `_world.CreateEntity(nodeFactory, ...)` | Same call, but with Unity's `INodeFactory` |

**Estimated effort**: The entire entity bridging portion is about 2 hours (130 lines of mechanical forwarding copy + 60 lines of engine API replacement), accounting for less than 5% of total adapter layer effort. The real adaptation cost is in `ISndSceneHost` (scene entity management), `IFileSystem` (engine file API), `ILogger` (engine log API), `INodeFactory/INodeHandle` (node lifecycle), Bootstrap, and other components.

---

[↑ Back to Origo.GodotAdapter](../README.en.md)
