<!-- docsync-pair: Origo.Core.Tests/META-TEST -->
<!-- docsync-revision: 13 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Test Documentation Maintenance Meta-Instructions

> [↑ Back to Origo Manual](../README.en.md)

## Test Documentation Positioning

Test documentation in `docs/` is a behavioral mirror of Origo framework tests. The goal is: **quickly understand
what tests cover a given capability, which happy/error/boundary paths are covered, and what gaps remain**,
without reading through source code.

## Writing Principles

### Capability Grouping Over Directory Mirroring

- Test documents are grouped by **capability under test**, one document describes tests for one capability
- If one source directory contains multiple independent capabilities, split into multiple documents
  (e.g., `Save/` split into `Save-Storage.md`, `Save-Serialization.md`, `Save-Meta.md`)
- If multiple test files together verify the same capability, merge into one document with sections

### Bottom-Up

1. **Test method level**: Each test method categorized as "Happy Path / Error Path / Boundary Path",
   listed in tables within the capability document
2. **Capability document level**: Summarizes all test files for that capability + test method tables +
   support strategies + coverage gaps + design decisions
3. **Module root**: Test project README (`Origo.Core.Tests/README.md`) — lists all capability document
   indexes, test support facilities, test strategy overview
4. **Top level**: Test navigation entry in `docs/README.md`

### Linking Conventions

- **Every document must include a link back to the parent (module README)**, format: `[↑ Back to Origo.Core.Tests](README.en.md)`
- **Every capability document must include cross-links** to the module-under-test's documentation,
  format: `` `[↔ Module under test: Origo.Core/Xxx](../Origo.Core/Xxx/README.en.md)` ``
- **When a capability document references behavior descriptions in usage/, it must link to the corresponding document**
- **No orphan documents**: All test documents must be strictly connected to the module README and top-level README via links

### Content Conventions

| Level | Content |
|-------|---------|
| Capability document | Behavior under test overview (citing usage/ or module documentation) → Test file list → File-specific test details (happy/error/boundary tables) → Support strategy list → Known coverage gaps → Design decisions |
| Module README | Test strategy overview → Test support facility description → All capability document indexes (including file and test counts) |
| Top-level navigation | Test navigation entry (path from `docs/README.md`) |

### Test Detail Table Conventions

**Happy path table**:

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `MethodName` | Concise description (one sentence) | `usage/xxx.md` or `Abstractions/README.md` |

**Error path table**:

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `MethodName` | Error input description | Exception type thrown / error message keywords |

**Boundary path table**:

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `MethodName` | Boundary condition description | Does not throw / returns default / etc. |

### Coverage Gap Conventions

- **Every capability document must include a "Known Coverage Gaps" section**
- Gaps must cite a document basis (indicating where the behavior is described in usage/ or module documentation)
- Gap format: table with "Gap Description", "Impact", and "Doc Basis" columns
- Gaps can serve as priority ranking for future test expansion

### Support Strategy Conventions

- `private sealed class XxxStrategy` and similar support strategy classes defined within test files
  must be listed in the "Test Support Strategies" table
- Describe each support strategy's purpose and usage (not the specific implementation, but what behavior it simulates)

### Writing Style

- Mark the parent link (↑) at the beginning of every document
- Mark the parent link at the end of every document (for easy back navigation)
- Tables clearly list test methods and verified behaviors
- **No evolution markers**: Do not use phrases like "newly added", "legacy", "since v0.x" that mark version
  evolution history. The existence of a test itself represents the current behavior that needs verification.
- **Uncertain behavior descriptions must be confirmed with the maintainer**, do not fabricate based on
  code implementation

### InternalsVisibleTo Whitelist Principle

Origo makes much orchestration logic (`OrigoRuntime`, `SndWorld`, `SessionRun`, `ProgressRun`,
`SndStrategyPool`, `SndStrategyManager`, etc.) `internal`. Tests access these types via `InternalsVisibleTo`,
but must observe the following whitelist principle:

**Permitted uses of InternalsVisibleTo (whitelist)**:

1. **Framework guardrail contracts**: Defensive validation during registration/construction of internal
   types, with no public API to trigger
   - Example: `SndWorld.RegisterStrategy()` rejecting strategies with instance fields (`AutoInitializerGuardTests`)
   - Example: `SaveCoordinator` constructor null argument validation (`SaveCoordinatorTests`)

2. **Internal orchestration correctness contracts**: Strategy pool reference counting, type branch safety,
   rollback behavior, etc.
   - Example: `SndStrategyPool` `GetStrategy` / `ReleaseStrategy` reference counting correctness
   - Example: `StackStateMachine` rollback behavior when `SndStrategyPool` acquisition fails during construction
   - Example: Entity phased lifecycle orchestration (AfterLoad/AfterSpawn/BeforeSave/BeforeQuit/BeforeDead trigger
     timing, LIFO/priority ordering, cross-entity visibility, and intermediate states like "created but hooks not
     yet triggered" and "BeforeQuit triggered but entity still in collection") verified directly via
     `IEntityLifecycle` phased methods + `FullMemorySndSceneHost` (`SndEntityLifecycleBatchTests`).
     These intermediate states and ordering **cannot** be observed through `ISessionRun` public API and are
     therefore whitelist-eligible.

3. **Scene host self-contracts**: The method contracts themselves of `FullMemorySndSceneHost` /
   `MemorySndSceneHost` / `StubSndSceneHost`'s `CreateEntity` / `RemoveEntity` / `RemoveAllEntities` /
   `ProcessAll` / `RequestKillEntity`, as well as `SndEntityFactory.Spawn` / `SpawnMany`.
   These are direct APIs of the hosts/factories under test (see [Snd-Scene.md](Snd-Scene.en.md),
   [Snd-Entity.md](Snd-Entity.en.md)).

4. **Precise measurement for performance benchmarks**: Benchmark tests (`[Trait("Category","Benchmark")]`)
   directly manipulate `SndStrategyPool` / `FullMemorySndSceneHost` / `IEntityLifecycle` to avoid session-layer
   overhead contaminating measurements (`SndStrategyPerformanceTests`).

5. **Test infrastructure construction**: Root objects like `OrigoRuntime`, `SndContextParameters` that
   tests need to construct to set up the test environment

6. **Direct invocation of static methods**: Bootstrap utility methods like
   `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`

7. **Payload deserialization validation and low-level operations with no public equivalent**: The following
   situations have no public path that can faithfully reproduce the same contract, so internal APIs are retained:
   - **Isolated contract verification of `DefaultSaveStorageService`** (`SavePathPolicyContractTests`,
     `SaveStorageContractTests`): per-method path assertions under a custom `ISavePathPolicy` injection, plus
     low-level methods with no public equivalent such as `EnumerateSavesWithMetaData`/`SnapshotCurrentToSave`/
     `WriteSavePayloadToCurrent` (the full `current/` read, `SavePayloadReader.ReadFromCurrent`, is a
     framework-internal reader with no public equivalent either) — the public `RequestSaveGame`/`RequestLoadGame`
     also carries progress files and idempotency logic, unable to isolate the storage service itself; observable
     behavior with a public equivalent (e.g. `EnumerateSaveIds` → `ctx.Save.ListSaves()`) must go through the
     public path.
   - `LevelBuilder`'s commit delegation contract (`LevelBuilder_Commit_UsesStorageService`): internal type,
     no public equivalent.
   - `ProgressRun.LoadFromPayload` validation of **manually constructed malformed/missing-field payloads**
     (malformed/missing topology, null `ProgressStateMachinesNode`) — the public `RequestLoadGame` goes through
     disk, and the save writer rejects such malformed payloads before the load validation stage, unable to
     faithfully reproduce (`ProgressRunSessionLoadingEdgeTests`, `LifecycleRunsTests`).
   - `ProgressRun.PersistProgress` (persisting progress only, without session data) — the public `RequestSaveGame`
     persists sessions alongside, with no "progress-only" public equivalent (`DisposeSemanticsTests`).
   - `ProgressRun.LoadAndMountForeground(levelId)` mounting an **arbitrary level** as the initial foreground —
     in production, initial mounting only goes through entry/save, with no public API for arbitrary-level
     initial mounting.
   - `ProgressRun.BuildSavePayload` / `LoadFromPayload` **in-memory round-trip encoding/decoding contract**
     (`PayloadCodec_InMemoryRoundTrip_PreservesState`) — isolating and verifying the serializer/deserializer
     codec itself, without going through disk; the public `RequestSaveGame` / `RequestLoadGame` couples
     codec with storage pipeline, unable to isolate codec verification.

8. **Test-only reset of global state**: `TypedData.ResetForTesting()` (internal) resets TypedData's kind
   registry between tests so each test starts clean. It is a **test-only reset hook**, unreachable from
   production paths; it must not be invoked from non-test code.

9. **Internal fault-state injection without a public trigger path**: when the fault state under test
   (faulted task, never-completing task, a listener already started before a port conflict, a broken
   client writer, etc.) **cannot be triggered through any public API**, reflection may be used to inject
   or read private fields to construct the fault state. Such injection is more fragile than
   `InternalsVisibleTo` (a field rename breaks the test at runtime) and must follow these rules:
   - Use it **only** where the fault state itself has no public trigger path; scenarios reachable via
     public paths must use them (e.g. `Start_AfterDispose`, `Start_PortInUse` both drive public calls)
   - Reflection field access stays confined to that test file — never spread into production code or
     other tests
   - Precedent: `Origo.ConsoleBridge.Tests/ConsoleBridgeServerErrorPathTests.cs` (`_acceptTask` /
     `_listener` / `_writer` / `_started` field injection; the ConsoleBridge assembly has no
     `InternalsVisibleTo`); timing-sensitive assertions must poll (e.g. `SpinUntil` waiting for
     `_writer` to become non-null), never use a fixed `Thread.Sleep`

10. **Internal properties as host-contract verification entry points**: when the test intent is to
    verify a **scene-host's own contract** (white-list item 3) but the host instance comes from inside a
    session, tests may obtain the host via an internal property such as `((SessionRun)bg).SceneHost` and
    drive host methods directly. This is the same white-list category as constructing the host directly
    (e.g. `MemorySndSceneHostTests`); only the host-acquisition path differs. Precedent:
    `BackgroundSessionTests.FullMemorySndSceneHost_LoadFromMetaList_ClearsAndLoads`.

**Recorded namespace deviation**: `Origo.GodotAdapter.Integration.Tests` uses
`Origo.GodotAdapter.Integration.Tests.Runner` / `.TestSupport` sub-namespaces for its `Runner/`
(lightweight test runner, `[GlobalClass]` Godot node) and `TestSupport/` (integration test fixtures)
infrastructure classes — a deliberate deviation from the flat-namespace convention: these types are
engine runtime components (AutoLoad nodes, Godot scene objects), not test cases; test case classes
themselves keep flat namespaces. Not applicable to other test projects.

**Prohibited uses of InternalsVisibleTo (should verify through public interfaces)**:

1. **Session lifecycle orchestration methods**: The behavior of internal methods like
   `SessionRun.PersistLevelState()`, `SessionRun.SerializeToPayload()`,
   `ProgressRun.LoadFromPayload()`/`BuildSavePayload()`/`SwitchForeground()`,
   `SessionManager.PersistSession()` should be verified through the public flow of
   `ISndSaveOperations.RequestSaveGame()`/`RequestLoadGame()`/`RequestSwitchForegroundLevel()` +
   `ISaveStorageService` (syncProcess state indirectly verified by whether `ProcessAllSessions`
   processes that session). Exceptions only for cases listed in whitelist item 7 above with no public equivalent.

2. **Scene host internal methods as behavior triggers**: When the test intent is verifying entity/strategy
   behavior (not scene host self-contracts), `FullMemorySndSceneHost.ProcessAll()` / `CreateEntity()` /
   `RemoveEntity()` / `RemoveAllEntities()` must not be used as trigger shortcuts — instead use the public
   flow of `ISessionRun.Spawn`, `ISessionManager.ProcessAllSessions(includeForeground: true)`,
   `ISessionRun.RequestKillEntity` + `ISessionManager.KillPendingAllSessions()`. (Exceptions for tests
   verifying scene host self-contracts, see whitelist item 3 above.)

3. **Manually patching hooks after entity spawn**: `((IEntityLifecycle)e).FireAfterSpawnHooks()` and similar
   must not be used to simulate spawn — use `ISessionRun.Spawn` (internally already triggers AfterSpawn hooks).
   (Exceptions for batch tests verifying phased orchestration intermediate states/ordering, see whitelist item 2 above.
   Unit-level bare entities — constructed directly via `SndWorld.CreateEntity`, without host/session wrapping, used
   for isolated strategy-mounting tests — have no `ISessionRun` public path available, so they may call
   `IEntityLifecycle` phased methods directly (`RecoverForLifecycle` / `FireAfterSpawnHooks` /
   `FireAfterLoadHooks` / `BuildMetaData`) to make the entity ready; `SndEntityFactory.Spawn` requires a scene
   host and `ISessionRun.Spawn` requires a session. Integration scenarios with a host/session must still use the
   public API. `SaveAndSwitchForegroundIntegrationTests` falls under the **host-contract boundary** of whitelist
   item 2: its intent is verifying the "cross-entity FindByName visibility during hooks" host intermediate-state
   contract, so manually firing hooks in a full session environment is allowed; tests in the same suite that
   verify session orchestration (`SaveAndSwitchForegroundTests` etc.) must use the `ISessionRun.Spawn` public
   API.)

4. **Internal properties of session mount keys**: `SessionRun.MountKey` should be verified through
   `ISessionManager.Contains()` / `ISessionManager.TryGet()`

5. **SessionManager's Clear / LoadSessionFromPayload**: Should be verified through `DestroySession()` /
   `ISndSaveOperations.RequestLoadGame()`

**Judgment criterion**: If I change the internal implementation but the behavioral contract remains unchanged,
this test should still pass. If the equivalent behavioral semantics cannot be verified through public interfaces,
`InternalsVisibleTo` may be used. Remember: `InternalsVisibleTo` is a "whitelist" — do not use unless necessary.

### Test Namespace Convention

All test files use a **flat namespace** (`Origo.Core.Tests`), not split into sub-namespaces like
`Origo.Core.Tests.Snd.Strategy` per subdirectory. All test support types (test doubles, helper strategies,
factory methods) can reference each other without cross-namespace `using` directives.

**Design Decision**:

- **Why**: xUnit test discovery is unaffected by namespace; flat namespaces eliminate cross-directory
  `using` directive maintenance cost. The test project is not an API library; namespace hierarchies
  are not exposed to downstream consumers.
- **Why not**: Splitting into sub-namespaces would require test files in `Snd/Strategy/` directories
  to add `using Origo.Core.Tests.TestSupport;` to reference shared facilities like `TestFactory`,
  increasing maintenance overhead.

**Implementation**: This convention is enforced via `.editorconfig` rules — the `IDE0130` diagnostic
on `[Origo.Core.Tests/**/*.cs]` paths is set to `none` (see repo root `.editorconfig`), same for other
test projects.

**Before deviating from this convention**: Confirm design intent with the maintainer. If a specific directory
needs sub-namespaces enabled, the reason must be recorded in the corresponding test capability document.

### Static Mutable State Isolation Principle

The framework requires strategies to be stateless (no instance fields, no writable instance properties),
enforced via reflection checks in `SndStrategyPool.Register()`. Test spy strategies therefore can only
use `static` fields to collect events.

However, pure `static` fields cause test-to-test data pollution. The solution is to use `AsyncLocal<T>`
to wrap static fields:

```csharp
// ✅ Correct: AsyncLocal isolation, compatible with strategy pool's static requirement
private static readonly AsyncLocal<ICollection<string>?> _events = new();
public static void Bind(ICollection<string> events) => _events.Value = events;

// ❌ Wrong: Pure static, pollutes across tests
private static ICollection<string>? EventSink { get; set; }
```

This principle ensures spy strategies satisfy framework constraints while each test has its own
independent event collector.

When a test class uses multiple `AsyncLocal<T>`-backed test spies, implement `IDisposable`
on the test class and clear all shared state in `Dispose()`. xUnit calls `Dispose()` after
each test method (regardless of pass/fail), ensuring cleanup even when assertions fail:

```csharp
public class MyTests : IDisposable
{
    public void Dispose()
    {
        SpyStrategyA.Events.Clear();
        SpyStrategyB.MountedCalls.Clear();
        GC.SuppressFinalize(this);
    }
}
```

This is preferred over per-test `try/finally` blocks when many tests share the same cleanup
pattern — it centralizes cleanup and guarantees execution after every test.

## Sync Rules

### Cases Requiring Sync Updates

1. **New test file added** → Find the corresponding capability document, add entry in "Test file list",
   add test methods in detail tables
2. **New test method added** → Update the corresponding capability document's happy/error/boundary tables
3. **Test method deleted** → Remove the corresponding row from tables
4. **Test covers a previously recorded gap** → Move the gap entry from "Known Coverage Gaps" into the
   correct test table
5. **New capability test directory added** → Create a new capability document
6. **Support strategy added/removed** → Update "Test Support Strategies" table

### Cases Not Requiring Sync

- Internal refactoring of test support strategies (does not affect externally simulated behavior)
- Test data/fixture value adjustments (does not change the behavioral semantics being verified)
- Test method renaming (must sync-update the method name in tables, but does not change verification content)

### Sync Checklist

After test code PR merge, check:
- [ ] Does the new capability have a corresponding document?
- [ ] Are new test methods recorded in happy/error/boundary tables?
- [ ] Are deleted test methods removed from tables?
- [ ] Are known coverage gaps updated (new gaps added or covered gaps removed)?
- [ ] Is the support strategy table consistent with test files?
- [ ] Are all document links valid?
- [ ] Do "Doc Reference" links in documents still point to correct locations?

## Document Generation

This test documentation is manually authored after analyzing test code (not auto-generated). Quality
depends on correct understanding of test intent and the maintainer's design knowledge. Report any
discrepancies to the documentation maintainer.

---

[↑ Back to Origo Manual](../README.en.md)
