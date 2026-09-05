<!-- docsync-pair: Origo.Core/Snd/README -->
<!-- docsync-revision: 12 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Snd

> [↑ Back to Origo.Core](../README.en.md)

## Module Capability

The complete implementation of the SND (Strategy + Node + Data) entity system. This is Origo's core business model — all game entities express behavioral logic through strategies, store mutable state through data, and map to the engine presentation layer through nodes.

## Sub-Modules

| Sub-Module | Capability | Details |
|-----------|-----------|---------|
| [Entity](Entity/README.en.md) | Runtime entity aggregate root | SndEntity + four internal managers (data/node/passive/active strategy) |
| [Metadata](Metadata/README.en.md) | Entity metadata model | TypedData / SndMetaData / NodeMetaData / StrategyMetaData / DataMetaData / SndMetaFluentBuilder |
| [Scene](Scene/README.en.md) | Scene host & spawn factory | SndEntityFactory + FullMemorySndSceneHost |
| [Strategy](Strategy/README.en.md) | Strategy system core | BaseStrategy → LifecycleStrategyBase \| ActiveStrategyBase \| ObserverStrategyBase. Strategy pool, passive/active/observer three kinds of managers + generic invocation extensions |
| [Archetype](Archetype/README.en.md) | Numeric recipe loading | SndArchetypeLoader: key-value pair file parsing and type inference |
| [Companions](Companions/README.en.md) | SndContext role companion objects | 8 internal companion classes in the `Companions/` subdirectory, 2 (FileAccess, ArchiveFileAccess) at the Snd/ root. Together implement ISndBlackboardAccess / ISndSaveOperations etc., exposed through ISndContext's companion properties |

## This Layer's Core Files

| File | Responsibility |
|------|---------------|
| `ISndContext.cs` | SND context unified facade interface: exposes all capabilities through 10 companion properties ([see Abstractions/Snd](../Abstractions/Snd/README.en.md)) |
| `SndContext.cs` | Default ISndContext implementation (global/progress-level). `Bootstrap()` method executes the complete startup flow: strategy discovery → alias/template loading → entry save loading. Provides `ISndFileAccess` through the companion `SndContextFileAccess` (file read/write delegated to `SndWorld.DataSourceIo`/`MetaAccess`/`ConverterRegistry`) |
| `SndContextParameters.cs` | SndContext construction parameter object. Contains startup configuration properties such as `AutoDiscoverStrategies`, `DiscoverySkipPrefixes`, `SceneAliasMapPath`, `SndTemplateMapPath`, `InitialLevelId` |
| `SndWorld.cs` | SND world: strategy pool + type mapping + converter registry + templates/aliases. `LoadSceneAliases` / `LoadTemplates` are `internal`, invoked by `SndContext.Bootstrap` or the `ISndTemplateAccess` companion (`ctx.Template.LoadTemplates` / `ctx.Template.LoadSceneAliases`) |
| `SndDefaults.cs` | `internal` — SND system default value constants. Defines `InitialSaveId` ("000"), `InitialLevelId` ("default"), `MainMenuLevelId` ("main_menu"), used by Core's internal persistence flow and startup orchestration. |
| `SndMappings.cs` | Scene alias resolution + template registration and parsing |
| `SndTemplateResolver.cs` | Template resolver: supports both JSON array and .map shorthand template formats |
| `TryGetNumericExtensions.cs` | Entity data numeric-type compatible read extensions: tries float → int → the remaining integer types (byte/sbyte/short/ushort/char/uint/ulong) → long → double in order. Note the precision boundary: int→float may lose precision above 2²⁴, uint/ulong→float loses precision, double→float narrowing may overflow to ±Infinity — none of these is checked — suitable for close-range gameplay values, not for metrology that needs exact representation |
| `ActiveStrategyExtensions.cs` | Generic ActiveStrategy invocation extension: eliminates JSON serialization boilerplate on the `InvokeStrategy` side |
| `EntityExtensions.cs` | Entity identity comparison extension methods such as `IsSameEntityAs` |
| `SndContextFileAccess.cs` | `internal` — `ISndFileAccess` companion implementation (see Companions) |
| `SndContextArchiveFileAccess.cs` | `internal` — `ISndArchiveFileAccess` companion implementation (see Companions) |

## Entity Model

```
SndEntity (aggregate root)
├── SndDataManager
│   ├── DataObserverManager (data key → subscription callbacks, for observer strategy wiring)
│   └── Dictionary<string, TypedData> (data storage, the single authoritative state)
├── SndNodeManager : INodeHost
│   ├── Dictionary<string, INodeHandle> (node storage)
│   └── INodeFactory (node creation, injected by adapter layer)
├── SndStrategyManager (passive strategies)
│   ├── List<StrategyEntry> (sorted by priority, iterated per frame)
│   └── SndStrategyPool (global strategy pool reference)
├── ActiveStrategyManager (active strategies)
│   ├── Dictionary<string, ActiveStrategyBase> (O(1) lookup by index)
│   └── SndStrategyPool (shares the same pool instance)
└── ObserverTopology reference (observer strategies, per-scene-host shared, not per-entity private)
    ├── Bidirectional binding index (observer ↔ target, centralized at scene host)
    └── Data change wiring (via ISndEntityRawSubscription) + binding serialization/recovery
```

## Strategy Lifecycle Hooks (in order)

1. **AfterSpawn** — After new entity creation
2. **AfterLoad** — After entity recovered from save
3. **AfterAdd** — After strategy dynamically added to entity
4. **Process** — Per frame execution (by priority)
5. **BeforeRemove** — Before strategy removed from entity
6. **BeforeSave** — Before serialization for save
7. **BeforeQuit** — Before entity normal exit
8. **BeforeDead** — Before entity destruction

> **Batch lifecycle (batch orchestration):** `CreateEntity`, `RecoverFromMetaList`, `RemoveAllEntities` are holistic container operations; they do not fire AfterSpawn / AfterLoad / BeforeDead hooks per entity. Hooks are uniformly fired by the upper layer (`SndEntityFactory`'s spawn, `SessionRun`'s load/save/quit/kill lifecycle) after batch operations complete, sorted by priority.

## Observation System

SND's observation is uniformly carried by observer strategies (`ObserverStrategyBase`); self-observation and cross-entity observation use the same mount API and the same binding topology:

- **Declare observed keys**: The `[ObserveData("hp")]` attribute declares keys the strategy cares about (multiple declarations supported)
- **Respond to data changes**: Implement `OnDataChanged(entity, ctx, target, dataKey, oldValue, newValue)`
- **Mount/unmount callbacks**: `OnMounted` / `OnUnmounted`
- **Self-observation**: `entity.MountObserverStrategy(entity.Name, "my_game.hp_watcher")`
- **Cross-entity observation**: First resolve target via `entity.OwningSession.FindByName(name)`, then `observer.MountObserverStrategy(target, "...")`

Observer binding topology is serialized with entities through `StrategyMetaData.ObserverIndices`; on load, `SessionRun` automatically restores wiring via the scene host's `ObserverTopology` — no need to manually reconnect in `AfterLoad`. Auto-unmounts when the target or observer dies. Public interfaces: see [ISndObserverStrategyAccess](../Abstractions/Entity/README.en.md#isndobserverstrategyaccess); strategy types: see [Strategy](Strategy/README.en.md); implementation details: see [SndEntity](Entity/README.en.md#sndentity-aggregate-root).

## Core Principles

- **Stateless strategies**: Strategy instances are shared; mutable state lives in entity Data
- **Node decoupling**: Core does not hold engine node references; operates through `INodeHandle` abstraction
- **Metadata-driven**: Entity creation/recovery/serialization all goes through `SndMetaData` mediation
- **Inline storage**: `TypedData` is a value type (struct), using source-generator-generated discriminated union to inline-store value types ≤ 8 bytes in `_inlineBits`, with zero boxing and zero heap allocation
- **Unified observation**: Self-observation and cross-entity observation use the same observer strategy API; binding topology persists with entities, auto-restores on load, and auto-unmounts on death

## Startup Flow

`SndContext.Bootstrap()` executes all Core initialization operations in a fixed order:

1. **Converter registration**: if `SndContextParameters.ConfigureConverters` is set, it is invoked to register custom `DataSourceConverter`s
2. **Strategy discovery**: If `SndContextParameters.AutoDiscoverStrategies` is true, scans assemblies for `[StrategyIndex]` annotated types via the `internal` `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`, using `DiscoverySkipPrefixes` to filter adapter-layer assemblies
3. **Scene alias loading**: If `SceneAliasMapPath` is non-empty, calls the `internal` `SndWorld.LoadSceneAliases()`
4. **SND template loading**: If `SndTemplateMapPath` is non-empty, calls the `internal` `SndWorld.LoadTemplates()`
5. **Entry save loading**: Calls `RequestLoadMainMenuEntrySave()`

The adapter layer only passes configuration via `SndContextParameters` and does not need to know the execution order or internal implementation of the above steps.

> **Bootstrap guard**: `Bootstrap()` may be executed only once (a second call throws `InvalidOperationException`), and it validates that the adapter scene host is ready before enqueuing the entry save load — if the host is an `IObserverTopologyHost` whose observer topology has no bound context (e.g. `Bootstrap` called before `SndManager.BindContext`), it throws immediately with a clear message instead of failing later at flush time. The entry save load is deferred (system deferred queue), so failing early prevents a misordered caller from getting a confusing late error. The single-use guard is committed before any bootstrap work starts, so **a failed first attempt also prevents retry on the same SndContext instance**; callers should construct a new context.

### Why Startup Orchestration Is Centralized in SndContext.Bootstrap()

The adapter layer should not directly call `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`, `LoadSceneAliases()`, `LoadTemplates()`, `RequestLoadMainMenuEntrySave()`. These are Core internal orchestration operations — strategy discovery must execute in the Core layer (using skip prefixes provided by the adapter), alias/template loading is Core configuration parsing, and entry save loading is a Core lifecycle entry point. Centralizing them in `Bootstrap()` ensures these operations complete in the correct dependency order in the correct layer.

---
[↑ Back to Origo.Core](../README.en.md)
