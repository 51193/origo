<!-- docsync-pair: usage/extension-directions -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Extension Directions and Deferred Designs

> [↑ Back to usage](README.en.md)

> **Nature of this document**: This page records alternative design directions produced by brainstorming. They are all "discussed, not implemented, deferred" ideas. They are not current framework capabilities, not roadmap commitments, and they do not change any existing interface semantics. The value of this page is to keep the complete "why not" trade-offs in the manual: when a developer or agent meets a related problem, they can first see the boundary of the current design and judge whether the benefit has grown enough to justify re-evaluation; when it has, they can pick up these pre-thought skeletons instead of reinventing them from scratch.

Before reading this page, understand the current baseline: [Architecture Overview](architecture-overview.en.md), [SND Entity Model](snd-entity-model.en.md), [Strategy Lifecycle](strategy-lifecycle.en.md), and [Design Patterns](design-patterns.en.md).

## Direction Summary

| Direction | Related Current Design | Core Reason for Deferral | Re-evaluation Signal |
|-----------|------------------------|--------------------------|----------------------|
| Unified tree namespace | `DataSourceNode` tree + `IDataSourceIoGateway` file-content boundary | Synchronous local reads are sufficient; remote access would block the frame, and a unified namespace affects every I/O contract | Local files, saves, and network resources need one access protocol |
| Entity-level concurrency | Single-threaded frame model + stateless strategy pool | Cross-entity reads/writes, observer notifications, dependencies, and teardown semantics would all need to be redefined; extremely high cost | Profiling shows entity `Process` dominates frame time, and entities are highly independent |
| Relative lifecycle strategy ordering | `StrategyIndexAttribute.Priority` numeric ordering | Numeric ordering is simple and sufficient at current scale; graph-based ordering has no realized benefit yet | Strategy count explodes and multi-person collaboration causes frequent priority collisions |
| Multiple ActiveStrategy implementations per index | Globally unique strategy index + per-entity single-implementation dictionary | The existing single-strategy switch covers current needs; dispatch and persistence complexity do not match the benefit | Many entity types share the same interaction verb and the switch becomes unmanageable |

## Direction 1: Unified Tree Namespace

### Relationship to the Current Design

Currently `DataSourceNode` is the intermediate product between persistent files and in-memory reads: files are parsed into a data tree by `IDataSourceIoGateway`, which selects codecs by suffix, and then converted into strongly-typed objects by `DataSourceConverterRegistry`. This design already has two important foundations:

- **Tree shape**: Map/Array/Text/Number/Bool/Null nodes form a universal data container.
- **Pluggable codecs**: any new file format can join the same I/O boundary by implementing a codec and registering a suffix route.

### Envisioned Shape

Promote "parsed result" into "unified namespace root": the local file system, the save `extra/` directory, entity data, and even network resources are all mounted as nodes of one tree. The runtime or context holds only a restricted tree root and no longer exposes a family of file I/O interfaces; all access becomes path navigation, for example:

```
path -> to -> file -> entity -> health_point
```

Directories, files, file contents, entities, and entity data keys are all nodes of the tree; codecs only mediate the boundary between nodes and external bytes. Access restriction becomes "grant only the root of a subtree" — anything outside the subtree is invisible, so path traversal is structurally impossible. Network access works the same way: remote resources are tree nodes, and callers do not distinguish local from remote.

### Benefits

- Removes the conceptual gaps among `ISndFileAccess` / `ISndArchiveFileAccess` / `IFileMetaAccess` / `IPathResolver`; strategies face a single access protocol.
- The save directory naturally becomes a restricted subtree; `extra/` isolation upgrades from string validation to structural isolation.
- The same node-access logic can cover local resources, save resources, and remote resources; tests can replace the entire tree root.

### Costs and Blockers

- **Synchronous model**: tree reads are synchronous today, which is acceptable for local files; once network resources become tree nodes, remote reads would block the frame. The DataSource pipeline has no async read, cancellation, or streaming expansion, and filling that gap would cut through codecs, the gateway, converters, and callers.
- **Content vs. metadata boundary**: file content (Gateway) and file-system structure operations (`IFileMetaAccess`) are currently separated. After tree unification, enumeration, existence checks, deletion, and copy must all be redefined as node operations while preserving the zero-bypass, fail-fast hard boundary.
- **Live-tree lifecycle**: `DataSourceNode` is currently a parsed snapshot; a unified namespace would make it a mutable live view, requiring definitions for cache invalidation, write-back, release, and partial-write failure.
- **Routing rules**: codec selection is currently by file suffix; after tree unification it must become selection by node capability or protocol, to avoid rule drift.

### Suggested Evaluation Slices

If re-evaluated, first build a read-only local file-system tree (still delegating to the existing `IFileSystem` and Gateway, as a compatibility facade) to verify that a "restricted tree root" can replace path strings; then mount the save `extra/` directory as a subtree; only then handle asynchronous remote nodes.

## Direction 2: Entity-Level Concurrency

### Relationship to the Current Design

The framework uses a single-threaded frame model: entities process serially within a frame, and deferred queues carry cross-frame actions. Strategy instances are stateless and globally shared, so strategy classes are conceptually thread-friendly; however, strategies within one entity are sorted by `Priority`, and that execution order is part of the design — all strategies of one entity cannot simply run concurrently.

### Envisioned Shape

The concurrency granularity should be the **entity**, not the strategy: strategies within one entity still run serially by priority, while different entities may run in parallel. For safety, concurrency should not be the default; it should be a property of the entity:

- The developer explicitly marks an entity as "concurrent", confirming that its logic does not depend on other concurrent entities.
- The entity data container switches to a concurrent mode (or uses a concurrency-safe structure / partitioned snapshot).
- Frame processing first runs all concurrent entities in parallel, waits at a barrier until all finish, then runs the remaining entities serially in a stable order.
- Lifecycle hooks, save/load, observer mount/teardown, and pending-kill cleanup remain serially orchestrated by the framework.

### Open Problems

| Problem | Risk | Candidate Constraint |
|---------|------|----------------------|
| A concurrent entity invokes another entity's ActiveStrategy | The target may be writing its own data concurrently, and multiple concurrent callers may hit the same target | The concurrent phase does not call across entities directly; requests are recorded as messages and delivered/executed by the framework in a stable order during the serial phase after the barrier |
| A concurrent entity reads another entity's Data | It may observe intermediate or stale state | Declare read/write key sets and build a dependency graph; or snapshot input data at the start of the concurrent phase |
| `SetData` fires observer callbacks synchronously | Callbacks run in a concurrent context, breaking the single-threaded assumption | The concurrent phase only collects changes; notifications are dispatched deterministically after the barrier |
| Dependencies between entities | Results depend on scheduling order after parallelization | Topologically order dependency edges; dependent entities join the serial batch; cycles fail fast |
| `Process` requests spawn/kill | The container is mutated concurrently | Mutations are recorded first and applied by the framework after the barrier |

### Why Deferred

Stateless strategies solve the "shared strategy instance across entities" pollution problem, which is **necessary but not sufficient** for entity-level concurrency; what actually requires redesign is the concurrency contract for entity Data, cross-entity calls, observer notifications, and container mutation. That cost is extremely high, and there is no performance bottleneck today — introducing it early would complicate the simplest and most reliable frame semantics.

### Re-evaluation Signals

- Profiling shows `SessionManager.ProcessAllSessions` / the scene host `ProcessAll` dominates frame time, and the hot spot is many largely independent entity computations.
- Large-scale background simulation appears, or on-screen entity counts exceed what the current serial model can cover.
- The team is willing to declare dependency and data-access constraints for "concurrent" entities.

## Direction 3: Relative Ordering Constraints for Lifecycle Strategies

### Relationship to the Current Design

Lifecycle strategy execution order is determined by `StrategyIndexAttribute.Priority`: smaller integers run first, the default is 6205, and equal priority uses insertion order. The framework also relies on this ordering for the layered "low priority produces, high priority consumes" execution model.

### Envisioned Shape

Replace "one global number" with explicit constraints:

```csharp
// Conceptual sketch; Before/After APIs do not currently exist
[StrategyIndex("game.action.move", Before = ["game.action.resolve"], After = ["game.intent"])]
public sealed class MoveActionStrategy : LifecycleStrategyBase { }
```

On registration or insertion, build a partial-order graph over the strategy indices and compute a topological order automatically; contradictions (cycles) throw immediately, and the remaining unordered strategies keep a deterministic order (for example insertion order / original Priority as a fallback).

### Benefits

- Eliminates the "what number should I choose near the default 6205" mental burden; teams no longer need to coordinate priority ranges.
- Ordering intent lives next to the strategy declaration and is locally readable; plugin strategies only declare their position relative to known strategies.
- Conflicts change from "silently wrong runtime results" to "fail fast at insertion/registration".

### Costs and Risks

- Dynamic `AddStrategy` / `RemoveStrategy` would need incremental topological insertion; current metadata stores only index lists. If constraints come from type attributes, the save format does not need to change, but per-entity constraint overrides would require extending `StrategyMetaData`.
- Transitive constraints can create distant orderings the developer did not expect; cycle detection fails fast, but locating the real contradictory edge still requires graph diagnostics.
- The complexity of graph construction and incremental sorting at large strategy counts is unverified; today's O(n) insertion plus O(n) traversal cost is negligible, and that guarantee becomes more complex once a graph is introduced.
- Numeric Priority is already intuitive enough for the current layered conventions (perception P4, scheduling P5, action P6).

### Re-evaluation Signals

- Strategy counts grow to the point where multiple people or plugins must collaborate, and priority-range conflicts become common.
- Repeated "two strategies both use 6205, and the order silently depends on insertion order" bugs appear.
- Business code starts requiring a hard constraint such as "strategy X must always run after strategy Y".

## Direction 4: Multiple ActiveStrategy Implementations per Index

### Relationship to the Current Design

Strategy indices are globally unique: one index can register exactly one implementation type. `ActiveStrategyBase` works like a function call, actively triggered by other strategies through `InvokeStrategy("index", input)`. The per-entity active strategy container is a `Dictionary<string, ActiveStrategyBase>`, so one entity also has only one implementation per index.

### Envisioned Shape

Upgrade the index from an "implementation name" to a "contract name / interface name"; one index may have multiple implementations, and the target entity chooses which one to bind:

- The contract `hurt` means the "received an attack" interaction interface; different entities bind `hurt` to different implementations (armored units, mechanical units, and bosses each have their own logic).
- Callers still use `InvokeStrategy("hurt", input)`; the dispatch layer routes to the concrete implementation using the target entity's binding table.
- A default implementation is supported; missing bindings fall back to the default, and binding conflicts or missing implementations fail fast.
- Binding relations persist with entity metadata so the contract-to-implementation mapping survives save/load.

### Benefits

- Cross-entity interaction gains an "interface-oriented programming" experience: callers depend only on the contract, not on the target entity type.
- Removes the large per-species/per-field `switch case` dispatch inside a single `hurt` strategy.
- New entity types only register their own implementation; the shared interaction strategy does not need modification.

### Costs and Risks

- The strategy pool changes from one-to-one registration (index → single factory) into three layers: a contract registry, an implementation registry, and a per-entity binding table.
- Every `InvokeStrategy` gains one dispatch lookup; contract/implementation namespaces must be defined to avoid colliding with the existing unique-index semantics.
- `StrategyMetaData` must record binding relations, and the save format plus recovery validation must be extended in sync.
- Input/output typing is weak: unless the contract layer enforces type signatures, errors move from compile time to invocation time.
- The full toolchain development cost is still unknown: contract discovery, binding diagnostics, and save-recovery failure localization would all have to be designed from scratch.

### Current Alternatives

Until the benefit materializes, the current capabilities can cover this:

- Read an entity field (such as `species`) inside one unique `hurt` ActiveStrategy and dispatch with `switch`.
- For lifecycle behavior, use the `*_impl` replaceable-implementation pattern: decide which concrete strategy to mount based on a data key.
- When callers are allowed to know the concrete index, invoke different concrete indices directly — at the cost of losing a uniform interface.

### Re-evaluation Signals

- Many entity types share the same interaction verbs (`hurt` / `use` / `talk`), and the switch in the shared strategy has become hard to maintain.
- Pluggable entity packs are needed: a new pack only declares "which contracts it supports" without modifying shared interaction strategies.
- Caller code repeatedly performs "read an entity type field first, then compose the concrete strategy index".

## Problem → Inspiration Index

| Symptom | Read Current Baseline First | Then Consider |
|---------|------------------------------|---------------|
| Strategies concatenate file paths, JSON paths, and entity data paths | [Architecture Overview - I/O Boundary](architecture-overview.en.md#io-boundary) | Direction 1 |
| Frame time concentrates in many entity updates, but in-entity strategy order must be preserved | [Architecture Overview - Concurrency Model](architecture-overview.en.md#concurrency-model) | Direction 2 |
| Priority numbers keep being adjusted and teams coordinate ranges around 6205 | [Design Patterns - Priority-Layered Execution](design-patterns.en.md#priority-layered-execution) | Direction 3 |
| A single ActiveStrategy contains a large switch by entity type | [Design Patterns - Entity Communication](design-patterns.en.md#inter-entity-communication) | Direction 4 |
| The save directory needs to be an inescapable sandbox | [Persistence Flow](persistence-flow.en.md) | Direction 1 |
| Observer notifications or cross-entity calls make concurrency dangerous | [SND Entity Model](snd-entity-model.en.md) | Direction 2 |

## Related Documents

- Current architecture: [Architecture Overview](architecture-overview.en.md)
- Strategy system implementation: [Strategy Module](../Origo.Core/Snd/Strategy/README.en.md)
- Data source implementation: [DataSource Module](../Origo.Core/DataSource/README.en.md)
- Scheduling implementation: [Scheduling Module](../Origo.Core/Scheduling/README.en.md)
- Entity implementation: [Entity Module](../Origo.Core/Snd/Entity/README.en.md)
- Common patterns: [Design Patterns](design-patterns.en.md)

---
[↑ Back to usage](README.en.md)
