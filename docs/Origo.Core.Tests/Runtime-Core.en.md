<!-- docsync-pair: Origo.Core.Tests/Runtime-Core -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Runtime Core Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Runtime](../Origo.Core/Runtime/README.en.md)
> [↔ Behavior under test: usage/architecture-overview](../usage/architecture-overview.en.md)

## Behavior Overview

Validates basic OrigoRuntime construction and console injection, flushing of end-of-frame deferred action queues, the two-phase mark-and-reap semantics of entity Kill/KillAll, and ActionScheduler enqueue/nested execution/clear.

`SchedulingAndTypeMappingTests.cs` hosts tests for both ActionScheduler and TypeStringMapping capabilities: this document records its ActionScheduler-related methods; its TypeStringMapping methods (`TypeStringMapping_HasDefaultTypes_AndSupportsCustomRegistration`) belong to the type serialization capability, recorded in [TypeStringMapping.en.md](TypeStringMapping.en.md), and are not duplicated here.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `OrigoRuntimeBasicTests.cs` | OrigoRuntime construction, SndWorld creation, console injection/uninjected, ResetConsoleState, FlushEndOfFrameDeferred, IOrigoFrameDriver.DriveFrame |
| `EntityKillTests.cs` | Entity Kill/KillAll: mark as pending destroy (IsPendingKill), KillPendingAllSessions reap, BeforeDead/BeforeQuit hooks, kill_all command |
| `SchedulingAndTypeMappingTests.cs` | ActionScheduler enqueue/nested execution/clear (TypeStringMapping methods see [TypeStringMapping.en.md](TypeStringMapping.en.md)) |

## OrigoRuntimeBasicTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `OrigoRuntime_Constructor_CreatesSndWorld` | After construction, SndWorld and Logger are available | Runtime: OrigoRuntime |
| `OrigoRuntime_ConsoleInputBuffer_NullWithoutInjection` | Console-related properties are null without console injection | Runtime: Console |
| `OrigoRuntime_WithConsole_CreatesConsole` | Console is available after injecting console input/output | Runtime: Console |
| `OrigoRuntime_ResetConsoleState_ClearsInputQueue` | Reset only clears the input queue | Runtime: Console |
| `OrigoRuntime_FlushEndOfFrameDeferred_ExecutesDeferredActions` | Both Business and System deferred actions executed | Scheduling |
| `OrigoRuntime_DriveFrame_DelegatesToFlushAndConsole` | IOrigoFrameDriver.DriveFrame(delta) executes business deferred queue and processes console pending | Abstractions/Runtime: IOrigoFrameDriver |

## EntityKillTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `RequestKillEntity_TriggersBeforeDead_ViaFlush` | RequestKillEntity first marks IsPendingKill, then triggers BeforeDead and removes on reap | Runtime: Entity Lifecycle |
| `ManualIterateAndRequestKillEntity_MarksAllAliveEntities` | Iterating session entities and calling RequestKillEntity marks all alive entities | Runtime: SessionManager |
| `ManualKillAll_SkipsAlreadyPendingEntities` | Skips already IsPendingKill entities during iteration, no duplicate requests | Runtime: SessionManager |
| `ManualKillAll_RemovesAllAfterFlush` | After marking all, KillPendingAllSessions removes all entities | Runtime: SessionManager |
| `KillPendingEntities_FiresBeforeDead` | Reap triggers BeforeDead hook and removes entity | Runtime: Entity Lifecycle |
| `KillPendingEntities_BusinessDeferredBeforeKillSweep` | Business deferred actions execute in enqueue order, and reap triggers BeforeDead after them | Scheduling |
| `KillPendingAllSessions_RemovesPendingEntities` | KillPendingAllSessions removes marked entities | Runtime: SessionManager |
| `DeadByName_RemovesEntity` | After RemoveEntity, entity is removed from scene | Runtime: Entity Lifecycle |
| `StubSndSceneHost_DeadByName_RemovesEntity` | Stub host RemoveEntity removes entity | Runtime: ISndSceneHost |
| `StubSndSceneHost_RequestKillEntity_MarksPendingKill` | Stub host RequestKillEntity marks IsPendingKill, entity still in collection | Runtime: ISndSceneHost |
| `IsPendingKill_CanBeCheckedByStrategy` | After RequestKillEntity, strategy can read IsPendingKill | Runtime: Entity Lifecycle |
| `ClearAll_TriggersBeforeQuit` | FireBeforeQuitHooks + RemoveAllEntities triggers BeforeQuit and clears scene | Runtime: Entity Lifecycle |
| `KillAllCommand_MarksAllEntities` | kill_all command marks all entities as IsPendingKill | console-commands: kill_all |
| `KillAllCommand_SkipsAlreadyPending` | kill_all keeps already marked entities as IsPendingKill | console-commands: kill_all |
| `FullCycle_ProcessMarksThenFlushRemoves` | ProcessAll → RequestKillEntity → KillPendingAllSessions full cycle removes entity | Runtime: SessionManager |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `StubSndSceneHost_RequestKillEntity_Missing_Throws` | RequestKillEntity for non-existent entity | InvalidOperationException |
| `Spawn_DuplicateEntityName_Throws` | Spawning an entity with a duplicate name in the session | InvalidOperationException (contains "already exists"), and only the first entity remains |
| `StubSndSceneHost_RequestKillEntity_AlreadyPending_Throws` | Duplicate RequestKillEntity on already IsPendingKill entity | InvalidOperationException |
| `StubSndSceneHost_DeadByName_MissingEntity_Throws` | RemoveEntity for non-existent entity | InvalidOperationException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `ManualKillAll_EmptyScene_DoesNotThrow` | Iterate and RequestKillEntity on empty scene | Does not throw |
| `KillPendingEntities_NoPendingEntities_DoesNotThrow` | KillPendingAllSessions with no pending entities | Does not throw, entities retained |
| `IsPendingKill_DefaultFalse` | Newly created entity | IsPendingKill defaults to false |

## SchedulingAndTypeMappingTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ActionScheduler_Tick_ExecutesQueuedAndNestedActions` | Tick executes queued and re-enqueued nested actions, returns execution count with correct order | Scheduling |
| `ActionScheduler_Clear_RemovesPendingActions` | After Clear, Tick does not execute cleared actions, returns 0 | Scheduling |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `KillProbeStrategy` | EntityKillTests.cs | LifecycleStrategyBase probe: records "before_dead" event on BeforeDead, verifying Kill reap triggers hooks |
| `QuitProbeStrategy` | EntityKillTests.cs | LifecycleStrategyBase probe: records "before_quit" event on BeforeQuit, verifying ClearAll triggers quit hooks |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| OrigoRuntime behavior when disposed immediately after construction | Correctness of resource cleanup | Runtime |
| SystemBlackboard isolation across multiple ProgressRun instances | Whether system blackboard is shared across Progress calls | Runtime: Four-Layer Runtime |
| Recursion guard boundary for ActionScheduler nesting depth | Depth limit semantics for infinite re-enqueue | Scheduling |

---

[↑ Back to Origo.Core.Tests](README.en.md)
