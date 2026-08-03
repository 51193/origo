<!-- docsync-pair: Origo.GodotAdapter/Snd/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
| `SndEntityNodeExtensions.cs` | Adapter-layer convenience extensions: `GetNativeNode()` (extracts Godot Node from INodeHandle), `GetNodeFromSnd<T>()` (traverses Godot scene tree from ISndEntity). Physically located at project root `Origo.GodotAdapter/SndEntityNodeExtensions.cs` (not in Snd/ subdirectory), namespace belongs to `Origo.GodotAdapter` |
| `TypedDataInitializer.cs` | Assembly loading forced entry point: accessing the `IsLoaded` property triggers all `[ModuleInitializer]` executions |

## Module Details

### GodotSndManager

The adapter layer's core entry point node (`[GlobalClass]`), mounted directly in the Godot scene tree:

- **Implements ISndSceneHost**: CreateEntity / RecoverFromMetaList / RemoveAllEntities (framework-internal lifecycle operation) / RequestKillEntity / RemoveEntity / GetEntities / FindByName / ProcessAll. `RemoveAllEntities()` uses `Free()` (immediate release) rather than `QueueFree()`, since Core guarantees it is called at a safe lifecycle point.
- **Implements ISndContextAttachableSceneHost**: Supports runtime context switching
- **Rollback mechanism**: In `RecoverFromMetaList`, if an entity fails to load, all already-created entities are rolled back and released
- **EntityView**: Lazily creates an `IReadOnlyList<ISndEntity>` wrapper, caching the reference to avoid reallocation
- **BuildMetaList()**: Calls entities' `BuildSndMetaData()` to collect metadata
- **ProcessAll(delta)**: Implements the unified entry for `ISndSceneHost.ProcessAll`, called by Core's `SessionManager.ProcessAllSessions`, maintaining `ProcessTickCount` and `ProcessDeltaSum` statistics

### GodotSndEntity

A Godot wrapper for Core `SndEntity` (`[GlobalClass]`):

- **Lazy initialization**: `_entity` is created on first access via `SndWorld.CreateEntity`
- **Lifecycle separation**: `DetachFromManager()` is called only by GodotSndManager, setting the released flag, nulling the entity reference, and calling `Free()` to release the node
- **BuildSndMetaData()**: Public wrapper for `BuildMetaData()`, used by GodotSndManager to collect metadata
- **IEntityLifecycle implementation**: Each method includes an `EnsureEntity()` guard (creates before use)
- **StableName**: Independently stores the entity's stable name (Godot Node's Name may be auto-modified with suffixes due to name conflicts)
- **GetNodeFromSnd<TNode>**: Godot-specific extension — finds nodes by name from the SND node system and casts to a concrete Godot type

### GodotPackedSceneNodeFactory

- **Create**: `ResourceLoader.Load<PackedScene>(resourceId)` → `Instantiate<Node>()` → `parent.AddChild(node)` → returns GodotNodeHandle
- resourceId is resolved through `SndMappings.ResolveSceneAlias` (supports aliases), so it can be a raw `res://` path or an alias
- Loaded `PackedScene` instances are cached to avoid repeated disk I/O when the same resource is instantiated multiple times

### GodotNodeHandle

- **Name**: The node name cached at construction time, unaffected by Godot releasing the original node
- **Free()** → checks `IsInstanceValid(_node)` then calls `_node.Free()` if valid
- **SetVisible(bool)** → first checks `IsInstanceValid(_node)`, then sets the appropriate Visible property based on node type (`CanvasItem` or `Node3D`)
- **UnsafeGetNode()** → `internal` — returns the underlying `Godot.Node` reference, used only by `SndEntityNodeExtensions.GetNativeNode()`

### SndEntityNodeExtensions

- **GetNativeNode(this INodeHandle)** → safely converts `INodeHandle` to a native `Godot.Node`. Only works when the handle is a `GodotNodeHandle`; returns null otherwise
- **GetNodeFromSnd<TNode>(this ISndEntity, string)** → traverses the Godot scene tree to find a node by name and casts to the specified type. Only works when the entity is a `GodotSndEntity`

## Design Decisions

### Why GodotSndManager does not own a _Process loop

Entity frame processing is a Core orchestration responsibility. If `GodotSndManager` held its own `_Process` loop iterating entities and calling `ProcessSnd(delta)`, it would duplicate Core's frame processing logic and bypass the formal processing pipeline. Therefore frame processing is uniformly executed by Core's `SessionManager.ProcessAllSessions(delta)` via `SceneHost.ProcessAll(delta)`, through `IOrigoFrameDriver.DriveFrame(delta)`. `ProcessTickCount` and `ProcessDeltaSum` are also maintained within `ProcessAll`. `ProcessSnd` and `SpawnSingle`/`LoadSingle`/`SaveSingle` are `internal` — lifecycle orchestration can only be triggered via Core's `ISessionRun` and the batch hook pipeline; external code must not call them through the concrete `GodotSndEntity` type.

### Why GodotSndEntity uses lazy creation for the Core Entity

Core `SndEntity` requires `INodeFactory` injection, and `INodeFactory` needs the GodotSndEntity itself as the parent node. This is a construction ordering problem — GodotSndEntity is created first, then `INodeFactory` is constructed with itself as the parameter, then the Core Entity is created through the factory. Lazy creation resolves this circular dependency.

### Why StableName is independent of Godot Node.Name

If nodes with the same name exist in Godot's scene tree, Godot automatically appends suffixes like `@2`, `@3` to the Name. SND entity lookups depend on stable names and cannot use Godot-modified Names. `StableName` is set at Spawn/Load time and is unaffected by Godot's automatic renaming of `Node.Name`.

### Why RecoverFromMetaList uses a rollback mechanism

If loading 100 entities and the 50th fails, the first 49 created entities are in an incomplete state (AfterLoad hooks may have fired but the scene is incomplete). Rollback releases all of them to prevent residual corrupted entities from contaminating subsequent operations.

### Why DetachFromManager must remove from list before releasing

If the node is directly released during iteration over `_entities`, Godot's node tree changes could cause subsequent iterations to skip entities or double-process. Remove first, release later, ensures safe list iteration.

### Adapter layer entity bridging: Why GodotSndEntity must hand-write forwarding

The code in `GodotSndEntity` (~206 lines) can be broken down into three categories:

| Category | Lines | Share | Notes |
|------|------|------|------|
| Pure forwarding boilerplate | ~130 | 45% | `ISndEntity` (~20 methods), `IEntityLifecycle` (8 hooks), `ISndEntityRawSubscription` (2 methods) — all follow the pattern `EnsureEntity(); _entity!.Foo(...)`, each method 4–6 lines |
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
  FireAfterSpawnHooks / FireAfterLoadHooks / FireBeforeSaveHooks
  FireBeforeQuitHooks / FireBeforeDeadHooks
  ReleaseStrategiesOnly / TeardownOnly / BuildMetaData

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
