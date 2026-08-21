<!-- docsync-pair: Origo.Core.Tests/Snd-Entity -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# SND Entity Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd/Entity](../Origo.Core/Snd/Entity/README.en.md)
> [↔ Behavior under test: usage/snd-entity-model](../usage/snd-entity-model.en.md)

## Behavior Under Test Overview

Validates the full behavior of SND entities: StubSndEntity data CRUD, AfterLoad hook trigger timing and ordering, AutoInitializer strategy/data recovery, batch lifecycle orchestration (AfterLoad/AfterSpawn/BeforeSave/BeforeQuit/BeforeDead), entity-to-OwningSession binding, SndEntityFactory spawn orchestration, and ProcessAll frame processing behavior.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `MemorySndEntityTests.cs` | SndEntity SetData/GetData/TryGetData/data isolation |
| `SndEntityAfterLoadTests.cs` | AfterLoad hook trigger ordering and error propagation |
| `SndEntityAndAutoInitializerTests.cs` | AutoInitializer recovering strategy and data from metadata; SndEntity AddStrategy/RemoveStrategy index updates |
| `SndEntityLifecycleBatchTests.cs` | Batch lifecycle orchestration: all hook stages, cross-entity lookup, priority, SndEntityFactory/Spawn, ProcessAll frame processing |
| `SndEntityOwningSessionTests.cs` | Entity OwningSession binding and unbinding |
| `SndDataManagerFailureTests.cs` | SndDataManager.SetData leaves no dictionary entry when the converter throws (prevents leaking into saves) |
| `SndEntityRecoveryRollbackTests.cs` | Verifies cross-stage rollback of RecoverForLifecycle (active phase failure releases previously acquired passive strategies; node phase failure releases created nodes and does not release unacquired indices) |
| `SndNodeManagerTests.cs` | SndNodeManager guard contract: null SetSceneAliasResolver parameter fails fast |

## MemorySndEntityTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Name_ReturnsConstructedName` | Name specified at construction is accessible via the Name property | ISndEntity |
| `SetData_GetData_RoundTrip` | SetData/GetData round-trip preserves value consistency | snd-entity-model: TypedData |
| `TryGetData_ReturnsTrueWhenFound` | TryGetData returns true and the value when the key exists | snd-entity-model: TypedData |
| `TryGetDataOut_ReturnsTrueAndValueWhenFound` | The out overload of TryGetData returns true and the value when the key exists | snd-entity-model: TypedData |
| `InitialNameData_IsSetInDictionary` | Name is auto-stored into the "name" data entry at construction | ISndEntity |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Constructor_ThrowsOnNullName` | null name parameter | ArgumentNullException |
| `GetData_ThrowsInvalidOperation_WhenMissing` | Non-existent key | InvalidOperationException |
| `GetData_ThrowsInvalidOperation_OnTypeMismatch` | Type mismatch | InvalidOperationException |
| `GetNode_ThrowsInvalidOperation` | Stub entity does not support node operations | InvalidOperationException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `TryGetData_ReturnsFalseWhenMissing` | Key does not exist | Returns false |
| `TryGetData_ReturnsFalseForTypeMismatch` | Type mismatch | Returns false, no exception |
| `TryGetDataOut_ReturnsFalseAndDefaultWhenMissing` | Out overload, key does not exist | Returns false, out value is default (0) |
| `TryGetDataOut_ReturnsFalseForTypeMismatch` | Out overload, type mismatch | Returns false, out value is default, no exception |
| `GetNodeNames_ReturnsEmpty` | Entity without nodes | Returns empty collection |
| `AddRemoveStrategy_DoesNotThrow` | AddStrategy/RemoveStrategy on Stub entity | No-op, no exception, existing data unaffected |

## SndEntityAfterLoadTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `SndEntity_Load_FromJson_InvokesAfterLoad_ForAllStrategies_InIndexOrder` | AfterLoad hooks trigger all strategies in metadata index order | snd-entity-model: Lifecycle hooks |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `AfterLoad_ThrowingStrategy_HookExceptionPropagates` | Strategy AfterLoad throws InvalidOperationException | InvalidOperationException propagates |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `AfterLoad_EmptyIndices_NoThrow` | Empty lifecycle_indices | No exception; entity is usable |

## SndEntityAndAutoInitializerTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `SndEntity_GetNodeNamesAndGetNode_ReturnExpectedHandles` | After Spawn, GetNodeNames/GetNode return node handles defined in metadata | ISndEntity |
| `SndEntity_AddRemoveStrategy_UpdatesExportedIndices` | AddStrategy/RemoveStrategy correctly updates exported lifecycle_indices; duplicate RemoveStrategy does not throw | ISndEntity |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_LoadsInlineMetaArray` | Load and batch spawn entities from a JSON array file | Snd/Entity: AutoInitializer |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `SndEntity_GetData_MissingKey_ThrowsInvalidOperation` | GetData on a non-existent key | InvalidOperationException |
| `SetData_NullOrWhitespaceName_ThrowsArgumentException` | Data key is null/empty/whitespace | null → ArgumentNullException; empty/whitespace → ArgumentException |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyPath_Throws` | Blank path string | ArgumentException, error logged |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_MissingFile_Throws` | File does not exist | InvalidOperationException |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyFile_Throws` | File content is empty/whitespace | Throws exception |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_NotArrayRoot_Throws` | JSON root node is not an array | InvalidOperationException |

## SndEntityLifecycleBatchTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `BatchLoad_AfterLoad_FiresAfterAllEntitiesRecovered` | AfterLoad hooks fire uniformly after all entities' RecoverFromMetaList completes | snd-entity-model: Batch lifecycle |
| `BatchLoad_CrossEntity_FindByName_SucceedsRegardlessOfOrder` | During AfterLoad, FindByName can find all entities regardless of recovery order | snd-entity-model: Batch lifecycle |
| `BatchLoad_Self_ActiveStrategyAvailableDuringAfterLoad` | During AfterLoad, own ActiveStrategy is available | snd-entity-model: Batch lifecycle |
| `BatchLoad_CrossEntity_ActiveStrategyAvailableDuringAfterLoad` | During AfterLoad, InvokeStrategy can call another entity's ActiveStrategy | snd-entity-model: Batch lifecycle |
| `BatchLoad_CrossEntity_SubscribeDuringAfterLoad` | During AfterLoad, cross-entity data observation subscriptions work | snd-entity-model: Batch lifecycle |
| `SpawnMany_AfterSpawn_FiresOnAllEntities` | AfterSpawn hooks fire after all entities are created | snd-entity-model: Batch lifecycle |
| `SpawnMany_CrossEntity_ActiveStrategyAvailableDuringAfterSpawn` | During AfterSpawn, ActiveStrategy can be cross-invoked between entities | snd-entity-model: Batch lifecycle |
| `BatchSave_BeforeSave_FiresBeforeAnySerialization` | BeforeSave hooks fire before BuildMetaList | snd-entity-model: Batch lifecycle |
| `BatchQuit_BeforeQuit_FiresBeforeAnyTeardown` | BeforeQuit fires before teardown, then ReleaseStrategiesOnly + RemoveAllEntities | snd-entity-model: Batch lifecycle |
| `BatchQuit_LifoOrder_Preserved` | BeforeQuit fires in LIFO order | snd-entity-model: Batch lifecycle |
| `BatchQuit_CrossEntity_FindByNameSucceedsDuringBeforeQuit` | During BeforeQuit, FindByName can still find other entities | snd-entity-model: Batch lifecycle |
| `BatchDead_BeforeDead_FiresBeforeAnyTeardown` | BeforeDead fires before RemoveEntity | snd-entity-model: Batch lifecycle |
| `BatchDead_CrossEntity_FindByNameSucceedsDuringBeforeDead` | During BeforeDead, FindByName can still find other entities | snd-entity-model: Batch lifecycle |
| `BatchLoad_StrategyPriorityWithinEntity_Preserved` | Multiple strategies on the same entity are sorted by Priority (lower first) | snd-entity-model: Strategy priority |
| `BatchLoad_SingleEntity_BehaviorCorrect` | Single-entity batch recovery correctly triggers AfterLoad | snd-entity-model: Batch lifecycle |
| `Spawn_ActiveStrategyAvailableDuringAfterSpawn` | After single-entity Spawn, ActiveStrategy is available during AfterSpawn | snd-entity-model: Batch lifecycle |
| `Load_ActiveStrategyAvailableDuringAfterLoad` | After single-entity Load, ActiveStrategy is available during AfterLoad | snd-entity-model: Batch lifecycle |
| `SndEntityFactory_SpawnMany_TriggersAfterSpawnAfterAllCreated` | SpawnMany triggers AfterSpawn uniformly after all entities are created | SndEntityFactory |
| `SndEntityFactory_Spawn_CallsCreateEntityThenFiresAfterSpawn` | Spawn calls CreateEntity first, then fires AfterSpawn | SndEntityFactory |
| `SndEntityFactory_SpawnMany_EntitiesVisibleInAfterSpawn` | During SpawnMany's AfterSpawn hooks, all entities are visible | SndEntityFactory |
| `ProcessAll_SingleEntity_CallsProcessOnStrategy` | ProcessAll calls strategy Process; delta propagates correctly | snd-entity-model: Frame processing |
| `ProcessAll_MultipleEntities_AllProcessed` | Multi-entity frame processing executes for all | snd-entity-model: Frame processing |
| `ProcessAll_DeltaPropagatesToStrategy` | ProcessAll's delta parameter is correctly passed to strategy Process | snd-entity-model: Frame processing |
| `ProcessAll_ProcessAddsStrategy_NewStrategyNotExecutedThisFrame` | Strategy added via AddStrategy during Process is not executed this frame | snd-entity-model: Frame processing |
| `ProcessAll_ProcessRemovesStrategy_RemainingStrategiesStillExecuted` | After RemoveStrategy during Process, subsequent strategies still execute normally | snd-entity-model: Frame processing |
| `SndEntityFactory_Spawn_CreatesEntityAndFiresAfterSpawn` | Spawn creates entity and fires AfterSpawn hook | SndEntityFactory |
| `SndEntityFactory_SpawnMany_BatchCreatesAllThenFiresHooks` | SpawnMany creates all then fires hooks one by one | SndEntityFactory |
| `SndEntityFactory_SpawnMany_EntitiesVisibleDuringAfterSpawn` | During SpawnMany AfterSpawn, cross-entity FindByName is visible | SndEntityFactory |
| `FullMemorySndSceneHost_RemoveEntity_ClearsCollectionOnly` | RemoveEntity only removes from collection; duplicate Remove throws | ISndSceneHost |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `BatchLoad_HookThrows_PropagatesException` | AfterLoad hook throws InvalidOperationException | Exception propagates (entity cleanup is rolled back by the scene host) |
| `SetData_WithNullValue_ThrowsArgumentNullException` | SetData value is null | ArgumentNullException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `BatchLoad_EmptyList_DoesNothing` | Empty list passed to RecoverFromMetaList | No exception; entity list is empty |
| `CreateEntity_DoesNotFireAfterSpawnHooks` | Calling CreateEntity directly | AfterSpawn hooks do not fire |
| `RemoveEntity_DoesNotFireBeforeDeadHooks` | Calling RemoveEntity directly | BeforeDead hooks do not fire |
| `SndEntityFactory_Spawn_WithNonLifecycleEntity_DoesNotThrow` | Non-IEntityLifecycle entity | No exception; returns normally |
| `SndEntityFactory_SpawnMany_WithNonLifecycleEntity_DoesNotThrow` | Multiple non-IEntityLifecycle entities | No exception; all created |
| `ProcessAll_DoesNotThrowForEmptyScene` | ProcessAll on a scene with no entities | No exception |

## SndEntityOwningSessionTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `CreateEntity_WithOwningSession_BindsOwningSessionToEntity` | After binding via IOwningSessionBindable.SetOwningSession, entity OwningSession is accessible | ISndEntity |
| `SndEntityFactory_Spawn_CreatesEntityAndFiresAfterSpawnHooks` | Spawn creates entity and fires AfterSpawn hook | SndEntityFactory |
| `SndEntityFactory_SpawnMany_CreatesMultipleEntitiesAndFiresHooks` | SpawnMany creates multiple entities and fires all AfterSpawn hooks | SndEntityFactory |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `CreateEntity_WithoutOwningSession_OwningSessionThrows` | Accessing OwningSession without binding | InvalidOperationException |

## SndDataManagerFailureTests Details

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `SetData_ConverterThrows_LeavesNoDictionaryEntry` | Object converter for a custom kind throws | InvalidOperationException; the failed SetData leaves no entry (SerializeMeta is empty, TryGetData returns false) |

## SndEntityRecoveryRollbackTests Details

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `RecoverForLifecycle_ActivePhaseFails_ReleasesPreviouslyAcquiredPassiveStrategies` | Active phase rejects a Lifecycle index | InvalidOperationException; cross-stage rollback releases the passive strategy acquired by the earlier phase (LogPoolLeaks emits no leak warning) |
| `RecoverForLifecycle_NodePhaseFails_ReleasesCreatedNodesAndNothingFromPool` | Creating the second node fails during the node phase | InvalidOperationException; nodes created before the failure are freed, and unacquired strategy indices are not released (no leak warning) |

## SndNodeManagerTests Details

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `SetSceneAliasResolver_Null_ThrowsArgumentNullException` | SetSceneAliasResolver with null | ArgumentNullException (fail-fast) |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| `ProbeStrategy` | SndEntityLifecycleBatchTests | AsyncLocal event recorder verifying that each lifecycle hook (AfterLoad/AfterSpawn/BeforeSave/BeforeQuit/BeforeDead) is called with the correct count and order |
| `CrossRefStrategy` | SndEntityLifecycleBatchTests | Cross-entity FindByName verification: within AfterLoad/AfterSpawn/BeforeQuit/BeforeDead, validates whether a named peer entity can be found |
| `QueryActiveProxy` | SndEntityLifecycleBatchTests | Cross-entity InvokeStrategy verification: during AfterLoad/AfterSpawn, validates calling another entity via ActiveStrategy |
| `SimpleActiveStrategy` | SndEntityLifecycleBatchTests | Active strategy; Invoke returns `hello_from:{entity.Name}` string |
| `SP50` / `SP100` | SndEntityLifecycleBatchTests | Priority verification: two strategies with Priority=50 and Priority=100 share an event collector, confirming lower priority executes first |
| `FailingStrategy` | SndEntityLifecycleBatchTests | Always-throws: AfterLoad always throws InvalidOperationException for error path testing |
| `SubscribeStrategy` | SndEntityLifecycleBatchTests | Data subscription test: cross-entity data change subscription during AfterLoad, recording notification events via AsyncLocal |
| `ProcessRecordingStrategy` | SndEntityLifecycleBatchTests | Records (entity.Name, delta) tuples of Process invocations |
| `AddDuringProcessStrategy` | SndEntityLifecycleBatchTests | Dynamically AddStrategy during Process; verifies new strategy is not executed this frame |
| `SelfRemoveRecordingStrategy` | SndEntityLifecycleBatchTests | Records self_remove events |
| `RemoveSelfDuringProcessStrategy` | SndEntityLifecycleBatchTests | Dynamically RemoveStrategy itself during Process; verifies subsequent strategies still execute |
| `AfterLoadProbeAStrategy` / `AfterLoadProbeBStrategy` | SndEntityAfterLoadTests | AsyncLocal shared event list verifying AfterLoad is triggered in index order |
| `ThrowingAfterLoadStrategy` | SndEntityAfterLoadTests | AfterLoad throws InvalidOperationException to verify exception propagation |
| `LifecycleStrategy` | SndEntityAndAutoInitializerTests | AsyncLocal event collector covering all lifecycle hooks (AfterSpawn/AfterAdd/BeforeRemove/BeforeSave/BeforeQuit) |
| `StubSessionRun` | SndEntityAndAutoInitializerTests | ISessionRun stub implementation, delegating Spawn/FindByName/GetEntities to ISndSceneHost |
| `AutoInitStrategyA` / `AutoInitStrategyB` | SndEntityAndAutoInitializerTests | Minimal LifecycleStrategyBase strategies, only declaring StrategyIndex, no behavior override |
| `StatefulAutoInitStrategy` | SndEntityAndAutoInitializerTests | Strategy with instance field (_counter), used to test framework guard against non-stateless strategies |
| `TrackingStrategy` | SndEntityOwningSessionTests | Records AfterSpawn events via constructor-injected List<string> |
| `StubSessionRun` | SndEntityOwningSessionTests | Minimal ISndSessionRun stub; all operations throw NotSupportedException |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| AutoInitializer recovery with metadata type mismatch | Data corruption scenario from corrupted metadata | Snd/Entity |
| Incremental strategy addition via AfterLoad when strategies already exist | Hook behavior of dynamic AddStrategy after AfterLoad | snd-entity-model |
| Frame-time stability of processing many entities concurrently via ProcessAll | Performance characteristics at extreme entity counts | snd-entity-model: Frame processing |

---

[↑ Back to Origo.Core.Tests](README.en.md)
