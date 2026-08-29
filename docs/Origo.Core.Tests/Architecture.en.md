<!-- docsync-pair: Origo.Core.Tests/Architecture -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Architecture Guardrail Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/README.md](../Origo.Core/README.en.md)
> [↔ Behavior under test: usage/architecture-overview](../usage/architecture-overview.en.md)

## Behavior Under Test Overview

Verifies Origo's architectural constraints: Core assembly does not reference Godot (layer isolation),
ISndContext is a pure composition interface (Interface Segregation Principle), strategies are validated
as stateless via reflection at registration (rejects instance fields and writable properties).

## Test File List

| File | Verification Focus |
|------|-------------------|
| `CoreArchitectureGuardrailTests.cs` | Layer isolation, interface composition, consumers can complete full workflow through pure interfaces |
| `AutoInitializerGuardTests.cs` | Strategy statelessness validation: instance fields rejected, static fields allowed, missing StrategyIndex throws |

## CoreArchitectureGuardrailTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `CoreAssembly_ShouldNotReferenceGodot` | Core assembly does not reference any Godot assemblies | architecture-overview: platform independence |
| `SceneWriteInterfacesAndSpawnFactory_AreInternal` | `ISndSceneHost`/`ISndSceneAccess`/`ISndContextAttachableSceneHost`/`IOwningSessionBindable` and `SndEntityFactory` are internal | architecture-overview: single access path |
| `PrivateFields_FollowUnderscoreCamelCase` | Core production private fields follow `_camelCase` naming | .editorconfig naming rule |
| `ISndContext_ShouldBeCompositionInterface_WithCompanionProperties` | ISndContext itself declares no methods/properties | Snd Abstraction: ISP |
| `ISndContext_ShouldExposeAllRoleInterfacesAsCompanionProperties` | ISndContext exposes all role-interface capabilities through 10 companion properties, not interface inheritance | Snd Abstraction: ISndContext composition |
| `SndContext_ShouldNotImplementRoleInterfaces` | The SndContext concrete type implements no role interfaces (pure composition object) | Snd Abstraction: ISndContext composition |
| `SndContext_CompanionProperties_ShareConsistentState` | Companion properties share the same blackboard instances (SystemBlackboard/ProgressBlackboard) | Snd Abstraction: ISndContext composition |
| `IStateMachineContext_ShouldInheritSharedRoleInterfaces` | IStateMachineContext inherits ISndBlackboardAccess + ISndDeferredActions | StateMachine Abstraction |
| `DeferredFlush_ShouldNotBePublicBusinessSurface` | Frame flushing goes only through `IOrigoFrameDriver.DriveFrame`; `ISndDeferredActions` and `OrigoRuntime` expose no bypassable public flush | architecture-overview: single access path |
| `ConsolePump_ShouldNotBePublicBusinessSurface` | Console processing goes only through `IOrigoFrameDriver.DriveFrame`; `ISndConsoleAccess` and `OrigoConsole` expose no bypassable public pump | architecture-overview: single access path |
| `IEntityLifecycle_ShouldBeInternal` | IEntityLifecycle is internal — business code must not trigger lifecycle hooks directly | Runtime: lifecycle orchestration |
| `SndEntity_LifecycleMethods_ShouldBeInternal` | Concrete lifecycle methods like SndEntity.Process are internal, invoked only by framework orchestration | Runtime: lifecycle orchestration |
| `Consumer_UsingOnlyPublicInterfaces_CanPerformSaveLoadWorkflow` | Completes save→load workflow using only public interfaces | architecture-overview: test strategy |
| `Consumer_AccessesAllRoleInterfaces_ThroughISndContext` | All role-interface capabilities accessible through ISndContext (including ISndFileAccess file read/write, ISndArchiveFileAccess in-archive files) | Snd Abstraction |
| `SaveLoad_TriggeredThroughISndSaveOperations` | Save/Load triggered through ISndSaveOperations interface | persistence-flow |
| `SessionLifecycle_ManagedThroughISessionManager` | Session lifecycle managed through ISessionManager | session-model |
| `ISessionRun_ProvidesRuntimeAccess` | ISessionRun provides Blackboard/SceneHost/StateMachines access | session-model |
| `SessionManager_ProvidesCreateAndDestroyOperations` | ISessionManager provides CreateBackgroundSession/DestroySession | session-model |
| `ConsoleCommandHandlerBase_ShouldBePublic_SoExternalProjectsCanExtendIt` | ConsoleCommandHandlerBase is public, external projects can derive custom command handlers | console-bridge |

## AutoInitializerGuardTests Test Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `SndWorld_RegisterStrategy_WithStatefulInstanceField_Throws` | Registering strategy with instance fields | InvalidOperationException (contains "invalid instance members") |
| `SndWorld_RegisterStrategy_WithWritableInstanceProperty_Throws` | Registering strategy with writable instance properties | InvalidOperationException (contains "invalid instance members") |
| `SndWorld_RegisterTypeMappings_NullCallback_Throws` | null callback | ArgumentNullException |

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `SndWorld_RegisterStrategy_WithOnlyStaticFields_Succeeds` | Strategy with only static fields registers successfully | snd-entity-model: strategy pool rules |
| `SndWorld_RegisterStrategy_WithReadonlyInstanceField_Succeeds` | Strategy with readonly instance field registers successfully (framework allows readonly fields) | snd-entity-model |
| `SndWorld_WriteMetaListNode_NonListEnumerable_UsesToListPath` | Non-List IEnumerable as meta list goes through ToList path, serializes correctly | snd-entity-model |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `DiscoverAndRegisterStrategies_WithBroadSkipPrefixes_ReturnsZero` | Skip all assemblies (Origo prefix) | Registers 0 strategies |
| `DiscoverAndRegisterStrategies_SkippingTestAssembly_DoesNotRegisterFromSkippedAssembly` | Skip the test assembly itself | Registers 0 strategies |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Core public API should not expose internal type concrete names | API stability | architecture-overview: public whitelist |

---

[↑ Back to Origo.Core.Tests](README.en.md)
