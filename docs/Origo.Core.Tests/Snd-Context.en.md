<!-- docsync-pair: Origo.Core.Tests/Snd-Context -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# SND Context Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd](../Origo.Core/Snd/README.en.md)
> [↔ Behavior under test: usage/snd-entity-model](../usage/snd-entity-model.en.md)

## Behavior Under Test Overview

Validates the full workflows of SndContext as the central orchestrator of the SND system: save/load/continue operations, console command submission, template cloning, deferred action queues, NullSndContext no-op behavior, LevelBuilder level construction, Archetype loading and attribute parsing, entry config startup flow, and template alias resolution and caching.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `SndContextWorkflowTests.cs` | SndContext save/load/continue/switch full-chain workflows |
| `SndContextEntryFlowTests.cs` | SndContext workflow starting from entry configuration |
| `LevelBuilderExtendedTests.cs` | LevelBuilder building and writing level data |
| `SndArchetypeLoaderTests.cs` | SndArchetypeLoader.TryLoad parsing and ApplyAttributes type inference |
| `SndTemplateResolverTests.cs` | Template alias resolution, caching, clone does not affect cache |

## SndContextWorkflowTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `ListSaves_ReturnsEmptyWhenNoSaves` | ListSaves returns empty when no saves exist | ISndSaveOperations |
| `ListSaves_ReturnsSaveIds` | ListSaves returns save IDs when saves exist | ISndSaveOperations |
| `RequestSaveGame_PersistsAndSetsActiveSaveSlot` | After save the file exists and ActiveSaveId is correctly set | persistence-flow |
| `RequestSaveGame_IncrementsThenDecrementsPendingCount` | Save request increments then decrements pending count | ISndDeferredActions |
| `RequestSaveGameAuto_WithExplicitId_UsesIt` | RequestSaveGameAuto uses the provided ID | ISndSaveOperations |
| `RequestSaveGameAuto_WithNullId_GeneratesTimestamp` | Generates a timestamp when no ID is provided | ISndSaveOperations |
| `RequestLoadGame_LoadsSaveAndRestoresProgress` | After LoadGame, ProgressBlackboard and ForegroundSession are available | ISndSaveOperations |
| `RequestLoadGame_IncrementsThenDecrementsPendingCount` | Pending count changes for Load requests | ISndDeferredActions |
| `SetContinueTarget_MakesHasContinueDataTrue` | After setting Continue target, HasContinueData returns true | ISndLifecycleOperations |
| `RequestContinueGame_ReturnsTrueAndLoadsWhenContinueSet` | Continue correctly loads the save | ISndLifecycleOperations |
| `RequestLoadInitialSave_LoadsFromInitialRoot` | Loads initial save from the initial path | ISndLifecycleOperations |
| `RequestSwitchForegroundLevel_SwitchesLevel` | After level switch, ForegroundSession.LevelId is correct | ISndLifecycleOperations |
| `CloneTemplate_ClonesAndOverridesName` | Clones a template and overrides the name | ISndTemplateAccess |
| `CloneTemplate_WithoutOverrideName_KeepsOriginal` | Keeps the original name when not overriding | ISndTemplateAccess |
| `TrySubmitConsoleCommand_ReturnsTrueWhenConsoleInputExists` | Command submission succeeds when console input exists | ISndConsoleAccess |
| `ProcessConsolePending_ProcessesQueuedCommands` | ProcessConsolePending processes queued commands | ISndConsoleAccess |
| `SubscribeConsoleOutput_ReturnsPositiveId` | Subscribing returns a positive ID | ISndConsoleAccess |
| `UnsubscribeConsoleOutput_RemovesSubscription` | After unsubscribing, no more messages are received | ISndConsoleAccess |
| `EnqueueBusinessDeferred_ExecutesOnFlush` | Deferred action executes on Flush | ISndDeferredActions |
| `GetPendingPersistenceRequestCount_InitiallyZero` | Initial pending count is 0 | ISndDeferredActions |
| `GetProgressStateMachines_NullWhenNoProgress` | State machine container is null when no ProgressRun exists | ISndStateMachineAccess |
| `GetProgressStateMachines_NotNullAfterProgressRunCreated` | State machine container is available after ProgressRun is created | ISndStateMachineAccess |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `RequestSaveGame_ThrowsOnEmptyId` | Empty saveId | ArgumentException |
| `RequestSaveGame_ThrowsOnNullId` | null saveId | ArgumentException |
| `RequestLoadGame_ThrowsOnEmptyId` | Empty saveId | ArgumentException |
| `RequestLoadGame_ThrowsOnNullId` | null saveId | ArgumentException |
| `RequestSwitchForegroundLevel_ThrowsOnEmptyId` | Empty levelId | ArgumentException |
| `TrySubmitConsoleCommand_ReturnsFalseForEmptyCommand` | Blank command | Returns false |
| `TrySubmitConsoleCommand_ReturnsFalseWhenNoConsoleInput` | No console input source | Returns false |
| `SubscribeConsoleOutput_ThrowsWhenNoChannel` | Subscribing when no output channel exists | InvalidOperationException |
| `RequestContinueGame_ReturnsFalseWhenNoContinue` | Continue target not set | Returns false |
| `Constructor_ThrowsOnNullRuntime` | null Runtime | ArgumentNullException |
| `Constructor_ThrowsOnNullFileSystem` | null FileSystem | ArgumentNullException |
| `Constructor_ThrowsOnEmptySaveRootPath` | Blank SaveRootPath | ArgumentException |
| `Constructor_ThrowsOnEmptyInitialSaveRootPath` | Blank InitialSaveRootPath | ArgumentException |
| `Constructor_ThrowsOnEmptyEntryConfigPath` | Blank EntryConfigPath | ArgumentException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `HasContinueData_FalseWhenNoTargetSet` | No Continue target set | Returns false |
| `InitialState_NoProgressBlackboard_NoForegroundSession` | Freshly created, no Progress and ForegroundSession is null | null |
| `RequestSaveGame_ConcurrentWorkflow_AllowsSequentialSavesInSingleFlush` | Multiple Saves in the same Flush | No exception thrown |

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

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SaveRootPath_ReturnsConstructorValue` | Constructor parameter | Returns the save root path passed to the constructor |
| `InitialSaveRootPath_ReturnsConstructorValue` | Constructor parameter | Returns the initial save root path |
| `EntryConfigPath_ReturnsConstructorValue` | Constructor parameter | Returns the entry config path |

## SndArchetypeLoaderTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `TryLoad_ValidMapFile_ReturnsAttributes` | Valid map file parsing returns 4 attributes with correct key/value pairs | SndArchetypeLoader.TryLoad |
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

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| RequestSaveGame behavior when no ProgressRun exists | How Save should be handled without a ProgressRun set | ISndSaveOperations |
| SndContext concurrent FlushDeferredActions calls | Thread safety of multi-threaded Flush | — |
| CloneTemplate behavior with empty overrideName | Empty name override | ISndTemplateAccess |

---

[↑ Back to Origo.Core.Tests](README.en.md)
