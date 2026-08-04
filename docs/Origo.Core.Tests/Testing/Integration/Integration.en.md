<!-- docsync-pair: Origo.Core.Tests/Testing/Integration/Integration -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
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

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ErrorPath_KillAlreadyKilledEntity_Throws` | Kill already-harvested entity | `InvalidOperationException` |

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
