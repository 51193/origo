<!-- docsync-pair: Origo.Core/Snd/Strategy/README -->
<!-- docsync-revision: 15 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Strategy

> [↑ Back to Snd](../README.en.md)

## Overview

Complete implementation of the SND strategy system. Strategies are the carriers of entity behavioral logic, following a "stateless shared" model: strategy instances are globally shared in a pool, hold no instance fields, and store all mutable state in the entity's Data.

Strategies are divided into four categories: passive entity strategies (frame-driven and lifecycle hooks), active strategies (externally invoked by index), observer strategies (data and binding lifecycle response), and state machine strategies (string-stack state management). All four share `BaseStrategy` + `SndStrategyPool` infrastructure, with completely independent containers and managers.

## Included Files

| File | Responsibility |
|------|---------------|
| `BaseStrategy.cs` | Abstract root class for all strategies |
| `LifecycleStrategyBase.cs` | Entity strategy base class: 8 lifecycle virtual method hooks |
| `ActiveStrategyBase.cs` | Active strategy base class: `Invoke(entity, ctx, input)` — externally invoked by index |
| `ObserverStrategyBase.cs` | Observer strategy base class: `OnMounted` / `OnDataChanged` / `OnUnmounted` |
| `ObserverTopology.cs` | `internal` — per-scene-host observer binding topology: centrally manages the directed graph of "who observes whom" (wire/teardown/serialization/load-time recovery), maintains bidirectional indices by observerName primary key |
| `ObserverBindingEntry.cs` | `internal` — single observer binding record (observerName / targetName / observerIndex / strategy / data subscription wrapper); `FullCleanup` unsubscribes + triggers `OnUnmounted` + returns strategy |
| `ObserverStrategyMetadata.cs` | `internal` — per-type reflection cache of data keys declared by `[ObserveData]` |
| `ObserveDataAttribute.cs` | Observation data key declaration attribute: `[ObserveData("key")]`, supports multiple declarations |
| `ActiveStrategyExtensions.cs` | `ISndEntity` extension methods: generic `InvokeStrategy<TInput, TOutput>` eliminates JSON serialization boilerplate; `EnsureStrategy` lazy strategy mount + idempotent guard. Physically located at `Origo.Core/Snd/` root (not under Strategy/) |
| `ActiveStrategyManager.cs` | `internal` — per-entity active strategy manager: Dictionary container + add/remove + serialization |
| `SndStrategyPool.cs` | `internal` — Strategy pool: registration, instantiation, reference counting, statelessness validation |
| `SndStrategyManager.cs` | `internal` — per-entity passive strategy manager: strategy container add/remove + lifecycle hook coordination |
| `StrategyIndexAttribute.cs` | Strategy index declaration attribute: `[StrategyIndex("core.health")]` |
| `ActiveStrategyJsonBase.cs` | JSON-contract active strategy base class: active strategies serializing input/output through JSON |
| `ActiveStrategyResults.cs` | Active strategy invocation result wrapper: unified success/failure and output value passing |
| `EntityStrategyExtensions.cs` | Entity strategy extensions: `EnsureReplaceableStrategy` and other mount helpers |

## Module Details

### Strategy Inheritance Hierarchy

```
BaseStrategy
├── LifecycleStrategyBase         (Passive: Process, AfterSpawn, AfterLoad, AfterAdd, BeforeRemove, BeforeSave, BeforeQuit, BeforeDead)
├── ActiveStrategyBase         (Active: Invoke)
├── ObserverStrategyBase       (Observer: OnMounted, OnDataChanged, OnUnmounted)
└── StateMachineStrategyBase   (State Machine: OnPushRuntime, OnPushAfterLoad, OnPopRuntime, OnPopBeforeQuit)
```

### ObserverTopology

Each scene host that creates real `SndEntity` instances (`FullMemorySndSceneHost`, `GodotSndManager`, both implementing `IObserverTopologyHost`) holds one `ObserverTopology` instance, centrally managing all observer bindings for entities within that host. The topology maintains bidirectional indices by observerName primary key: outgoing edges (observer → its binding list, for serialization and outgoing teardown), incoming edges (target → set of observerNames observing it, for O(1) incoming teardown). Entities are injected with the topology reference at construction and delegate all observer operations to it (analogous to sharing the strategy pool). Data change signals always originate from the target entity's `ISndEntityRawSubscription`; the topology only manages binding records and wiring/teardown.

- **BindContext(ctx)**: Injected by the host when binding the context, used for `OnMounted`/`OnDataChanged`/`OnUnmounted` callbacks
- **Mount(observer, target, observerIndex)**: Acquire strategy instance → establish `SubscribeDataRaw` wiring on target for each key declared by `[ObserveData]` attribute → record binding → trigger `OnMounted`. Mounting is atomic: if wiring or `OnMounted` throws, all established subscriptions are canceled, partially-added bindings are removed, strategy reference is returned to pool, and the exception propagates. Mounting the same (observer, target, observerIndex) twice throws `InvalidOperationException` (consistent with the passive/active strategy managers' duplicate-mount rejection)
- **Unmount(observer, target, observerIndex)**: Remove binding record → tear down `UnsubscribeDataRaw` → trigger `OnUnmounted` → release pool reference. The binding is removed before the callbacks so re-entrant unmounting from inside `OnUnmounted` is safe; a `finally` block guarantees the pool reference is returned even when a hook throws. Throws `InvalidOperationException` when the binding does not exist (fail-fast, consistent with `RemoveStrategy` throwing on a non-mounted index)
- **ReleaseStrategiesFor(observer)**: Release all strategy references held by an observer and clear its outgoing edges (does not trigger `OnUnmounted`, does not unsubscribe), corresponding to the `ReleaseStrategiesOnly` phase of entity teardown
- **RecoverBindingsFor(observer, bindings, resolveTarget)**: Recover from archived observer_indices topology, resolve target entities by name, re-wire and trigger `OnMounted`. A missing or blank target (inconsistent save topology) throws `InvalidOperationException` (fail-fast) instead of being silently skipped
- **BuildBindingsFor(observerName)**: Serialize an observer's full outgoing edges as `List<ObserverBinding>` (grouped by target) into `StrategyMetaData`
- **TeardownOutgoingFor(observer, resolveTarget)**: Clean an observer's full outgoing edges; if target is resolvable, full `Unmount`; otherwise return strategy and remove record
- **TeardownAllBindingsFor(observer)**: Self-contained cleanup path that calls `FullCleanup` on all outgoing edges of the observer (unsubscribe + `OnUnmounted` + release strategy), not depending on the scene host — binding entries already hold `TargetEntity` references. Invoked by `SessionRun.ReleaseAllEntitiesAndClear` through `IEntityLifecycle.TeardownObserverBindings` when a session quits
- **GetObserverNamesTargeting(targetName)** / **RemoveBindingsTargetingFor(observer, targetName)**: Incoming teardown support — query the observers targeting a name, clean bindings where a specific observer points to a specific target
- **Bidirectional teardown**: `SessionRun.KillPending` handles both outgoing (killed entity as observer) and incoming (killed entity as target, located via incoming index), preventing re-entrant modification in `OnUnmounted` callbacks through snapshots

### Observer Strategy Persistence

The serialization format for observer bindings resides within `StrategyMetaData`:

```json
{
  "observer_indices": [
    { "player_1": ["hp_watcher", "intent_watcher"] },
    { "goblin_3": ["threat_watcher"] }
  ]
}
```

- For self-observation, target equals the own entity name
- On load, Observer bindings are restored after `FireAfterLoadHooks` (entity strategy AfterLoad executes first, Observer wires later)
- On death, Observer bindings are cleaned before `FireBeforeDeadHooks` (Observer tears down first, entity strategy BeforeDead executes later)

### SndStrategyPool

Global registry and instance pool for strategies:

- **Register**: `Register(Type strategyType, Func<BaseStrategy> factory)` — validates statelessness via reflection before registering the type (checks instance fields and writable properties)
- **GetStrategy<TBase>(index)**: If an instance exists in the pool, reuse it (reference count +1); otherwise create via factory
- **ReleaseStrategy(index)**: Reference count -1; when it reaches zero, remove from pool (but factory is retained for future creation)
- **GetPriority(index)**: Returns the strategy's execution priority on an entity (default 6205)
- **LogPoolLeaks()**: Diagnostic method, iterates all reference counts; if any non-zero counts exist, outputs a Warning log. Called during testing or shutdown to detect unreturned strategy references (leaks)
- **Strategy ordering**: Only passive entity strategies are sorted ascending by priority; same priority sorts by insertion order

### SndStrategyManager

Each `SndEntity` holds one manager instance, managing passive entity strategies. Exposes phased operation methods (all `internal`), called by `SndEntity` via `IEntityLifecycle`:

| Method | Description |
|--------|-------------|
| `RecoverStrategiesOnly(indices)` | Acquire strategies from pool by index, sort-insert (releases old strategies, does not trigger hooks) |
| `ReleaseStrategiesOnly()` | Release all strategy references and clear list (does not trigger hooks) |
| `TriggerAfterSpawn(entity, ctx)` | Snapshot-iterate to trigger AfterSpawn |
| `TriggerAfterLoad(entity, ctx)` | Snapshot-iterate to trigger AfterLoad |
| `TriggerBeforeSave(entity, ctx)` | Snapshot-iterate to trigger BeforeSave |
| `TriggerBeforeQuit(entity, ctx)` | Snapshot-iterate to trigger BeforeQuit |
| `TriggerBeforeDead(entity, ctx)` | Snapshot-iterate to trigger BeforeDead |
| `GetStrategyIndices()` | Return all currently held strategy indices |
| `Process(entity, delta, ctx)` | Frame update (snapshot iteration) |
| `Add(entity, index, ctx)` | Dynamically add strategy and trigger `AfterAdd`; if `AfterAdd` throws, roll back insertion and return pool reference before propagating (addition is atomic). Mounting the same index twice throws `InvalidOperationException`; the plan engine (`PlanExecutionStrategyBase`) reuses an already-mounted action instead of remounting it, so plan-managed actions may also appear in `LifecycleIndices` |
| `Remove(entity, index, ctx)` | Dynamically remove strategy (triggers BeforeRemove); a non-mounted index throws `InvalidOperationException` (fail-fast, symmetric with `Add`'s strictness) |

- **Recover**: Type filter on pool acquisition, keeping only `LifecycleStrategyBase` subclasses; non-`LifecycleStrategyBase` types (such as `ActiveStrategyBase`, `ObserverStrategyBase`) immediately throw `InvalidOperationException`
- **Lifecycle hook triggering**: All based on `ToArray()` snapshot iteration — because hooks may add or remove strategies. The five trigger methods (`TriggerAfterSpawn/Load/Save/Quit/Dead`) uniformly delegate to `TriggerAll`, eliminating copy-paste duplication

### ActiveStrategyManager

Each `SndEntity` holds one manager instance, managing active strategies:

- **Container**: `Dictionary<string, ActiveStrategyBase>` — O(1) lookup by index, does not participate in per-frame traversal
- **Recover**: Batch recovery from metadata (does not trigger hooks); upon encountering non-`ActiveStrategyBase` types, immediately throws `InvalidOperationException` and rolls back all active strategies already acquired in this recovery, leaving no half-initialized state — consistent fail-fast semantics with `SndStrategyManager`'s entity strategy recovery
- **ReleaseAll**: Call `ReleaseStrategy` on each and clear container (does not trigger hooks)
- **Add / Remove**: Dynamic addition and removal of active strategies; `Remove` throws `InvalidOperationException` for a non-attached index (fail-fast)
- **Invoke**: Look up strategy instance by index, call `Invoke(entity, ctx, input)` and return the result
- **Serialization**: `SerializeIndices()` returns all currently held indices

### ActiveStrategyExtensions

Extension methods for `ISndEntity`, providing type-safe generic ActiveStrategy invocation and lazy strategy mounting:

```csharp
// Generic invocation (strongly-typed input and output)
var result = entity.InvokeStrategy<SearchInput, PathResult>("traversability.find_path", input);

// Generic invocation without input
var list = entity.InvokeStrategy<List<FoodEntry>>("food.get_registry");

// Lazy strategy mount (with idempotent guard)
entity.EnsureStrategy("character.path_impl", "character.pathfind.astar");
```

- `InvokeStrategy<TInput, TOutput>` / `InvokeStrategy<TOutput>`: transparently handles JSON serialization/deserialization, eliminating call-side boilerplate
- `EnsureStrategy(string dataKey, string strategyIndex)`: checks whether the entity's dataKey already has a value; if not, mounts the strategy first and writes dataKey as the idempotency marker only after the mount succeeds. Used for lazy strategy layer initialization; idempotently safe for repeated calls

The raw `entity.InvokeStrategy(string, object?)` interface remains unchanged; the generic extension methods serve as an optional convenience layer.

### Strategy Order in Entity Lifecycle

```
Phase 1 (RecoverForLifecycle):
  1. Recover Data
  2. Recover Nodes
  3. Recover EntityStrategy (RecoverStrategiesOnly)
  4. Recover ActiveStrategy

Phase 2 (Trigger hooks):
  5. Trigger entity strategy hooks (TriggerAfterSpawn/AfterLoad/etc., driven by IEntityLifecycle's internal methods)

Phase 2.5 (Observer recovery):
  6. Recover Observer cross-entity bindings (re-wire from StrategyMetaData.ObserverIndices + trigger OnMounted)

Phase 3 (Teardown):
  7. Observer binding cleanup (outgoing + incoming, triggers OnUnmounted)
  8. Release ActiveStrategy (ReleaseAll)
  9. Release EntityStrategy (ReleaseStrategiesOnly)
  10. Teardown Nodes + Data (TeardownOnly)
```

Observer bindings are recovered in Phase 2.5 (after entity strategy AfterLoad) and cleaned early in Phase 3 (before entity strategy BeforeDead).

### StrategyMetaData Split

Strategy indices in entity metadata are stored separately by type:

```
StrategyMetaData
├── LifecycleIndices: List<string>          (passive entity strategies)
├── ActiveIndices: List<string>          (active strategies)
└── ObserverIndices: List<ObserverBinding>  (observer bindings; each binding contains Target and ObserverIndices)
```

Serialized JSON format:
```json
{
  "lifecycle_indices": ["patrol", "idle"],
  "active_indices": ["query.hp"],
  "observer_indices": [
    { "player_1": ["hp_watcher"] }
  ]
}
```

All three are recovered separately during `RecoverForLifecycle` with no cross-contamination.

### StrategyIndexAttribute

```csharp
[StrategyIndex("my_game.player_control", Priority = 100)]
public sealed class PlayerControlStrategy : LifecycleStrategyBase { ... }
```

- `Index`: required, unique index key for the strategy in the pool
- `Priority`: optional, default 6205, determines the execution order of multiple passive strategies on the same entity; active strategies do not participate in ordering

## Design Decisions

### Why strategies must be stateless (registration-time validation)

Strategy instances are shared across multiple entities; instance fields (such as `int _hp`) would cause cross-entity contamination. Registration-time reflection checks all levels between `BaseStrategy` and the concrete type, rejecting strategies that declare instance fields or writable properties, blocking this error at the source.

A side effect of this constraint: **test strategies cannot use instance fields as event receivers** and must use static fields (`static List<string>?`) to share event collection across strategy instances. Test classes using static fields must be serialized via `[Collection]` attributes or globally disable parallel execution via `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to prevent race conditions between parallel tests. See `Origo.Core.Tests/Architecture.en.md`.

### Why strategy types must be sealed

Strategy instances are shared by the pool. Allowing inheritance would let derived types bypass registration-time field validation or let one strategy index produce different subclass instances at different call sites, breaking the one-to-one relationship between index and behavior. Registration enforces `sealed` so strategy behavior is fixed to the registered type.

### Why use reference counting instead of a single instance

The same strategy may be referenced by multiple entities simultaneously (e.g., the `core.health` strategy is active on every entity). Reference counting ensures the strategy is not reclaimed as long as at least one entity holds it. It is only released when the count reaches zero, and recreated or reused on next access.

### Why passive and active strategy containers are separate

Active strategies only require index-based lookup (O(1) Dictionary) and do not participate in per-frame traversal. Passive strategies require priority-ordered iteration (List). Separate containers avoid type checks and irrelevant data during traversal and provide clear grouping boundaries for serialization.

### Why all lifecycle hooks use snapshot iteration

Hook callbacks frequently need to add or remove strategies (e.g., removing one's own strategy during `BeforeDead`). Direct iteration on a mutating list would cause exceptions. Snapshots ensure list stability for each hook invocation, trading a small allocation for safety.

### Why ActiveStrategy is recovered before entity strategy hooks

ActiveStrategy is recovered during `RecoverForLifecycle` (Phase 1), before `FireAfterSpawnHooks` / `FireAfterLoadHooks` (Phase 2). This ensures entity strategy hooks can call their own ActiveStrategy via `InvokeStrategy` and can also call ActiveStrategies of other recovered entities — enabling loading-order-independent cross-entity interoperability.

### Why the strategy pool's reference counting is not thread-safe

`SndStrategyPool`'s reference counts, registration table, and instance cache are only accessed on the frame thread (single-threaded frame model). Cross-thread scenarios (deferred-queue enqueue/dequeue, console input, etc.) only carry actions and data and never touch the pool; engine callbacks and business strategy hooks all run on the frame thread. Reference counting therefore needs no locking — concurrent pool access is a contract violation with undefined behavior.

---

[↑ Back to Snd](../README.en.md)
