<!-- docsync-pair: Origo.Core.Tests/Compatibility -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Shell Compatibility Contract Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core.Kernel/Ports](../Origo.Core.Kernel/Ports/README.en.md)
> [↔ Behavior under test: architecture/shell-kernel-boundary](../architecture/shell-kernel-boundary.en.md)
> [↔ Save format: Save-Storage](Save-Storage.en.md)

## Behavior Overview

These tests verify that the lifecycle ordering, observer recovery, fail-fast validation, and session state transitions visible to shell consumers remain stable as the kernel evolves, and that kernel-shell ports call the existing orchestration paths instead of bypassing validation or binding.

They drive the stable Contracts entry surface (`OrigoHost`, `ISndContext`, `ISessionRun`, `ISndWorldAccess`); apart from test infrastructure (in-memory file system, event collectors), they do not depend on `Origo.Core.Kernel` types. Golden save-format fixtures and missing-version/future-version failure semantics are documented in [Save-Storage.md](Save-Storage.en.md).

## Test Files

| File | Focus |
|------|-------|
| `Compatibility/ShellCompatibilityContractTests.cs` | `OrigoHost` shell entry: lifecycle hook ordering, observer binding recovery, post-Bootstrap fail-fast, background-session create/destroy transitions |
| `Hosting/OrigoHostTests.cs` | Stable `OrigoHost`/`HostKernelPort` runtime construction and background workflow; `AdapterHostKernelPort` runtime/observer/context binding order; explicit failure when the runtime binder, context binder, or file system is missing |

## ShellCompatibilityContractTests Details

### Happy Path

| Test Method | Behavior Verified | Documentation Source |
|-------------|-------------------|----------------------|
| `ShellEntry_LifecycleOrdering_IsPreservedThroughSaveLoad` | Via `OrigoHost` Spawn/DriveFrame/Save/Load/Kill: AfterSpawn → Process → BeforeSave → AfterLoad → BeforeDead ordering holds, and entity data/hook behavior match after reload | [shell-kernel-boundary](../architecture/shell-kernel-boundary.en.md) |
| `ShellEntry_ObserverRecovery_IsPreservedThroughSaveLoad` | An explicitly mounted observer receives OnMounted/OnDataChanged; after save and load the persisted observer binding is recovered, and later data changes keep notifying | [shell-kernel-boundary](../architecture/shell-kernel-boundary.en.md) |
| `ShellEntry_SystemBlackboardPersistence_IsPreservedAcrossHostRestart` | Saving and recreating `OrigoHost` over the same file system restores active/continue state from `<SaveRootPath>/system.json`, so `HasContinueData()` remains true | [Origo.Core](../Origo.Core/README.en.md) |
| `ShellEntry_SessionStateTransitions_ArePreserved` | A background session is created, spawns an entity, and is destroyed through `ISessionManager`; BeforeQuit fires and the key leaves Keys | [session-model](../usage/session-model.en.md) |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|-----------------|-------------------|
| `ShellEntry_FailFastValidation_IsPreservedAfterBootstrap` | Strategy registration after Bootstrap freeze, invalid save ID, null entity metadata | `InvalidOperationException` / `ArgumentException` / `ArgumentNullException` respectively; no silent degradation |

## Hosting/OrigoHostTests and AdapterHostKernelPortTests Details

### Happy Path

| Test Method | Behavior Verified | Documentation Source |
|-------------|-------------------|----------------------|
| `HostKernelPort_ShouldBeInternal_AndLiveInKernelPortsNamespace` | `HostKernelPort` and its contract stay internal and never enter the export surface | [Ports](../Origo.Core.Kernel/Ports/README.en.md) |
| `OrigoHost_ShouldCreateStableRuntimeAndRunBackgroundWorkflow` | `OrigoHost` constructs a stable runtime/context and drives frames, strategy registration, and a session workflow | [Ports](../Origo.Core.Kernel/Ports/README.en.md) |
| `AdapterHostKernelPort_ShouldBuildRuntimeAndBindSceneHost` | The port creates the runtime and default console channels, binds runtime/observer topology first, then creates and binds the SND context | [Ports](../Origo.Core.Kernel/Ports/README.en.md) |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|-----------------|-------------------|
| `AdapterHostKernelPort_ShouldRejectSceneHostWithoutRuntimeBinder` | Scene host does not implement `ISndSceneHostRuntimeBinder` | `InvalidOperationException`; observer-topology binding is not skipped |
| `AdapterHostKernelPort_ShouldRejectSceneHostWithoutContextBinder` | Scene host does not implement `ISndContextAttachableSceneHost` | `InvalidOperationException`; context binding is not skipped |
| `AdapterHostKernelPort_ShouldRejectMissingFileSystem` | `OrigoHostOptions.FileSystem` is null | `InvalidOperationException`; no silent fallback to an empty file system |

## Test Support Strategies

| Strategy | Purpose |
|----------|---------|
| `LifecycleProbeStrategy` | Collects AfterSpawn/Process/AfterLoad/BeforeSave/BeforeQuit/BeforeDead events through AsyncLocal to verify lifecycle ordering |
| `ObserverProbeStrategy` | Collects OnMounted/OnDataChanged/OnUnmounted events to verify observer binding and recovery |
| `LateProbeStrategy` | Registers after the Bootstrap freeze to verify fail-fast registration sealing |

## Known Coverage Gaps

| Gap | Impact | Documentation Source |
|-----|--------|----------------------|
| The same compatibility scenario does not yet run in a standalone packaged consumer (`PackageReference`) | Package restore and startup paths are covered by issue #42 | [shell-kernel-boundary](../architecture/shell-kernel-boundary.en.md) |
| No cross-release shell-package × kernel-package binary matrix yet | 0.1.0 promises source/behavior compatibility only; a binary matrix needs released artifacts as input | [shell-kernel-boundary](../architecture/shell-kernel-boundary.en.md) |
| Adapter Godot `Node` entry headless behavior is not duplicated here | Covered separately by the Godot integration tests | [Origo.GodotAdapter.Integration.Tests](../Origo.GodotAdapter.Integration.Tests/README.en.md) |

## Design Decisions

### Why Drive Through `OrigoHost` Instead of an Internal Harness

The compatibility promise is for shell consumers. The tests use only stable entry points such as `OrigoHost`, `ISndContext`, `ISessionRun`, and `ISndWorldAccess`. If a kernel change bypasses the port orchestration path, lifecycle hooks, observer recovery, or state transitions fail directly instead of being masked by a test double.

### Why Port Tests Cover Missing Binders and File Systems

`AdapterHostKernelPort` is the kernel-shell construction entry parallel to `HostKernelPort`. The tests verify both the successful binding order and explicit failure when the runtime binder, context binder, or file system is missing, preventing a silent fallback from bypassing observer topology, context binding, or input validation.

### Why Golden Save Tests Live in Save-Storage

The golden fixture and format-version semantics belong to the storage capability. This document links to that capability instead of duplicating the same tests in two capability documents. See [Save-Storage.md](Save-Storage.en.md).

---
[↑ Back to Origo.Core.Tests](README.en.md)
