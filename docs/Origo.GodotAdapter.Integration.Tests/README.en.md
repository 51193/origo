<!-- docsync-pair: Origo.GodotAdapter.Integration.Tests/README -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.GodotAdapter.Integration.Tests

> [↑ Back to Origo.manual](../README.en.md)

## Overview

**Origo.GodotAdapter.Integration.Tests** is an integration test project that runs inside a real Godot runtime.
Unlike the unit test project `Origo.GodotAdapter.Tests`, this project uses `Godot.NET.Sdk` and runs in
`godot --headless` mode, enabling verification of actual behavior of engine-dependent code.

## Test Runner

Integration tests use a custom lightweight runner rather than xUnit:

- **`IntegrationTestRunner`**: `[GlobalClass]` AutoLoad Node, discovers and executes all
  `[IntegrationTest]`-marked methods via reflection in `_Ready()`.
  Supports two test modes:
  - **Immediate tests** (`[IntegrationTest]`): Executed immediately in `_Ready()`, suitable for tests
    that do not require tree operations
  - **Deferred tests** (`[DeferredTest]` + `IDeferredTestFixture`): Queued in `_Ready()`,
    executed in subsequent `_Process()` frames, suitable for tests requiring `AddChild` to SceneTree.
    `Setup()` method called in first frame (adds nodes to tree), test body executed in later frames.
- **`[IntegrationTest]`**: Custom attribute marking immediate test methods
- **`[DeferredTest]`**: Custom attribute marking deferred test methods
- **Assertions**: `IntegrationTestRunner.Assert(condition, message)` / `AssertEqual` /
  `AssertNotNull` / `AssertNull` / `AssertThrows<TException>` /
  `AssertContains` / `AssertEmpty` / `AssertNotEmpty`
- **Output**: Test results output to stdout with `INTEGRATION_TEST_RESULTS:` and
  `INTEGRATION_TEST_SUMMARY:` prefixes for easy CI parsing

## Capability Overview

| Test Class | File | Tests | Engine Dependency Covered |
|-----------|------|-------|--------------------------|
| GodotRuntimeSmokeTests | `Tests/GodotRuntimeSmokeTests.cs` | 5 | Godot runtime smoke (GD.Print, FileAccess/DirAccess static classes, Vector2 type, SceneTree) |
| GodotFileSystemIntegrationTests | `Tests/GodotFileSystemIntegrationTests.cs` | 5 | `GodotFileSystem` (`res://`/`user://` read/write, directory creation, file enumeration, deletion) |
| GodotFileOperationsIntegrationTests | `Tests/GodotFileOperationsIntegrationTests.cs` | 7 | `GodotFileOperations` (ReadAllText/WriteAllText/Copy/Delete guards and correctness) |
| GodotDirectoryOperationsIntegrationTests | `Tests/GodotDirectoryOperationsIntegrationTests.cs` | 10 | `GodotDirectoryOperations` (Create/Exists/EnumerateFiles/Recursive/EnumerateDirectories/DeleteRecursive, hidden-file enumeration/deletion) |
| GodotNodeHandleIntegrationTests | `Tests/GodotNodeHandleIntegrationTests.cs` | 8 | `GodotNodeHandle` (Name cache, Free, SetVisible for CanvasItem/Node3D, UnsafeGetNode) |
| GodotSndManagerInitializationTests | `Tests/GodotSndManagerInitializationTests.cs` | 6 | `GodotSndManager.BindRuntimeDependencies` / `BindContext` (null guards, chained bind flow); unbound entity operations throw the NotReady contract error instead of NRE |
| GodotSndEntityIntegrationTests | `Tests/GodotSndEntityIntegrationTests.cs` | 9 | `GodotSndEntity` (construction null guards, SetData/GetData/TryGetData, type safety, fail-fast after release) |
| GodotSndManagerIntegrationTests | `Tests/GodotSndManagerIntegrationTests.cs` | 7 | `GodotSndManager` (BindRuntimeDeps double bind guard, BindContext order guard, null guards, ProcessAll empty list) |
| GodotSndManagerCreationIntegrationTests | `Tests/GodotSndManagerCreationIntegrationTests.cs` | 5 | `GodotSndManager` (CreateEntity/RemoveEntity/BuildMetaList/RequestKillEntity/GetEntities) |
| GodotPackedSceneNodeFactoryIntegrationTests | `Tests/GodotPackedSceneNodeFactoryIntegrationTests.cs` | 4 | `GodotPackedSceneNodeFactory` (valid/invalid scene loading, child node adding, cache reuse) |
| OrigoAutoHostBootstrapIntegrationTests | `Tests/OrigoAutoHostBootstrapIntegrationTests.cs` | 2 | `OrigoAutoHost` full `_Ready()` startup (Runtime/SndManager/ConsoleChannels) |
| AdapterCommandHandlerIntegrationTests | `Tests/AdapterCommandHandlerIntegrationTests.cs` | 5 | `TreeDebugCommandHandler`, `PressButtonCommandHandler`, `CameraViewCommandHandler` |
| OrigoDefaultEntryBootstrapIntegrationTests | `Tests/OrigoDefaultEntryBootstrapIntegrationTests.cs` | 1 | `OrigoDefaultEntry` complete default property values |
| BootstrapIntegrationTests | `Tests/BootstrapIntegrationTests.cs` | 2 | `OrigoAutoHost` / `OrigoDefaultEntry` property defaults and instantiation |
| SndEntityNodeExtensionsIntegrationTests | `Tests/SndEntityNodeExtensionsIntegrationTests.cs` | 3 | `SndEntityNodeExtensions` (GetNativeNode/GetNodeFromSnd type guards) |
| GodotAdapterTypedDataRegistrationIntegrationTests | `Tests/GodotAdapterTypedDataRegistrationIntegrationTests.cs` | 1 | GodotAdapter assembly load triggers the generated `[ModuleInitializer]` kind registration |
| ObserverSaveReloadIntegrationTests | `Tests/ObserverSaveReloadIntegrationTests.cs` | 3 | Observer bindings restored across save/load; session destroy fires OnUnmounted |
| UserDataCleanupIntegrationTests | `Tests/UserDataCleanupIntegrationTests.cs` | 5 | Pre-test user:// cleanup: leftover write markers / prefixed artifacts removed, non-test and Godot system content preserved, idempotent |
| GodotSndManagerExitTreeIntegrationTests | `Tests/GodotSndManagerExitTreeIntegrationTests.cs` | 1 | `GodotSndManager._ExitTree` out-of-contract cleanup: removing the manager node directly leaves no strategy-pool reference leaks on the Core side |
| OrigoDefaultEntryBootstrapFailureTests | `Tests/OrigoDefaultEntryBootstrapFailureTests.cs` | 1 | `OrigoDefaultEntry` derived entry failure after base._Ready(): the next frame must fail fast instead of driving a half-initialized runtime |

## Running

### CI

```bash
bash scripts/godot-test.sh
```

This script automatically:
1. Parses `Godot.NET.Sdk` version from `Origo.GodotAdapter.csproj`
2. Downloads the matching Godot mono binary (cached in `.godot_binary/`)
3. Runs `godot --headless --path Origo.GodotAdapter.Integration.Tests`
4. Parses exit code and displays results; leaked ObjectDB nodes fail the script even when tests pass

### Local

```bash
# One-click run (auto-downloads Godot binary)
bash scripts/godot-test.sh

# Manual mode (existing Godot installation)
godot --headless --path Origo.GodotAdapter.Integration.Tests
```

## File Structure

```
Origo.GodotAdapter.Integration.Tests/
├── project.godot                          # Godot 4 project config
├── Origo.GodotAdapter.Integration.Tests.csproj
├── Runner/
│   ├── IntegrationTestRunner.cs           # AutoLoad test runner
│   ├── IntegrationTestAttribute.cs        # [IntegrationTest] attribute
│   ├── DeferredTestAttribute.cs           # [DeferredTest] attribute (frame-advanced tests)
│   ├── IDeferredTestFixture.cs            # Deferred test fixture interface
│   └── TestResult.cs                      # Result DTO
├── Tests/
│   ├── GodotRuntimeSmokeTests.cs          # Runtime smoke tests
│   ├── GodotFileSystemIntegrationTests.cs # File system integration tests
│   ├── GodotFileOperationsIntegrationTests.cs # File operation guard tests
│   ├── GodotDirectoryOperationsIntegrationTests.cs # Directory operation tests
│   ├── GodotNodeHandleIntegrationTests.cs # Node handle tests
│   ├── GodotSndManagerInitializationTests.cs # BindRuntimeDependencies/BindContext initialization tests
│   ├── GodotSndEntityIntegrationTests.cs # SND Entity tests
│   ├── GodotSndManagerIntegrationTests.cs # SND Manager tests
│   ├── GodotSndManagerCreationIntegrationTests.cs # Entity create/remove tests
│   ├── GodotPackedSceneNodeFactoryIntegrationTests.cs # PackedScene loading tests
│   ├── OrigoAutoHostBootstrapIntegrationTests.cs # Full startup tests
│   ├── AdapterCommandHandlerIntegrationTests.cs # Command handler tests
│   ├── SndEntityNodeExtensionsIntegrationTests.cs # Extension method tests
│   ├── GodotAdapterTypedDataRegistrationIntegrationTests.cs # Assembly-load-triggered TypedData kind registration tests
│   ├── BootstrapIntegrationTests.cs       # Bootstrap defaults / instantiation tests
│   ├── OrigoDefaultEntryBootstrapIntegrationTests.cs # Default entry property tests
│   ├── ObserverSaveReloadIntegrationTests.cs # Observer binding save/load recovery tests
│   ├── UserDataCleanupIntegrationTests.cs # Test-process user:// cleanup tests
│   └── OrigoDefaultEntryBootstrapFailureTests.cs # Derived entry bootstrap failure fail-fast test
├── TestSupport/
│   ├── StubConsoleOutput.cs
│   ├── StubNodeFactory.cs
│   └── IntegrationTestHarness.cs
└── TestScenes/
    └── minimal.tscn                       # Minimal root scene
```

## Version Management

- **`Godot.NET.Sdk` NuGet version**: Automatically tracked by dependabot (NuGet ecosystem)
- **Godot engine binary**: CI script auto-downloads matching binary after parsing SDK version from `.csproj`,
  no separate version tracking file needed

## Complementarity with Unit Tests

| Dimension | Unit Tests | Integration Tests |
|-----------|-----------|-------------------|
| Runtime | Pure .NET | Godot `--headless` |
| Speed | Fast (milliseconds) | Slower (requires Godot engine startup) |
| Coverage | Core logic, serialization, path handling | Real file I/O, Node lifecycle, engine API |
| CI Role | Primary blocking gate (with coverage threshold) | Supplementary blocking gate |

---

[↑ Back to Origo.manual](../README.en.md)
