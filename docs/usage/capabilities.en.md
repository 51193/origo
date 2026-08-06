<!-- docsync-pair: usage/capabilities -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Capabilities

> [↑ Back to Usage Documentation](README.en.md)

All capabilities of the Origo framework, organized by functional domain. Each entry includes a capability description and documentation entry point, making it easy for developers to look up as needed.

## Entities & Strategies

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| SND entity model | Strategy (behavior), Node (presentation), Data (state) — ternary decoupled model | [SND Entity Model](snd-entity-model.en.md) |
| 8 lifecycle hooks | AfterSpawn / AfterLoad / AfterAdd / Process / BeforeRemove / BeforeSave / BeforeQuit / BeforeDead | [SND Entity Model](snd-entity-model.en.md) |
| Stateless strategy pool | Strategy instances are shared and reused; registration validates statelessness via reflection; ref-counted management | [↔ Snd/Strategy](../Origo.Core/Snd/Strategy/README.en.md) |
| Strategy priority ordering | Process and other hooks execute in ascending Priority order; equal priority uses FIFO | [SND Entity Model](snd-entity-model.en.md) |
| TypedData type preservation | Read-only partial struct inline storage; Source Generator generates typed conversions; JSON round-trip preserves precision | [SND Entity Model](snd-entity-model.en.md) |
| Data observers | Observer strategies (`ObserverStrategyBase` + `[ObserveData]` attribute) respond to entity data changes; mount/unmount via `MountObserverStrategy`/`UnmountObserverStrategy`; bindings persist with saves, auto-restore on load, auto-cleanup on entity death | [SND Entity Model](snd-entity-model.en.md) |
| Cross-entity observation | `MountObserverStrategy(target, observerIndex)` supports self-observation and cross-entity observation; `OnMounted`/`OnUnmounted` carries lifecycle awareness | [SND Entity Model](snd-entity-model.en.md) |
| Active strategies | Externally invoked by index via Invoke; independently managed container separate from passive strategies; O(1) lookup | [Strategy Testing](strategy-testing.en.md) |
| Generic active strategy invocation | `InvokeStrategy<TInput, TOutput>` extension methods; type-safe, eliminating JSON serialization boilerplate | [↔ Snd/Strategy](../Origo.Core/Snd/Strategy/README.en.md) |
| SndMetaFluentBuilder | Fluent API for building entity metadata, eliminating `??= new DataMetaData()` boilerplate | [↔ Snd/Metadata](../Origo.Core/Snd/Metadata/README.en.md) |
| TryGetNumeric | Compatible numeric reads from entity data, bridging the type mismatch between `SetData("k", 5)` (int) and `TryGetData<float>("k")` | [↔ Snd](../Origo.Core/Snd/README.en.md) |
| Numeric recipe loading | SndArchetypeLoader loads archetypes from key-value pair files and infers types to write into entities | [↔ Snd/Archetype](../Origo.Core/Snd/Archetype/README.en.md) |
| Lazy strategy attachment | EnsureStrategy extension methods, lazy strategy layer initialization with idempotent guard | [SND Entity Model](snd-entity-model.en.md), [↔ Snd/Strategy](../Origo.Core/Snd/Strategy/README.en.md) |

## Session Management

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| Four-layer runtime | SystemRun → ProgressRun → SessionManager → SessionRun layered lifecycle | [Architecture Overview](architecture-overview.en.md) |
| Foreground/background session isomorphism | Background sessions share the same ISessionRun interface and strategy pipeline as the foreground | [Session Model](session-model.en.md) |
| Session topology encoding | SessionTopology text-format codec, recording key/levelId/syncProcess for all active sessions | [Session Model](session-model.en.md) |
| LevelId global uniqueness | At most one session per levelId at any given time; throws on conflict | [Session Model](session-model.en.md) |

## Persistence

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| Two-phase write | Write `current/` first (with .write_in_progress marker), atomically copy to `save_{id}/` after validation | [Persistence Flow](persistence-flow.en.md) |
| Strict read validation | .write_in_progress marker detection, level three-file integrity check, progress.json mandatory presence | [Persistence Flow](persistence-flow.en.md) |
| Snapshot management | EnumerateSaveIds / EnumerateSavesWithMetaData, supports save-selection UI | [Persistence Flow](persistence-flow.en.md) |
| meta.map display metadata | Display metadata system separated from business data, ISaveMetaContributor pluggable contributor pattern | [Persistence Flow](persistence-flow.en.md) |
| Idempotent deduplication | SHA256 hash comparison; same game state skips I/O write | [↔ Save/Storage](../Origo.Core/Save/Storage/README.en.md) |

## State Machine

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| String-stack state machine | Stack holds only string identifiers; behavior is defined by associated Push/Pop strategies | [State Machine](state-machine.en.md) |
| Push/Pop strategy hooks | OnPushRuntime / OnPushAfterLoad / OnPopRuntime / OnPopBeforeQuit, with BeforeTop/AfterTop migration context | [State Machine](state-machine.en.md) |
| Two-phase load recovery | On load, the framework restores the stack via internal `RestoreStackWithoutHooks` (silent restore) → `FlushAfterLoad` (replay hooks in order) | [State Machine](state-machine.en.md) |

## Console System

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| 11 built-in commands | help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy | [Console Commands](console-commands.en.md) |
| Custom command registration | Core layer inherits ConsoleCommandHandlerBase; adapter layer inherits CommandHandlerBase | [Console Commands](console-commands.en.md) |
| TCP remote console bridge | ConsoleBridgeServer listens on localhost:9876, single-connection mode, bidirectional I/O via pub-sub | [Console Commands](console-commands.en.md) |
| Command type inference | bb_set / entity_set_data auto-infers int/float/bool/string types; existing keys preserve their original type | [Console Commands](console-commands.en.md) |

## Data & Serialization

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| DataSource abstraction layer | Unified tree data model DataSourceNode (Map/Array/Text/Number/Bool/Null + Lazy), with IDataSourceIoGateway as Core layer's sole file entry point | [↔ DataSource](../Origo.Core/DataSource/README.en.md) |
| JSON + .map codec | JsonDataSourceCodec (lazy expansion), MapDataSourceCodec (key:value flat structure) | [↔ DataSource/Codec](../Origo.Core/DataSource/Codec/README.en.md) |
| Lazy JSON expansion | Nested objects/arrays are parsed only on first access, amortizing large save parsing costs | [↔ DataSource](../Origo.Core/DataSource/README.en.md) |
| Type-string bidirectional mapping | TypeStringMapping maintains bidirectional mapping between CLR types and stable string identifiers, avoiding FullName version coupling | [↔ Serialization](../Origo.Core/Serialization/README.en.md) |
| Godot 14 type serialization | Vector2/3/4, Vector2I/3I, Quaternion, Color, Basis, Transform2D/3D, Rect2/2I, Aabb, Plane — full JSON round-trip | [↔ GodotAdapter/Serialization](../Origo.GodotAdapter/Serialization/README.en.md) |
| Converter registration & inheritance backtracking | DataSourceConverterRegistry backtracks along base class and interface chains when no exact type converter is registered | [↔ DataSource/Converters](../Origo.Core/DataSource/Converters/README.en.md) |
| Strategy file access (ISndFileAccess) | Strategies read/write JSON/Map files via ISndContext, automatically parsed through the IDataSourceIoGateway boundary into DataSourceNode trees or strongly-typed objects | [Architecture Overview](architecture-overview.en.md), [↔ Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.en.md) |
| In-save file access (ISndArchiveFileAccess) | Strategies read/write files (including deletion) in the save's extra/ subdirectory via ISndContext; files follow save lifecycle: included in save snapshots after writes, auto-restored on load | [Architecture Overview](architecture-overview.en.md), [↔ Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.en.md) |

## Godot Adapter

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| res:// + user:// file system | GodotFileSystem implements IFileSystem, supporting virtual paths and path traversal protection | [↔ GodotAdapter/FileSystem](../Origo.GodotAdapter/FileSystem/README.en.md) |
| Log proxy | GodotLogger injects GD.Print / PushWarning / PushError via delegates, no internal formatting | [↔ GodotAdapter/Logging](../Origo.GodotAdapter/Logging/README.en.md) |
| PackedScene node instantiation | GodotPackedSceneNodeFactory loads scenes from resource paths and instantiates them as GodotNodeHandle | [↔ GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.en.md) |
| GodotEntity + StableName | GodotSndEntity bridges ISndEntity with the Godot Node lifecycle; independent StableName avoids Godot auto-rename interference | [↔ GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.en.md) |
| Scene alias resolution | Resolves logical aliases to res:// resource paths via SndMappings | [↔ GodotAdapter/Bootstrap](../Origo.GodotAdapter/Bootstrap/README.en.md) |
| Adapter-layer console commands | press_button (simulate button click), tree_debug (print entity node tree) | [↔ GodotAdapter/Console](../Origo.GodotAdapter/Console/README.en.md) |

## Testing Infrastructure

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| StrategyTestScenario | Declarative strategy unit testing framework, three-phase pattern (Configure → Simulate → Inspect) | [Strategy Testing](strategy-testing.en.md) |
| ActiveStrategy testing | Standalone ActiveStrategy test harness, Invoke / InvokeViaEntity / data read-write | [Strategy Testing](strategy-testing.en.md) |
| Architecture guardrail tests | Core / Adapter / ConsoleBridge each verify architectural constraints such as dependency direction, interface composition, and strategy statelessness | [↔ Core.Tests](../Origo.Core.Tests/README.en.md), [↔ GodotAdapter.Tests](../Origo.GodotAdapter.Tests/README.en.md), [↔ ConsoleBridge.Tests](../Origo.ConsoleBridge.Tests/README.en.md) |

## Auxiliary Capabilities

| Capability | Description | Doc Entry |
|------------|-------------|-----------|
| XorShift128+ random | Period 2^128-1, no global state, caller explicitly passes seed, cross-platform consistency | [↔ Random](../Origo.Core/Random/README.en.md) |
| PersistentRandom | Blackboard-persisted random state: InitSeed → TryNextInt32/NextInt32/NextFloat, save-safe and recoverable | [↔ Random](../Origo.Core/Random/README.en.md) |
| 2D noise map generation | OpenSimplex2 (70%) + Worley Cellular (30%) mixed noise, basic + extended overloads (custom octaves/lacunarity/gain) | [↔ Random](../Origo.Core/Random/README.en.md) |
| Grid coordinate system | GridPos type, GridCoordinateSystem single/dual-axis conversion, A* pathfinding, GridParser coordinate parsing | [↔ Grid](../Origo.Core/Grid/README.en.md) |
| In-memory blackboard | IBlackboard default implementation, SetValue/TryGet/SerializeAll/DeserializeAll, key case-sensitive | [↔ Blackboard](../Origo.Core/Blackboard/README.en.md) |
| Deferred action scheduling | ConcurrentActionQueue thread-safe queue, snapshot-drain pattern, supports re-enqueue during execution | [↔ Scheduling](../Origo.Core/Scheduling/README.en.md) |
| Structured log builder | LogMessageBuilder fluent API (SetElapsedMs / AddPrefix / AddSuffix / Build) | [↔ Logging](../Origo.Core/Logging/README.en.md) |

## Framework Design Properties

| Property | Description | Doc Entry |
|----------|-------------|-----------|
| Platform-agnostic | Origo.Core depends only on System.\*, references no engine-specific code | [Architecture Overview](architecture-overview.en.md) |
| Adapter-layer isolation | Engine code only implements Core abstractions in Origo.GodotAdapter; adapter layer does not participate in strategy lifecycle management | [Architecture Overview](architecture-overview.en.md) |
| Interface Segregation (ISP) | ISndContext split into 9 narrow role interfaces; ISessionRun returns abstract IStateMachineContainer | [Architecture Overview](architecture-overview.en.md) |
| Single-threaded frame model | One frame = one logical atomic boundary; deferred actions execute sequentially through queues. The host (e.g., Godot `_Process`) drives the frame via `IOrigoFrameDriver.DriveFrame(double delta)`; Core internal order: entity Process → business queue → Kill pending → system queue → console | [Architecture Overview](architecture-overview.en.md) |

---

> **Usage suggestion**: New users can start from [Quick Start](quick-start.en.md), then browse this checklist and pick capabilities of interest for deeper reading.
>
> **AI agents**: Please consult [Agent Reference](agent-reference.en.md) directly for complete interface signatures and runtime reference.

[↑ Back to Usage Documentation](README.en.md)
