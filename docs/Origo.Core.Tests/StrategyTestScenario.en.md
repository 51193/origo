<!-- docsync-pair: Origo.Core.Tests/StrategyTestScenario -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Strategy Test Framework Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Behavior under test: usage/strategy-testing](../usage/strategy-testing.en.md)

## Behavior Under Test Overview

Validates the correctness of the StrategyTestScenario test framework itself. This framework resides in `Origo.Core.Tests/TestSupport/` and is part of the test infrastructure (outlined in [Tests README](README.en.md)), with no corresponding production module.

Ensures that the framework's Harness correctly simulates EntityStrategy's Process/RunFrames/lifecycle hooks and ActiveStrategy's Invoke calls, and correctly records side effects (Save/Load/LevelSwitch/ControlConsole/DeferredAction).

## Test File List

| File | Verification Focus |
|------|-------------------|
| `StrategyTestScenarioTests.cs` | EntityStrategy Harness: Process/AfterSpawn/lifecycle hooks/blackboard/template cloning/side effects |
| `ActiveStrategyTestScenarioTests.cs` | ActiveStrategy Harness: Invoke/InvokeViaEntity/data read-write/blackboard/side effects/templates |

## StrategyTestScenarioTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Process_ModifiesDataAcrossFrames` | After RunFrames(5, 1.0), hp drops from 100 to 50 | strategy-testing: Phase 2 |
| `RunFrame_ExecutesDeferredActions` | RunFrame executes deferred actions | strategy-testing: Deferred actions |
| `Build_CallsAfterSpawn` | Build automatically triggers AfterSpawn → max_hp=200 | strategy-testing: Phase 1 |
| `SaveRequest_IsRecorded` | SaveRequests list records save requests | strategy-testing: Phase 3 |
| `LoadRequest_IsRecorded` | LoadRequests list records load requests | strategy-testing: Phase 3 |
| `SystemBlackboardConfig_IsAccessible` | After WithSystemConfig, strategy can read SystemBlackboard | strategy-testing |
| `ProgressBlackboardConfig_IsAccessible` | After WithProgressConfig, strategy can read ProgressBlackboard | strategy-testing |
| `SessionBlackboardConfig_IsAccessible` | After WithSessionConfig, strategy can read SessionBlackboard | strategy-testing |
| `EntityName_DefaultsAndCanBeOverridden` | Default __test_entity__; WithEntityName("MyPlayer") overrides | strategy-testing |
| `Template_CanBeRegisteredAndCloned` | After WithTemplate, strategy can Clone to get template data | strategy-testing |
| `TriggerLifecycleHooks_ExecuteStrategyHooks` | After 3 hooks triggered, hook_count=3 | strategy-testing |
| `LevelSwitchRequest_IsRecorded` | LevelSwitchRequests list records level switches | strategy-testing |
| `ConsoleCommand_IsRecorded` | ConsoleCommands list records console commands | strategy-testing |
| `MultipleFrames_AccumulateCorrectly` | After 100 frames, frame_count=100 | strategy-testing |
| `TryGetEntityData_ReturnsTrueForExistingKey` | TryGetEntityData returns (true, value) for existing key | strategy-testing |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `For_EmptyStrategyIndex_ThrowsArgumentException` | Empty strategy index | ArgumentException |
| `TryGetEntityData_ReturnsFalseForMissingKey` | Non-existent key | found=false |
| `TryGetEntityData_ReturnsFalseForTypeMismatch` | int read as string type | found=false |
| `WithTemplate_Null_ThrowsArgumentNullException` | null template | ArgumentNullException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `WithEntityName_EmptyString_UsesDefault` | "  " whitespace name | Falls back to __test_entity__ |

## ActiveStrategyTestScenarioTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Invoke_WithNoInput_ReturnsExpectedResult` | Build → Invoke with no input returns strategy default 42 | strategy-testing: ActiveStrategy |
| `Invoke_WithInput_PassesInputToStrategy` | Build → Invoke("hello") passes the string; strategy returns the same value | strategy-testing: ActiveStrategy |
| `Invoke_WithComplexInput_PassesThrough` | Anonymous object input passed to strategy and returned as-is | strategy-testing: ActiveStrategy |
| `Strategy_ReadsEntityData_SetViaBuilder` | WithData sets counter/label; strategy reads them and concatenates into return string | strategy-testing: ActiveStrategy |
| `Strategy_WritesEntityData_HarnessCanReadBack` | Strategy writes invoke_count/invoke_status; Harness reads back via GetEntityData | strategy-testing: ActiveStrategy |
| `MultipleInvokes_IncrementData` | After 3 Invokes, invoke_count=3 | strategy-testing: ActiveStrategy |
| `InvokeViaEntity_DelegatesToStrategy` | InvokeViaEntity with no input delegates to strategy returning 42 | strategy-testing: ActiveStrategy |
| `InvokeViaEntity_WithInput_DelegatesCorrectly` | InvokeViaEntity("world") delegates to strategy returning "world" | strategy-testing: ActiveStrategy |
| `SystemConfig_AccessibleInStrategy` | After WithSystemConfig, strategy reads via SystemBlackboard | strategy-testing: Blackboard |
| `ProgressConfig_AccessibleInStrategy` | After WithProgressConfig, strategy reads via ProgressBlackboard | strategy-testing: Blackboard |
| `SessionConfig_AccessibleInStrategy` | After WithSessionConfig, strategy reads via SessionBlackboard | strategy-testing: Blackboard |
| `AllThreeBlackboards_Accessible` | All three blackboard layers configured; strategy can read all | strategy-testing: Blackboard |
| `DefaultEntityName_IsTestEntity` | Default entity name is __test_entity__ | strategy-testing: ActiveStrategy |
| `CustomEntityName_PassedToStrategy` | After WithEntityName("MyCustomEntity"), strategy gets it via entity.Name | strategy-testing: ActiveStrategy |
| `Strategy_EnqueueBusinessDeferred_TracksCount` | Invoke → FlushDeferredActions; DeferredActionCount=1 | strategy-testing: Deferred actions |
| `Strategy_MultipleDeferredActions_TracksAll` | WithData("defer_count", 3) → Invoke → Flush; count=3 | strategy-testing: Deferred actions |
| `Strategy_SubmitConsoleCommand_TracksInList` | After Invoke, ConsoleCommands contains "test_command arg1" | strategy-testing: Console |
| `Strategy_RequestSave_TracksRequest` | After Invoke, SaveRequests contains "slot_001" | strategy-testing: ActiveStrategy |
| `Strategy_RequestLoad_TracksRequest` | After Invoke, LoadRequests contains "slot_002" | strategy-testing: ActiveStrategy |
| `Strategy_RequestSwitchLevel_TracksRequest` | After Invoke, LevelSwitchRequests contains "dungeon" | strategy-testing: ActiveStrategy |
| `WithTemplate_RegistersTemplateForCloning` | After WithTemplate, CloneTemplate gets template data and concatenates into return string | strategy-testing: Templates |
| `Entity_AfterBuild_IsAccessible` | After Build, Entity is non-null; Name is __test_entity__ | strategy-testing: ActiveStrategy |
| `FoodKeyGeneration_Invoke_GeneratesSequentialKeys` | 3 Invokes produce Food_xxxx ascending keys; next_id=4 | strategy-testing: ActiveStrategy |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `GetEntityData_WithMissingKey_Throws` | Reading non-existent key | InvalidOperationException |
| `GetEntityData_WithWrongType_Throws` | Reading int field as string type | InvalidOperationException |
| `ForActive_WithNullOrEmptyIndex_Throws` | null/empty/whitespace strategy index | ArgumentException |
| `WithTemplate_WithNull_Throws` | null template | ArgumentNullException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Invoke_WithNullInput_StrategyReceivesNull` | Invoke with no input passes null | Strategy receives null and returns null |
| `Invoke_StrategyReturnsNull_IsNull` | Strategy returns null | Invoke returns null |
| `TryGetEntityData_WithMissingKey_ReturnsFalse` | TryGetEntityData for non-existent key | found=false |
| `WithEntityName_EmptyOrWhitespace_ResetsToDefault` | WithEntityName("  ") whitespace name | Entity name falls back to __test_entity__ |
| `Entity_AfterBuild_StartswithCleanData` | After Build without WithData, entity has no data | TryGetEntityData returns false for non-existent key |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| `DamageStrategy` | StrategyTestScenarioTests.cs:280 | Deducts hp each frame via Process; verifies multi-frame accumulation |
| `DeferredActionStrategy` | StrategyTestScenarioTests.cs:292 | EnqueueBusinessDeferred during Process |
| `AfterSpawnInitStrategy` | StrategyTestScenarioTests.cs:299 | Sets max_hp=200 in AfterSpawn |
| `SaveOnLowHpStrategy` | StrategyTestScenarioTests.cs:305 | RequestSaveGame when hp≤0 |
| `LoadRequestStrategy` | StrategyTestScenarioTests.cs:319 | RequestLoadGame during Process |
| `BlackboardReaderStrategy` | StrategyTestScenarioTests.cs:325 | Reads SystemBlackboard to entity data |
| `ProgressBlackboardReaderStrategy` | StrategyTestScenarioTests.cs:336 | Reads ProgressBlackboard |
| `SessionBlackboardReaderStrategy` | StrategyTestScenarioTests.cs:347 | Reads SessionBlackboard |
| `TemplateCloneStrategy` | StrategyTestScenarioTests.cs:358 | CloneTemplate to obtain template data |
| `LifecycleRecordingStrategy` | StrategyTestScenarioTests.cs:373 | Increments hook_count across 3 hooks |
| `LevelSwitchStrategy` | StrategyTestScenarioTests.cs:395 | RequestSwitchForegroundLevel during Process |
| `ConsoleLogStrategy` | StrategyTestScenarioTests.cs:402 | TrySubmitConsoleCommand during Process |
| `FrameCounterStrategy` | StrategyTestScenarioTests.cs:409 | Increments frame_count each frame |
| `NopStrategy` | StrategyTestScenarioTests.cs:419 | Empty strategy for default value verification |
| `SimpleAnswerStrategy` | ActiveStrategyTestScenarioTests.cs:486 | Invoke returns 42 |
| `EchoInputStrategy` | ActiveStrategyTestScenarioTests.cs:492 | Invoke returns input |
| `DataWritingStrategy` | ActiveStrategyTestScenarioTests.cs:498 | Writes entity data during Invoke |
| `BusinessDeferredStrategy` | ActiveStrategyTestScenarioTests.cs:510 | EnqueueBusinessDeferred 1 or defer_count times |
| `ConsoleCommandStrategy` | ActiveStrategyTestScenarioTests.cs:524 | TrySubmitConsoleCommand |
| `SaveRequestStrategy` | ActiveStrategyTestScenarioTests.cs:533 | RequestSaveGame/Load/SwitchLevel |
| `TemplateCloneStrategy` | ActiveStrategyTestScenarioTests.cs:555 | CloneTemplate and serialize data |
| `DataReadingStrategy` | ActiveStrategyTestScenarioTests.cs:571 | Reads entity data and concatenates string |
| `BlackboardReadingStrategy` | ActiveStrategyTestScenarioTests.cs:590 | Reads from three blackboard layers |
| `EntityNameStrategy` | ActiveStrategyTestScenarioTests.cs:613 | Returns entity.Name |
| `NullReturnStrategy` | ActiveStrategyTestScenarioTests.cs:619 | Invoke returns null |
| `FoodKeyGeneratorStrategy` | ActiveStrategyTestScenarioTests.cs:625 | Generates Food_xxxx format keys |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Behavior of Harness's Entity property when accessed before Build | Defensive programming | strategy-testing |
| Multi-strategy entity testing (multiple strategies on one entity) | Inter-strategy interaction verification | snd-entity-model |
| BeforeDead hook behavior verification | BeforeDead is not tested | strategy-testing: TriggerBeforeDead |

---

[↑ Back to Origo.Core.Tests](README.en.md)
