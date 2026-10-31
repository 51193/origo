# SND Context Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd](../Origo.Core/Snd/README.en.md)
> [↔ Behavior under test: usage/snd-entity-model](../usage/snd-entity-model.en.md)

## Behavior Under Test Overview

Validates the full workflows of SndContext as the central orchestrator of the SND system: save/load/continue operations, console command submission, template cloning, deferred action queues (including shutdown cancellation of queued persistence requests), payload mounts clearing adapter-scene remnants, NullSndContext no-op behavior, TestSupport LevelBuilder level construction, Archetype loading and attribute parsing, entry config startup flow, and template alias resolution and caching.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `SndContextWorkflowTests.cs` | SndContext save/load/continue/switch full-chain workflows |
| `SndContextShutdownFailureTests.cs` | Cleanup invariant when unloading the old ProgressRun throws: both the ProgressRun reference and the foreground session are cleared; failed `current/` cleanup remains retryable without replaying hooks; nested `Shutdown` from a quit hook cannot commit terminal state before the outer cleanup; a quit hook that queues a persistence request and then requests shutdown during workflow teardown has that queued work cancelled before the terminal flag; `Shutdown` requested from a load/save/level-switch workflow hook or from a `BeforeSave` hook is rejected without tearing the run down; a throwing deferred switch releases the lifecycle-workflow guard for later workflows |
| `SndContextShutdownGuardTests.cs` | Shutdown raised from a frame's session processing, kill sweep, or spawn is rejected before the progress run can empty the host; the scene stays intact, `BeforeQuit` does not run after `BeforeDead`, and an `AfterSpawn` shutdown rolls the entity back. The failed-shutdown path also rejects a later workflow until cleanup is retried |
| `SndContextEntryFlowTests.cs` | SndContext workflow starting from entry configuration |
| `SndContextBootstrapTests.cs` | Bootstrap startup flow: order of strategy discovery, ordering validation and registration freeze, alias/template loading, entry save loading, and configuration switches |
| `PersistenceRequestTrackingTests.cs` | Persistence requests (save/continue/initial/main menu entry/switch level) tracked as pending count until flushed; concurrent auto-save id allocation stays unique; shutdown cancels queued requests, runs their discard accounting, and stays retryable when a discard callback throws |
| `LevelBuilderExtendedTests.cs` | LevelBuilder building and writing level data |
| `SndArchetypeLoaderTests.cs` | SndArchetypeLoader.TryLoad parsing and ApplyAttributes type inference |
| `SndTemplateResolverTests.cs` | Template alias resolution, caching, clone does not affect cache |
| `TemplateAccessPublicPathTests.cs` | Public `ISndTemplateAccess` path: cloning templates then session spawn, runtime template/alias map loading, JSON entity-list template shorthand resolution |

## SndContextShutdownGuardTests Details

### Error Paths

| Test Method | Triggered Error | Expected Behavior |
|-------------|-----------------|-------------------|
| `DriveFrame_ShutdownFromProcess_IsRejectedWithoutCorruptingScene` | A strategy calls `ctx.Shutdown()` from `Process` during a real `DriveFrame` | `InvalidOperationException` naming session processing; the context and scene host remain live, and a later frame runs normally once the strategy stops requesting shutdown |
| `DriveFrame_ShutdownFromBeforeDead_IsRejectedAndBeforeQuitDoesNotRun` | A strategy calls `ctx.Shutdown()` from `BeforeDead` during the end-of-frame kill sweep | `InvalidOperationException` naming the kill sweep; the entity is still removed, `BeforeQuit` is not fired after `BeforeDead`, and the next frame runs normally |
| `Spawn_ShutdownFromAfterSpawn_IsRejectedAndEntityIsRolledBack` | A strategy calls `ctx.Shutdown()` from `AfterSpawn` during a direct `ISessionRun.Spawn` | `InvalidOperationException` naming spawning; the spawned entity is rolled back and the context stays live |

## SndContextWorkflowTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `ListSaves_ReturnsEmptyWhenNoSaves` | ListSaves returns empty when no saves exist | ISndSaveOperations |
| `ListSaves_ReturnsSaveIds` | ListSaves returns save IDs when saves exist | ISndSaveOperations |
| `ListSavesWithMetaData_ReturnsEmptyWhenNoSaves` | ListSavesWithMetaData returns empty when no saves exist | ISndSaveOperations |
| `DeleteSave_RemovesInactiveSlot` | After deleting an inactive slot ListSaves no longer returns it while other slots remain | ISndSaveOperations.DeleteSave |
| `DeleteSave_MainMenuProgressRun_DoesNotBlockMatchingRealSlot` | The main-menu ProgressRun uses an internal working SaveId that is not the active save; a real slot with the same id remains deletable | ISndSaveOperations.DeleteSave |
| `RequestSwitchForegroundLevel_MainMenuSyntheticSave_IgnoresRuntimeSave000Snapshot` | A synthetic initial/main-menu run switches from `current/` only; a user-created `save_000` level payload is never mounted | ISndSaveOperations.RequestSwitchForegroundLevel |
| `RequestSaveGame_PersistsAndSetsActiveSaveSlot` | After save the file exists and ActiveSaveId is correctly set | persistence-flow |
| `RequestSaveGame_IncrementsThenDecrementsPendingCount` | Save request increments then decrements pending count | ISndDeferredActions |
| `RequestSaveGameAuto_WithExplicitId_UsesIt` | RequestSaveGameAuto uses the provided ID | ISndSaveOperations |
| `RequestSaveGameAuto_WithNullId_GeneratesTimestamp` | Generates a timestamp when no ID is provided | ISndSaveOperations |
| `RequestLoadGame_LoadsSaveAndRestoresProgress` | After LoadGame, ProgressBlackboard and ForegroundSession are available | ISndSaveOperations |
| `RequestLoadGame_IncrementsThenDecrementsPendingCount` | Pending count changes for Load requests | ISndDeferredActions |
| `SetContinueTarget_MakesHasContinueDataTrue` | After setting Continue target, HasContinueData returns true | ISndLifecycleOperations |
| `RequestContinueGame_ReturnsTrueAndLoadsWhenContinueSet` | Continue correctly loads the save | ISndLifecycleOperations |
| `Shutdown_ClearsProgressAndCurrent_IsIdempotentAndRejectsBootstrap` | progress cleared, current/ removed, second Shutdown no-op, Bootstrap/persistence rejected | [ISndContext](../Origo.Core.Contracts/Abstractions/Snd/README.en.md) |
| `Shutdown_ConsoleClearFailure_RemainsRetryable` | first Shutdown throws on Clear failure; retry after recovery completes | [ISndContext](../Origo.Core.Contracts/Abstractions/Snd/README.en.md) |
| `Shutdown_Retry_ConsoleClearQueuesRequest_CancelsItBeforeCommittingTerminalState` | first Shutdown fails on console Clear; the retried Clear enqueues a save request, and the retry cancels it before committing the terminal flag: pending count 0, `IsPersistenceIdle` true, no save snapshot, and no deferred action throws on a later frame | [ISndContext](../Origo.Core.Contracts/Abstractions/Snd/README.en.md) |
| `ContinueLookup_LegacyMixedCaseActiveId_FindsRenamedLowercaseSlot` | case-insensitive lookup uses the storage slot ID | [session-model](../usage/session-model.en.md) |
| `DeleteSave_ActiveSaveMixedCase_Throws` | active-save protection is case-insensitive | [persistence-flow](../usage/persistence-flow.en.md) |
| `DeleteSave_LegacyCaseVariantSlot_ThrowsMigrationError` | ArgumentException with rename guidance; directory preserved | [persistence-flow](../usage/persistence-flow.en.md) |
| `RequestLoadAndContinue_LegacyCaseVariantSlot_ThrowMigrationError` | load/continue/save rejected before enqueue | [persistence-flow](../usage/persistence-flow.en.md) |
| `RequestLoadInitialSave_LoadsFromInitialRoot` | Loads initial save from the initial path | ISndLifecycleOperations |
| `RequestLoadInitialSave_PayloadMount_ClearsForeignAdapterEntities` | The full-memory adapter host holds a foreign entity before `RequestLoadInitialSave` mounts the restored payload foreground: the entity is gone from the mounted session and the adapter host, and the next snapshot's `snd_scene.json` does not contain it | ISndLifecycleOperations |
| `RequestLoadInitialSave_RestoresExtraFilesFromInitialRoot` | Initial-save extra/ files are restored from the initial storage root into runtime current/extra | ISndArchiveFileAccess |
| `RequestSwitchForegroundLevel_SwitchesLevel` | After level switch, ForegroundSession.LevelId is correct | ISndLifecycleOperations |
| `CloneTemplate_ClonesAndOverridesName` | Clones a template and overrides the name | ISndTemplateAccess |
| `CloneTemplate_WithoutOverrideName_KeepsOriginal` | Keeps the original name when not overriding | ISndTemplateAccess |
| `TrySubmitConsoleCommand_ReturnsTrueWhenConsoleInputExists` | Command submission succeeds when console input exists | ISndConsoleAccess |
| `DriveFrame_ProcessesQueuedConsoleCommands` | Submitted commands are processed at frame end by `IOrigoFrameDriver.DriveFrame` | ISndConsoleAccess / IOrigoFrameDriver |
| `SubscribeConsoleOutput_ReturnsPositiveId` | Subscribing returns a positive ID | ISndConsoleAccess |
| `UnsubscribeConsoleOutput_RemovesSubscription` | After unsubscribing, no more messages are received | ISndConsoleAccess |
| `EnqueueBusinessDeferred_ExecutesOnFlush` | Deferred action executes on Flush | ISndDeferredActions |
| `GetPendingPersistenceRequestCount_InitiallyZero` | Initial pending count is 0 | ISndDeferredActions |
| `GetProgressStateMachines_NullWhenNoProgress` | State machine container is null when no ProgressRun exists | ISndStateMachineAccess |
| `GetProgressStateMachines_NotNullAfterProgressRunCreated` | State machine container is available after ProgressRun is created | ISndStateMachineAccess |
| `RequestLoadInitialSave_WithCustomInitialLevelId_UsesConfiguredLevel` | InitialLevelId configuration takes effect: ForegroundSession.LevelId is the configured value after loading the initial save | ISndLifecycleOperations |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `RequestSaveGame_ThrowsOnEmptyId` | Empty saveId | ArgumentException |
| `RequestSaveGame_ThrowsOnNullId` | null saveId | ArgumentException |
| `RequestLoadGame_ThrowsOnEmptyId` | Empty saveId | ArgumentException |
| `RequestLoadGame_ThrowsOnNullId` | null saveId | ArgumentException |
| `RequestSwitchForegroundLevel_ThrowsOnEmptyId` | Empty levelId | ArgumentException |
| `DeleteSave_MissingSlot_Throws` | Deleting a missing slot | InvalidOperationException |
| `DeleteSave_ActiveSave_Throws` | Deleting the active slot referenced by ActiveSaveId | InvalidOperationException, slot remains |
| `DeleteSave_PendingPersistenceRequest_Throws` | Deleting a different slot while a persistence request is pending | InvalidOperationException |
| `TrySubmitConsoleCommand_ReturnsFalseForEmptyCommand` | Blank command | Returns false |
| `TrySubmitConsoleCommand_ReturnsFalseWhenNoConsoleInput` | No console input source | Returns false |
| `SubscribeConsoleOutput_ThrowsWhenNoChannel` | Subscribing when no output channel exists | InvalidOperationException |
| `RequestContinueGame_ReturnsFalseWhenNoContinue` | Continue target not set | Returns false |
| `HasContinueData_FalseWhenTargetSaveDoesNotExist` | Continue target points to a missing save slot | Returns false (the “save exists” contract must consult storage enumeration) |
| `RequestContinueGame_ReturnsFalseWhenTargetSaveDoesNotExist` | Continue target points to a missing save slot | Returns false and does not enqueue a load request |
| `Constructor_ThrowsOnNullRuntime` | null Runtime | ArgumentNullException |
| `Constructor_ThrowsOnNullFileSystem` | null FileSystem | ArgumentNullException |
| `Constructor_ThrowsOnEmptySaveRootPath` | Blank SaveRootPath | ArgumentException |
| `Constructor_ThrowsOnEmptyInitialSaveRootPath` | Blank InitialSaveRootPath | ArgumentException |
| `Constructor_ThrowsOnEmptyEntryConfigPath` | Blank EntryConfigPath | ArgumentException |
| `Constructor_ThrowsOnBlankInitialLevelId` | Blank InitialLevelId | ArgumentException |
| `Constructor_ThrowsOnInvalidInitialLevelId` | InitialLevelId contains a path separator | ArgumentException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `HasContinueData_FalseWhenNoTargetSet` | No Continue target set | Returns false |
| `InitialState_NoProgressBlackboard_NoForegroundSession` | Freshly created, no Progress and ForegroundSession is null | null |
| `RequestSaveGame_ConcurrentWorkflow_AllowsSequentialSavesInSingleFlush` | Multiple Saves in the same Flush | No exception thrown |
| `UnsubscribeConsoleOutput_NegativeId_DoesNotThrow` | Unsubscribing with a negative ID | No exception thrown |
| `UnsubscribeConsoleOutput_ZeroId_DoesNotThrow` | Unsubscribing with ID 0 | No exception thrown |

## PersistenceRequestTrackingTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `RequestSaveGame_IsTrackedUntilFlushed` | RequestSaveGame queues and the pending count is 1, returning to 0 after Flush | ISndDeferredActions |
| `RequestContinueGame_IsTrackedUntilFlushed` | A successful RequestContinueGame queues and the pending count is 1, returning to 0 after Flush | ISndDeferredActions |
| `RequestLoadInitialSave_IsTrackedUntilFlushed` | RequestLoadInitialSave queues and the pending count is 1 (tracking semantics only) | ISndDeferredActions |
| `RequestLoadMainMenuEntrySave_IsTrackedUntilFlushed` | RequestLoadMainMenuEntrySave queues and the pending count is 1, returning to 0 after Flush | ISndDeferredActions |
| `RequestSwitchForegroundLevel_IsTrackedUntilFlushed` | RequestSwitchForegroundLevel queues and the pending count is 1, returning to 0 after Flush | ISndDeferredActions |
| `FailedTrackedRequest_DiscardingLaterTrackedRequest_ReturnsPendingCountToZero` | An earlier persistence request fails and a later request in the same batch is discarded by fail-fast; the pending count returns to 0 | ISndDeferredActions |
| `IsPersistenceIdle_ReflectsPendingRequestTracking` | Idle state matches the pending count initially, around entry loading, and before/after a save request | ISndDeferredActions |
| `Shutdown_CancelsQueuedPersistenceRequests_AndRunsDiscardCallbacks` | A save and a system action with a discard callback are queued, then `Shutdown` runs before any frame: the queued action's `onDiscard` ran, the pending count is back to 0, `IsPersistenceIdle` is true, no snapshot was written, and a later frame drive does not throw from the cancelled queue | ISndDeferredActions / ISndContext |
| `Shutdown_WhenDiscardCallbackThrows_ClearsQueueAndRemainsRetryable` | A queued action's discard callback throws during `Shutdown` | The first `Shutdown` throws that callback failure; the queue is already cleared, and the retry commits the terminal state without replaying the action |
| `RequestSaveGameAuto_ConcurrentCalls_ProduceUniqueIds` | 8 threads enqueue two auto-save requests each after a common start signal, then one sequential allocation follows: all 16 concurrent ids are distinct, the later sequential allocation is greater than every concurrent id (atomic monotonic counter), and the test drains the queued requests leaving the pending count at 0 | ISndSaveOperations |

## SndTemplateResolverTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Resolve_WhenCalledTwice_UsesCacheAndAvoidsSecondRead` | Second Resolve uses cache, no repeated file read | SndTemplateResolver |
| `Resolve_CacheThenClone_CloneDoesNotAffectCache` | DeepClone does not pollute the cache | SndTemplateResolver |
| `Resolve_TemplateFile_EmptyObject_ReturnsMinimalMetaData` | Empty JSON → MetaData with Name as empty string | — |
| `Resolve_TemplateFile_MissingNameField_ReturnsEmptyName` | No name field → MetaData with Name as empty string | — |
| `Resolve_MapFileComments_Skipped` | Template files parse correctly, name returned correctly | SndTemplateResolver |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Resolve_MissingAlias_ThrowsKeyNotFoundException` | Non-existent alias | KeyNotFoundException |
| `Resolve_WhitespaceAlias_ThrowsArgumentException` | Whitespace alias | ArgumentException |
| `Resolve_InvalidJson_Throws` | Invalid JSON template file | Exception |
| `Resolve_ConverterReturnsNull_ThrowsInvalidOperationException` | Converter returns null | InvalidOperationException (contains "deserialized to null") |

## TemplateAccessPublicPathTests Details

### Correct Path

| Test Method | Verified Behavior | Doc Source |
|---------|-----------|---------|
| `CloneTemplate_AndSessionSpawn_CreateTemplateEntity` | `ctx.Template.CloneTemplate` clones a template and `ISessionRun.Spawn` creates the entity | ISndTemplateAccess / ISessionRun |
| `LoadTemplates_AndLoadMetaListFromFile_ResolveTemplateShorthand` | Loads a template map at runtime, resolves `templateKey` / `sndName` shorthand from a JSON entity list, then spawns via `SpawnMany` | ISndTemplateAccess / ISessionRun |
| `LoadSceneAliases_LoadsAliasMap_ThroughPublicTemplateAccess` | Reloads an alias map via `ctx.Template.LoadSceneAliases` and the runtime mapping resolves the new alias | ISndTemplateAccess |

## NullSndContext (Test Infrastructure)

`NullSndContext` lives in the test project (`Origo.Core.Tests/TestSupport/`) and is used as a test utility class.

## LevelBuilderExtendedTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Build_ProducesLevelPayload` | Build() produces a valid payload containing LevelId, SndSceneNode, SessionNode, SessionStateMachinesNode | LevelBuilder |
| `Commit_WritesToFileSystem` | Commit() writes the payload to the filesystem at `root/current/level_lvl1/snd_scene.json` | LevelBuilder |
| `AddEntities_BatchAdd` | AddEntities batch-adds 3 entities, SceneHost entity count is 3 | LevelBuilder |
| `AddEntityFromTemplate_ClonesAndAdds` | AddEntityFromTemplate clones a template; the entity is findable via SceneHost.FindByName | LevelBuilder |
| `SessionBlackboard_IsAccessible` | After SetSessionData, SessionBlackboard.TryGet can read it back | SessionBlackboard |
| `LevelId_ExposesConstructedValue` | The LevelId passed at construction is exposed via the LevelId property | LevelBuilder |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Build_ThenModify_Throws` | Calling AddEntity / SetSessionData / Build after Build | InvalidOperationException |
| `AddEntity_DuplicateName_Throws` | Duplicate name with AddEntity | InvalidOperationException |
| `AddEntity_NullMeta_Throws` | null SndMetaData | ArgumentNullException |
| `AddEntity_EmptyName_Throws` | SndMetaData with empty Name | ArgumentException |
| `AddEntities_NullList_Throws` | null entity list | ArgumentNullException |
| `AddEntityFromTemplate_EmptyKey_Throws` | Empty template key | ArgumentException |
| `Constructor_EmptyLevelId_Throws` | Empty levelId | ArgumentException |
| `Constructor_NullSndWorld_Throws` | null SndWorld | ArgumentNullException |
| `Constructor_NullStorage_Throws` | null ISaveStorageService | ArgumentNullException |

## SndContextEntryFlowTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `RequestLoadMainMenuEntrySave_MountsForegroundAndSpawnsEntryEntities` | After loading the entry save, ProgressBlackboard is non-null, ForegroundSession is non-null, and entry entities are findable in the host | ISndLifecycleOperations |
| `RequestLoadMainMenuEntrySave_ClearsPreviousForegroundEntities` | Entities leftover from before loading the entry save are cleared after load | ISndLifecycleOperations |

## SndContextBootstrapTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Bootstrap_CompletesWithoutError` | Bootstrap executes fully without exception when entry.json is present | ISndContext.Bootstrap |
| `Bootstrap_AfterCall_ForegroundSessionIsEstablished` | After Bootstrap + deferred flush, a foreground session is mounted | ISndContext.Bootstrap |
| `Bootstrap_CompletionState_TransitionsAfterEntryLoad` | Before entry loading `IsBootstrapCompleted=false` and `IsPersistenceIdle=false`; after Flush both are true and pending returns to 0 | ISndLifecycleOperations.IsBootstrapCompleted / ISndDeferredActions.IsPersistenceIdle |
| `Bootstrap_WithConfigureConverters_CallbackIsInvoked` | ConfigureConverters callback invoked before strategy discovery | ISndContext.Bootstrap |
| `Bootstrap_AutoDiscoverDisabled_SkipsStrategyDiscovery` | AutoDiscoverStrategies=false skips the strategy scan | SndContextParameters.AutoDiscoverStrategies |
| `Bootstrap_WithTemplates_LoadsAndAllowsCloning` | CloneTemplate works after configuring a template path | SndWorld.LoadTemplates |
| `IStateMachineContext_SceneAccess_AfterBootstrap_NotNull` | State machine context SceneAccess available after Bootstrap | IStateMachineContext |
| `IStateMachineContext_SystemBlackboard_AfterBootstrap_NotNull` | System blackboard accessible after Bootstrap | IStateMachineContext |
| `IStateMachineContext_ProgressBlackboard_AfterBootstrap_NotNull` | Progress blackboard accessible after Bootstrap | IStateMachineContext |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Bootstrap_WithoutEntryJson_ThrowsOnFlush` | entry.json missing | Deferred flush throws (fail-fast) |
| `Bootstrap_EntryLoadFailure_LeavesCompletionFalseAndPersistenceIdle` | Entry load fails | After the exception propagates, `IsBootstrapCompleted=false`, pending returns to 0, and `IsPersistenceIdle=true` |
| `Bootstrap_Twice_Throws` | Calling Bootstrap twice | InvalidOperationException |
| `Bootstrap_WhenSceneHostTopologyUnbound_Throws` | Bootstrap when the scene host topology is not bound to a context | InvalidOperationException (message contains "not bound to a context") |
| `Bootstrap_AfterUnboundHostFailure_CannotBeRetried` | First Bootstrap attempt fails (host not bound) and the context is then bound | The single-use guard is already committed: retry throws InvalidOperationException (message contains "already been executed") |
| `Bootstrap_InvalidStrategyOrdering_ThrowsDuringSeal` | A registered lifecycle strategy references an unregistered Before target | Bootstrap throws InvalidOperationException while sealing (message contains the missing target index) |
| `CloneTemplate_NullKey_ThrowsArgumentException` | CloneTemplate with null key | ArgumentException |
| `CloneTemplate_WhitespaceKey_ThrowsArgumentException` | CloneTemplate with whitespace key | ArgumentException |
| `CloneTemplate_NonExistingKey_Throws` | CloneTemplate with a non-existent template alias | InvalidOperationException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SaveRootPath_ReturnsConstructorValue` | Constructor parameter | Returns the save root path passed to the constructor |
| `InitialSaveRootPath_ReturnsConstructorValue` | Constructor parameter | Returns the initial save root path |
| `EntryConfigPath_ReturnsConstructorValue` | Constructor parameter | Returns the entry config path |

## SndContextShutdownFailureTests Details

### Error Paths

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `Workflow_WhenOldProgressDisposeThrows_ClearsProgressRunReference` | The old ProgressRun's session state machine throws from its quit-time pop hook during unload | The original exception propagates; `ForegroundSession` is null; the `_progressRun` reference is cleared (internal invariant asserted via InternalsVisibleTo) |
| `Shutdown_WhenCurrentDeleteFails_RetriesCleanupWithoutReplayingHooks` | `current/` deletion fails during shutdown | The first call throws; the progress run is gone, the retry deletes `current/`, and the context becomes terminal without replaying quit hooks |
| `Shutdown_ReenteredFromQuitHook_DoesNotCommitTerminalStateBeforeOuterCleanup` | A quit-hook strategy calls `Shutdown` while the outer shutdown is disposing the progress run, and `current/` deletion fails | The nested call is a no-op; the outer failure keeps the retry record and the next `Shutdown` deletes `current/` |
| `Shutdown_DuringSwitchTeardown_ThrowsBeforeTouchingProgress` | `Shutdown` is requested while the level-switch foreground teardown guard is active | `InvalidOperationException` before any progress cleanup; the context is not torn down |
| `Workflow_ShutdownFromQuitHook_WhenCurrentDeleteFails_RemainsRetryable` | A main-menu reload disposes the old progress run, its BeforeQuit hook requests shutdown, and `current/` deletion fails | The workflow throws, `current/` remains, and a later `Dispose`/`Shutdown` retries the delete without replaying hooks |
| `Workflow_QuitHookQueuesRequestThenShutdown_CancelsQueuedWorkBeforeTerminalState` | A quit hook queues `RequestSaveGameAuto` and then requests shutdown while the workflow tears the old progress run down | The workflow cancels the queued request before committing shutdown: pending count 0, `IsPersistenceIdle` true, no save snapshot is written, the following frame does not throw, and persistence mutations are rejected as terminal |
| `Shutdown_FromAfterSpawnHookDuringWorkflow_IsRejectedAndDoesNotTearDownWorkflow` | An entry-workflow AfterSpawn hook requests `Shutdown` while the lifecycle workflow body owns the new ProgressRun | `InvalidOperationException` naming the in-progress lifecycle workflow; the workflow aborts and cleans up, the context does not commit shutdown (persistence mutations still accepted), and a later `Shutdown` is clean |
| `Shutdown_FromBeforeSaveHookDuringSaveWorkflow_IsRejectedAndSaveIsNotWritten` | A `BeforeSave` hook requests `Shutdown` during `RequestSaveGame` | `InvalidOperationException` naming the BeforeSave hook; no snapshot is written, the foreground session and its entity survive, and a later `Shutdown` is clean |
| `Shutdown_FromSwitchAfterLoadHook_IsRejectedAndSwitchCompletes` | An `AfterLoad` hook of the level being mounted requests `Shutdown` during `RequestSwitchForegroundLevel` (and catches the rejection) | `InvalidOperationException` naming the in-progress lifecycle workflow; the switch completes, the foreground is the requested level with its entity, the context stays live, and a later `Shutdown` is clean |
| `Switch_WhenDeferredActionThrows_ReleasesWorkflowGuardForLaterWorkflows` | A deferred level switch fails while loading the target level (scene references an unregistered strategy) | The switch failure propagates without an "already in progress" error; a subsequent switch, save, and clean `Shutdown` all succeed because the workflow guard was released on the failure path |

## SndArchetypeLoaderTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `TryLoad_ValidMapFile_ReturnsAttributes` | Valid map file parsing returns 4 attributes with correct key/value pairs | SndArchetypeLoader.TryLoad |
| `TryLoad_DisposesReturnedSourceNode` | TryLoad disposes the DataSourceNode from ISndFileAccess after copying attributes | SndArchetypeLoader.TryLoad |
| `ApplyAttributes_IntString_StoresAsInt` | Integer string "100" stored as int(100) | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_LargeIntegerString_StoresAsLong` | Large integer string exceeding int.MaxValue stored as long, not float | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_FloatString_StoresAsFloat` | Float string "3.14" stored as float(3.14f) | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_BoolString_StoresAsBool` | "true" stored as bool(true) | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_PlainString_StoresAsString` | Plain string "hero" stored as string | SndArchetypeLoader.ApplyAttributes |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `TryLoad_FileNotExists_ReturnsFalse` | File does not exist | Returns false, attrs is empty |
| `TryLoad_EmptyObject_ReturnsFalse` | Empty JSON object {} | Returns false, attrs is empty |
| `TryLoad_NonObjectNode_ReturnsFalse` | JSON value is string, not object | Returns false, attrs is empty |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| `NullMetaConverter` | SndTemplateResolverTests.cs | Converter that returns null, for verifying null detection |
| `ShutdownOnProcessStrategy` | SndContextShutdownGuardTests.cs | Calls `Shutdown` from `Process` once armed, for the frame-iteration guard |
| `ShutdownOnBeforeDeadStrategy` | SndContextShutdownGuardTests.cs | Calls `Shutdown` from `BeforeDead` and records `BeforeQuit` ordering |
| `ShutdownOnAfterSpawnStrategy` | SndContextShutdownGuardTests.cs | Calls `Shutdown` from `AfterSpawn`, for the spawn-rollback guard |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| RequestSaveGame behavior when no ProgressRun exists | How Save should be handled without a ProgressRun set | ISndSaveOperations |
| CloneTemplate behavior with empty overrideName | Empty name override | ISndTemplateAccess |

---

[↑ Back to Origo.Core.Tests](README.en.md)
