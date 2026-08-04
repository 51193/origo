<!-- docsync-pair: Origo.Core/Snd/Entity/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Entity

> [↑ Back to Snd](../README.en.md) · [↔ Abstractions: Abstractions/Entity](../../Abstractions/Entity/README.en.md)

## Overview

Concrete implementation of the SND entity model. `SndEntity` is the runtime entity aggregate root, combining four internal managers — `SndDataManager` (data), `SndNodeManager` (nodes), `SndStrategyManager` (passive strategies), and `ActiveStrategyManager` (active strategies) — and holding a constructor-injected per-scene-host `ObserverTopology` (observer binding) reference, implementing `ISndEntity`, `IEntityLifecycle`, and `ISndEntityRawSubscription` interfaces.

Strategy lifecycle hooks are triggered via phased methods exposed by the `IEntityLifecycle` interface, with batch hook invocation orchestrated centrally by the framework's `SndEntityFactory` and `SessionRun`, rather than being called directly by business code on entities.

`SndEntity` is also a participant in the observation system: observer strategies (`ObserverStrategyBase`) are mounted onto target entities via `MountObserverStrategy`, with per-scene-host `ObserverTopology` managing the binding topology, wiring target data changes through `ISndEntityRawSubscription`, and auto-unmounting when the entity quits or dies.

## Included Files

| File | Responsibility |
|------|---------------|
| `SndEntity.cs` | Entity aggregate root: combines four managers (data/nodes/passive strategies/active strategies) + injected `ObserverTopology` reference, implements `ISndEntity` + `IEntityLifecycle` + `ISndEntityRawSubscription` |
| `SndDataManager.cs` | `internal` — Entity data dictionary management + observer change notification |
| `SndNodeManager.cs` | `internal` — Entity node management: from metadata recovery to node creation/lookup/release |
| `DataObserverManager.cs` | `internal` — Generic data observer subscription/notification infrastructure (key → callback list) |
| `ISndEntityRawSubscription.cs` | Raw data subscription interface (`SubscribeDataRaw` / `UnsubscribeDataRaw`). Used by `ObserverTopology` in internal pipelines to directly operate on the target entity's `SndDataManager`, wiring observer strategies into data changes |

> `TryGetNumericExtensions.cs` (in the `Origo.Core.Snd` namespace) provides `TryGetNumeric` / `GetNumeric` extension methods, bridging the type mismatch between `SetData("k", 5)` (int) and `TryGetData<float>("k")` (float). Attempts reading in float → int → long → double order. See [TryGetNumeric](../README.en.md).

## Module Details

### SndEntity (Aggregate Root)

The **constructor** requires injection of `INodeFactory`, `SndStrategyPool`, `Func<string, string> sceneAliasResolver`, `ISndContext`, `ILogger`, and `ObserverTopology`. No parameterless constructor is exposed. `sceneAliasResolver` is a scene alias resolution function (extracted from `SndMappings.ResolveSceneAlias`), avoiding passing the entire `SndMappings` object into the entity layer. `ObserverTopology` is the core observer binding topology, held by the scene host and shared across entities.

**Observer wiring**:

Observation is implemented via observer strategies, with the mount entry points being the four methods of `ISndObserverStrategyAccess`, all delegated to the injected per-scene-host `ObserverTopology`:

| Public Method | Behavior |
|---------------|----------|
| `MountObserverStrategy(targetName, observerIndex)` | Resolve target by name (own Name = self-observation; cross-entity names require the scene host), delegate to `ObserverTopology.Mount` |
| `MountObserverStrategy(target, observerIndex)` | Mount with an already-resolved target entity (preferred for cross-entity) |
| `UnmountObserverStrategy(...)` | Corresponding unmount, triggers observer strategy `OnUnmounted` |

`ObserverTopology` maintains the observer binding topology for all entities within this host. During mounting, it wires into the target entity's data changes via `ISndEntityRawSubscription.SubscribeDataRaw` (using keys declared by `[ObserveData]`), and serializes this entity's outgoing edges via `BuildBindingsFor(Name)` into `StrategyMetaData.ObserverIndices`. On load, `SessionRun` restores them through the host topology's `RecoverBindingsFor()`.

**`IEntityLifecycle` phased methods** (`internal` interface):

These methods are used by the framework layer for batch orchestration; business code should not call them directly:

| Method | Phase | Description |
|--------|-------|-------------|
| `RecoverForLifecycle(meta)` | Phase 1: Recovery | Recover Name + Data + Node + EntityStrategy + ActiveStrategy; does not trigger any hooks |
| `FireAfterSpawnHooks()` | Phase 2: Hooks | Trigger strategy AfterSpawn by priority |
| `FireAfterLoadHooks()` | Phase 2: Hooks | Trigger strategy AfterLoad by priority |
| `FireBeforeSaveHooks()` | Phase 2: Hooks | Trigger strategy BeforeSave by priority |
| `FireBeforeQuitHooks()` | Phase 2: Hooks | Trigger strategy BeforeQuit by priority |
| `FireBeforeDeadHooks()` | Phase 2: Hooks | Trigger strategy BeforeDead by priority |
| `ReleaseStrategiesOnly()` | Phase 3: Teardown | Release passive + active + observer strategy references (no hooks triggered) |
| `TeardownOnly()` | Phase 3: Teardown | Release Node + Data resources |
| `BuildMetaData()` | Serialization | Build metadata (including ObserverIndices; does not trigger BeforeSave) |

> **Visibility**: `IEntityLifecycle` and the single-entity convenience methods (`SpawnSingle` / `LoadSingle` / `QuitSingle` / `DeadSingle` / `SaveSingle` / `Process`) are `internal` — entity lifecycle orchestration can only be triggered via `ISessionRun` (`Spawn` / `SpawnMany` / `RequestKillEntity`) and the framework's internal batch hook pipeline. Adapter and test projects access them via `InternalsVisibleTo`.

Teardown order for `QuitSingle` / `DeadSingle`: first `FireBeforeQuit/DeadHooks`, then unmount observer bindings (`OnUnmounted`), then `ReleaseStrategiesOnly`, and finally `TeardownOnly`.

`Process(delta)` triggers strategy Process by priority + snapshot iteration (internal; invoked by scene host `ProcessAll` and adapter-layer frame processing).

`IsPendingKill` flag is set immediately by `RequestKillEntity()`. BeforeDead hooks are triggered in batch by `SessionRun.KillPending()`; `RemoveEntity()` only performs teardown.

> **Note**: `CreateEntity` is a method on the scene host (`ISndSceneHost`), not on the entity itself. `ISndSceneHost.CreateEntity` creates the entity and recovers data/strategies/nodes via `RecoverForLifecycle`, but does not trigger AfterSpawn hooks. AfterSpawn hooks are uniformly triggered by `SndEntityFactory.Spawn` / `SndEntityFactory.SpawnMany` after all entities are created.

### SndDataManager

- **Storage**: `Dictionary<string, TypedData>`
- **SetData**: Uses `CollectionsMarshal.GetValueRefOrAddDefault` for in-place writes; skips notification when old value is the same (avoiding meaningless events). Throws `ArgumentNullException` when `value` is null for reference types.
- **GetData / GetRequiredData vs TryGetData**: Both `GetData` and `GetRequiredData` require `T : notnull`. They throw `InvalidOperationException` on KeyNotFound or type mismatch; the latter safely returns `(found, value?)`
- **Subscribe/Unsubscribe**: Receives `Action<ISndEntity, TypedData, TypedData>` (`(target, old, new)`), internally wrapped as `Action<TypedData, TypedData>` adapting `DataObserverManager`; `_subscriptionMap` stores `(OriginalCallback, WrappedCallback)` pairs for unsubscribe matching. This data subscription channel is driven by `ObserverTopology` via `ISndEntityRawSubscription` and is not directly exposed to business strategies
- **Recover / Release / SerializeMeta**: Save recovery / cleanup / serialization

### SndNodeManager

- Implements `INodeHost` (internal interface)
- `Recover`: First release old nodes, then create new nodes one by one via `INodeFactory.Create` according to metadata. On creation failure, rolls back and releases all
- `Release`: Call `node.Free()` on each then clear
- Node resource IDs are resolved via `SndMappings.ResolveSceneAlias` (supports aliases)

### DataObserverManager

Engine-independent observer infrastructure:
- Each data key maintains a `List<Subscription>`
- Each Subscription contains `Callback(Action<TypedData, TypedData>)` + optional `Filter(Func<TypedData, TypedData, bool>)`
- `NotifyObservers` iterates via `ToArray()` snapshot, allowing callbacks to modify the subscription list
- `Unsubscribe` removes by delegate reference comparison

## Design Decisions

### Why separate IEntityLifecycle

The trigger timing for strategy lifecycle hooks (AfterSpawn/AfterLoad/BeforeSave/BeforeQuit/BeforeDead) is controlled by the framework layer and should not be directly exposed on `ISndEntity` (which faces business code). The `IEntityLifecycle` interface exposes phased methods to `SndEntityFactory` and `SessionRun`, enabling batch orchestration while keeping the business code interface clean.

See [IEntityLifecycle](../../Abstractions/Entity/README.en.md).

### Why SndEntity is an aggregate root rather than exposing sub-managers

External strategy code operates on entities via the `ISndEntity` interface (SetData/TryGetData/AddStrategy/MountObserverStrategy) without awareness of internal managers. Aggregate root encapsulation ensures internal state consistency of entities.

### Why roll back all created nodes when node recovery fails

In `SndNodeManager.Recover`, if the Nth node creation fails, the previous N-1 created nodes are in a half-initialized state and cannot be safely used. Rolling back ensures no incomplete state remains.

### Why DataObserverManager uses snapshot iteration for notification callbacks

Notification callbacks may trigger Subscribe/Unsubscribe/SetData (and thus NotifyObservers again). Directly foreach-ing on the list while modifying it would cause `Collection was modified` exceptions. `ToArray()` snapshot trades a small allocation for safety.

### Why observation goes through per-scene-host ObserverTopology rather than entity subscription APIs

Observer strategies, like passive/active strategies, are stateless and poolable. Delegating observer wiring to the scene-host-level `ObserverTopology` for unified governance allows the binding topology to be serialized with entities (`ObserverIndices`) and auto-restored on load, eliminating the need for business code to manually reconnect in `AfterLoad` or manually unsubscribe in `BeforeDead` — the topology auto-unmounts all bindings when an entity quits or dies. Cross-entity bindings form a directed graph rather than per-entity private state; centralizing them to the per-scene-host topology means `SessionRun`'s kill/clear bidirectional teardown locates observers via the incoming edge index without entities needing to expose their internal managers in reverse.

> **⚠️ Adapter integration contract**: `SessionRun.KillPending`'s observer bidirectional teardown and load recovery apply only to entities that are bare `SndEntity` types inside the host. Non-bare wrapper entity types (e.g. Godot's `GodotSndEntity`) intentionally do not participate in that bidirectional teardown — new adapter implementations must handle observer unwiring for their wrapper entities themselves, or transcribe the bare-`SndEntity` semantics at the wrapper layer (see [Scene/README](../Scene/README.en.md)).

### Why SndDataManager stores (OriginalCallback, WrappedCallback) pairs

Data subscriptions exist in `DataObserverManager` as wrapped `(old, new)` delegates, while unsubscribe requests carry the original `(target, old, new)` delegate. The `SubscriptionPair` in `_subscriptionMap` uses `OriginalCallback` for reference matching to locate the subscription and `WrappedCallback` to perform the actual unsubscribe on `DataObserverManager`, ensuring the wrapping chain is reversible.

### Why separate ISndEntityRawSubscription interface

Observer wiring requires direct subscription to the target entity's data change channel. `ISndEntityRawSubscription` provides a `TypedData`-level raw data subscription entry point, with members exposed via explicit interface implementation — business code holding `ISndEntity` cannot see these methods; only `ObserverTopology` and other framework-internal pipelines (via `SndEntity` / `GodotSndEntity`) use them.

---

[↑ Back to Snd](../README.en.md)
