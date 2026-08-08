<!-- docsync-pair: Origo.Core.Tests/Session-Lifecycle -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Session Lifecycle Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Runtime/Lifecycle](../Origo.Core/Runtime/Lifecycle/README.en.md)
> [↔ Behavior under test: usage/session-model](../usage/session-model.en.md)

## Behavior Overview

Validates the complete lifecycle of the Origo session model: SessionRun/ProgressRun creation and destruction, Dispose semantics (idempotent, no auto-persist, BeforeQuit hook, exception safety),
interface and behavioral consistency of foreground and background sessions (ISessionRun), IsFrontSession flag, foreground uniqueness constraint, session topology codec,
level switching (SwitchForeground), save→switch→load round-trip, session decoupling (independent Blackboard/SceneHost),
full SessionManager API (create/find/destroy/enumerate/ProcessAll/KillPending), and persistence round-trip of ProgressRun with background sessions.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `LifecycleRunsTests.cs` | SessionRun/ProgressRun lifecycle, MountKey, LoadFromPayload, SwitchForeground, logging |
| `DisposeSemanticsTests.cs` (3 partial files: `SessionRun.cs`, `ProgressRun.cs`, `RoundTrip.cs`) | Dispose does not trigger BeforeSave, triggers BeforeQuit, idempotent, Save-then-Dispose-then-Continue round-trip, exception safety |
| `ForegroundBackgroundContractTests.cs` | Complete behavioral consistency of foreground/background ISessionRun (blackboard/state machines/serialization/Dispose) |
| `EmptySessionManagerTests.cs` | No-op behavior of EmptySessionManager |
| `PlayStopPlayRoundTripTests.cs` | Round-trip consistency of multiple Play→Stop→Play cycles (identity/blackboard/Tick/Progress) |
| `ProgressRunSessionLoadingEdgeTests.cs` | ProgressRun load error paths (topology format errors/file missing/background load failure) |
| `ProgressRunLoadRollbackMaskingTests.cs` | Regression: session cleanup after a background mount failure must not mask the original load exception if cleanup itself throws (BeforeQuit hook) — cleanup failure is only logged as Warning, the original exception propagates unchanged |
| `SessionRunLoadRollbackMaskingTests.cs` | Regression: SessionRun load-failure rollback (`ResetAfterLoadFailure`) executes cleanup step by step; when the `OnUnmounted` hook throws, the remaining steps still execute (entities/blackboard cleared), the original exception is not masked, and cleanup failures are logged as Warning |
| `SaveAndSwitchForegroundIntegrationTests.cs` | Combined save + switch level operations, collision handling, deferred queue orchestration |
| `SessionDecouplingTests.cs` | Sessions run independently without interference (SessionStateMachineContext, SceneHost, path policy) |
| `SessionManagerTests.cs` | ISessionManager: create/find/destroy/enumerate/ProcessAll/KillPending |
| `SessionTopologyCodecTests.cs` | SessionTopology codec round-trip |
| `TopologyInvariantTests.cs` | Topology invariant validation: EnsureActiveLevel happy/missing/empty/whitespace/mismatched topologies (fail-fast) |
| `BackgroundSessionTests.cs` | Independent background session tests (entities/Process/serialization/persistence round-trip) |
| `BackgroundSession_CreationWithCorrectFlagTests.cs` | Background IsFrontSession=false |
| `BackgroundSession_MultipleInstancesAllowedTests.cs` | Background allows multiple instances |
| `FrontSession_CreationWithCorrectFlagTests.cs` | Foreground IsFrontSession=true |
| `FrontSession_StrategyContextReceivesFrontFlagTests.cs` | Strategy context receives foreground flag |
| `FrontSession_UniqueConstraintValidationTests.cs` | Foreground uniqueness constraint |
| `SwitchForegroundCleanupTests.cs` | Regression: SwitchForeground runs full disposal semantics (BeforeQuit / observer teardown / strategy pool release), re-mounts observer bindings when switching back to a previous level, and leaves no half-mounted foreground on load failure |
| `SessionRunHookIterationTests.cs` | Regression: spawning entities inside AfterLoad/BeforeSave/BeforeQuit hooks does not break batch iteration (live-view host); disposal harvests in passes until convergence, and a non-converging quit hook (infinite spawn) fails loudly instead of hanging |

## LifecycleRunsTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ProgressRun_LoadFromPayload_RestoresProgressAndSession` | Restores Progress and Session blackboard data from Payload | session-model: Persistence Restoration |
| `ProgressRun_SwitchForegroundLevel_PersistsOldSession_AndLoadsNewSessionFromCurrent` | On level switch, old session explicitly persisted, new session loaded from current/ | session-model: Level Switch |
| `ProgressRun_SwitchForegroundLevel_WhenTargetMissing_EntersEmptySessionAndClearsScene` | When target level has no data, enters empty session, clears Scene | session-model |
| `ProgressRun_LoadAndMountForeground_SyncsSessionTopologyToProgressBlackboard` | After LoadAndMountForeground, topology written to ProgressBlackboard | session-model: Session Topology |
| `SessionRun_SerializeToPayload_RoundTrip_PreservesBlackboardData` | After RequestSaveGame writes to file, blackboard data not lost | session-model: Persistence |
| `LoadAndMountForeground_WhenNoPayloadFound_MountsEmptySession` | Loads empty session when no save data | session-model |
| `SessionRun_Create_LogsCreation` | Logs recorded on SessionRun creation | Logging |
| `ProgressRun_Create_LogsCreation` | Logs recorded on ProgressRun creation | Logging |
| `SessionManager_Mount_LogsMounting` | Logs recorded on session mount | Logging |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SessionRun_Dispose_ClearsSessionAndScene_ThenThrowsOnAccess` | Access SessionBlackboard/FindByName/StateMachines after Dispose | ObjectDisposedException |
| `ProgressRun_LoadFromPayload_WithEmptyProgressNode_ThrowsMissingSessionTopology` | progress.json without topology field | InvalidOperationException |
| `ProgressRun_BuildSavePayload_ThrowsWhenProgressTopologyForegroundDoesNotMatchForeground` | Topology foreground mismatch with actual foreground | InvalidOperationException |
| `SessionRun_LoadFromPayload_WhenSceneLoadFails_ResetsSessionState` | Scene JSON syntax error | Exception |
| `ProgressRun_LoadFromPayload_MissingProgressStateMachinesNode_Throws` | Payload missing ProgressStateMachinesNode | InvalidOperationException |
| `LoadAndMountForeground_WithEmptyLevelId_Throws` | Empty or whitespace levelId | ArgumentException |
| `SwitchForeground_WithEmptyLevelId_Throws` | Empty or whitespace levelId | ArgumentException |
| `BuildSavePayload_WithoutTopologySet_Throws` | Topology in ProgressBlackboard is empty string | InvalidOperationException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SessionRun_MountKey_IsNull_WhenNotMounted` | Create background then destroy, Contains returns false | Unmounted |
| `SessionRun_MountKey_SetOnMount_ClearedOnUnmount` | Create background then Contains is true, false after destroy | ISessionManager correctly manages mounts |
| `SessionRun_Dispose_AutoUnmountsFromManager` | Auto-unmount from Manager on Dispose | Contains returns false |
| `SessionManager_Clear_EmptiesAllSessions` | Destroy sessions one by one, Keys empty | All cleared |
| `ResolveLevelPayload_ReturnsNull_WhenNoData` | Returns null when no save data exists | Returns null |

## DisposeSemanticsTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SessionRun_Dispose_DoesNotWriteFilesToCurrent` | Dispose does not auto-write files (verified via ISaveStorageService) | session-model: Dispose Does Not Persist |
| `SessionRun_Dispose_DoesNotTriggerBeforeSave` | Dispose does not trigger BeforeSave hook | session-model: Dispose Does Not Persist |
| `SessionRun_Dispose_TriggersBeforeQuit` | Dispose triggers BeforeQuit hook | session-model |
| `SessionRun_ExplicitPersistLevelState_WritesToCurrent_BeforeDispose` | Persists to file via RequestSaveGame | session-model |
| `SessionRun_ExplicitPersistLevelState_TriggersBeforeSave` | Triggers BeforeSave via RequestSaveGame | session-model |
| `ProgressRun_Dispose_DoesNotCallPersistProgress` | ProgressRun.Dispose does not call PersistProgress | session-model |
| `ProgressRun_Dispose_DeletesCurrentDirectory` | ProgressRun.Dispose deletes current/ | session-model |
| `SessionRun_AfterDispose_SaveDoesNotPersistSessionData` | Save after Dispose does not include disposed session data | File does not exist |
| `SessionRun_AfterDispose_SaveExcludesDisposedSession` | RequestSaveGame after Dispose excludes disposed session | File does not exist |
| `ExplicitSave_ThenDispose_ThenContinue_LoadsSavedState` | Explicit save→Dispose→Continue round-trip restores entities and blackboard | session-model: Persistence |
| `Save_ThenDispose_ThenContinue_ProgressBlackboardPreserved` | ProgressBlackboard data preserved after Continue | session-model |
| `SaveAfterSwitch_HasCorrectActiveLevel` | ActiveLevelId correct after switch + save | session-model |
| `SaveSwitchDisposeReload_RestoresToSavedState` | Save→Switch→Dispose→ReLoad full round-trip restores all state | session-model |
| `FullRoundTrip_SwitchForeground_OldLevelDataPersistedImplicitly` | SwitchForeground explicitly persists old foreground level data to current/ | session-model |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SessionRun_AfterDispose_SessionBlackboard_ThrowsObjectDisposed` | Access SessionBlackboard after Dispose | ObjectDisposedException |
| `SessionRun_AfterDispose_SceneHost_ThrowsObjectDisposed` | FindByName after Dispose | ObjectDisposedException |
| `SessionRun_AfterDispose_GetSessionStateMachines_ThrowsObjectDisposed` | Get state machines after Dispose | ObjectDisposedException |
| `SessionRun_Dispose_DisposingSubscriberThrows_PropagatesAndSessionStillReleases` | Disposing subscriber throws | Exception propagates, but dispose state committed (second dispose is a no-op, access throws ObjectDisposedException) |
| `SessionRun_Dispose_DisposingSubscriberThrows_SessionMachinesAndEntitiesStillReleased` | Disposing subscriber throws | Exception propagates, but session state machines and entity strategies are all released (LogPoolLeaks finds no leak), disposed flag committed |
| `SessionRun_Dispose_PopHookThrows_SessionMachinesAndEntitiesStillReleased` | Session state-machine quit pop hook throws | Exception propagates, but session state machines and entity strategies are all released (LogPoolLeaks finds no leak), disposed flag committed |
| `ProgressRun_Dispose_PopHookThrows_ProgressStateStillReleasedAndFlagCommitted` | Quit pop hook throws | Exception propagates, but progress blackboard cleared, state machines released, disposed flag committed (second dispose idempotent) |
| `ProgressRun_Dispose_SessionTearDownThrows_ProgressStateStillReleased` | Subscriber throws during session teardown | Exception propagates, but progress state still released and dispose state committed (second dispose no-op) |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SessionRun_Dispose_Twice_IsIdempotent` | Two Dispose calls do not throw | Idempotent |
| `ProgressRun_Dispose_Twice_IsIdempotent` | Two Dispose calls do not throw | Idempotent |
| `ProgressRun_Dispose_DeletesCurrentDirectory_EvenWhenEmpty` | Safe on empty current/ | Idempotent |
| `ProgressRun_AfterDispose_ForegroundSession_IsNull` | ForegroundSession is null after Dispose | Returns null |
| `ProgressRun_AfterDispose_SessionManagerKeys_IsEmpty` | Keys empty after Dispose | Empty collection |
| `ProgressRun_AfterDispose_ProgressBlackboard_IsCleared` | ProgressBlackboard cleared after Dispose | TryGet returns false |
| `ProgressRun_Dispose_SafeEvenWhenNoCurrentDirectory` | Safe when current/ does not exist | Does not throw |
| `ProgressRun_Dispose_StateMachineContainerClear_DoesNotThrow` | StateMachineContainer Clear does not throw | Does not throw |
| `SessionRun_Dispose_BeforeQuit_CanAccessSceneHost` | Session resources still accessible during BeforeQuit | Does not throw ObjectDisposedException |
| `SessionRun_Dispose_BeforeQuitThrows_EntitiesStillRemoved` | Entities still removed after BeforeQuit throws | Exception propagates but entities cleaned up |
| `SessionRun_Dispose_BeforeQuitThrows_DoubleDisposeStillIdempotent` | Second Dispose after BeforeQuit throws does not throw | Idempotent |

## ForegroundBackgroundContractTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `CreateBackgroundSession_ReturnsISessionRun_NotConcreteType` | Background session exposed as ISessionRun | session-model: Shared Foreground/Background Interface |
| `CreateBackgroundSession_ThenLoadPayload_ReturnsISessionRun_NotConcreteType` | Background session after LoadPayload still exposed as ISessionRun | session-model: Shared Foreground/Background Interface |
| `ForegroundSession_ExposedAsISessionRun` | Foreground session exposed as ISessionRun | session-model |
| `SerializeToPayload_ProducesSameFormat_ForForegroundAndBackground` | Serialization format consistent | session-model |
| `LoadFromPayload_WorksIdentically_ForForegroundAndBackground` | Deserialization behavior consistent | session-model |
| `SessionBlackboard_ReadWrite_IdenticalBehavior` | Blackboard read/write behavior consistent | session-model |
| `SessionBlackboard_Isolated_BetweenForegroundAndBackground` | Foreground/background blackboard data isolated | session-model |
| `StateMachines_WorkIdentically_ForForegroundAndBackground` | State machine trigger strategy hook behavior consistent for foreground/background | session-model |
| `PersistLevelState_WritesToStorage_ForBothForegroundAndBackground` | PersistLevelState behavior consistent for foreground/background | session-model |
| `BusinessCode_CanTreatBothSessionsIdentically_ThroughInterface` | Business code can uniformly operate foreground/background through ISessionRun | session-model |
| `RoundTrip_SerializeAndLoad_IdenticalBetweenForegroundAndBackground` | Serialization round-trip identical for foreground/background | session-model |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Dispose_ThrowsOnAccess_ForBothForegroundAndBackground` | Access SessionBlackboard/FindByName/StateMachines after Dispose | ObjectDisposedException |

## EmptySessionManagerTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `EmptySessionManager_CreateBackgroundSession_Throws` | Call CreateBackgroundSession | InvalidOperationException (contains "ProgressRun") |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `EmptySessionManager_QueryAndNoOps` | ForegroundSession is null, Keys empty, TryGet/Contains return null/false, Destroy/ProcessAll no-ops | All no-ops, no throws |

## PlayStopPlayRoundTripTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `RoundTrip_ForegroundIdentity_Preserved` | Serialize→Dispose→Reconstruct→Deserialize: IsFrontSession and LevelId preserved | session-model |
| `RoundTrip_BackgroundTickState_Preserved` | Background syncProcess flag correctly restored after round-trip | session-model |
| `RoundTrip_SessionBlackboards_Isolated_NoCrossContamination` | Foreground/background blackboard data still isolated after round-trip | session-model |
| `RoundTrip_ProgressBlackboard_Shared_AcrossSessions` | ProgressBlackboard data shared across sessions after round-trip | session-model |
| `RoundTrip_AllSessionProperties_Restored_Correctly` | All session properties (flag/LevelId/Blackboard/Tick(syncProcess)) correctly restored after round-trip | session-model |
| `LoadFromPayload_FullyRestoresFromPayloadOnly` | LoadFromPayload fully restores from Payload, does not inject from external blackboard | session-model |
| `PayloadCodec_InMemoryRoundTrip_PreservesState` | Isolated in-memory payload round-trip (BuildSavePayload→LoadFromPayload, no disk) preserves foreground/background state | session-model |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `NewProgressRun_AlwaysStartsWithEmptyBlackboard` | ProgressRun always creates its own empty ProgressBlackboard | ForegroundSession null, Keys empty, blackboard keys empty |
| `LoadFromPayload_CanBeCalledMultipleTimes` | Call LoadFromPayload again on already-loaded ProgressRun | Cleanly replaces all state, no remnants from previous load |

## ProgressRunSessionLoadingEdgeTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `LoadFromPayload_WhenTopologyMalformed_ThrowsInvalidOperation` | Topology entry malformed (e.g. `bad_entry`) | InvalidOperationException (contains "Malformed session topology entry") |
| `LoadFromPayload_WhenTopologyMissing_ThrowsInvalidOperation` | progress.json without topology field | InvalidOperationException |
| `LoadAndMountForeground_WhenSndSceneIsEmpty_ThrowsInvalidOperation` | snd_scene.json empty or whitespace | InvalidOperationException (contains "invalid snd_scene.json") |
| `LoadAndMountForeground_WhenSessionStateMachineJsonIsMalformed_Throws` | session_state_machines.json syntax error | Exception |
| `LoadFromPayload_WhenBackgroundSessionLoadFails_ClearsMountedSessions` | Background session snd_scene invalid format causes load failure | Foreground set to null, no background keys |
| `RequestLoadGame_Failure_DisposesProgressRunAndClearsContextReference` | Save load fails (corrupted background level) | ProgressRun disposed, context reference cleared (ProgressBlackboard null, EnsureProgressRun throws InvalidOperationException) |

## ProgressRunLoadRollbackMaskingTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `LoadFromPayload_WhenBackgroundMountFails_OriginalExceptionSurvivesCleanupFailure` | Corrupted background level + foreground entity BeforeQuit hook throws | Original load exception propagates (without the BeforeQuit error message); cleanup still completes (foreground set to null); cleanup failure logged as Warning |

## SessionRunLoadRollbackMaskingTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `LoadFromPayload_WhenFlushFails_OriginalExceptionSurvivesCleanupFailure` | `FlushAllAfterLoad` throws (push strategy failure) + `OnUnmounted` hook throws during rollback | Original FLUSH exception propagates (without the OnUnmounted message); scene host cleared, session blackboard cleared; cleanup failure logged as Warning |

## SaveAndSwitchForegroundIntegrationTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `FullMemorySndSceneHost_Spawn_FindByName_FindsSelfDuringAfterSpawn` | FindByName finds self during AfterSpawn hook | session-model: FindByName |
| `FullMemorySndSceneHost_Spawn_FindByName_FindsSiblingsDuringAfterSpawn` | FindByName finds sibling entities during AfterSpawn hook | session-model |
| `FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSelfDuringAfterLoad` | FindByName finds self during AfterLoad hook | session-model |
| `FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSiblingsDuringAfterLoad` | FindByName finds sibling entities during AfterLoad hook | session-model |
| `SaveBackgroundWithEntities_ThenSwitchForeground_LoadsEntitiesIntoForeground` | Save background→Destroy→Switch foreground, entities load into new foreground | session-model: Level Switch |
| `SaveBackgroundWithEntities_ThenSwitchForeground_PreservesBlackboard` | Save background→Switch, blackboard data preserved | session-model |
| `SaveBackgroundWithEntities_ThenSwitchForeground_LevelIdMustNotConflict` | After switch, original background key absent, foreground owns the level | session-model |
| `PersistProgress_WritesFullTopologyIncludingBackgroundSessions` | PersistProgress writes full topology (foreground + background) | session-model: Session Topology |
| `SwitchForeground_PreservesBackgroundSessionsInTopology` | Background info preserved in topology after switch | session-model |
| `SwitchForeground_WithoutBackgroundSessions_TopologyIsForegroundOnly` | Topology is foreground-only when no backgrounds | session-model |
| `SwitchForeground_WithMultipleBackgroundSessions_PreservesAllInTopology` | All backgrounds preserved in topology with multiple | session-model |
| `SaveBackgroundSession_ThenSwitch_WritesAllLevelDataToCurrent` | Save background→Switch writes level data to current/ | session-model |
| `SaveBackgroundSession_ThenSwitch_ProgressJsonHasCorrectActiveLevel` | ActiveLevelId correct after switch | session-model |
| `SaveBackgroundSession_ThenSwitch_ThenReloadFromSnapshot_EntitiesPreserved` | Full round-trip: save→switch→snapshot→reload, entities preserved | session-model |
| `SwitchForeground_WithoutSave_WhenTargetLevelInBackgroundSession_LoadsEntities` | Direct switch (no explicit save) to level held by background | session-model |
| `RequestSaveGameAuto_ThenRequestSwitchForeground_EntitiesLoadRegardlessOfFlushOrder` | Save and Switch correctly orchestrated in deferred queue | session-model: Level Switch |
| `SwitchForeground_AutoPersistsOldForegroundSessionToCurrent` | Old foreground auto-persisted on switch | session-model |
| `SwitchForeground_BackgroundSessionEntitiesUntouched` | Background entities unaffected after switch | session-model |
| `SwitchForeground_BackgroundSessionTickStatePreserved` | Background syncProcess flag preserved after switch | session-model |
| `RequestSwitchForegroundLevel_ExecutesInSystemDeferredQueue` | Switch executes in system deferred queue | session-model |
| `RequestSwitchForegroundLevel_RunsAfterBusinessDeferred` | Switch runs after business deferred queue | session-model |
| `SwitchForeground_ExplicitPersist_WritesOldForegroundToCurrent` | Explicit PersistForegroundLevelState writes old foreground data | session-model |
| `SwitchForeground_BackgroundSessionStateIsNotAutoPersisted` | Background data not auto-persisted on switch | session-model |
| `SwitchForeground_BackgroundSessionStateCanBeExplicitlyPersisted` | Explicit PersistSession can persist background | session-model |
| `SwitchForeground_BackgroundCollision_AutoDestroysBackground` | Foreground switching to background-held levelId auto-destroys background | session-model |
| `SwitchForeground_BackgroundCollision_PreservesBackgroundData` | Background data preserved on collision switch and restored through foreground | session-model |
| `SwitchForeground_BackgroundCollision_ManyEntitiesAllPreserved` | Many entities all preserved on collision switch | session-model |
| `SwitchForeground_BackgroundCollision_OtherBackgroundsUntouched` | Other backgrounds unaffected on collision switch | session-model |
| `SwitchForeground_BackgroundCollision_TopologyCorrectAfterAutoDestroy` | Topology correct after auto-destroy (no destroyed background) | session-model |
| `SwitchForeground_BackgroundCollision_WithForegroundActive` | Collision switch with foreground active, old foreground data does not pollute new foreground | session-model |
| `SwitchForeground_BackgroundCollision_ProgressPersistedAtEnd` | progress.json exists and correct after collision switch completion | session-model |
| `SwitchForeground_BackgroundCollision_NoDataLossRoundTrip` | Collision switch→save→reload full round-trip with no data loss | session-model |
| `SwitchForeground_BackgroundCollision_DeferredQueueHandling` | Collision switch in deferred queue handled correctly | session-model |
| `SwitchForeground_BackgroundCollision_SubsequentSwitchStillWorks` | Two consecutive collision switches correct | session-model |
| `SaveBackgroundSession_ManyEntities_ThenSwitch_AllLoaded` | 50-entity collision switch, all loaded | session-model |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `BuildSavePayload_LevelIdCollision_CaughtAtSessionCreation` | Background created with levelId conflicting with foreground | InvalidOperationException |
| `BuildSavePayload_WithoutForegroundSession_Throws` | BuildSavePayload without foreground session | InvalidOperationException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SaveBackgroundSession_WithNoEntities_ThenSwitch_LoadsEmptyForeground` | Background with no entities, foreground empty after switch | Blackboard preserved, entities empty |
| `SwitchForeground_ToSameLevel_ReloadsFromCurrent` | Switching to same level reloads from current/ | Entities and blackboard data preserved |
| `SwitchForeground_WithoutSave_WhenTargetMissing_EntersEmptySession` | Target level has no data | Enters empty session |
| `SwitchForeground_BackgroundCollision_EmptyBackgroundWorks` | Empty background collision switch | Normal |

## SessionDecouplingTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SessionStateMachineContext_Binds_SessionBlackboard` | Foreground/background state machines each bind independent SessionBlackboard | session-model: Session Decoupling |
| `SessionStateMachineContext_Binds_SceneAccess` | Foreground/background state machines each bind independent SceneAccess | session-model |
| `SceneHost_ReturnsISndSceneHost_ForBothForegroundAndBackground` | Foreground/background SceneHost both are ISndSceneHost | session-model |
| `BackgroundSession_SceneHost_Spawn_FindByName_WithoutCasting` | Background SceneHost can Spawn/FindByName without type casting | session-model |
| `DefaultSaveStorageService_Uses_Injected_PathPolicy` | ISaveStorageService uses injected ISavePathPolicy | ISaveStorageService |
| `LevelBuilder_Commit_UsesStorageService` | LevelBuilder.Commit delegates to ISaveStorageService | session-model |

## SessionManagerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `CreateBackgroundSession_AddsSession_TryGetReturnsIt` | After creating background, TryGet returns same instance | ISessionManager |
| `DestroySession_RemovesSession_TryGetReturnsNull` | After destroy, TryGet returns null | ISessionManager |
| `ForegroundKey_IsAvailable_WhenForegroundSessionExists` | ForegroundSession non-null when foreground exists | ISessionManager |
| `DestroySession_ForegroundKey_ClearsForegroundSession` | After destroying foreground key, ForegroundSession is null | ISessionManager |
| `TryGet_ForegroundKey_ReturnsForegroundSession` | TryGet(ForegroundKey) returns foreground session | ISessionManager |
| `Contains_ForegroundKey_TrueWhenSessionActive` | Contains(ForegroundKey) true when foreground exists | ISessionManager |
| `Keys_IncludesForegroundAndBackground` | Keys includes both foreground and background keys | ISessionManager |
| `ProcessAllSessions_OnlyProcessesSyncedSessions` | ProcessAllSessions only processes sessions with syncProcess=true | ISessionManager |
| `SessionTopology_WellKnownKey_Exists` | WellKnownKeys.SessionTopology constant exists | Constant definition |
| `SwitchForeground_AutoHandlesBackgroundSessionCollision` | SwitchForeground auto-handles background levelId collision | ISessionManager |
| `CreateForegroundSession_DifferentLevelId_Succeeds` | Background with different levelId can be created then switch foreground | ISessionManager |
| `AppendBackgroundPayloads_DifferentLevelIds_IncludesBothInPayload` | Payload includes foreground and background with different levelIds | ISessionManager |
| `SessionRun_Spawn_CreatesEntity` | ISessionRun.Spawn creates entity | ISessionRun |
| `SessionRun_SpawnMany_CreatesMultipleEntities` | ISessionRun.SpawnMany creates multiple entities | ISessionRun |
| `SessionRun_RequestKillEntity_MarksEntityPending` | RequestKillEntity marks IsPendingKill | ISessionRun |
| `KillPendingAllSessions_ProcessesForegroundPendingKill` | KillPendingAllSessions executes cleanup of pending-kill entities | ISessionRun |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `CreateBackgroundSession_DuplicateKey_Throws` | Creating background with duplicate key | InvalidOperationException |
| `CreateBackgroundSession_DuplicateLevelIdWithForeground_Throws` | Background levelId conflicts with foreground | InvalidOperationException |
| `CreateBackgroundSession_DuplicateLevelIdWithAnotherBackground_Throws` | Background levelId conflicts with another background | InvalidOperationException |
| `AppendBackgroundPayloads_LevelIdCollisionBetweenForegroundAndBackground_Throws` | Background levelId conflicts with foreground | InvalidOperationException |
| `CreateBackgroundSession_DuplicateLevelId_ClearErrorMessage` | levelId conflict | InvalidOperationException (contains key/levelId/owner/Destroy suggestion) |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `DestroySession_NonExistentKey_DoesNotChangeMountedSessions` | Destroy non-existent key | Does not affect mounted sessions |
| `ForegroundSession_ReflectsProgressRunForegroundSession` | ProgressRun exists but no foreground session | ForegroundSession is null |
| `CreateBackgroundSession_SameLevelIdAsDestroyedSession_Succeeds` | Use levelId of already-destroyed session | Succeeds (levelId released) |
| `Contains_EmptyKey_ReturnsFalse` | Contains("") | Returns false |
| `DestroySession_EmptyKey_DoesNotThrow` | DestroySession("") | Does not throw |
| `TryGet_EmptyKey_ReturnsNull` | TryGet("") | Returns null |
| `TryGet_WhitespaceKey_ReturnsNull` | TryGet("   ") | Returns null |

## SessionTopologyCodecTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Parse_AndSerialize_RoundTripPreservesDescriptors` | Serialize→Parse round-trip preserves key/levelId/syncProcess | SessionTopologyCodec |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Parse_MalformedOrEmptyKeyOrLevel_ThrowsInvalidOperation` | Malformed/empty key or levelId | InvalidOperationException |
| `Parse_NonBooleanSyncField_ThrowsInvalidOperation` | syncProcess field is non-boolean (not_bool) | InvalidOperationException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Parse_ExtraFields_ThrowsInvalidOperation` | levelId contains `=` separator (more than 3 fields) | InvalidOperationException (exactly key=levelId=syncProcess required) |
| `Parse_SyncFieldParsing_FollowsBoolTryParseRules` | syncProcess field as TRUE/true/False/not_bool | Parsed following bool.TryParse rules; non-boolean throws InvalidOperationException |
| `Join_EmptyEntries_ReturnsEmptyString` | Empty entry list | Returns empty string |
| `Parse_IgnoreEmptyEntries` | Consecutive commas between entries (empty entries) | Empty entries ignored |

## TopologyInvariantTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `EnsureActiveLevel_ValidTopology_DoesNotThrow` | Blackboard topology contains the expected levelId — does not throw; validation passes | TopologyInvariant |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `EnsureActiveLevel_MissingTopology_Throws` | No topology key in blackboard | InvalidOperationException |
| `EnsureActiveLevel_EmptyTopology_Throws` | Topology is empty string | InvalidOperationException |
| `EnsureActiveLevel_WhitespaceTopology_Throws` | Topology is whitespace | InvalidOperationException |
| `EnsureActiveLevel_MismatchedLevelId_Throws` | Foreground levelId in topology differs from expected | InvalidOperationException |
| `EnsureActiveLevel_NullBlackboard_Throws` | Blackboard is null | ArgumentNullException |
| `EnsureActiveLevel_EmptyExpectedLevelId_Throws` | Expected levelId is empty string | ArgumentException |
| `EnsureActiveLevel_CorruptedTopology_Throws` | Topology is an invalid format string | InvalidOperationException |

## BackgroundSessionTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `CreateBackgroundSession_ReturnsInitializedSession` | After creating background, LevelId/SessionBlackboard/StateMachines/SceneHost all initialized | ISessionManager |
| `SharedProgressBlackboard_ForegroundWriteVisibleToBackground` | Foreground writes to ProgressBlackboard visible to background | session-model |
| `SharedProgressBlackboard_BackgroundWriteVisibleToForeground` | Background writes to ProgressBlackboard visible to foreground | session-model |
| `SharedSndWorld_StrategiesFireInBackground` | Background entity strategy hooks fire | session-model |
| `SessionContext_OwningSession_CorrectlyBoundToBackgroundSession` | Strategy Process OwningSession.LevelId is background session levelId | session-model |
| `OwnSessionBlackboard_IsolatedFromForeground` | Background blackboard data not visible to foreground | session-model |
| `OwnEntities_IsolatedFromForeground` | Background entities not visible to foreground | session-model |
| `KillAllEntities_FireBeforeDead` | RequestKillEntity + KillPendingAllSessions triggers BeforeDead for all entities | ISessionRun |
| `Spawn_AddsEntity` | FullMemorySndSceneHost.CreateEntity adds entity and can find it | ISndSceneHost |
| `SpawnMany_AddsAll` | All multiple created entities exist | ISndSceneHost |
| `DeadByName_RemovesEntity_FiresBeforeDead` | RemoveEntity removes entity and triggers BeforeDead | ISndSceneHost |
| `Dispose_RemovesDirectHostEntities_FiresBeforeQuit` | Dispose triggers BeforeQuit and clears host entities | ISessionRun |
| `ProcessAll_FiresProcessOnEntities` | ProcessAllSessions triggers Process strategy | ISessionManager |
| `SerializeMetaList_ReturnsAllEntities` | BuildMetaList returns all entity metadata | ISndSceneHost |
| `PersistLevelState_WritesPayloadToFileSystem` | Level files exist after Save | session-model |
| `FullWorkflow_CreatePopulateTickSave` | Complete workflow: create→populate entities→Process→set blackboard→Save→verify files | session-model |
| `SerializeToPayload_ReturnsLevelPayload_WithCorrectLevelIdAndData` | Save output contains correct levelId and entity/blackboard data | session-model |
| `LoadFromPayload_RestoresSessionState` | After save, files exist and can be verified | session-model |
| `SerializeToPayload_ThenLoadFromPayload_RoundTrips` | Serialize→Deserialize: blackboard data and entities unchanged | session-model |
| `LoadFromPayload_Throws_WhenDisposed` | After Dispose, save does not produce level files | session-model |
| `SerializeToPayload_Throws_WhenDisposed` | After Dispose, save does not produce level files | session-model |
| `CreateBackgroundSession_ThenLoadSessionFromPayload_RestoresState` | After save, session contains entities and blackboard data | session-model |
| `FullMemorySndSceneHost_ProcessAll_FiresProcess` | FullMemorySndSceneHost ProcessAll triggers Process | ISndSceneHost |
| `FullMemorySndSceneHost_LoadFromMetaList_ClearsAndLoads` | RemoveAllEntities + RecoverFromMetaList + FireAfterLoadHooks | ISndSceneHost |
| `BuildSavePayload_IncludesBackgroundSessionsInPayload` | BuildSavePayload includes background level data | session-model |
| `SaveAndLoad_RoundTrips_BackgroundSessions` | Foreground/background save→Dispose→reload full round-trip | session-model |
| `BuildSavePayload_WithNoBackgroundSessions_ClearsBackgroundLevelIds` | Payload only contains foreground when no backgrounds | session-model |
| `BuildSavePayload_IncludesSyncProcessInBackgroundLevelIds` | syncProcess flag correct in Payload | session-model |
| `SaveAndLoad_RoundTrips_SyncProcessFlag` | syncProcess flag correctly restored after round-trip | session-model |
| `SaveAndLoad_FromDisk_RestoresBackgroundSessions` | Write to disk→read snapshot→load→background restored | session-model |
| `ReadFromCurrent_IncludesAllLevelDirectories` | ReadFromCurrent includes all level directories (including backgrounds) | ISaveStorageService |
| `FullSave_FiresBeforeSaveHooks_OnForegroundEntities` | Full save triggers BeforeSave hooks on foreground entities (regression: hooks used to be skipped) | session-model |
| `FullSave_BeforeSaveHookOverwritesSessionTopology_FrameworkValueWins` | When a BeforeSave hook overwrites SessionTopology, the framework-computed value wins and is persisted | session-model: Session Topology |
| `FullSave_BeforeSaveHookWrites_ArePersistedIntoForegroundSceneData` | Data written by BeforeSave hooks lands in foreground snd_scene.json | session-model |
| `SaveAndLoad_ReSolidifiesFullTopology_IncludingBackgroundSessions` | Save→full teardown→reload re-solidifies topology to the complete session set (foreground + background with syncProcess) | session-model: Session Topology |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `CreateBackgroundSession_Throws_WhenLevelIdInvalid` | null/empty/whitespace levelId | ArgumentException |
| `LoadSessionFromPayload_Throws_WhenPayloadNull` | Empty levelId | ArgumentException |
| `Dispose_ClearsEntities` | FindByName after Dispose | ObjectDisposedException |
| `DisposedSession_ThrowsOnAllPublicMethods` | SessionBlackboard/StateMachines/FindByName/GetEntities after Dispose | ObjectDisposedException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `FindByName_ReturnsNullWhenNotFound` | Find non-existent entity | Returns null |
| `Dispose_IsIdempotent` | Two Dispose calls | Does not throw |

## SessionRunHookIterationTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `LoadFromPayload_AfterLoadHookSpawnsEntity_DoesNotThrow` | Spawning inside the AfterLoad hook does not break batch iteration; after load both new and old entities exist | session-model: Hook Iteration |
| `BuildLevelPayload_BeforeSaveHookSpawnsEntity_DoesNotThrow` | Spawns an entity inside the BeforeSave hook via the public `RequestSaveGame` flow without breaking serialization; after the save round-trip both new and old entities exist | session-model: Hook Iteration |
| `Dispose_BeforeQuitHookSpawnsEntity_DoesNotThrowAndReleasesEverything` | Spawning inside the BeforeQuit hook does not break disposal; all entities released with no strategy pool reference leaks | session-model: Dispose Semantics |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Dispose_QuitHookSpawnsForever_FailsLoudlyInsteadOfHanging` | Quit hook spawns forever (does not converge) | InvalidOperationException (contains "did not converge"), no hang and no silent leak |

## SwitchForegroundCleanupTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SwitchForeground_RunsFullDisposalSemantics_ForOldForegroundEntities` | Switching runs full disposal semantics for old foreground entities: BeforeQuit fires, observer bindings torn down bidirectionally, no strategy pool leaks | session-model: Level Switch |
| `SwitchForeground_BackToPreviousLevel_RemountsObserverBindings` | Switching back to a previous level re-mounts persisted observer bindings (OnMounted fires) | session-model: Level Switch |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SwitchForeground_LoadFailure_LeavesNoHalfMountedForeground` | Target level snd_scene references an unregistered strategy, load fails | InvalidOperationException (contains "not found"), no half-mounted foreground left (ForegroundSession null), subsequent switch to a healthy level succeeds |

## BackgroundSession_CreationWithCorrectFlagTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GivenSessionManager_WhenCreateBackgroundSession_ThenIsFrontSessionIsFalse` | Background IsFrontSession is false | ISessionRun.IsFrontSession |
| `GivenSessionManager_WhenCreateBackgroundWithSync_ThenIsFrontSessionIsFalse` | Background with syncProcess=true still IsFrontSession=false | ISessionRun.IsFrontSession |

## BackgroundSession_MultipleInstancesAllowedTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GivenSessionManager_WhenCreateMultipleBackgroundSessions_ThenAllCreatedSuccessfully` | Creating 3 backgrounds simultaneously, all succeed | ISessionManager |
| `GivenSessionManager_WhenMultipleBackgroundSessionsExist_ThenForegroundStillIsFront` | Multiple backgrounds do not affect foreground IsFrontSession=true | ISessionRun.IsFrontSession |

## FrontSession_CreationWithCorrectFlagTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GivenSessionManager_WhenCreateForegroundSession_ThenIsFrontSessionIsTrue` | Foreground IsFrontSession is true | ISessionRun.IsFrontSession |
| `GivenSessionManager_WhenCreateForegroundFromPayload_ThenIsFrontSessionIsTrue` | After save, foreground IsFrontSession still true | ISessionRun.IsFrontSession |
| `GivenSessionManager_WhenSwitchForeground_ThenNewForegroundStillIsFrontSession` | After switch, new foreground IsFrontSession is true | ISessionRun.IsFrontSession |

## FrontSession_StrategyContextReceivesFrontFlagTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GivenGlobalSndContext_WhenForegroundMounted_ThenContextIsFrontSessionIsTrue` | After foreground mount, ForegroundSession.IsFrontSession is true | ISessionRun.IsFrontSession |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `GivenGlobalSndContext_WhenNoForeground_ThenContextIsFrontSessionIsFalse` | No foreground, ForegroundSession is null | Returns null |

## FrontSession_UniqueConstraintValidationTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GivenSessionManager_WhenCreateForegroundTwice_ThenOldForegroundReplaced` | Creating new foreground replaces old foreground | ISessionManager |
| `GivenSessionManager_WhenForegroundExists_ThenOnlyOneForegroundKey` | Only one __foreground__ key in Keys | ISessionManager.ForegroundKey |
| `GivenSessionManager_WhenForegroundAndBackgroundExist_ThenOnlyForegroundHasFlag` | Foreground/background coexist, only foreground IsFrontSession=true | ISessionRun.IsFrontSession |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `BeforeSaveSpyStrategy` | DisposeSemanticsTestInfrastructure.cs | Overrides BeforeSave hook, records calls via AsyncLocal<List<string>> |
| `BeforeQuitSpyStrategy` | DisposeSemanticsTestInfrastructure.cs | Overrides BeforeQuit hook, records calls via AsyncLocal<List<string>> |
| `SessionAccessQuitStrategy` | DisposeSemanticsTestInfrastructure.cs | Verifies SceneHost and SessionBlackboard are still accessible during BeforeQuit |
| `ThrowingQuitStrategy` | DisposeSemanticsTestInfrastructure.cs | Deliberately throws in BeforeQuit to verify exception safety |
| `PopHookThrowsPushStrategy` | DisposeSemanticsTestInfrastructure.cs | StateMachineStrategyBase: empty push strategy paired with the throwing pop strategy to construct a state machine |
| `PopHookThrowsPopStrategy` | DisposeSemanticsTestInfrastructure.cs | StateMachineStrategyBase: deliberately throws in OnPopBeforeQuit to verify Dispose exception safety (shared by progress- and session-level tests) |
| `ContractPushStrategy` | ForegroundBackgroundContractTests.cs | StateMachineStrategyBase: OnPushRuntime records BeforeTop→AfterTop events |
| `ContractPopStrategy` | ForegroundBackgroundContractTests.cs | Empty Pop strategy (placeholder only) |
| `TrackingStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase: records all hooks AfterSpawn/AfterLoad/AfterAdd/BeforeRemove/BeforeSave/BeforeQuit/BeforeDead |
| `ProcessCounterStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase: Process hook calls AsyncLocal<Action> |
| `SessionContextSpyStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase: Process hook records OwningSession.LevelId |
| `FindByNameStrategy` | SaveAndSwitchForegroundIntegrationTests.cs | LifecycleStrategyBase: Looks up self and sibling entities via OwningSession.FindByName during AfterSpawn/AfterLoad hooks |
| `BeforeSaveDataWriterStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase: Writes entity data in the BeforeSave hook, verifying hook writes reach the save file |
| `TopologyOverwriteStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase: Deliberately overwrites the framework-owned session-topology key in BeforeSave, verifying the framework re-solidifies topology |
| `BlackboardProbeStrategy` | SessionDecouplingTests.cs | StateMachineStrategyBase: OnPushRuntime reads marker key from SessionBlackboard |
| `SceneAccessProbeStrategy` | SessionDecouplingTests.cs | StateMachineStrategyBase: OnPushRuntime reads all entity names from SceneAccess |
| `NoOpPopStrategy` | SessionDecouplingTests.cs | Empty Pop strategy (placeholder, paired with BlackboardProbeStrategy/SceneAccessProbeStrategy) |
| `PrefixedSavePathPolicy` | SessionDecouplingTests.cs | ISavePathPolicy implementation: all path segments prefixed, verifying policy injection capability |
| `TrackingSaveStorageService` | SessionDecouplingTests.cs | ISaveStorageService decorator: records WriteLevelPayloadOnly call count and last Payload |
| `TickProbeStrategy` | PlayStopPlayRoundTripTests.cs | LifecycleStrategyBase: On Process, records OwningSession.LevelId to AsyncLocal collection for indirectly verifying syncProcess (which sessions are processed by ProcessAllSessions) |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| Automatic collision resolution when background session shares same levelId with foreground | SwitchForeground auto-saves and destroys background | session-model: levelId uniqueness constraint |
| Performance boundaries with large number of background sessions (100+) | Extreme concurrent session count | — |
| ProgressRun.LoadFromPayload handling of Payload.Levels being null | Defense against null Levels | session-model |
| Race condition between session double Dispose, ForegroundSession and external references | External ISessionRun reference used after Dispose | — |
| SessionTopologyCodec parsing of keys/levelIds containing commas | Special values with comma as separator | SessionTopologyCodec |

---

[↑ Back to Origo.Core.Tests](README.en.md)
