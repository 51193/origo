<!-- docsync-pair: Origo.Core.Tests/Testing/Integration/Integration -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Frame-Driven Game Simulation Integration Tests

> [↑ Back to Origo.Core.Tests](../../README.en.md)
> [↔ Behavior under test: usage/architecture-overview](../../../usage/architecture-overview.en.md)
> [↔ Module under test: Origo.Core/Runtime](../../../Origo.Core/Runtime/README.en.md)

## Behavior Under Test Overview

Validates the complete frame simulation pipeline driven by `IOrigoFrameDriver.DriveFrame(delta)`:
from `OrigoRuntime` → `SndContext` → `ProgressRun` → `SessionManager` → `SessionRun`,
the four-layer runtime, with real `SndEntity` entities and strategies participating, executes entity processing, business deferred queues, entity harvesting, and system deferred queues in frame loop order.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `GameplayIntegrationTests.cs` | Multi-frame data processing, inter-entity interaction (FindByName / SessionBlackboard), business deferred action execution, save persistence, entity destruction, console commands, observers; error path coverage: duplicate kill on already-dead entity |
| `GameplaySessionSwitchAndConcurrencyTests.cs` | Session switch blackboard isolation, same-frame concurrent spawn/kill, respawn after kill, parallel processing of multiple background sessions; error path coverage: kill already-harvested entity |
| `AdvancedGameplayIntegrationTests.cs` | Batch spawn/kill of many entities (100 entities), console command routing (snd_count / bb_set/bb_get system layer), entity data direct API round-trip, multi-strategy entity combinations (Lifecycle+Observer, Lifecycle+Active, all three types mounted), multi-entity save/load state preservation; error paths: request kill unknown entity, spawn unregistered strategy index |
| `ActiveStrategyIntegrationTests.cs` | ActiveStrategy integration in full frame loop: direct InvokeStrategy calls, self-invocation triggered by Process, cross-entity InvokeStrategy, ActiveStrategy index save/load persistence, AfterLoad Invoke verification, Lifecycle+Active hybrid entity frame loop, dynamic AddActiveStrategy/RemoveActiveStrategy lifecycle; error paths: InvokeStrategy on killed entity, duplicate AddActiveStrategy |
| `StateMachineIntegrationTests.cs` | State machine integration in frame loop: Push/Pop frame-driven, OnPushRuntime/OnPopRuntime hook triggers, OnPopBeforeQuit triggers on session destroy, state machine stack save/load AfterLoad recovery, multiple independent state machine stacks, Lifecycle strategy cross-frame Push/Pop states; error path: operate state machine after session destroy |
| `ObserverTopologyIntegrationTests.cs` | Observer topology integration in frame loop: mount triggers OnMounted+OnDataChanged (with correct old/new values), unmount stops notifications, target kill triggers OnUnmounted, data change old/new value correctness, multi-target independent notifications, frame-driven strategy auto-mounts observer during Process; error paths: invalid index mount, duplicate mount, mount on killed entity |
| `PlanningIntegrationTests.cs` | Intent-driven plan execution: intent triggers plan start, two-step plan completion, no intent no start, data attribute key validation, multi-entity independent plans |
| `StrategyStateSaveLoadIntegrationTests.cs` | Strategy state persistence: lifecycle count state survives, entity data+blackboard survives, continued processing after reload, 20-entity batch with no loss, overwrite saves, multi-session full state preservation; error path: corrupted progress.json causes load failure |
| `ErrorPathIntegrationTests.cs` | Correct execution of deferred actions within frame; error paths: corrupted session.json load, corrupted snd_scene.json load, non-existent save load |

## GameplayIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `MultiFrameProcessing_AccumulatesData` | Strategy increments count each frame; after RunFrames(10) count=10 | architecture-overview: Frame loop |
| `EntityInteraction_FindByName_ReadsPeerData` | Entity A reads peer_value of entity B via OwningSession.FindByName("peer") in Process | ISessionRun.FindByName |
| `EntityInteraction_ViaBlackboard_TransfersDataBetweenFrames` | Entity A writes to SessionBlackboard → same-frame entity B reads bridge_value | architecture-overview: Session model |
| `DeferredAction_ExecutesAfterFlush` | Strategy EnqueueBusinessDeferred → DriveFrame FlushEndOfFrameDeferred; deferred_ran=true | Scheduling |
| `SaveDuringGameplay_PersistsToDisk` | Run frames → RequestSaveGameAuto → verify progress.json/level snd_scene.json exist; entity data unchanged | persistence-flow |
| `EntityKill_BeforeDeadAndRemoval` | RequestKillEntity → DriveFrame → KillPendingAllSessions harvest; BeforeDead fires; entity removed | Runtime: SessionManager |
| `ConsoleCommand_DuringFrame` | Strategy TrySubmitConsoleCommand("snd_count") → DriveFrame ProcessPending → console output contains "Snd count:" | console-commands |
| `FullGameLoopRoundTrip_SaveDisposeReload` | Run frames → set session data → Save → dispose all sessions → Reload → game session's SessionBlackboard data restored | persistence-flow |
| `ObserverStrategy_MountAndNotify` | Entity B MountObserverStrategy(EntityA) → EntityA SetData("hp") → observer OnDataChanged triggers | snd-entity-model |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `MultiFrameProcessing_VariousFrameCounts_AccumulatesCorrectly` | (Boundary) 1/3/100 frames parameterized | count === frameCount for all frame counts |

## GameplaySessionSwitchAndConcurrencyTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `SwitchSession_BackgroundSessionBlackboard_Isolated` | Create background sessions → set independent keys on respective blackboards → verify no cross-contamination | SessionManager |
| `ConcurrentSpawnKill_SameFrame_AllCleanedUp` | Concurrently spawn 2 entities → kill both in same frame → verify BeforeDead fires twice + all entities removed | Runtime: SessionManager |
| `KillEntity_ThenRespawn_NewEntityIndependent` | Entity kill → respawn with same name → verify new entity data is independent (does not inherit old entity state) | ISndEntity lifecycle |
| `MultipleBackgroundSessions_EntitiesProcessedInParallel` | Create 3 sessions (1 foreground + 2 background) → each session spawns one FrameCounter → DriveFrame → verify each count=1 | SessionManager: Multi-session |
| `CrossSession_EntityReadsPeerInAnotherSession` | After an entity in a background session runs frames, its data can be read via TryGetSession + FindByName (count accumulates across frames) | SessionManager: Multi-session |
| `BackgroundSession_SaveLoad_IndependentEntityState` | Foreground and background session entities/blackboards are saved and loaded independently: foreground count and background count/blackboard values all restored | persistence-flow |
| `BackgroundSession_KillEntities_DuringForegroundPlay` | Harvesting a background session entity during foreground play (BeforeDead fires, entity removed); foreground entity unaffected | Runtime: SessionManager |
| `MultipleBackgroundSessions_SaveLoadCycle` | Multiple background sessions + foreground session all restored after save/reload (entity counts and each session's blackboard preserved independently) | persistence-flow |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ErrorPath_KillAlreadyKilledEntity_Throws` | Kill already-harvested entity | `InvalidOperationException` |

## AdvancedGameplayIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `BatchSpawn_100Entities_AllProcessed` | Batch-spawning 100 entities then running 5 frames: every entity has count=5 | architecture-overview: Frame loop |
| `BatchSpawn_ThenBatchKill_AllCleanedUp` | Batch-spawn 100 entities then batch-kill them in the same frame; all harvested and removed after DriveFrame | Runtime: SessionManager |
| `ConsoleCommand_SndCount_PublishesOutput` | Submitting the snd_count command produces console output containing "Snd count:" | console-commands |
| `ConsoleCommand_BbSetSystemLayer_RoundTrip` | bb_set/bb_get system-layer commands: int/string written and read back via SystemBlackboard; bb_get prints the value | console-commands |
| `EntityDataSetGet_DirectAPI_RoundTrip` | Entity SetData/GetData/TryGetData direct API round-trips (int/string/bool) | snd-entity-model: TypedData |
| `MultiStrategyEntity_LifecyclePlusObserver` | Lifecycle+Observer hybrid entity: frame processing increments count and triggers the observer's data change | snd-entity-model: Observer |
| `MultiStrategyEntity_LifecyclePlusActive` | Lifecycle+Active hybrid entity: frame processing accumulating count and InvokeStrategy work together | snd-entity-model |
| `MultiStrategyEntity_AllThreeTypes` | Entity with all three strategy types (Lifecycle+Observer+Active) processes frames, notifies, and invokes normally | snd-entity-model |
| `SaveLoad_MultipleEntities_StatePreserved` | Save/reload of 10 entities + session blackboard restores entity count, count, tag, and blackboard values | persistence-flow |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ErrorPath_RequestKillUnknownEntity_Throws` | RequestKillEntity on a non-existent entity | InvalidOperationException |
| `ErrorPath_SpawnWithUnregisteredStrategyIndex_Throws` | Spawning with an unregistered strategy index | Throws |

## ActiveStrategyIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `InvokeStrategy_DirectCall_ReturnsResult` | After dynamically AddActiveStrategy, direct InvokeStrategy returns the strategy result (21→42) | snd-entity-model: ActiveStrategy |
| `InvokeStrategy_ProcessTriggersActive_WithinFrame` | A Lifecycle strategy calls its own InvokeStrategy during Process and writes the result to entity data | snd-entity-model: ActiveStrategy |
| `InvokeStrategy_PeerEntityActiveStrategy_CrossEntity` | During Process, calls another entity's ActiveStrategy via OwningSession.FindByName | snd-entity-model: ActiveStrategy |
| `ActiveStrategyIndices_SaveLoad_Persisted` | Dynamically mounted ActiveStrategy indices persist across save/reload; Invoke still works (result correct) | persistence-flow |
| `ActiveStrategy_AfterLoad_InvokeWorks` | After reload the entity's ActiveStrategy works and entity data is preserved | persistence-flow |
| `HybridEntity_LifecycleProcessAndActiveInvoke` | Lifecycle+Active hybrid entity: frame-loop Process accumulates count while Invoke remains usable | snd-entity-model |
| `ActiveStrategy_DynamicAddRemove_InFrameLoop` | Dynamic Add/RemoveActiveStrategy in the frame loop: Invoke throws before add, works after add, throws again after remove | snd-entity-model |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ErrorPath_InvokeActiveStrategyOnKilledEntity_Throws` | InvokeStrategy after the entity is killed | Throws |
| `ErrorPath_AddDuplicateActiveStrategy_Throws` | Duplicate AddActiveStrategy | Throws |

## StateMachineIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `StateMachine_PushPop_InFrameLoop` | Push/Pop of the state machine stack in the frame loop; Peek reflects the correct top | state-machine |
| `StateMachine_OnPushHook_FiresCorrectly` | Push triggers the OnPushRuntime hook (records the after-top value) | state-machine: Push |
| `StateMachine_OnPopHook_FiresCorrectly` | TryPopRuntime triggers the OnPopRuntime hook; Peek returns false once the stack is empty | state-machine: TryPopRuntime |
| `StateMachine_OnPopBeforeQuit_FiresOnSessionDestroy` | OnPopBeforeQuit fires for stacked states when the session is destroyed | state-machine: TryPopOnQuit |
| `StateMachine_SaveLoad_PreservesStack` | The stack survives save/reload (AfterLoad hooks fire per layer; Pop continues to work) | state-machine: Load Restoration |
| `StateMachine_SaveLoad_AfterLoadHookFiresOncePerLayer` | Each restored stack layer fires OnPushAfterLoad exactly once (no double flush) | state-machine: Load Restoration |
| `StateMachine_MultipleEntities_IndependentStacks` | Multiple state machine stacks in the same session are independent | state-machine |
| `StateMachine_EntityLifecycleStrategy_PushesAndPopsState` | A Lifecycle strategy pushes/pops an entity state machine across frames | state-machine |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ErrorPath_PushStateMachineAfterSessionDestroy_Throws` | Operating the state machine (Peek/Push) after session destroy | ObjectDisposedException |

## ObserverTopologyIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Observer_Mount_TriggersOnMountedAndDataChange` | Mount triggers OnMounted; target data changes trigger OnDataChanged (new value correct) | snd-entity-model: Observer |
| `Observer_Unmount_StopsNotifying` | Target data changes no longer notify after Unmount | snd-entity-model: Observer |
| `Observer_TargetKilled_TriggersOnUnmounted` | Killing the target entity triggers OnUnmounted | snd-entity-model: Observer |
| `Observer_OldAndNewValues_CorrectOnChange` | Consecutive changes deliver the correct oldValue/newValue sequence to OnDataChanged | snd-entity-model: Observer |
| `Observer_MultipleTargets_NotifiedIndependently` | An observer watching multiple targets is notified independently per target | snd-entity-model: Observer |
| `Observer_FrameDriven_StrategyMountsObserverInProcess` | A Lifecycle strategy auto-mounts an observer in AfterSpawn; notifications work in the frame loop | snd-entity-model: Observer |
| `Observer_Bindings_RestoredAcrossSaveAndReload` | Observer bindings are restored after save/reload; data changes still notify | persistence-flow |
| `Observer_OnMounted_FiresAgainAfterReload` | OnMounted fires again after reload restores the binding | persistence-flow |
| `Observer_AfterLoadFiresBeforeObserverRecoveryOnReload` | During reload, every entity's AfterLoad runs before Observer bindings recover and fire OnMounted | snd-entity-model: Observer |
| `Observer_OnUnmountedFiresBeforeTargetBeforeDead` | When a target dies, Observer unwiring fires OnUnmounted before the target's BeforeDead runs | snd-entity-model: Observer |
| `Observer_OnUnmounted_FiresWhenSessionIsDestroyed` | Observers receive OnUnmounted when the session is destroyed | snd-entity-model: Observer |
| `Observer_TargetDataNoLongerNotifiesAfterSessionDestroyed` | Target data changes no longer notify after the session is destroyed | snd-entity-model: Observer |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Observer_MountWithInvalidIndex_Throws` | Mounting with an unregistered observer index | InvalidOperationException ("not found") |
| `Observer_DuplicateMount_Throws` | Duplicate mount of the same (observer, target, index) | InvalidOperationException ("already mounted"); OnMounted does not fire twice |
| `Observer_MountToKilledEntity_Throws` | Mounting onto a killed target entity | InvalidOperationException ("pending kill") |
| `Observer_KilledObserverCannotMount_Throws` | A killed observer entity initiates a mount | InvalidOperationException ("pending kill") |
| `Observer_MountAcrossSessions_Throws` | Cross-session mount (entity from another session mounting a foreground entity) | InvalidOperationException ("different sessions") |

## PlanningIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `PlanExecution_SetIntent_StartsPlanInFrameLoop` | Setting the intent data starts the plan frame-driven (plan_step=step_a, action_status=executing) | Planning: PlanExecutionStrategyBase |
| `PlanExecution_CompletePlan_InFrameLoop` | After actions complete, the frame-driven plan advances steps until the intent completes (task_status=completed) | Planning: PlanExecutionStrategyBase |
| `PlanExecution_WithoutIntent_DoesNotStart` | Without intent the plan does not start (no plan_step/task_status data) | Planning: PlanExecutionStrategyBase |
| `PlanExecution_DataAttributeKeys_AreSetCorrectly` | plan_step and action_index data keys are set correctly once the plan starts | Planning: PlanExecutionStrategyBase |
| `PlanExecution_MultipleEntities_IndependentPlans` | Plans on multiple entities are independent; one entity advancing does not affect another | Planning: PlanExecutionStrategyBase |

## StrategyStateSaveLoadIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `LifecycleStrategy_StateSurvivesSaveLoad` | Lifecycle strategy count state and entity data survive save/reload; frame processing continues after reload | persistence-flow |
| `EntityDataAndBlackboard_BothSurviveSaveLoad` | Entity data and SessionBlackboard (int/string) both restored after save/reload | persistence-flow |
| `SaveLoad_ThenContinue_EntityStillProcesses` | After reload, running frames continues to accumulate the entity count from the restored value | persistence-flow |
| `SaveLoad_NoLossOfEntities` | Batch save/reload of 20 entities loses nothing (entity count, count, and id all correct) | persistence-flow |
| `SaveTwice_SecondOverwrites_StateCorrect` | Second save to the same slot overwrites: reload yields the second state (count and blackboard version) | persistence-flow |
| `SaveLoad_MultipleSessions_AllStatePreserved` | Foreground + background session entities and blackboards all preserved after save/reload | persistence-flow |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ErrorPath_LoadCorruptSave_Throws` | Loading with corrupted progress.json | Flush throws (fail-fast) |

## ErrorPathIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `DeferredAction_ExecutesAndFlushesThroughDriveFrame` | Deferred actions enqueued during Process execute via the DriveFrame flush (count accumulates + deferred_ran=true) | Scheduling |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ErrorPath_LoadNonexistentSave_Throws` | Loading a non-existent save | Flush throws (message contains "nonexistent") |
| `ErrorPath_LoadSaveWithCorruptedSessionFile_Throws` | Loading with corrupted session.json | Flush throws |
| `ErrorPath_LoadSaveWithCorruptedSndScene_Throws` | Loading with corrupted snd_scene.json | Flush throws |

## Test Helper Facilities

| Facility | Location | Purpose |
|----------|----------|---------|
| `GameplaySimulationHarness` | `TestSupport/GameplaySimulationHarness.cs` | One-liner to create full runtime: OrigoRuntime + SndContext + background game session (syncProcess=true), provides DriveFrame/RunFrames/SpawnEntity/RequestKillEntity/CreateBackgroundSession/GetEntityData/SubmitConsoleCommand/SetEntityData/InvokeEntityStrategy/MountObserver/SaveAndReload |
| `GameplaySimulationBuilder` | `TestSupport/GameplaySimulationHarness.cs` | Fluent Builder: WithStrategy to register strategies, WithSessionConfig to set session blackboard |
| `TestStrategies` | `TestSupport/TestStrategies.cs` | Shared abstract strategy base classes: `SharedFrameCounterStrategy` (AfterSpawn initializes count=0, Process increments each frame), `SharedEchoActiveStrategy` (Invoke returns input×2), `SharedKillProbeStrategy` (records events in BeforeDead), `SharedNoopLifecycleStrategy` (empty lifecycle), `SharedNoopStateMachineStrategy` (empty state machine). Each test file references them via `private sealed` subclasses with independent `[StrategyIndex]`. |
| `PeerLookupStrategy` | `GameplayIntegrationTests.cs` | Looks up peer entity via OwningSession.FindByName in Process and reads its data |
| `BbWriterStrategy` | `GameplayIntegrationTests.cs` | Writes bridge_value to OwningSession.SessionBlackboard during Process |
| `BbReaderStrategy` | `GameplayIntegrationTests.cs` | Reads bridge_value from OwningSession.SessionBlackboard during Process and stores it in entity data |
| `DeferredProbeStrategy` | `GameplayIntegrationTests.cs` | EnqueueBusinessDeferred during Process; sets deferred_ran=true |
| `ConsoleCommandStrategy` | `GameplayIntegrationTests.cs` | TrySubmitConsoleCommand("snd_count") during Process; verifies command is handled through DriveFrame processing |
| `HpObserverIntegrationStrategy` | `GameplayIntegrationTests.cs` | ObserverStrategyBase: OnDataChanged records "changed:{dataKey}"; verifies observer Mount/Notify mechanism |
| `DataObserverIntegrationStrategy` | `AdvancedGameplayIntegrationTests.cs` | ObserverStrategyBase: ObserveData("count"), OnDataChanged records "changed:{dataKey}"; used for multi-strategy combination tests |
| `BatchFrameCounterStrategy` | `AdvancedGameplayIntegrationTests.cs` | SharedFrameCounterStrategy subclass; batch spawn/kill frame counting |
| `EchoActiveStrategy` | `AdvancedGameplayIntegrationTests.cs` | SharedEchoActiveStrategy subclass (input×2); all-three-strategy-type combination tests |
| `EchoActiveStrategy` | `ActiveStrategyIntegrationTests.cs` | SharedEchoActiveStrategy subclass (input×2); verifies ActiveStrategy Invoke/save persistence |
| `SelfInvokeStrategy` | `ActiveStrategyIntegrationTests.cs` | Calls its own InvokeStrategy during Process and writes the result |
| `PeerInvokeStrategy` | `ActiveStrategyIntegrationTests.cs` | Calls a peer entity's ActiveStrategy via FindByName during Process and writes the result |
| `AdvFrameCounterStrategy` | `ActiveStrategyIntegrationTests.cs` | SharedFrameCounterStrategy subclass; hybrid entity frame counting |
| `ErrorPathFrameCounterStrategy` | `ErrorPathIntegrationTests.cs` | SharedFrameCounterStrategy subclass; frame counting for corrupted-save load scenarios |
| `DeferredCounterStrategy` | `ErrorPathIntegrationTests.cs` | Increments count each frame in Process; enqueues a deferred action setting deferred_ran=true when count>0 |
| `BlackboardMarkerStrategy` | `GameplaySessionSwitchAndConcurrencyTests.cs` | SharedNoopLifecycleStrategy subclass; placeholder for session switch blackboard isolation scenarios |
| `KillableTestStrategy` | `GameplaySessionSwitchAndConcurrencyTests.cs` | SharedKillProbeStrategy subclass; records BeforeDead events |
| `FrameCounterStrategy` | `GameplaySessionSwitchAndConcurrencyTests.cs` | SharedFrameCounterStrategy subclass (TestStrategyIndices.FrameCounter); frame counting for multi-session parallel/save scenarios |
| `TopologyObserverStrategy` | `ObserverTopologyIntegrationTests.cs` | ObserverStrategyBase watching hp; records OnMounted/OnDataChanged/OnUnmounted events |
| `ValueCapturingObserverStrategy` | `ObserverTopologyIntegrationTests.cs` | ObserverStrategyBase watching hp; records oldValue/newValue |
| `TargetAwareObserverStrategy` | `ObserverTopologyIntegrationTests.cs` | ObserverStrategyBase watching hp; records TargetName |
| `AutoMountObserverLifecycleStrategy` | `ObserverTopologyIntegrationTests.cs` | Auto-mounts an observer onto "target" in AfterSpawn; verifies frame-driven mounting |
| `LifecycleOrderProbeStrategy` | `ObserverTopologyIntegrationTests.cs` | Records AfterLoad / BeforeDead events to verify observer wiring order relative to lifecycle hooks |
| `TwoStepPlanStrategy` | `PlanningIntegrationTests.cs` | PlanExecutionStrategyBase subclass: two-step plans (step_a→step_b) for intents "build"/"repair" |
| `NoopActionStrategy` | `PlanningIntegrationTests.cs` | SharedNoopLifecycleStrategy subclass; plan Action placeholder |
| `PushTrackingStateMachineStrategy` | `StateMachineIntegrationTests.cs` | SharedNoopStateMachineStrategy subclass; drives Push/Pop stack in the frame loop |
| `HookRecordingStateMachineStrategy` | `StateMachineIntegrationTests.cs` | Records on_push_runtime/on_push_after_load/on_pop_runtime/on_pop_before_quit events |
| `SmPushingLifecycleStrategy` | `StateMachineIntegrationTests.cs` | Lifecycle strategy: pushes "active" in AfterSpawn, pops and pushes "idle" once frame count reaches 3 |
| `StateFrameCounterStrategy` | `StrategyStateSaveLoadIntegrationTests.cs` | SharedFrameCounterStrategy subclass; frame counting for strategy state save/load |

## Usage Pattern

```csharp
var harness = GameplaySimulationHarness.Create()
    .WithStrategy(() => new CounterStrategy())
    .Build();

harness.SpawnEntity("counter", ["test.counter"]);

harness.RunFrames(10);

var count = harness.GetEntityData<int>("counter", "count");
Assert.Equal(10, count);
```

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Extended scenarios of multi-entity batch spawn + frame processing (entity count > 100) | Stability of frame loop with large entity counts not verified | architecture-overview: Frame loop |
| Cross-entity state machine interaction of StrategyStateMachine in frame loop | Cross-entity effects triggered by state machine transitions not verified | state-machine |

---

[↑ Back to Origo.Core.Tests](../../README.en.md)
