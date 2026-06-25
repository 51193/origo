# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Added

- **`ObserverStrategyBase`** — new first-class strategy base class (alongside LifecycleStrategyBase and ActiveStrategyBase). Provides three virtual hooks: `OnMounted`, `OnDataChanged`, `OnUnmounted`. Observer strategies are stateless, pooled, and auto-persisted across save/load.
- **`ISndObserverStrategyAccess`** — new entity interface: `MountObserverStrategy(targetName, observerIndex)` / `UnmountObserverStrategy(targetName, observerIndex)`. Supports both self-observation (targetName == entity.Name) and cross-entity observation.
- **`ObserveDataAttribute`** — declare data keys that an ObserverStrategy observes. Multiple attributes per class supported. Keys are extracted at pool registration time.
- **`StrategyMetaData.ObserverBindings`** — new serialization field storing observer binding topology (`[{targetName: [observerIndices]}]`) alongside entity_indices and active_indices in save files.
- **`DiffUtility`** — generic diff helper in `Origo.Core.Utility`: `Diff<T>(old, new) -> (added, removed)`.
- **Observer persistence** — observer bindings are automatically serialized and restored across save/load cycles, eliminating the need for manual AfterLoad re-subscription.
- **`GridParser`** — coordinate string parser supporting `"x,z"` format and `JsonElement` input; returns `(int X, int Z)?`
- **`ISndEntity.EnsureStrategy()`** extension method — lazy strategy attachment with idempotency guard; checks a data key and only adds the strategy if no value is already set
- **Logger minimum level filtering** — `GodotLogger` and `TestLogger` accept a `MinimumLevel` parameter (default `Info`) to suppress `Debug`-level messages in production. `TestLogger` also exposes a settable `MinimumLevel` property for tests.
- **`PlanExecutionStrategyBase`** — intent-driven entity-level plan execution base class in `Origo.Core.Planning`. Manages subscription wiring, action strategy plug/unplug, plan advancement, and idle timing automatically. Users provide only `ResolveNextStep(intent, currentStep, failed, entity)` and `StepToActionIndex(stepType)`. Reduces AI scheduling boilerplate from ~250 lines to ~70.
- **`ISndEntity.EnsureReplaceableStrategy()`** extension method — supports the `*_impl` replaceable-strategy pattern. Reads a configured override from entity data, falling back to a default strategy index, with idempotency guard.

### Changed

- **BREAKING: `EntityStrategyBase` renamed to `LifecycleStrategyBase`** — the passive entity strategy base class that provides 8 lifecycle hooks (Process, AfterSpawn, AfterLoad, AfterAdd, BeforeRemove, BeforeSave, BeforeQuit, BeforeDead) has been renamed to more accurately convey its behavioral domain of lifecycle control. All subclasses must update their base type.
- **BREAKING: `SndStrategyPool`** — concrete strategy types must now be `sealed`. Registration rejects non-sealed, non-abstract types at startup with `InvalidOperationException`. This enforces the pool's singleton-sharing model and prevents accidental subclassing.
- **`LogMessageBuilder` format simplified** — `AddPrefix`/`AddSuffix` replaced by unified `AddContext`; output format changed from `[+ms] prefix=val | msg | suffix=val` to `[+ms] msg | key=val, key=val`
- **Log tags standardized** — all components use `nameof(ClassName)` consistently instead of mixed string literals
- **High-frequency log messages downgraded to `Debug`** — strategy pool create/release, entity lifecycle hooks (spawn/load/quit/dead), strategy manager add/remove, and per-strategy auto-registration messages no longer appear at `Info` level
- **`OrigoRuntime` banner downgraded to `Debug`** — no longer outputs the multi-line banner at `Info` level
- **Performance timing added to key operations** — `OrigoConsole` command processing, `SessionRun` create/dispose/load/persist, `ProgressRun` create/dispose, `SaveStorageFacade` write/snapshot, and `SessionManager` mount/destroy now include elapsed milliseconds in log output

### Removed

- **`ISndDataAccess.GetData<T>`** — removed from interface. Use `TryGetData<T>` (returns `(bool found, T? value)`) or `TryGetNumeric` (for numeric cross-type coercion) instead. The concrete `SndEntity` retains the method for framework-internal use, but it is no longer accessible through `ISndEntity`.
- **`ISndObservation`** — removed. The old cross-entity ObserveData/ObserveLifecycle API has been replaced by `ISndObserverStrategyAccess.MountObserverStrategy` / `UnmountObserverStrategy` with ObserverStrategy as a first-class strategy type.
- **`ISndEntityLifecycleAccess` / `EntityLifecycleEvent`** — removed. Lifecycle subscriptions via SubscribeLifecycle/UnsubscribeLifecycle on ISndEntity are no longer supported, and the accompanying `EntityLifecycleEvent` enum is removed along with them. Use ObserverStrategy with OnMounted/OnUnmounted instead.
- **`ISndDataAccess.Subscribe` / `Unsubscribe`** — removed. Self-data subscriptions must now use MountObserverStrategy with the entity's own name as the target.

### Fixed

- **`SndRuntime.ClearAll()`** — now properly tears down observer strategy bindings before releasing entities, triggering `OnUnmounted` callbacks (previously skipped, causing observer cleanup to be missed on scene clear).
- **`ISndEntityRawSubscription`** — removed dead `SubscribeLifecycleRaw` / `UnsubscribeLifecycleRaw` methods from the interface and all implementations (`SndEntity`, `GodotSndEntity`, `StubSndEntity`). These had no callers after the old `ISndObservation` / `ISndEntityLifecycleAccess` APIs were deleted.
- **`SndEntity.ResolveTargetForMount`** — error message now correctly guides users to resolve target via `SceneHost.FindByName` and use the `ISndEntity` overload, instead of referencing a nonexistent scene host method.
- **Demo log overflow** — excessive `Info`-level messages from strategy lifecycle and entity operations reduced through level adjustments and filtering
- **Test compliance** — `OrigoConsoleLoggingTests` refactored to verify logging behavior (level correctness, message ordering, tag correctness) rather than exact format strings; other format-coupled assertions cleaned up to match project test conventions
- **`SessionRun.Dispose`** — BeforeQuit hooks can now safely access `ctx.CurrentSession.SceneHost` and `ctx.CurrentSession.SessionBlackboard` during session teardown. Previously, the disposed flag was set before hooks fired, causing `ObjectDisposedException`. Now uses two-phase flag (`_disposing` for re-entrancy guard, `_disposed` set only after cleanup completes) with `try/finally` to guarantee entity removal even if hooks throw.
- **`ProgressRun.SessionLoading.ResetForeground`** — removed redundant `SndRuntime.ClearAll()` call after `DestroyForeground()`. The dispose already cleans all entities via `ReleaseAllEntitiesAndClear` + `RemoveAllEntities`. The redundant call could cause double BeforeQuit trigger on partially-cleaned entities if the first cleanup threw an exception.

---

## [0.0.7] - 2026-06-11

### Added

- **`Origo.ConsoleBridge`** module — TCP remote console server (`ConsoleBridgeServer`) for external agent interaction; single-connection model with Accept + Handle threads; configurable via `ConsoleBridgeOptions` (default port 9876)
- **`Origo.SourceGeneration`** module — Roslyn incremental source generator for TypedData inline types; dual-mode architecture (Home / Adapter) via `SndInlineTypesAttribute`; generates `KindMap`, `AsXxx`/`TryGetXxx` accessors, `TypedDataFactory<T>`, and `ModuleInitializer` registration
- **`ISndFileAccess`** — role interface for strategy-scoped structured file I/O (5 members: `ReadFile`, `WriteFile`, `FileExists`, `ReadObject<T>`, `WriteObject<T>`)
- **`ISndArchiveFileAccess`** — archive-scoped file I/O role interface (6 members: adds `DeleteFile`); paths relative to archive's `extra/` subdirectory
- **`SndMetaFluentBuilder`** — fluent API for constructing `SndMetaData` with typed setters (`SetInt`, `SetFloat`, `SetDouble`, `SetLong`, `SetBool`, `SetString`, `SetBytes`, `SetNode`, `AddEntityStrategy`, `AddActiveStrategy`) and `From(SndMetaData)` factory
- **`SndArchetypeLoader`** — `.map`-based archetype loading with type inference order (int → float → bool → string); `TryLoad(ISndFileAccess, string)` and `ApplyAttributes(ISndEntity, Dictionary<string, string>)`
- **`GridCoordinateSystem`** — grid↔world coordinate conversion utilities (`GridToWorld`, `WorldToGrid` with `outOfBounds` detection)
- **`PersistentRandom`** — entity-scoped deterministic RNG with blackboard persistence; `InitSeed`, `TryNextInt32`, `NextInt32(min, max)`, `NextFloat`; customizable state key names
- **Generic `InvokeStrategy<TInput, TOutput>`** extension methods on `ISndEntity` — type-safe active strategy invocation eliminating manual JSON serialization boilerplate
- **`TryGetNumericExtensions`** — cross-numeric-type entity data reading (`TryGetNumeric`, `GetNumeric`); fallback order: float → int → long → double
- **`SndEntityNodeExtensions`** — adapter-layer Godot node access: `GetNativeNode(INodeHandle)` and `GetNodeFromSnd<TNode>(ISndEntity, string)`
- **`ISndEntityOperations`** — entity destruction API: `RequestKillEntity(string entityName)`, `RequestKillAll()`; entity marked immediately (`IsPendingKill = true`), physically destroyed at end-of-frame
- **Dynamic strategy management** — `ISndStrategyAccess.AddStrategy` / `RemoveStrategy` (passive) and `ISndActiveStrategyAccess.AddActiveStrategy` / `RemoveActiveStrategy` (active) with `AfterAdd` / `BeforeRemove` lifecycle hooks
- **Entity lifecycle batch orchestration** — `SpawnMany` interface for bulk entity spawning
- **TypedData generation for `Origo.GodotAdapter`** — adapter-layer inline type registration (`Vector2`, `Vector3`, `Transform2D`, `Transform3D`, `Color`, etc.) and converter/TypeMap registration via source-generated `ModuleInitializer`
- **`ISaveMetaContributor` registration** via `ISndSaveOperations.RegisterSaveMetaContributor` — supports both `ISaveMetaContributor` interface instances and `Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>>` delegate overloads
- **Console commands**: `entity_get_data <entity> <key>`, `entity_set_data <entity> <key> <value>` (with type inference preserving existing key type), `tree_debug <entity>` (Godot scene tree visualization)
- **`CommandHandlerBase`** / **`PressButtonCommandHandler`** — adapter-layer console command infrastructure with tests
- **Strategy Priority** (`StrategyIndexAttribute.Priority`, default 6205): all lifecycle hooks (Process, AfterSpawn, AfterLoad, BeforeRemove, BeforeSave, BeforeQuit, BeforeDead) execute in ascending priority order; same priority uses FIFO insertion order; `SndStrategyPool.GetPriority(string index)` query API; `SndStrategyManager.InsertSorted` maintains sorted order
- **Extended `NoiseMapGenerator`** capabilities for procedural content generation
- **Deferred entity cleanup** — end-of-frame entity destruction with extended type mapping support
- **Extensive new test coverage**: SND hot-path functional and performance tests, TypedData integration and performance tests, save metadata contributor integration tests, coverage gap elimination, internal type downcast removal, static state isolation

### Changed

- **`IFileSystem` eliminated from public API** — all file content I/O routed exclusively through `IDataSourceIoGateway` (`ReadTree` / `WriteTree`); zero-bypass Gateway enforcement; `MemoryFileSystem` remains public for tests/adapters
- **`INodeHandle.Native` removed** — DIP enforced in `ISndStateMachineAccess`; adapter-layer node access migrated to `SndEntityNodeExtensions`
- **`ISaveMetaContributor.Contribute`** return type changed to `IReadOnlyDictionary<string, string>` (immutable contract)
- **`ISndEntity` subscription methods redesigned** — improved `Subscribe` / `Unsubscribe` API with lifecycle observation support
- **Frame control, entity processing, and bootstrap delegated from adapter to Core** — `GodotSndManager` thinned to pure Godot scene-tree binding; `OrigoAutoHost` delegates orchestration to Core
- **Session lifecycle overhauled** — collision detection and session switch semantics redesigned; topology builder deduplicated
- **ConsoleBridge pipeline simplified** — thread safety hardened with bounded buffers; `OrigoConsole` refactored for Open/Closed Principle
- **Save subsystem I/O streamlined** — public interface contract tightened; runtime error handling hardened with transactional rollback guards
- **Adapter/Core layer boundary solidified** — strict SOLID principle enforcement; hook firing made internal; constants extracted and code deduplicated
- **`ISndContext` interface redesigned** — SHA-based duplicate save detection; deferred entity cleanup integration
- **`SndStrategyManager.Add()` and `Recover()`** use insertion sort; all lifecycle hook snapshots inherit priority ordering; `SerializeIndices` returns indices in priority order preserved across Save/Load roundtrips
- **API renames** to resolve analyzer warnings and remove suppressions (e.g., `SetValue` → renamed, `ConsoleInputBuffer` alignment, `DataSourceNodeKind` alignment)

### Fixed

- **ConsoleBridge deadlock** resolved — thread synchronization rewritten to prevent blocking on concurrent read/write
- **Entity discoverability during hooks** — entities now visible to queries during lifecycle callbacks; session topology integrity preserved across concurrent operations
- **Command handler base classes** made `public` (were incorrectly `internal`, preventing adapter-layer extension)
- **Original load error preserved** — framework no longer swallows the root-cause exception during failed save loads
- **Readonly field validation relaxed** — strategy stateless enforcement no longer rejects `readonly` fields (only writable instance fields flagged)
- **Duplicate Godot entry** in bootstrap pipeline fixed
- **Assembly version format** corrected for nightly builds (`0.0.7.0` instead of malformed format)

---

## [0.0.6] - 2026-05-24

### Added

- **FastNoiseLite** vendored library (`Origo.Core/Addons/FastNoiseLite/`) and **`NoiseMapGenerator`** for deterministic noise map generation, enabling procedural content generation in background sessions
- **`THIRD_PARTY_NOTICES.md`** — attribution for vendored FastNoiseLite library
- **`IDataSourceIoGateway` / `DataSourceIoGateway`** — unified DataSource file I/O routing layer that replaces the scattered `IDataSourceCodec` injection in `SndWorld`; routes `meta.map`, save payloads, and serialization through a single abstraction
- **`StrategyTestScenario`** fluent builder and **`StrategyTestContext`** test double for isolated entity strategy testing with configurable data, blackboards, templates, and lifecycle hooks — enables fast standalone strategy verification without spinning up a full runtime
- **`SndContextParameters`** — sealed parameter object replacing the 8-argument `SndContext` constructor; supports optional `ISaveStorageService`, `ISavePathPolicy`, and initial-storage override via `init`-only properties
- **`SystemParameters`** — internal record struct consolidating the 6 scattered `SystemRun` constructor parameters into a single immutable argument
- **`ISndSceneHost.ProcessAll(double delta)`** — new interface method for unified frame-update of all alive entities; implementations that do not manage process cycles default to no-op
- **`ISndContextAttachableSceneHost`** — interface allowing foreground `SessionRun` to bind its `SessionSndContext` into the scene host after construction
- **`ISaveStorageService.ReadLevelPayload(string levelId)`** — internal method exposing level payload reads through the storage abstraction
- **`Debug`** level added to `LogLevel` enum (was documented as supported but not implemented)
- **`SndWorld.IsStrategyRegistered(string index)`** and **`SndWorld.GetRegisteredStrategyIndices()`** — diagnostics surface for debugging strategy registration failures
- **`OrigoMeta`** record (`OrigoMeta.cs`) — lightweight metadata carrier exposing `Name`, `Version`, and `Banner` for project introspection; default banner is a simple emoticon
- **GitHub Actions `release.yml`** — tagged-release workflow that builds, tests, packs `Origo.Core` and `Origo.GodotAdapter` NuGet packages, and creates a GitHub Release with auto-generated notes
- **Godot adapter tests**: `GodotSndBootstrapTests`, `GodotFileSystemPathTests`, `GodotJsonConverterRegistryTests`
- **New Core test files**: `StrategyTestScenarioTests`, `SndContextWorkflowTests`, `NullSndContextExtendedTests`, `SessionSndContextExtendedTests`, `NullLoggerTests`, `SaveGamePayloadTests`, `LevelBuilderExtendedTests`, `StubSndEntityTests`, `LifecycleStrategyBaseTests`, `StateMachineStrategyBaseTests`, `BlackboardSerializerTests`, `SaveContextTests`, `SndSceneSerializerTests`, `SaveMetaMapCodecExtendedTests`, `ProgressRunSessionLoadingEdgeTests`, `ConcurrentActionQueueConcurrencyTests`, and others

### Changed

- **`SndContext`** constructor refactored: accepts `SndContextParameters` instead of 8 individual parameters; `RunWorkflow` pattern extracted to consolidate `BeginWorkflow`/`try`/`finally`/`EndWorkflow` across save/load operations
- **`SystemRun`** constructor now takes `SystemParameters` record struct instead of 6 scattered parameters
- **`SndWorld`** constructor replaced `IDataSourceCodec jsonCodec` + `IDataSourceCodec mapCodec` with a single `IDataSourceIoGateway`
- **Save I/O**: `meta.map` read/write routed through `IDataSourceIoGateway`; `SavePayloadReader` enforces `.write_in_progress` marker detection to fail fast on interrupted saves; `ReadLevelPayload()` exposed as `internal` via `ISaveStorageService`
- **`RandomNumberGenerator`** made stateless — all state embedded in returned seed for reproducibility; improved seed handling
- **Strategy stateless enforcement** unified across auto-discovery and manual registration paths; extended to detect writable instance properties (not only fields)
- **Test reorganization**: `IntegrationTests/`, `SystemRuntimeTests/`, `SessionRuntimeTests/`, `SessionManagerRuntimeTests/`, `ProgressRuntimeTests/` flattened into domain-aligned subdirectories (`Architecture/`, `Runtime/`, `Snd/`, `Save/`, etc.); deleted `Utils/KeyValueFileParser.cs` moved to `DataSource/`
- **Code quality**: 14 implementation types correctly made `internal`; all files reformatted; functions split to meet 50-line constraint including `ConsoleCommandParser.TryParse` (66→42), `SaveStorageFacade.SnapshotCurrentToSave` (57→32), `DataSourceFactory.CreateDefaultRegistry` (55→8), `StateMachineContainer.DeserializeFromNode` (53→14), `OrigoAutoHost.CreateRuntime` (52→35)
- **`NullSndContext`** mutation operations now throw `InvalidOperationException` instead of silently swallowing — fail-fast semantics for save/load/level-change ops
- **`SystemRuntime`** constructor uses `ISaveStorageService` directly instead of raw file-system primitives
- **`ProgressRun`** split into nested `SessionLifecycle` and `SaveCoordinator` to isolate mount, switch, and save orchestration
- **`FullMemorySndSceneHost`** derives from `StubSndSceneHost` to reduce duplication
- **Godot adapter renames**: `GodotPathHelper` → `GodotPathResolver`, `SaveStorageCommon` → `SaveStorageGatewayFactory`
- **README** and **README.zh-CN** rewritten as concise, synchronized integration-focused guides aligned with current architecture; XML doc caveats added for `TryGetData<T>`, `ISndSceneHost.Spawn()`, and `ProjectReference` bridge-class requirement

### Removed

- **`origo.active_level_id`** — consolidated into `origo.session_topology`; all session topology is now a single codec-managed key
- **`IRandom`** abstraction — `RandomNumberGenerator` is now directly consumed
- **`SessionManagerParameters`** — empty YAGNI record struct deleted
- **`Origo.Core.Utils`** namespace — types relocated to `DataSource` and `Scheduling`
- Selected redundant integration test files replaced by the reorganised test suite

### Fixed

- **Crash recovery**: stale `current/` directory is now deleted on startup (`ExecuteLoadMainMenuEntrySaveNow`) to prevent entity name conflicts from interrupted prior runs
- `SndEntity.Spawn` / `Load` now tolerates null `StrategyMetaData` (uses `?.Indices`), matching the nullable declaration and avoiding a null-reference crash
- **Transactional rollback guards** in `SessionRun`, `ProgressRun`, `SndStrategyManager`, and `StackStateMachine` prevent partial state leaks when load or acquire operations fail mid-way
- **`DataSourceNode` lazy expansion** commits atomically — child graph is built in a local structure and assigned only on success, avoiding half-applied mutations
- Save **write-in-progress marker** validated by `SavePayloadReader` before reading — prevents loading corrupted or half-written saves

### Security

- Save `.write_in_progress` marker enforcement prevents loading inadvertently corrupted saves after an interrupted write, reducing risk of irreversible data corruption

---

## [0.0.5] - 2026-04-12

### Added

- **Structured runtime containers** — three internal, single-responsibility holders replacing the old `RunDependencies` / `RunFactory` DI pattern:
  - `SystemRuntime` — system-layer container; holds objects shared across the entire application lifetime: `ILogger`, `IFileSystem`, `OrigoRuntime`, `ISaveStorageService`, `ISavePathPolicy`, and `SaveRootPath`; convenience accessors expose `SndWorld`, `SndRuntime`, `ISndSceneHost`, and `SystemBlackboard`
  - `ProgressRuntime` — progress-layer container built from `SystemRuntime`; narrows the exposed surface to only the dependencies `ProgressRun` and `SessionManager` need; carries `IStateMachineContext` and `ISndContext` in addition to the shared subset
  - `SessionManagerRuntime` — session-manager-layer container built from `ProgressRuntime`; adds `ProgressBlackboard` passed directly to avoid ordering hazards; provides convenience `JsonCodec` and `ConverterRegistry` accessors
- **`NullSndContext`** — Null Object implementation of `ISndContext` for pure unit-test scenarios; all members are safe no-ops; exposes a singleton `Instance`; used internally before any `ProgressRun` is active
- **`SessionSndContext`** — session-level `ISndContext` decorator that wraps a global context but pins `CurrentSession` to a specific `ISessionRun`; ensures entity strategies executing within a session see the correct session binding via `IsFrontSession` and `CurrentSession`
- **`ISndContextAttachableSceneHost`** — new interface allowing `SessionRun` to bind its `SessionSndContext` to the scene host after construction, so all strategy hooks on entities see the session-scoped context
- **`SessionTopologyCodec`** — internal static codec for the session topology string stored under `WellKnownKeys.SessionTopology`; serializes / deserializes named session descriptors (`key`, `levelId`, `syncProcess`) with explicit failure on malformed entries; format: `key=levelId=syncProcess,...`
- **Structured parameter records** for lifecycle construction — each run tier now receives an immutable record instead of a flat parameter list:
  - `ProgressParameters(string SaveId)` — identifies the save slot to load for `ProgressRun`
  - `SessionManagerParameters` — empty record reserved for the unified (Runtime, Parameters) construction pattern
  - `SessionParameters(string LevelId, IBlackboard SessionBlackboard, ISndSceneHost SceneHost, bool IsFrontSession)` — full construction context for `SessionRun`, including the front-session flag set immutably by `SessionManager`
- **`ISessionRun.IsFrontSession`** — new read-only boolean property indicating whether the session is the foreground session; value is determined by `SessionManager` at creation time and never changes
- **`ISndContext.CurrentSession`** and **`ISndContext.IsFrontSession`** — new properties exposing the session binding and front-session flag to strategy hooks and game code; global context returns the foreground session; `SessionSndContext` returns its pinned session
- **`ISessionManager.ProcessAllSessions(double delta, bool includeForeground = false)`** — unified session process API replacing `ProcessBackgroundSessions`; by default processes only background sessions; optionally includes the foreground session when `includeForeground` is `true`
- **Expanded and reorganised test suite**:
  - `IntegrationTests/ContextBoundaryTests` — verifies `SessionSndContext` delegation and `NullSndContext` safety
  - `IntegrationTests/CoverageBoostTests` — broad integration coverage across runtime components
  - `ProgressRuntimeTests/PlayStopPlayRoundTripTests` — full play-stop-play round-trip: `ProgressRun` serializes, disposes, rebuilds, and deserializes; asserts foreground identity preservation, per-session blackboard isolation, and tick-state retention
  - `ProgressRuntimeTests/SndContextEntryFlowTests` — validates `RequestLoadMainMenuEntrySave` mounts a foreground session and spawns entities from the entry JSON config
  - `SessionRuntimeTests/BackgroundSession/BackgroundSession_CreationWithCorrectFlagTests` — asserts `IsFrontSession == false` on background sessions
  - `SessionRuntimeTests/BackgroundSession/BackgroundSession_MultipleInstancesAllowedTests` — asserts multiple background sessions may coexist
  - `SessionRuntimeTests/BackgroundSession/BackgroundSession_StrategyContextReceivesBackgroundFlagTests` — asserts strategies in background sessions receive `IsFrontSession == false`
  - `SessionRuntimeTests/FrontSession/FrontSession_CreationWithCorrectFlagTests` — asserts `IsFrontSession == true` on the foreground session
  - `SessionRuntimeTests/FrontSession/FrontSession_StrategyContextReceivesFrontFlagTests` — asserts strategies in the foreground session receive `IsFrontSession == true`
  - `SessionRuntimeTests/FrontSession/FrontSession_UniqueConstraintValidationTests` — asserts that creating a second foreground session is rejected
  - Existing tests reorganised into `IntegrationTests/`, `ProgressRuntimeTests/`, `SessionManagerRuntimeTests/`, `SessionRuntimeTests/` subdirectories for clearer categorisation; `TestDoubles.cs` added as a shared test-double module

### Changed

- **`SndContext`** refactored constructor: builds `SystemRuntime` and `SystemRun` directly instead of delegating to `RunFactory`; `partial` modifier removed; no longer holds `EntryPointWorkflow`, `SaveGameWorkflow`, or a `_saveMetaContributors` list
- **`SessionRun`** constructor simplified: accepts `(SessionManagerRuntime, SessionParameters)` instead of six individual parameters; creates and stores a `SessionSndContext` to bind itself as `CurrentSession` for all strategies executing within the session
- **`ISndContext`** API simplified:
  - `RequestSaveGame(string newSaveId)` — `baseSaveId` and optional `customMeta` parameters removed
  - `RequestSaveGameAuto(string? newSaveId = null)` — optional `customMeta` parameter removed
  - `ListSavesWithMetaData()` removed — save metadata retrieval is no longer part of the context interface
  - `ClearContinueTarget()` removed
  - `CreateLevelBuilder(string levelId)` removed from the interface (functionality remains internal)
  - `RegisterSaveMetaContributor` overloads removed from the public interface
- **`OrigoAutoHost`** and **`OrigoDefaultEntry.Bootstrap`** updated to match the refactored `SndContext` and `OrigoRuntime` initialization APIs

### Removed

- **`RunDependencies`** — superseded by the three structured runtime containers (`SystemRuntime`, `ProgressRuntime`, `SessionManagerRuntime`)
- **`RunFactory`** — replaced by inline construction in `SndContext` and the individual run classes
- **`IProgressRun`**, **`ISystemRun`** — public lifecycle interfaces removed; internal runtime state is no longer exposed via interfaces
- **`IOrigoRuntimeProvider`** — abstraction removed
- **`EntryPointWorkflow`**, **`SaveGameWorkflow`** — workflow orchestration helpers removed; logic consolidated into `SndContext`
- **`DefaultSessionDefaultsProvider`**, **`ISessionDefaultsProvider`** — session-defaults abstraction removed
- **`SndContext.SaveMeta.cs`** partial — save-meta-contributor registration removed from the `ISndContext` public API
- **Console command handlers** removed from `Runtime/Console/CommandImpl/`: `AutoSaveCommandHandler`, `ChangeLevelCommandHandler`, `ContinueGameCommandHandler`, `ListSavesCommandHandler`, `LoadGameCommandHandler`, `SaveGameCommandHandler`
- **`LevelBuilderTests`** (flat file) — replaced by tests reorganised into the new subdirectory structure

---

## [0.0.4] - 2026-04-04

### Added

- **Full three-tier lifecycle runtime** — concrete interfaces and implementations for the `SystemRun` / `ProgressRun` / `SessionRun` hierarchy:
  - `ISystemRun` — holds the system blackboard and loads/continues a save slot into a `ProgressRun`
  - `IProgressRun` — holds the progress blackboard, `ISessionManager`, and process-level state machine container
  - `ISessionRun` — holds the session blackboard, scene access, and session-level state machine container
  - `SystemRun`, `ProgressRun`, `SessionRun` — concrete sealed implementations of the three lifecycle tiers
  - `ISessionManager`, `SessionManager` — KVP-based session lifecycle manager; creates, holds, serializes/deserializes, and destroys sessions; no architectural distinction between foreground and background sessions
  - `EmptySessionManager` — Null Object implementation used before any `ProgressRun` is active
  - `RunFactory` — internal DI factory that constructs all three run tiers from `RunDependencies`
  - `RunStateScope` — scoping container holding a run's `IBlackboard` and deferred scheduler reference
- **`OrigoRuntime`** — unified runtime entry point aggregating the SND subsystem and the system blackboard; exposes dual `ActionScheduler` queues for business-deferred and system-deferred work; drives `FlushEndOfFrameDeferred` at end of each frame
- **`OrigoAutoInitializer`** — reflection-based auto-initialization: scans loaded assemblies for `BaseStrategy` subclasses and registers them to the strategy pool; reads a JSON config array of `SndMetaData` to auto-spawn entities; skips system/Microsoft/Godot assembly prefixes
- **`SndWorld`** — unified SND entry point encapsulating the strategy pool (`SndStrategyPool`), type string mapping, DataSource converter registry, and JSON/Map codecs; exposes entity creation, serialization, and template resolution
- **`ISndContext`** — comprehensive facade interface for strategy hooks and game logic: three-tier blackboard access, session management, save/load/auto-save, level change, console registration, deferred action scheduling, and state machine access; does not expose internal framework details
- **`SndContext`** — full `ISndContext` implementation; orchestrates `OrigoRuntime`, `SystemRun` / `ProgressRun` / `SessionRun` lifecycles, `SndWorld`, and all built-in console commands; handles `ContinueGame`, `LoadGame`, `SaveGame`, `ChangeLevel`, `ClearEntities`
- **`SndRuntime`** — lightweight facade combining `SndWorld` and an `ISndSceneHost`; provides `Spawn`, `SpawnMany`, `SerializeMetaList`, `ClearAll`, and `FindByName` over the scene host
- **`LevelBuilder`** — fluent API for offline level construction using `StubSndSceneHost`; supports adding entities and session blackboard key-value pairs; produces a `LevelPayload` via `Build()` or commits directly to disk via `Commit()`; decoupled from concrete storage via `ISaveStorageService`
- **`StateMachineContainer`** — manages multiple named `StackStateMachine` instances keyed by string; lifecycle aligned with strategy pool reference counts; uses `IStateMachineContext` to remain compatible with both foreground and background sessions
- **`IStateMachineContext`** — minimal context interface exposing system/progress/session blackboards, scene access, and deferred schedulers for state machine strategy hooks; carries no foreground/background semantics
- **`SessionStateMachineContext`** — session-level adapter that binds an `IStateMachineContext` global with a specific session's `IBlackboard` and `ISndSceneAccess`; ensures foreground and background sessions have identical state machine hook semantics
- **In-memory scene infrastructure**:
  - `StubSndSceneHost` — pure in-memory `ISndSceneHost`; used by `LevelBuilder` and unit tests
  - `FullMemorySndSceneHost` — full-featured in-memory scene host creating real `SndEntity` instances via `SndWorld`; complete strategy lifecycle, data subscription, and `Process` support; used for background sessions created via `SndContext.CreateBackgroundSession`
  - `NullNodeFactory` — engine-free `INodeFactory` producing `NullNodeHandle` placeholders; used internally by `FullMemorySndSceneHost`
- **`MemoryFileSystem`** — pure in-memory `IFileSystem` implementation with full directory and file emulation; used for background levels, `LevelBuilder`, and unit tests
- **`INodeHost`** — internal abstraction for SND node container behavior: node recovery, query, release, and metadata export
- **Save storage abstraction layer**:
  - `ISaveStorageService` — abstract read/write service for save slots; decouples callers from concrete layout; supports current-directory writes, snapshot copies, progress JSON, level scenes, state machine snapshots, and metadata
  - `DefaultSaveStorageService` — default `ISaveStorageService` implementation backed by `IFileSystem` and `ISavePathPolicy`
  - `ISavePathPolicy` — pluggable path policy interface for all save-related directory and file paths
  - `DefaultSavePathPolicy` — default `ISavePathPolicy` implementation using `SavePathLayout` rules
  - `SavePathLayout` — internal static helper defining standard relative path constants and assembly rules (`current/`, `save_*`, `level_*`, `.write_in_progress`, etc.)
  - `SavePathResolver` — resolves full paths by combining a root with `ISavePathPolicy` outputs
  - `SavePayloadReader` — typed payload reader that deserializes progress, session blackboards, state machines, and SND scenes from a save directory
  - `SavePayloadWriter` — typed payload writer that serializes a `SaveGamePayload` into the `current/` directory layout
  - `SaveGamePayloadFactory` — assembles a `SaveGamePayload` from live scene and blackboard state
- **Save metadata pipeline**:
  - `SaveMetaBuildContext` — context object passed to `ISaveMetaContributor` implementations during meta-building
  - `DelegateSaveMetaContributor` — delegate-based `ISaveMetaContributor` for inline registration
  - `SaveMetaMerger` — merges contributions from multiple `ISaveMetaContributor` instances into a unified `meta.map` file
- **`LogMessageBuilder`** — structured log message builder with prefix/suffix key-value context and optional elapsed-milliseconds annotation; used internally for consistent log formatting
- **`ConcurrentActionQueue`** — thread-safe deferred execution queue; batches `Action` delegates and drains them in bulk; guards against infinite synchronous re-entrancy (internal)
- **`ActionScheduler`** — `IScheduler` implementation backed by `ConcurrentActionQueue`; host calls `Tick()` to drain queued actions
- **Strategy infrastructure**:
  - `BaseStrategy` — root abstract base for all strategy types; enforces stateless constraint (no instance fields) detected at registration time by `OrigoAutoInitializer`
  - `LifecycleStrategyBase` — entity strategy base class with full lifecycle hooks: `Process`, `AfterSpawn`, `AfterLoad`, `AfterAdd`, `BeforeRemove`, `BeforeSave`, `BeforeQuit`, `BeforeDead`
  - `SndStrategyManager` — internal per-entity strategy set manager; drives all lifecycle callbacks
- **Godot adapter — bootstrap infrastructure**:
  - `OrigoAutoHost` — Godot `[GlobalClass]` node implementing `IOrigoRuntimeProvider`; creates a new `OrigoRuntime` or binds to an existing host via `HostPath`
  - `OrigoDefaultEntry` — default entry-point node extending `OrigoAutoHost`; delegates full initialization to `OrigoAutoInitializer` with Godot-specific skip prefixes; exports `ConfigPath`, `SceneAliasMapPath`, `SndTemplateMapPath`, `SaveRootPath`, `InitialSaveRootPath`, and `AutoDiscoverStrategies`
  - `OrigoConsolePump` — Godot node that pumps console input from a UI source into `OrigoConsole` on each frame
  - `GodotSndBootstrap` — static helper binding `GodotSndManager` runtime dependencies and context in a single call
  - `GodotSndManager` — Godot `Node`-backed `ISndSceneHost` that manages `GodotSndEntity` nodes in the scene tree
  - `GodotSndEntity` — Godot `[GlobalClass]` node wrapping Core's `SndEntity`; binds Core strategy lifecycle to Godot's `_Process` / `_Ready` / `_ExitTree` callbacks
  - `GodotPackedSceneNodeFactory` — `INodeFactory` that instantiates a Godot `PackedScene` and mounts it under a parent node
  - `GodotJsonConverterRegistry` — one-stop registration of all Godot built-in type mappings (`Vector2`, `Vector3`, `Transform2D`, `Transform3D`, `Color`, `Rect2`, `Quaternion`, `Basis`, etc.) and DataSource converters
  - `GodotFileSystem` refactored into three focused classes: `GodotFileOperations`, `GodotDirectoryOperations`, `GodotPathHelper`
- **Extensive new test coverage** (40+ test classes):
  - `AutoInitializerGuardTests` — strategy registration guard and stateless enforcement
  - `BackgroundSessionTests` — full lifecycle of background sessions (create, process, save, load, dispose)
  - `ConsoleTests` — console command parsing, routing, and output channel
  - `EmptySessionManagerTests` — Null Object session manager contract
  - `EntityAndSerializationExtendedTests` — extended entity data, node, and strategy serialization
  - `ForegroundBackgroundContractTests` — contract parity between foreground and background sessions
  - `JsonAndMappingsTests` — JSON codec and `TypeStringMapping` round-trips
  - `LevelBuilderTests` — `LevelBuilder` fluent API, `Commit`, and `Build`
  - `LifecycleRunsTests` — `SystemRun` / `ProgressRun` / `SessionRun` lifecycle state transitions
  - `MemoryFileSystemTests` — `MemoryFileSystem` read/write/rename/delete contract
  - `NullNodeFactoryTests` — `NullNodeFactory` and `NullNodeHandle` contract
  - `PersistentBlackboardTests` — `PersistentBlackboard` read/write/persist contract
  - `RandomAndStateMachine.ContainerTests` — `StateMachineContainer` create/get/persistence
  - `RandomAndStateMachine.SessionAndAdapterTests` — session-level state machine adapter
  - `RandomAndStateMachine.StringStackTests` — `StackStateMachine` string-key push/pop/peek
  - `RandomNumberGeneratorTests` — XorShift128+ determinism and distribution
  - `SaveMetaMergerTests` — multi-contributor metadata merge
  - `SavePathPolicyContractTests` — `ISavePathPolicy` path composition contracts
  - `SaveSystemExtendedTests` — extended save/load round-trips across all payload components
  - `SchedulingAndTypeMappingTests` — `ActionScheduler` tick behaviour and type mapping
  - `SessionDecouplingTests` — session isolation (independent blackboards, entity sets, state machines)
  - `SessionManagerTests` — session manager create/get/destroy/serialize lifecycle
  - `SndContextChangeLevelContractTests` — `ChangeLevel` contract and scene transition
  - `SndContextContinueContractTests` — `ContinueGame` contract
  - `SndContextDeferredExecutionTests` — deferred action scheduling and flush
  - `SndContextFlowTests` — full new-game and load-game flow through `SndContext`
  - `SndContextListSavesContractTests` — `ListSaves` enumeration contract
  - `SndContextLoadGameContractTests` — `LoadGame` contract
  - `SndContextSaveGameContractTests` — `SaveGame` contract
  - `SndEntityAfterLoadTests` — `AfterLoad` hook invocation on deserialized entities
  - `SndEntityAndAutoInitializerTests` — entity creation via `OrigoAutoInitializer`
  - `SndWorldAndDiscoveryCoverageTests` — strategy discovery and `SndWorld` coverage
  - `SpawnTemplateCommandHandlerTests` — `SpawnTemplateCommandHandler` integration
  - `StrategyPoolAndRuntimeTests` — strategy pool reference counting and runtime integration
  - `SystemBlackboardPersistenceTests` — system blackboard persist/restore across runs
  - `UtilityTests` — `ConcurrentActionQueue`, `KeyValueFileParser`, and other utilities

### Changed

- `GodotFileSystem` decomposed into `GodotFileOperations`, `GodotDirectoryOperations`, and `GodotPathHelper` for improved separation of concerns
- Save storage responsibility separated: `SaveStorageFacade` now coexists with the new `ISaveStorageService` / `DefaultSaveStorageService` abstraction used by the lifecycle runtime, enabling pluggable storage backends for testing and non-Godot environments
- `SndStrategyPool` integrated with `SndStrategyManager` for per-entity lifecycle dispatch
- `StackStateMachine` wired through `StateMachineContainer` for keyed multi-machine management

---

## [0.0.3] - 2026-03-31

### Added
- **DataSource abstraction system** — a new tree-based data representation layer for structured data access and conversion:
  - `DataSourceNode`, `DataSourceNodeKind`, `DataSourceFactory`
  - `DataSourceConverter`, `DataSourceConverterRegistry`, `IDataSourceCodec`
  - Codecs: `JsonDataSourceCodec`, `MapDataSourceCodec`
  - Converters: `DomainConverters`, `PrimitiveConverters`, `TypedDataConverter`
- **Expanded console command system** — 13 new command handlers covering all major runtime operations:
  - `AutoSaveCommandHandler` (`auto_save`)
  - `BlackboardGetCommandHandler` (`bb_get`)
  - `BlackboardKeysCommandHandler` (`bb_keys`)
  - `BlackboardSetCommandHandler` (`bb_set`)
  - `ChangeLevelCommandHandler` (`change_level`)
  - `ClearEntitiesCommandHandler` (`clear_entities`)
  - `ContinueGameCommandHandler` (`continue_game`)
  - `FindEntityCommandHandler` (`find_entity`)
  - `HelpCommandHandler` (`help`)
  - `ListSavesCommandHandler` (`list_saves`)
  - `LoadGameCommandHandler` (`load_game`)
  - `SaveGameCommandHandler` (`save_game`)
  - `ConsoleCommandHandlerBase` — abstract base class for all command handlers
- New abstractions: `IOrigoRuntimeProvider`, `ISndSceneHost`, `IStateMachine`, `INodeHandle`
- `OrigoJson` — unified JSON serialization utilities
- `BlackboardSerializer` — refactored blackboard serialization
- `WellKnownKeys` — well-known save key constants
- `SaveMetaDataEntry`, `ISaveMetaContributor` — structured save metadata
- `NodeMetaData`, `DataMetaData`, `SndMetaData` — explicit metadata structures for entity components
- `TypedData` — type-aware data value wrapper
- `StrategyIndexAttribute` — attribute for strategy indexing
- `StateMachinePersistenceModels` — persistence model for state machines
- `GodotDataSourceConverters`, `GodotLogger`, `GodotNodeHandle` — Godot adapter additions
- Architecture guardrail tests (`AdapterArchitectureGuardrailTests`, `CoreArchitectureGuardrailTests`)
- New tests: `DataSourceTests`, `ConsoleCommandExtendedTests`, `TypeStringMappingTests`

### Changed
- Refactored `ConsoleCommandParser` and `ConsoleCommandRouter` with improved command dispatching
- `ConsoleCommandInvocation` renamed to `CommandInvocation`
- Random module relocated to `Origo.Core/Random/RandomNumberGenerator.cs`
- State machine refactored with new `StateMachinePersistenceModels`; `StateMachineDataKeys` replaced by `WellKnownKeys`
- `SndSceneJsonSerializer` renamed to `SndSceneSerializer`
- Updated README with expanded documentation

### Removed
- Legacy Godot serialization files: `GodotJsonPropertyNames`, `GodotJsonReaderStrict`, `GodotMiscConverters`, `GodotTransformConverters`, `GodotVectorConverters`
- Old Snd JSON converters: `DataMetaDataJsonConverter`, `SndMetaDataJsonConverter`, `StrategyMetaDataJsonConverter`, `TypedDataJsonConverter` (replaced by unified converters)
- `StateMachineStrategyEntityAdapter` (functionality integrated elsewhere)
- `SaveStorageAndPayloadTests` (replaced with more targeted tests)

---

## [0.0.2] - 2026-03-30

### Added
- New abstractions: `ISndDataAccess`, `ISndNodeAccess` for typed component access on entities
- `NullLogger` — no-op logger implementation
- `RunDependencies` — lifecycle dependency injection container
- `SndDefaults` — default configuration values
- `EntryPointWorkflow`, `SaveGameWorkflow` — orchestration helpers for common game flows
- `BclTypeNames` — BCL type name mapping for serialization
- `KeyValueFileParser` — configuration file parser
- `SndTemplateResolver` — entity template resolution
- Godot adapter serialization infrastructure: `GodotEngineTypeNames`, `GodotJsonPropertyNames`, `GodotJsonReaderStrict`
- New test coverage: `DataObserverManagerTests`, `ExtendedCoverageTests`, `StrategyPoolTypeSafetyAndExtensionTests`, `TechnicalDebtFixTests`, `SaveMetaMapCodecTests`, `SaveStorageAndPayloadTests`
- Chinese documentation (`README.zh-CN.md`)
- MIT License (`LICENSE`)
- CI workflow (`.github/workflows/ci.yml`)
- `.editorconfig` for consistent code style

### Changed
- Blackboard API: `ExportAll()` renamed to `SerializeAll()`, `ImportAll()` renamed to `DeserializeAll()`
- `IFileSystem` extended with `Rename()` and `DeleteDirectory()` methods
- `INodeFactory.Create()` now returns non-nullable `INodeHandle` (previously `INodeHandle?`)
- `ISndEntity` and related interfaces updated with improved component access patterns
- `ConsoleCommandParser` improved for robust command tokenisation
- `TypedDataJsonConverter` and `StrategyMetaDataJsonConverter` enhanced for better type mapping
- `Directory.Build.props` centralised project-wide build settings (nullable, analysers)
- Project file cleaned up — duplicate `TargetFramework`/`Nullable` properties removed from `Origo.Core.csproj`

### Removed
- `IClock` abstraction — time handling delegated to the scheduler
- `SaveFormat` — format handling simplified
- `SaveSnapshotService` — snapshot logic integrated into `SaveStorageFacade`
- `GodotScheduler` — scheduling moved to adapter configuration
- Partial `SndContext` files (`ActiveSaveState`, `Entry`, `SaveFlow`) — consolidated into `SndContext.cs`

### Fixed
- Strategy pool now enforces fail-fast type checking for type safety

---

## [0.0.1] - 2026-03-26

### Added
- Initial release of **Origo** — a game architecture framework targeting .NET 8 with a first-party Godot 4 adapter
- **SND entity model** — Data, Node, and Strategy component architecture for game entities
- **Three-layer lifecycle system**: `SystemRun`, `ProgressRun`, `SessionRun`
- **Typed blackboards** (`IBlackboard`) with `TypedData` serialization support
- **Slot-based save system** with `SaveMetaMapCodec`, `SaveMetaMerger`, `SaveStorageFacade`, `SaveSnapshotService`
- **Persistent blackboard** (`PersistentBlackboard`) for cross-session data persistence
- **Stack state machine** (`StackStateMachine`, `StateMachineStrategyBase`, `StateMachineStrategyEntityAdapter`)
- **Strategy pool** (`SndStrategyPool`) for stateless strategy management
- **Built-in console system** with `SndCountCommandHandler` and `SpawnTemplateCommandHandler`
- **Deterministic RNG** (`RandomNumberGenerator`) using XorShift128+ algorithm
- **Data observer manager** (`DataObserverManager`) for reactive data-change notifications
- **Godot 4 adapter** with file system, scheduling, serialization, and entity factory implementations
- Core abstractions: `IBlackboard`, `IClock`, `IFileSystem`, `INodeFactory`, `IScheduler`, `ISndEntity`, `ISndSceneAccess`, `ISndStrategyAccess`, `ISndDataAccess`, `ISndNodeAccess`
- Comprehensive test suite covering lifecycle, save/load, strategies, state machines, and serialization
