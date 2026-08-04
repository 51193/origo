# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

> See [AGENTS.md](AGENTS.md) for the development loop and changelog rules. Changes are recorded only after passing the test loop (source -> tests -> run -> fix & retest -> changelog -> docs sync).

---

## [Unreleased]

### Added

- **Complete English documentation** — 116 English `.en.md` files alongside existing Chinese `.zh.md` files.
- **`camera_view` console command** — displays screen coordinates and depth of Godot entity nodes visible through the active `Camera3D`.
- **`ILogger<TCategory>`** — generic logging interface that auto-derives the log tag from the category type name.
- **`SndContextParameters.InitialLevelId`** — configurable initial save level ID (defaults to `"default"`).

- **`ActiveStrategyJsonBase<TInput>`** — active strategy base class that owns the JSON
  serialization contract: input strings are deserialized to `TInput` and `Execute` results
  are serialized back, so subclasses implement strongly-typed logic with plain object
  returns. Use `ActiveStrategyResults.Ok()` / `Err(message)` for the conventional
  success/error markers.
- **`ISndDataAccess.TryGetData<T>(string, out T?)`** — out-parameter variant of the
  tuple-returning `TryGetData`, enabling the idiomatic `if (TryGetData("hp", out var hp))`
  pattern without nullable propagation traps.
- **`EntityExtensions.IsSameEntityAs`** — reliable entity identity comparison across
  inner/wrapper references (name + owning session), where `ReferenceEquals` is unreliable.
- **Levels-based entry config** — `RequestLoadMainMenuEntrySave` now resolves the main-menu
  level's `snd_scene` from a levels-structured `entry.json`
  (`{ "levels": { "<id>": { "snd_scene": "..." } }, "main_menu_level": "<id>" }`).
- **`InvokeStrategy<TInput, TOutput>` bare-string tolerance** — string results that are
  not valid JSON are returned as-is when the expected output type is `string`, instead of
  throwing opaque `JsonException`s at the call site.

### Changed

- **BREAKING: Target framework upgraded from `net8.0` to `net10.0`** — all projects (Origo.Core, Origo.ConsoleBridge, Origo.GodotAdapter, and test projects) now target `net10.0`; consumers must build with the .NET 10 SDK and run on the .NET 10 runtime. `Origo.SourceGeneration` keeps `netstandard2.0` (Roslyn analyzer constraint). GodotAdapter verified against Godot 4.7.1 mono (74 headless integration tests pass on `net10.0`).
- **BREAKING:** `SndContext` and `ISndContext` refactored to companion-object pattern. All role interfaces (`ISndSaveOperations`, `ISndBlackboardAccess`, `ISndDeferredActions`, etc.) are now exposed through typed companion properties (`ctx.Save`, `ctx.Blackboard`, `ctx.Deferred`, etc.) instead of direct interface inheritance. `ISndEntityRawSubscription` made `internal`.
- **BREAKING:** `DataSourceNode.AsByte`/`AsInt`/`AsFloat`/... (12 numeric-typed methods) replaced by a single unified `As<T>()` generic method. `AsString()` and `AsChar()` retained.
- **BREAKING:** `DataSourceNode.AsChar` now throws `InvalidOperationException` on `Map`/`Array` nodes instead of silently returning `'\0'`.
- **BREAKING:** `ISndDataAccess.GetData<T>` and `SetData<T>` now require `T : notnull`. `SetData<T>` throws `ArgumentNullException` when `value` is null for reference types.
- **BREAKING:** `SndEntity.IsPendingKill` setter is now `internal` — use `ISessionRun.RequestKillEntity(name)` instead.
- **BREAKING:** Concrete entity lifecycle methods are now `internal` — `SndEntity.Process`/`SpawnSingle`/`LoadSingle`/`QuitSingle`/`DeadSingle`/`SaveSingle`, `GodotSndEntity.SpawnSingle`/`LoadSingle`/`SaveSingle`/`ProcessSnd`, and the `IEntityLifecycle` interface are no longer public. Entity lifecycle must be driven through `ISessionRun` (`Spawn`/`SpawnMany`/`RequestKillEntity`) and the framework's batch hook pipeline; concrete-type casts no longer expose lifecycle orchestration.
- **BREAKING:** `ConsoleCommandRouter.Register` throws `InvalidOperationException` on duplicate command names instead of silently overwriting.
- **BREAKING:** All console messages are now in English (built-in commands, error messages, `tree_debug`, `camera_view` output).
- **BREAKING:** `GodotLogger` requires a non-null handler at construction.
- **BREAKING:** `GodotNodeHandle.SetVisible` throws `ObjectDisposedException` on freed nodes instead of silently returning.
- **BREAKING:** `GetNumeric` extension method no longer accepts a default fallback — callers must explicitly pass a fallback value.
- **Public API XML doc comments translated to English** — all previously Chinese `<summary>` comments are now in English for IDE IntelliSense.
- **Godot upgraded to 4.7.1** — `Godot.NET.Sdk` version bumped from 4.6.3 to 4.7.1.
- **ConsoleBridgeServer** uses `async`/`await` for the accept/read loop, eliminating the 100ms polling loop. Connection handling is race-free and survives hard client disconnects and handler exceptions.
- **Save idempotency now includes `extra/` files in hash computation**, preventing silent skip of changed side-channel files.
- Source generator diagnostic messages carry source locations from `SndInlineTypesAttribute` syntax, so build errors point to the exact attribute location.
- **BREAKING:** `RequestLoadMainMenuEntrySave` requires the levels-based entry config
  structure; a bare entity array in `entry.json` is rejected with an explicit error.
  `OrigoAutoInitializer.LoadAndSpawnFromFile` keeps its array semantics for direct use.
- **BREAKING:** `ISndDataAccess` gained the `TryGetData<T>(string, out T?)` member;
  all implementers (including test doubles) were updated.
- **BREAKING: Session level IDs and keys are restricted to ASCII letters, digits, `.`, `_`, `-`** — creating a session with other characters throws `ArgumentException`, and the session-topology parser rejects entries with extra fields or non-boolean sync values. Saves written with special characters in level IDs fail to load with an explicit error instead of silently mis-parsing.
- **`SndWorld.ResolveTemplate` returns a deep copy** — mutating the returned metadata no longer pollutes the template cache.
- **Mounting the same strategy index twice on an entity now throws** — previously the passive strategy manager silently ran the strategy twice per frame.
- **Attaching a second `PlanExecutionStrategyBase`-derived strategy to the same entity now throws** — previously it silently unsubscribed the first plan strategy's callbacks.
- **BREAKING: TypedData kind 255 rejected by the source generator** — `ORIGOSG003` now limits the valid range to `[1, 254]`; 255 is the reserved `UnregisteredKind` sentinel, and registering it used to corrupt `DataType` resolution and extraction.
- **Session checkpoint writes (`WriteProgressOnlyToCurrent` / `WriteLevelPayloadOnlyToCurrent`) leave a write-in-progress marker on failure** — readers now reject partially written checkpoints instead of accepting them.

### Removed

- **BREAKING:** `GodotSndBootstrap` class and `BindRuntimeAndContext` method removed. Callers should chain `GodotSndManager.BindRuntimeDependencies(sndWorld, logger)` followed by `GodotSndManager.BindContext(context)` directly.
- **`SndEntity.QuitSingle` / `DeadSingle` removed** — single-entity teardown now goes exclusively through `ISessionRun.RequestKillEntity` / the session kill pipeline. The two methods had diverging hook orders from the session pipeline and were only exercised by tests.

### Fixed

- **`DataSourceNode.ComputeSha256Hash` expands lazy subtrees recursively** — the save idempotency hash previously treated unexpanded nested JSON children as empty maps, so deep changes inside `extra/` files produced identical hashes and the whole save could be silently skipped.
- **Save topology is re-solidified after `BeforeSave` hooks** — the framework-computed `SessionTopology` value is written after hooks fire, so hook writes to framework-owned blackboard keys cannot corrupt the persisted save topology.
- **Plan actions already mounted via `LifecycleIndices` are reused** — `PlanExecutionStrategyBase` no longer fails when the action strategy for a plan step is already mounted on the entity; it reuses the existing mount instead of throwing a duplicate-mount exception.
- **Foreground sessions now fire `BeforeSave` hooks on full save** — `RequestSaveGame` previously serialized foreground entities without triggering `BeforeSave`, so hook-written data never reached the save file; background sessions already fired them.
- **`ConsoleBridgeServer` accept-loop faults are observable** — a non-cancellation socket error now logs `Error` and stops the listener (instead of dying silently), `Dispose` logs accept-task faults and join timeouts instead of swallowing them, and connection-handler logs include full stack traces.
- **`ConsoleBridgeServer.Start` is retryable** — a failed start (e.g. port in use) rolls back its internal state instead of permanently disabling the instance.
- **`entity_set_data` reports parse failures** — a value that cannot be converted to an existing key's type now returns an error message and keeps the original value, instead of reporting success.
- **Level-switch checkpoints are marker-protected** — `WriteProgressOnlyToCurrent` / `WriteLevelPayloadOnlyToCurrent` now use the write-in-progress marker, so a crash between the two files leaves a state that readers reject instead of silently accepting a mixed generation.
- **`PersistentBlackboard` writes are backup-swapped** — crash between the old delete+rename steps no longer loses `system.json`; the previous version is kept in a `.bak.json` file and restored by `LoadFromDisk` when the primary is missing.
- **`PersistentRandom.NextFloat` stays in `[0, 1)`** — the raw-value upper bound could previously round up to exactly `1.0f` (~1-in-16-million chance per call).
- **`ObserverTopology.Mount` rollback guard** — prevents strategy reference-count corruption when `GetStrategy` throws before pool acquisition.
- **`GodotDirectoryOperations.Create` / `DeleteRecursive`** — error codes from Godot API calls are checked (fail-fast) instead of discarded. `DeleteRecursive` now clears directory contents but leaves the container intact, avoiding failures when the Godot editor holds file descriptors to `user://` paths.
- **`GodotFileOperations.Delete`** — error code from `DirAccess.RemoveAbsolute` is checked instead of discarded.
- **`PersistentBlackboard` atomic write** — uses temp-file + rename to prevent `system.json` corruption on crash. Stale temp files from interrupted writes are cleaned up automatically.
- **`ConsoleBridgeServer` connection robustness** — no longer intermittently resets an in-flight read via concurrent stream disposal on `Dispose()`. Output buffer overflow now emits a warning instead of dropping lines silently.

- **Documentation examples now match the real API** — `ctx.RequestSaveGame`,
  `ctx.RequestKillEntity`, and `ctx.CloneTemplate` usages in docs were corrected to
  `ctx.Save.RequestSaveGame`, `entity.OwningSession.RequestKillEntity`, and
  `ctx.Template.CloneTemplate`.

## [0.0.8] - 2026-06-30

### Added

- **`ObserverStrategyBase`** — new first-class strategy base class (alongside LifecycleStrategyBase and ActiveStrategyBase). Provides three virtual hooks: `OnMounted`, `OnDataChanged`, `OnUnmounted`. Observer strategies are stateless, pooled, and auto-persisted across save/load.
- **`ISndObserverStrategyAccess`** — new entity interface: `MountObserverStrategy(targetName, observerIndex)` / `UnmountObserverStrategy(targetName, observerIndex)`. Supports both self-observation (targetName == entity.Name) and cross-entity observation.
- **`ObserveDataAttribute`** — declare data keys that an ObserverStrategy observes. Multiple attributes per class supported. Keys are extracted at pool registration time.
- **`StrategyMetaData.ObserverIndices`** — new serialization field storing observer binding topology (`[{targetName: [observerIndices]}]`) alongside `lifecycle_indices` and `active_indices` in save files. Observer bindings are automatically serialized and restored across save/load cycles, eliminating the need for manual AfterLoad re-subscription.
- **`DiffUtility`** — generic diff helper in `Origo.Core.Utility`: `Diff<T>(old, new) -> (added, removed)`.
- **`GridParser`** — coordinate string parser supporting `"x,z"` format and `JsonElement` input; returns `(int X, int Z)?`
- **`ISndEntity.EnsureStrategy()`** extension method — lazy strategy attachment with idempotency guard; checks a data key and only adds the strategy if no value is already set.
- **`ISndEntity.EnsureReplaceableStrategy()`** extension method — supports the `*_impl` replaceable-strategy pattern. Reads a configured override from entity data, falling back to a default strategy index, with idempotency guard.
- **Logger minimum level filtering** — `GodotLogger` and `TestLogger` accept a `MinimumLevel` parameter (default `Info`) to suppress `Debug`-level messages in production. `TestLogger` also exposes a settable `MinimumLevel` property for tests.
- **`PlanExecutionStrategyBase`** — intent-driven entity-level plan execution base class in `Origo.Core.Planning`. Manages subscription wiring, action strategy plug/unplug, plan advancement, and idle timing automatically. Users provide only `ResolveNextStep(intent, currentStep, failed, entity)` and `StepToActionIndex(stepType)`. Reduces AI scheduling boilerplate from ~250 lines to ~70.
- **TypedData generator diagnostics `ORIGOSG001`–`ORIGOSG004`** — the source generator now fails the build instead of silently degrading: when a system primitive is registered in an adapter `SndInlineTypes` group (`ORIGOSG001`), an unsupported value type is registered in the home assembly (`ORIGOSG002`), a registered kind falls outside the byte range `[1, 255]` (`ORIGOSG003`), or overlapping `SndInlineTypes` startKind ranges assign one kind byte to multiple types (`ORIGOSG004`). This prevents out-of-bounds inline registrations and silent kind collisions that would corrupt TypedData storage at runtime.
- **`Origo.SourceGeneration.Tests`** — dedicated test project that drives the TypedData source generator over in-memory compilations, verifying home/adapter output, the inline-vs-`_ref` storage model, the diagnostics, and generation determinism (Coverlet line coverage gate ≥ 85%). It also includes lenient performance benchmarks comparing the generated inline `TypedData` against an unoptimized boxing implementation across several value types and the `string` reference type; benchmarks are tagged `[Trait("Category","Benchmark")]` and run in a dedicated CI step (`scripts/benchmark.sh`), separate from the coverage-gated test run.
- **`ISndEntity.OwningSession`** — every entity exposes its owning `ISessionRun` via this non-null property, bound at creation time. Strategies reach their session (and other sessions via `OwningSession.SessionManager`) directly, without going through a global context lookup.
- **`ISessionRun.SessionManager`** — each session exposes its parent `ISessionManager`, enabling strategies to access cross-session operations via `entity.OwningSession.SessionManager.TryGet(key)`.
- **`ISessionRun` entity operations** — `FindByName(name)`, `GetEntities()`, `Spawn(meta)`, `SpawnMany(metaList)`, `RequestKillEntity(name)`. These provide a session-scoped entity operation facade, replacing the now-internal `SceneHost` property. `Spawn`/`SpawnMany` fire `AfterSpawn` lifecycle hooks (via `SndEntityFactory`); `CreateEntity` remains available on `ISndSceneHost` for framework-internal use without hooks.
- **SndEntity AfterLoad edge-case tests** — `AfterLoad_EmptyIndices_NoThrow` (entity with zero lifecycle strategies loads without error) and `AfterLoad_ThrowingStrategy_HookExceptionPropagates` (hook exceptions from `AfterLoad` propagate to the caller, upholding fail-fast).
- **TypedData source generator branch coverage tests** — home assembly with only reference types (verifies `GeneratAsMethods`/`GenerateTryGetMethods`/`GenerateImplicitConversions` early-return when no inline types exist); overlapping kind ranges with the same CLR type (verifies `RejectKindCollisions` same-type deduplication path).

### Changed

- **BREAKING: `ISndSessionAccess` and `ISndEntityOperations` removed from `ISndContext`** — the two role interfaces are deleted. `SessionManager` is now a standalone `public` member of `ISessionRun` (accessible via `entity.OwningSession.SessionManager`), not on `ISndContext`. `CurrentSession`, `IsFrontSession`, `RequestKillAll`, and `RequestKillEntity` are no longer part of the public `ISndContext` contract. Strategies that used `ctx.CurrentSession` should migrate to `entity.OwningSession`; `ctx.RequestKillEntity(name)` callers should use `entity.OwningSession.RequestKillEntity(name)`. (`CurrentSession` / `IsFrontSession` remain as convenience members on the concrete `SndContext` class, where `CurrentSession` simply returns the foreground session.)
- **BREAKING: `ISndEntity` now requires a non-null `OwningSession` member** — every entity must have its owning session bound at creation time. Implementations of `ISndEntity` (including test stubs and adapter-level entities) must provide this property.
- **BREAKING: `ISessionRun.SceneHost` removed from the public interface** — session-level scene host access is no longer exposed to strategies. Code that previously accessed `ctx.CurrentSession.SceneHost.FindByName(name)` should instead call `entity.OwningSession.FindByName(name)` (same session) or `entity.OwningSession.SessionManager.TryGet(key)` (cross-session). `ISessionRun` now provides direct entity operation methods (`FindByName`, `GetEntities`, `Spawn`, `SpawnMany`, `RequestKillEntity`) that replace the corresponding `SceneHost.*` calls. The concrete `SessionRun.SceneHost` remains `internal` for framework-internal use only.
- **BREAKING: `SndWorld.CreateEntity` is now `internal`** — the entity factory takes a per-scene-host observer topology and is no longer part of the public API surface. Entities are still created through `ISndSceneHost` / `ISessionRun`; observer-strategy behavior (mount/unmount, cross-entity teardown, save/load of observer bindings) and the save-file format are unchanged. Observer bindings are tracked by a per-scene-host topology rather than per-entity, an internal architecture change with no effect on observer-strategy authoring.
- **BREAKING: `StrategyMetaData.EntityIndices` renamed to `LifecycleIndices`** — the C# property and its save-file JSON key `"entity_indices"` are now `LifecycleIndices` / `"lifecycle_indices"`, matching the concrete base class `LifecycleStrategyBase`. `SndMetaFluentBuilder.AddEntityStrategy` is now `AddLifecycleStrategy`. All existing save files and templates must be updated.
- **BREAKING: `EntityStrategyBase` renamed to `LifecycleStrategyBase`** — concrete strategy types must now be `sealed`. Registration rejects non-sealed, non-abstract types at startup with `InvalidOperationException`. This enforces the pool's singleton-sharing model and prevents accidental subclassing.
- **BREAKING: OrigoConsole no longer swallows command handler exceptions** — `ProcessPending()` now lets exceptions from command handlers propagate to the caller instead of logging at Warning level and publishing an error message. Console command handlers that throw will now surface bugs immediately during development.
- **ConsoleOutputChannel.Publish no longer drops subsequent listeners when one throws** — exceptions from individual listeners are caught so that all subscribers receive every published line; the first exception is re-thrown after the publish loop completes, preserving fail-fast while ensuring output is never silently lost to a single misbehaving subscriber.
- **ConsoleBridgeServer: all best-effort exception swallowing removed** — Dispose, AcceptLoop, and HandleConnection no longer silently catch and discard `IOException`/`ObjectDisposedException`/`SocketException`. Unrecoverable I/O errors now propagate.
- **ProgressRun.Dispose: exception swallowing removed** — `SessionManager.Clear()` and `DeleteCurrentDirectory()` errors during Dispose are no longer caught and logged at Warning; they now propagate to the caller.
- **SessionRun.ResetAfterLoadFailure: multi-step exception accumulation removed** — each cleanup step (state machine clear, entity release, scene remove, blackboard clear) now propagates immediately instead of accumulating `firstError` and wrapping in `AggregateException`.
- **SaveStorageFacade: SHA read fallback removed** — failing to read the existing `.payload.sha` file no longer falls back to `string.Empty` and unconditional overwrite; the error now propagates.
- **`LogMessageBuilder` format simplified** — `AddPrefix`/`AddSuffix` replaced by unified `AddContext`; output format changed from `[+ms] prefix=val | msg | suffix=val` to `[+ms] msg | key=val, key=val`.
- **Log tags standardized** — all components use `nameof(ClassName)` consistently instead of mixed string literals.
- **High-frequency log messages downgraded to `Debug`** — strategy pool create/release, entity lifecycle hooks (spawn/load/quit/dead), strategy manager add/remove, and per-strategy auto-registration messages no longer appear at `Info` level.
- **`OrigoRuntime` banner downgraded to `Debug`** — no longer outputs the multi-line banner at `Info` level.
- **Performance timing added to key operations** — `OrigoConsole` command processing, `SessionRun` create/dispose/load/persist, `ProgressRun` create/dispose, `SaveStorageFacade` write/snapshot, and `SessionManager` mount/destroy now include elapsed milliseconds in log output.
- **`TreatWarningsAsErrors` enabled solution-wide** — all projects now fail the build on compiler and analyzer warnings, making code-quality regressions a hard build failure rather than ignorable warnings.
- **Console command handlers for entity operations now require a foreground session** — `kill_all`, `snd_count`, and `spawn` no longer silently fall back to `ForegroundSceneHost` when no session is active. With no foreground session, `kill_all` and `spawn` return a clear error message; `snd_count` reports zero. This closes a side door left behind by the SessionManager refactoring.

- **BREAKING: `OrigoRuntime.ForegroundSceneHost` removed** — the `internal` property is deleted, and with it the entire `ForegroundSceneHost` cascade through `SystemRuntime`, `ProgressRuntime`, and `SessionManagerRuntime`. The adapter scene host now flows through `SystemParameters` → `SystemRuntime.AdapterSceneHost` → `ProgressRuntime.AdapterSceneHost` → `SessionManagerRuntime.AdapterSceneHost` → `SessionManager` (where it is stored and used exclusively for session creation). `OrigoRuntime` retains the host only via the narrow `GetAdapterSceneHost()` bootstrap method, which is called once by `SndContext`. Console handlers (`ConsoleCommandHelper`, GodotAdapter `PressButtonCommandHandler`, `TreeDebugCommandHandler`) no longer fall back to the removed property. The `ResetForeground` logic now accesses the scene through the current foreground session, not through a runtime-level property.

- **BREAKING: `SessionManager.CreateForegroundSession` and `CreateForegroundFromPayload` no longer accept an `ISndSceneHost` parameter** — `SessionManager` stores the adapter scene host at construction and uses it internally when creating foreground sessions. Callers (`ProgressRun.SessionLoading`) no longer thread the host from `ProgressRuntime` into session creation.

- **`SystemParameters` gains `AdapterSceneHost` field** — the adapter scene host is now a first-class system parameter, threaded through `SystemRuntime` → `ProgressRuntime` → `SessionManagerRuntime` → `SessionManager`.

- **BREAKING: `ISessionManager SessionManager` removed from `ISndContext` and `SndContext`** — the session manager is no longer part of the context interface or concrete class. Framework internals access `ProgressRun.SessionManager` directly. Strategies that need cross-session access must use `entity.OwningSession.SessionManager`. Tests access the session manager through `SndContext.Runtime.SessionManager` (via `InternalsVisibleTo`). This change closes the last remaining side door in the context interface: `ctx.SessionManager` allowed bypassing entity ownership, since the caller had to know the target session's key — `entity.OwningSession.SessionManager` intrinsically knows which session the entity belongs to.

- **BREAKING: `ProgressRun.ForegroundSession` removed** — `ProgressRun` no longer holds a forwarding property for `_sessionManager.ForegroundSession`. All internal callers (`SndContext`, `SessionLifecycle`, `RequireForegroundSession`) now dereference through `SessionManager` explicitly. Cross-layer holding of session state is eliminated.

- **`StubSndSceneHost` now implements `IOwningSessionBindable`** — stub entities created through `StubSndSceneHost.CreateEntity` / `RecoverFromMetaList` now have their `OwningSession` bound. `StubSndEntity.OwningSession` accepts a setter. Test strategies that receive `(ISndEntity entity, ISndContext ctx)` can now use `entity.OwningSession` rather than casting `ctx` to access the session manager.

- **`StrategyTestScenario` harness now binds `OwningSession` on test entities** — `BaseStrategyTestScenarioBuilder.Build()` sets `entity.OwningSession` to the foreground session after creation. `TestSessionRun.SessionManager` no longer throws — it receives a back-reference from `TestSessionManager` at construction.

- **BREAKING: `CurrentSession` and `IsFrontSession` removed from `SndContext`** — both properties are deleted. `IsFrontSession` remains available on `ISessionRun` via `entity.OwningSession.IsFrontSession`. The session can be accessed through `ctx.Runtime.SessionManager.ForegroundSession` (internal, for framework/test use). Test strategies that used `((SndContext)ctx).CurrentSession` to reach `SceneHost.FindByName` now use `entity.OwningSession.FindByName`.

- **`TestSceneHost` now implements `IOwningSessionBindable`** — consistent with `StubSndSceneHost` and `FullMemorySndSceneHost`. Entities created through `TestSceneHost.CreateEntity()` now have `OwningSession` bound.

- **`OrigoRuntime.SessionManager` changed from `internal` to `public`** — non-entity code (e.g. console command handlers in external assemblies) now has a public path to `ISessionManager`. Strategies should still use `entity.OwningSession.SessionManager`.
- **`GodotAdapter.Tests` coverage exclusions expanded** — `GodotFileSystem.cs`, `GodotSndBootstrap.cs`, and `PressButtonCommandHandler.cs` are now excluded from line coverage measurement. These files are thin passthrough delegates to Godot engine APIs with no independently testable logic.

### Removed

- **BREAKING: `SndRuntime` removed** — the public SND runtime facade is gone. Spawning (with `AfterSpawn` hooks) is now performed by `SndEntityFactory` / `ISessionRun.Spawn`, while frame drive and end-of-frame kill-pending orchestration moved to `SessionManager` / `SessionRun`. The save-file format and observer-strategy behavior are unchanged.
- **`ISndObservation`** — removed. The old cross-entity ObserveData/ObserveLifecycle API has been replaced by `ISndObserverStrategyAccess.MountObserverStrategy` / `UnmountObserverStrategy` with ObserverStrategy as a first-class strategy type.
- **`ISndEntityLifecycleAccess` / `EntityLifecycleEvent`** — removed. Lifecycle subscriptions via SubscribeLifecycle/UnsubscribeLifecycle on `ISndEntity` are no longer supported, and the accompanying `EntityLifecycleEvent` enum is removed along with them. Use ObserverStrategy with OnMounted/OnUnmounted instead.
- **`ISndDataAccess.Subscribe` / `Unsubscribe`** — removed. Self-data subscriptions must now use `MountObserverStrategy` with the entity's own name as the target.
- **BREAKING: `TypedData.Data` property** — removed. The public boxing accessor formed a bypass of the zero-boxing `TryGetXxx` and `TypedDataFactory<T>.TryExtract` read paths. For type-erased access, framework-internal callers (serialization, console debug) now use `TypedDataObjectConverter.ToObject` directly.
- **BREAKING: `TypedData(Type, object?)` constructor and `TypedData.FromObject` static factory** — removed. Construct TypedData values via explicit operators (`(TypedData)42`), `TypedDataFactory<T>.Create()`, or the `SndMetaFluentBuilder` convenience API. The deserialization path (`TypedDataConverter.Read`) now uses `TypedDataTypeMap.GetKindForType` + `TypedDataObjectConverter.FromObject` internally.
- **Dead code in `TypedDataGenerator.ExtractTypes`** — removed unreachable branch that matched a single `INamedTypeSymbol` constructor argument (`SndInlineTypes` always uses `params Type[]`, so the argument is always `TypedConstantKind.Array`).

### Fixed

- **`DataSourceNode.Dispose` and `BuildCanonicalString` now use iterative traversal** — prevents `StackOverflowException` on extremely deep or degenerate tree structures that can arise from runtime-generated data.
- **`GodotNodeHandle` name is now cached at construction** — prevents crashes when accessing `.Name` after the underlying Godot node has been freed externally. `Free()` and `SetVisible` now check `IsInstanceValid` before accessing the node.
- **`GodotPackedSceneNodeFactory` now caches loaded scenes** — avoids redundant disk I/O when the same `PackedScene` resource is instantiated multiple times.
- **`GodotSndManager.RemoveAllEntities` properly frees Godot nodes** — entities are now detached and freed before the internal list is cleared, preventing orphaned nodes in the scene tree.
- **`GodotPathResolver.GetParentDirectory` throws on root paths** — paths without a parent directory (e.g. `"/"`, `"res://"`) now throw `InvalidOperationException` instead of returning an empty string.
- **`OrigoDefaultEntry.Bootstrap` throws a descriptive error when Console is null** — replaces the null-forgiving operator with a runtime null check that produces a clear `InvalidOperationException`.
- **`PersistentRandom.NextInt32` validates its range** — calling it with `maxExclusive <= minInclusive` now throws `ArgumentOutOfRangeException` instead of throwing `DivideByZeroException` (when equal) or returning an out-of-range value (when inverted).
- **Archetype integer values no longer lose precision** — `.map` archetype values that exceed `int` range are now stored as `long` instead of being silently coerced to `float`. Integer parsing is also culture-invariant.
- **Snapshotting an existing save no longer risks losing it on failure** — overwriting a save slot now moves the old directory aside, renames the freshly built copy into place, and only then drops the backup, so the previous data is never deleted before the new data is durably in place.
- **`current/` is no longer left half-written when a save payload is incomplete** — the payload's active level is now validated before any file is written, so a payload missing its active level fails before touching `current/` instead of after writing the progress and meta files.
- **ConsoleBridge no longer corrupts output under concurrent writes** — when a client connects, the buffered backlog is now flushed while holding the writer lock, so it can no longer interleave with the live output broadcast on another thread.
- **`ConsoleBridgeServer` thread-safety hardening** — `_handleThread` creation is now performed inside the `_acceptLock` critical section, and `Start()` uses `Interlocked.CompareExchange` for its idempotency guard, closing races that could allow two concurrent Handle threads.
- **`ConsoleBridgeServer` accept loop no longer races with HandleConnection teardown** — `Monitor.Wait`/`Pulse` over the accept lock now coordinates the handoff between the accept loop and the handle-thread finally block, ensuring a newly accepted connection is not immediately closed because `_hasActiveClient` hasn't been cleared yet by the previous connection's teardown.
- **`ISndEntity.AddStrategy` is now atomic on failure** — if a strategy's `AfterAdd` hook throws, the strategy is removed from the entity and returned to the pool before the exception propagates, instead of leaving it half-attached and leaking its pool reference.
- **`Astar.FindPath` missing start bounds check** — the start position is now validated against `gridSize` before pathfinding begins, matching the existing endpoint validation.
- **`SndStrategyManager.RecoverStrategiesOnly` no longer silently drops non-`LifecycleStrategyBase` strategies** — recovering a non-lifecycle strategy in entity strategy slots now throws `InvalidOperationException` instead of silently releasing the strategy.
- **Active-strategy recovery no longer silently drops non-`ActiveStrategyBase` strategies** — loading an entity whose active-strategy slot references a non-`ActiveStrategyBase` strategy now throws `InvalidOperationException`, and any active strategies already acquired during the same recovery are rolled back so no half-initialized state remains.
- **`PersistProgress` now requires an active foreground session** — calling `PersistProgress` without a mounted foreground session now throws `InvalidOperationException` instead of silently writing a partially-empty session payload.
- **`SessionRun.Dispose` no longer throws `ObjectDisposedException` during BeforeQuit** — session resources remain accessible while BeforeQuit hooks run during teardown, via a two-phase flag (`_disposing` re-entrancy guard, `_disposed` set only after cleanup completes) with `try/finally` to guarantee entity removal even if a hook throws.

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
