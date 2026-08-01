<!-- docsync-pair: Origo.Core.Tests/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.Core.Tests

> [↑ Back to Origo.manual](../README.en.md)
> [↔ Test Documentation Meta-Instructions](META-TEST.en.md)

## Test Strategy Overview

The tests for Origo.Core follow the "**behavior-oriented, documentation-contract-oriented**" principle:

- **Do not test internal implementation details**: Each test verifies a specific behavioral contract
  described in `usage/` or module documentation, not the internal shape of code. Principle: if behavior
  can be verified through public interfaces like `ISndContext` / `ISessionManager`, `InternalsVisibleTo`
  should not be used to directly access internal types. See
  [Test Documentation Meta-Instructions — InternalsVisibleTo Whitelist Principle](META-TEST.en.md#internalsvisibleto-whitelist-principle).
- **Happy path, error path, boundary path equally covered**: Each capability document organizes test
  methods by these three path categories.
- **Use TestFileSystem**: All file I/O tests use the in-memory file system (`TestFileSystem`), no real
  disk operations. Strategy test context (`StrategyTestContext`) has built-in `MemoryFileSystem` full
  pipeline, supporting ISndFileAccess behavioral verification.
- **Strategy isolation testing**: `StrategyTestScenario` framework allows testing individual strategy
  lifecycles in a completely runtime-free environment. `TestContextBuilder` provides SndContext
  construction for integration test scenarios.

## Test Support Facilities

The test project provides the following core support facilities via `TestSupport/` files:

| Facility | Type | Purpose |
|----------|------|---------|
| `TestFileSystem` | `IFileSystem` implementation | In-memory file system supporting full read/write/enumerate/copy/rename/delete, for I/O tests |
| `TestSndSceneHost` | `ISndSceneHost` implementation | Render-free scene host, maintains entity list, records Spawn/ClearAll calls |
| `TestLogger` | `ILogger` implementation | Collects logs into lists, supports categorized queries by level (Debug/Info/Warning/Error) |
| `TestNodeFactory` | `INodeFactory` implementation | Node factory capable of injecting failing resources |
| `DummySndEntity` | `ISndEntity` implementation | In-memory entity implementation, provides SetData/GetData/TryGetData |
| `NullSndContext` | `ISndContext` null object implementation | Null context for runtime-only unit tests; queries return empty objects, mutation operations (save/load/level switch, etc.) explicitly throw for fail-fast |
| `StrategyStateTestsCollection` | xUnit `[CollectionDefinition]` | Defines `StrategyStateTests` serial collection (`DisableParallelization`), for strategy test classes with static mutable state to run serially, preventing cross-test pollution |
| `TestFactory` | Static factory class | Quickly creates commonly used compositions: OrigoRuntime / SndWorld / ProgressRun / ConverterRegistry, etc. |
| `TestContextBuilder` | Fluent Builder | Constructs `SndContext` instances (integration tests), provides sensible defaults and optional overrides, replacing repetitive 10-line construction patterns |
| `GameplaySimulationHarness` | Fluent Builder + Harness | One-click creation of complete frame-driven game simulation environment: OrigoRuntime + SndContext + background game session (syncProcess=true), supports DriveFrame/RunFrames/SpawnEntity/GetEntityData/SaveAndReload |
| `TestStrategies` | Abstract base class collection | `SharedFrameCounterStrategy`, `SharedEchoActiveStrategy`, `SharedKillProbeStrategy`, `SharedNoopLifecycleStrategy`, `SharedNoopStateMachineStrategy` — referenced by integration test files via 1-line sealed subclass, eliminating duplicate strategy definitions |
| `TestObserverEvents` | Structured event recording | `TestObserverEvent` record (EventType/TargetName/DataKey/OldValue/NewValue) + `EventCollector` static AsyncLocal collector + `SharedDataChangeObserverStrategy` abstract base class — observer test assertions upgrade from substring matching to typed field exact comparison |
| `PerfReporter` | Static utility class | Performance test output formatting: Compare/Report methods, prints time/throughput/allocation comparison. Supports dual-channel output (`Console.Out` + `ITestOutputHelper`), ensuring results visible in both CI and local |
| `ConsoleInputBuffer` | `IConsoleInputSource` implementation | Console input queue (Core production code, used directly in tests) |
| `ConsoleOutputChannel` | `IConsoleOutputChannel` implementation | Console output channel (Core production code, used directly in tests) |

All test support facilities are `internal`, exposed to the test project via `InternalsVisibleTo`.

## Capability Document Index

Tests are grouped by **capability under test**, each document corresponding to an independent capability:

| Capability | Document | Verification Focus |
|-----------|----------|-------------------|
| Architecture Guardrails | [Architecture.md](Architecture.en.md) | Layer isolation (Core does not reference Godot), interface composition (ISndContext pure composition), strategy statelessness validation |
| Test Doubles | [Abstractions.md](Abstractions.en.md) | TestFileSystem / NullLogger / TestFileSystemAdditional correctness |
| Blackboard | [Blackboard.md](Blackboard.en.md) | Set/Get/TryGet/Clear/SerializeAll/DeserializeAll full lifecycle + key validation |
| Data Observer | [DataObserver.md](DataObserver.en.md) | Subscribe/Unsubscribe/Notify/Multiple subscribers/Re-entrancy safety/Clear |
| Data Source | [DataSource.md](DataSource.en.md) | DataSourceNode creation/access/lazy expansion, JSON encoding/decoding, Map encoding/decoding, type converter registration, TypedData converters, SndMetaData converters, IDisposable |
| Logging | [Logging.md](Logging.en.md) | LogMessageBuilder structured construction (prefix/suffix/elapsed) |
| Grid | [Grid.md](Grid.en.md) | GridCoordinateSystem single/dual-axis conversion, A* pathfinding, GridParser coordinate parsing |
| Random | [Random.md](Random.en.md) | XorShift128+ seed determinism, noise map generation |
| Planning | [Planning.md](Planning.en.md) | PlanExecutionStrategyBase: intent-driven plan execution, Action strategy auto plug/unplug, step progression |
| Type Serialization | [TypeStringMapping.md](TypeStringMapping.en.md) | TypeStringMapping bidirectional mapping, BCL pre-registration, conflict detection |
| Scheduling | [Scheduling.md](Scheduling.en.md) | ConcurrentActionQueue enqueue/drain/concurrency safety/recursive depth protection |
| Console | [Console.md](Console.en.md) | Command parser/router/input queue/output channel, 13 built-in command handlers (11 Core + 2 GodotAdapter), type inference |
| Runtime Core | [Runtime-Core.md](Runtime-Core.en.md) | OrigoRuntime construction, console injection, deferred frame action execution |
| Session Lifecycle | [Session-Lifecycle.md](Session-Lifecycle.en.md) | Session creation/destruction/switching, Dispose semantics, foreground/background protocol consistency, topology encoding/decoding |
| Persistence: Storage | [Save-Storage.md](Save-Storage.en.md) | Two-phase write, write_in_progress marker contract, level three-piece set integrity, path strategy, snapshot read/write, idempotent dedup |
| Persistence: Serialization | [Save-Serialization.md](Save-Serialization.en.md) | BlackboardSerializer, SndSceneSerializer, SaveContext orchestration |
| Persistence: Metadata | [Save-Meta.md](Save-Meta.en.md) | ISaveMetaContributor, SaveMetaMerger, meta.map encoding/decoding |
| SND Entity | [Snd-Entity.md](Snd-Entity.en.md) | SndEntity CRUD, AfterLoad hook, AutoInitializer recovery, batch lifecycle, owning session binding |
| SND Metadata | [Snd-Metadata.md](Snd-Metadata.en.md) | TypedData struct value semantics and IEquatable, SndMetaData deep copy, SG output verification, Fluent construction, TypedData integration |
| Performance Benchmarks | [Benchmarks.md](Benchmarks.en.md) | `[Category=Benchmark]` suite (run separately by `benchmark.sh`): TypedData real simulation + Entity lifecycle + Observer topology + DataSourceNode + Blackboard + Save + Concurrent queue + Random + Strategy performance |
| SND Scene | [Snd-Scene.md](Snd-Scene.en.md) | MemorySndSceneHost and FullMemorySndSceneHost Spawn/FindByName/LoadFromMetaList/ClearAll/CreateEntity/RemoveEntity/RequestKillEntity, NullNodeFactory |
| SND Strategy | [Snd-Strategy.md](Snd-Strategy.en.md) | Strategy priority ordering, pool reference counting/recycling, entity strategy lifecycle hooks, observer strategies, active strategy Invoke, strategy pool Get/Release and Process scaling performance measurement |
| SND Context | [Snd-Context.md](Snd-Context.en.md) | SndContext save/load/continue workflow, LevelBuilder, template resolution, Archetype loading |
| SND Extensions | [Snd-Extensions.md](Snd-Extensions.en.md) | EnsureStrategy lazy strategy attachment (idempotent), TryGetNumeric cross-numeric type read, InvokeStrategy generic invocation |
| File Access | [Snd-FileAccess.md](Snd-FileAccess.en.md) | ISndFileAccess DataSourceNode read/write round-trip on SndContext, strongly-typed round-trip, overwrite semantics, error/boundary paths |
| StrategyTestContext File Access | [StrategyTestContext-FileAccess.md](StrategyTestContext-FileAccess.en.md) | ISndFileAccess in-memory file system behavior on StrategyTestContext, DataSourceNode and strongly-typed round-trips |
| Archive File Access | [Snd-ArchiveFileAccess.md](Snd-ArchiveFileAccess.en.md) | ISndArchiveFileAccess extra/ subdirectory file operations on SndContext, DeleteFile, path traversal protection, save/load round-trips |
| State Machine | [StateMachine.md](StateMachine.en.md) | StackStateMachine push/pop/recovery/FlushAfterLoad, empty stack/empty string/Dispose boundary tests, Container CreateOrGet/serialization |
| Strategy Test Framework | [StrategyTestScenario.md](StrategyTestScenario.en.md) | Three-phase pattern (configure/run/assert), EntityStrategy harness, ActiveStrategy harness |
| Frame-Driven Integration Tests | [Testing/Integration/Integration.md](Testing/Integration/Integration.en.md) | GameplaySimulationHarness full runtime simulation: SndContext → Bootstrap → DriveFrame frame loop → Entity processing/Blackboard interaction/Deferred actions |
| Collection Diff Comparison | [Utility.md](Utility.en.md) | DiffUtility generic collection diff comparison (added/removed) + dedup semantics |
| Framework Meta-Info | [Meta.md](Meta.en.md) | OrigoMeta records: default banner non-empty, ToString includes name and version, value equality semantics |

---

[↑ Back to Origo.manual](../README.en.md)
