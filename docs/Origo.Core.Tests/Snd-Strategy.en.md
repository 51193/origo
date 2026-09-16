<!-- docsync-pair: Origo.Core.Tests/Snd-Strategy -->
<!-- docsync-revision: 16 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# SND Strategy Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd/Strategy](../Origo.Core/Snd/Strategy/README.en.md)
> [↔ Behavior under test: usage/snd-entity-model](../usage/snd-entity-model.en.md)

## Behavior Under Test Overview

Validates the full behavior of the SND strategy system: strategy partial ordering, pool reference counting/recycling, 8 lifecycle hooks for entity strategies, ActiveStrategy Invoke calls, observer strategy mount/unmount/data change notifications/persistence/topology queries, and type-safety checks during strategy registration.

The three performance tests in `SndStrategyPerformanceTests` use `Stopwatch` + `PerfReporter` to measure throughput/allocation with accompanying correctness assertions, carry `[Trait("Category","Benchmark")]`, and execute through `scripts/benchmark.sh` rather than the functional test pipeline.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `ActiveStrategyTests.cs` | ActiveStrategy Invoke calls, Spawn/Load recovery, Quit/Dead release, dynamic add/remove, serialization, registration validation, Entity/Active mixed scenarios |
| `ActiveStrategyJsonBaseTests.cs` | ActiveStrategyJsonBase JSON contract: input deserialization/result serialization, invalid input returns err result, bare string results pass through, null input execution, generic extension round-trip |
| `LifecycleStrategyBaseTests.cs` | Default hooks do not mutate data; concurrent semantics of Add/Kill/SelfKill/OtherKill during Process; AfterAdd failure rollback; safe handling of non-existent strategy operations |
| `ObserverStrategyTests.cs` | Observer registration & statelessness enforcement; Mount/Unmount lifecycle and parameter correctness; data change notifications (correct key/non-observed key/after unmount) and old/new values; multi-key observation; serialization (ObserverIndices population/empty bindings/grouping); Dead/Quit release and OnUnmounted; attribute reflection extraction; cross-entity mount rejection; null/empty/unknown parameter defenses; RecoverBindings fault tolerance; Has/Remove topology queries; Teardown/KillPending/ClearAll cleanup paths |
| `StrategyOrderingTests.cs` | Complete registry projection, dynamic mutation, save/load/quit/death, startup sealing, invalid declarations and cycle diagnostics |
| `StrategyOrderingIntegrationTests.cs` | Real simulation host: Ordinal order, reentrant insertion in BeforeRemove, and rejection of late registration; verified red → green |
| `StrategyPoolTypeSafetyAndExtensionTests.cs` | Strategy pool type-branch safety (generic GetStrategy type mismatch does not leak ref count), StackStateMachine two-phase acquisition failure rollback, third-domain base class extension, RecoverStrategiesOnly rejects non-Lifecycle strategies |
| `SndStrategyPoolLeakDetectionTests.cs` | Strategy pool leak detection: refcounts return to zero on normal release / mid-failure teardown; LogPoolLeaks emits no residual warnings |
| `SndStrategyPerformanceTests.cs` | Strategy pool Get/Release throughput, Process strategy count scaling, TriggerAll ToArray allocation (marked `[Trait("Category","Benchmark")]`, run by `scripts/benchmark.sh`) |

## ActiveStrategyTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Invoke_ReturnsResult` | Invoke returns a strongly-typed result from ActiveStrategy | snd-entity-model |
| `Invoke_EntityPassedCorrectly` | Invoke passes the entity name correctly (input="get_name" returns entity name) | snd-entity-model |
| `Invoke_InputPassedCorrectly` | Invoke correctly routes the input parameter to the strategy | snd-entity-model |
| `Spawn_RecoversActiveStrategies` | After Spawn, ActiveStrategy is available | snd-entity-model |
| `Load_RecoversActiveStrategies` | After Load, ActiveStrategy is available | snd-entity-model |
| `Quit_ReleasesAllActiveStrategies` | After Quit, Invoke throws (strategy has been released) | snd-entity-model |
| `Dead_ReleasesAllActiveStrategies` | After Dead, Invoke throws (strategy has been released) | snd-entity-model |
| `AddActiveStrategy_Then_Invoke_Works` | After dynamically adding ActiveStrategy, Invoke succeeds | snd-entity-model |
| `SerializeMetaData_IncludesActiveIndices` | After Save, MetaData contains ActiveIndices | snd-entity-model |
| `SerializeMetaData_EntityAndActive_Separated` | LifecycleIndices and ActiveIndices are correctly separated, neither contains the other | snd-entity-model |
| `SerializeMetaData_DynamicAdd_Then_Serialized` | Dynamically added ActiveStrategy appears in serialization results | snd-entity-model |
| `SerializeMetaData_DynamicRemove_NotSerialized` | After dynamic removal, serialization result is empty | snd-entity-model |
| `SameEntity_HasBothTypeStrategies` | Same entity mounts both LifecycleStrategy and ActiveStrategy; Process and Invoke both work | snd-entity-model |
| `RemoveLifecycleStrategy_LeavesActiveStrategy` | After removing LifecycleStrategy, ActiveStrategy Invoke still works | snd-entity-model |
| `RemoveActiveStrategy_LeavesLifecycleStrategy` | After removing ActiveStrategy, LifecycleStrategy Process still works | snd-entity-model |
| `ActiveStrategy_AutoDiscovered` | After registration, discoverable via GetRegisteredStrategyIndices() | snd-entity-model |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Invoke_UnregisteredIndex_Throws` | Calling an unregistered index | InvalidOperationException (contains index name) |
| `Invoke_LifecycleStrategyIndex_Throws` | Calling Invoke with LifecycleStrategy index | InvalidOperationException |
| `Load_ActiveIndexWithNonActiveType_Throws` | ActiveIndices contain non-ActiveStrategyBase type | InvalidOperationException (contains index name and type name) |
| `Load_ActiveIndexWithNonActiveType_RollsBackAcquiredActives` | ActiveStrategies acquired before failure must be rolled back | InvalidOperationException verified + Invoke throws again after failure |
| `AddActiveStrategy_Duplicate_Throws` | Duplicate ActiveStrategy added with same name | InvalidOperationException ("already attached") |
| `AddActiveStrategy_NonActiveType_Throws` | Adding non-ActiveStrategyBase type | InvalidOperationException |
| `AddActiveStrategy_NullOrWhitespace_Throws` | null or whitespace index | ArgumentException |
| `RemoveActiveStrategy_Then_Invoke_Throws` | Invoke called after removal | InvalidOperationException |
| `ActiveStrategy_StatelessnessEnforced` | Registering ActiveStrategy with instance field (_counter) | InvalidOperationException ("invalid instance members", contains field name) |
| `ActiveStrategy_MissingAttribute_Throws` | Registering ActiveStrategy without [StrategyIndex] | InvalidOperationException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `RemoveActiveStrategy_NotExists_Throws` | Removing non-existent ActiveStrategy | Throws `InvalidOperationException` (fail-fast) |

## ActiveStrategyJsonBaseTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Invoke_ValidJsonInput_DeserializesAndSerializesResult` | Valid JSON input is deserialized to a strong type and passed to Execute; return value is serialized as a JSON string | Strategy README: ActiveStrategyJsonBase |
| `Invoke_StringResult_IsSerializedAsJsonString` | Ok string result is serialized as a JSON string (`"ok"`) | Strategy README: ActiveStrategyJsonBase |
| `Invoke_ErrorResult_IsSerializedAsJsonString` | Err result is serialized as a JSON string (`"err:invalid"`) | Strategy README: ActiveStrategyJsonBase |
| `Invoke_InvalidJsonInput_ReturnsErrorResult` | Invalid JSON input returns `"err:Invalid request"` error result instead of throwing | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NonStringInput_ReturnsErrorResult` | Non-string input returns `"err:Invalid request"` error result | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NullResult_SerializesNull` | When Execute returns null, it is serialized as the JSON literal `null` | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NullInput_ExecutesWithDefault` | null input executes with the default value (int default 0, result `"0"`) | Strategy README: ActiveStrategyJsonBase |
| `Invoke_StringReferenceTypeInput_RoundTrips` | String reference type input round-trips (`"hello"` → `"hello"`) | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NullJsonInput_ExecutesWithNullReference` | JSON literal `null` input executes with a null reference and serializes as `null` | Strategy README: ActiveStrategyJsonBase |
| `GenericInvoke_JsonBaseStrategy_RoundTripsThroughExtensions` | Generic InvokeStrategy<TestPayload,TestPayload> round-trips fully through the JSON base class | Snd README: ActiveStrategyExtensions |
| `GenericInvoke_BareStringResult_ReturnsStringAsIs` | Strategies that return bare strings return them as-is through the generic call without throwing JSON exceptions | Snd README: ActiveStrategyExtensions |
| `GenericInvoke_ErrorBareString_ReturnsStringAsIs` | Bare string err result (`"err:no gold"`) returned as-is | Snd README: ActiveStrategyExtensions |

## LifecycleStrategyBaseTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `DefaultHooks_DoNotMutateEntityData` | All 8 default lifecycle hooks do not change entity data | snd-entity-model: Strategy lifecycle hooks |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `AddStrategy_WhenAfterAddThrows_RollsBackInsertionAndPoolReference` | Strategy AfterAdd hook throws InvalidOperationException | Strategy insertion rolled back, pool reference returned, subsequent Process does not execute this strategy |
| `AddStrategy_SameIndexTwice_Throws` | Calling AddStrategy again on an already-mounted strategy index | InvalidOperationException ("already mounted") |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Process_AddsNewStrategy_DoesNotThrow` | Calling AddStrategy to add a new strategy during Process | No exception |
| `Process_KillsItself_MarksEntity` | RequestKillEntity(self) called during Process | Entity marked IsPendingKill |
| `Process_KillsOtherEntity_MarksTargetEntity` | RequestKillEntity("B") called during Process | Target entity marked IsPendingKill; current entity unaffected |
| `Process_RequestKillDuringProcess_RemainingStrategiesStillExecuted` | First strategy kills itself; subsequent strategies on the same entity still execute | KillSelfRecordingStrategy executes first and records; ProcessCalledStrategy still executes afterward |
| `Remove_NonexistentStrategy_Throws` | Removing a non-existent strategy | Throws `InvalidOperationException` (fail-fast) |

## ObserverStrategyTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `ObserverStrategy_CanBeRegistered` | Observer strategy can be registered via RegisterStrategy | snd-entity-model: Observer |
| `Mount_TriggersOnMounted_WithCorrectParameters` | Mount triggers OnMounted with correct Entity and Target parameters | snd-entity-model: Observer |
| `Unmount_TriggersOnUnmounted_WithCorrectParameters` | Unmount triggers OnUnmounted with correct parameters | snd-entity-model: Observer |
| `SetData_TriggersOnDataChanged_ForObservedKey` | Setting an observed key (character.hp) triggers OnDataChanged | snd-entity-model: Observer |
| `SetData_DoesNotTrigger_ForUnobservedKey` | Setting a non-observed key (character.mp) does not trigger | snd-entity-model: Observer |
| `SetData_DoesNotTrigger_AfterUnmount` | After Unmount, setting observed data key no longer triggers callback | snd-entity-model: Observer |
| `SetData_TriggersForMultipleKeys` | Multi-key observation (hp, mp) triggers respective callbacks | snd-entity-model: Observer |
| `SetData_OldAndNewValuesCorrect` | OnDataChanged receives correct oldValue and newValue | snd-entity-model: Observer |
| `BuildMetaData_IncludesObserverBindings` | After Save, MetaData contains ObserverIndices (Target + ObserverIndices) | snd-entity-model: Observer |
| `BuildMetaData_EmptyBindings_WhenNoObservers` | ObserverIndices is an empty list when there are no observers | snd-entity-model: Observer |
| `BuildMetaData_MultipleTargets_GroupedCorrectly` | Multiple observer strategies mounted on the same target are merged into one ObserverBinding | snd-entity-model |
| `Dead_ReleasesObserverStrategies` | After Dead, data change notifications no longer fire | snd-entity-model: Observer |
| `Dead_TriggersOnUnmounted` | Dead triggers OnUnmounted | snd-entity-model: Observer |
| `ObserveDataAttribute_ExtractsKeys` | Reflection extracts data keys declared by [ObserveData] attributes | Strategy README: ObserverStrategyMetadata |
| `ObserveDataAttribute_MultipleKeys` | Multiple [ObserveData] attributes all correctly extracted | Strategy README: ObserverStrategyMetadata |
| `ObserveDataAttribute_NoAttributes_ReturnsEmpty` | Returns empty collection when no [ObserveData] attributes exist | Strategy README: ObserverStrategyMetadata |
| `MountObserverStrategy_WithSelfTargetName_Succeeds` | Mounting observer with own entity name succeeds | snd-entity-model: Observer |
| `Quit_TriggersOnUnmounted` | Quit triggers OnUnmounted | snd-entity-model: Observer |
| `DeepClone_PreservesObserverBindings` | SndMetaData.DeepClone() preserves ObserverIndices | snd-entity-model: Observer |
| `SaveSingle_ThenRecover_PreservesObserverBindings` | Save → new entity Spawn + RecoverBindingsFor; data change notification works | snd-entity-model: Observer |
| `GetObserverNamesTargeting_ExistingTarget_ReturnsTrue` | GetObserverNamesTargeting returns the observer name when mounted | Strategy README: ObserverTopology |
| `GetObserverNamesTargeting_NonexistentTarget_ReturnsFalse` | Returns an empty collection when no binding targets the name | Strategy README: ObserverTopology |
| `RemoveAllObserverBindingsTargeting_ClearsBindings` | RemoveBindingsTargetingFor clears all bindings for a specified target | Strategy README: ObserverTopology |
| `TeardownOutgoingObserverBindings_TriggersOnUnmounted` | TeardownOutgoingFor triggers OnUnmounted | Strategy README: ObserverTopology |
| `DataChange_OnlyTargetEntityNotified` | Data changes only notify observers of the target entity (EntityName and TargetName are both the target entity) | snd-entity-model: Observer |
| `BuildObserverBindings_TwoTargets_GroupsCorrectly` | BuildBindingsFor groups correctly by target | Strategy README: ObserverTopology |
| `OnDataChanged_OldAndNewValues_Correct` | OnDataChanged parameters oldValue=100, newValue=50 | snd-entity-model: Observer |
| `GetObserverNamesTargeting_MountedObserver_ReturnsObserverName` | GetObserverNamesTargeting returns the observer name when mounted | Strategy README: ObserverTopology |
| `GetObserverNamesTargeting_NoBindings_ReturnsEmpty` | GetObserverNamesTargeting returns empty with no bindings (including unknown target names) | Strategy README: ObserverTopology |
| `GetObserverNamesTargeting_AfterUnmount_IndexCleared` | GetObserverNamesTargeting no longer returns the observer name after Unmount | Strategy README: ObserverTopology |
| `MountObserverStrategy_ByEntityOverload_Works` | Mounting an observer onto another entity via the entity overload; target data changes trigger the callback with correct Entity/Target parameters | snd-entity-model: Observer |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ObserverStrategy_StatelessEnforcement` | Registering observer strategy with instance field (_counter) | InvalidOperationException ("invalid instance members") |
| `ObserverStrategy_MissingAttribute_Throws` | Registering observer without [StrategyIndex] | InvalidOperationException |
| `Mount_WhenOnMountedThrows_RollsBackAndReturnsToPool` | OnMounted throws InvalidOperationException | Data subscriptions rolled back; subsequent SetData does not trigger callback; strategy returned to pool |
| `MountObserverStrategy_WithDifferentTargetName_Throws` | Mounting with target name different from own entity name | InvalidOperationException ("Cross-entity") |
| `Mount_NullTargetName_Throws` | null target name | InvalidOperationException |
| `Mount_EmptyObserverIndex_Throws` | Empty string observer index | ArgumentException |
| `Mount_UnknownObserverIndex_Throws` | Unregistered observer index | InvalidOperationException |
| `MountObserverStrategy_ByEntityOverload_NullTarget_Throws` | null target for the entity overload | ArgumentNullException |
| `Mount_WhenGetStrategyThrows_PropagatesOriginalError` | Observer strategy acquisition fails | Original InvalidOperationException propagates (contains index name) |
| `Unmount_WhenOnUnmountedThrows_PoolReferenceStillReleased` | OnUnmounted hook throws InvalidOperationException | Exception propagates and the strategy is still returned to the pool (LogPoolLeaks emits no leak warning) |
| `FullCleanup_NullTargetEntity_ThrowsInvalidOperation` | FullCleanup with null TargetEntity | InvalidOperationException (message contains "TargetEntity") |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Mount_Duplicate_Throws` | Duplicate mounting of same observer on same target | InvalidOperationException (duplicate mount rejected) |
| `Unmount_NotMounted_Throws` | Unmount a binding that is not mounted | InvalidOperationException |
| `NoDataKeyObserver_CanMountAndUnmount` | Mount/unmount observer with no [ObserveData] attributes | No exception |
| `RecoverBindings_TargetNotFound_Throws` | RecoverBindingsFor when resolveTarget returns null | InvalidOperationException (dangling binding fails the load) |
| `RecoverBindings_EmptyTarget_Throws` | Archived binding target is null/blank | InvalidOperationException |
| `KillPendingEntities_NoObserverBindings_NoError` | KillPending on entities with no observer bindings | Completes normally; entity count becomes 0 |
| `ClearAll_NoObserverBindings_NoError` | RemoveAllEntities with no observer bindings | Completes normally; entity count becomes 0 |

## StrategyOrderingTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `FullRegistry_ProjectsTransitiveOrder_RegardlessOfRegistrationAndMountingOrder` | Registration and mounting order do not affect results; A → B → C stays transitive when only A/C are mounted; AfterSpawn, Process, and metadata share one order | snd-entity-model: Strategy execution order |
| `DynamicAddAndRemove_KeepTransitiveOrderAndOptionalTargets` | Dynamic add/remove inserts and reorders by the complete registry; constraints hold when the target is unmounted; duplicate add and removal of an unmounted index throw | snd-entity-model: Strategy execution order |
| `SaveLoadQuitAndDead_AllUseSameProjectedOrder` | Save, load, quit, and death all use the same projected order; recovered metadata order is correct and quit leaves no pool reference leaks | snd-entity-model: Strategy execution order |
| `EquivalentBeforeAfterAndDuplicateEdges_DoNotCreateFalseCycles` | Equivalent Before / After edges and duplicate declarations are deduplicated without false cycles; unknown order queries throw; registration after sealing is rejected | Strategy README: SndStrategyPool |

### Error Paths

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `BootstrapWithoutAutoDiscovery_ValidatesUnusedConstraintsImmediately` | An unmounted strategy declares an unregistered target | Throws InvalidOperationException while sealing startup registration |
| `UnknownTarget_FailsBeforeAnyEntityOrPoolReferenceIsCreated` | Before points to an unregistered index | SealRegistration throws InvalidOperationException containing the target index and "unregistered" |
| `NonLifecycleTarget_IsRejected` | A lifecycle strategy references a non-lifecycle target | SealRegistration throws InvalidOperationException containing "non-lifecycle" |
| `Cycles_FailWithAnActualClosedPath_WithoutIncludingUnrelatedPredecessors` | Lifecycle ordering declarations form a cycle with an unrelated predecessor | Throws InvalidOperationException with the actual closed path and no unrelated predecessor; repeated Seal still throws |
| `Cycles_WithAcyclicBranch_ReportOnlyClosedPath` | An unrelated acyclic node completes traversal before the cycle is found | Throws InvalidOperationException with only the closed path; the acyclic node is absent |
| `InvalidDeclarations_FailAtRegistration` | Self reference, blank target, null array, or a non-lifecycle strategy declaring constraints | Throws InvalidOperationException at registration |
| `DirectEntityRecovery_SealsRegistrationWithoutBootstrap` | Registering again after direct lifecycle-strategy recovery | Recovery seals the registry; subsequent Register throws InvalidOperationException |

## StrategyOrderingIntegrationTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `SpawnAndProcess_UnconstrainedStrategies_UseOrdinalIndexOrder` | Lifecycle strategies without constraints execute in index Ordinal order regardless of mounting order | snd-entity-model: Strategy execution order |
| `RemoveStrategy_HookChangesOtherEntries_RemovesRequestedEntry` | When BeforeRemove removes another strategy and adds a new one, removal still targets the requested entry by identity without deleting the wrong entry or double-releasing the pool reference | Strategy README: Why freeze the complete registry graph |

### Error Paths

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `RegisterStrategy_AfterFirstLifecycleEntity_FailsExplicitly` | Registering a new strategy after a lifecycle entity starts | Throws InvalidOperationException and keeps the registry frozen |

## StrategyPoolTypeSafetyAndExtensionTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `GetStrategy_WrongBranchGeneric_DoesNotLeakReferenceCount` | Generic type mismatch failure does not leak reference count (acquiring again is not the same instance) | Strategy README: SndStrategyPool |
| `StackStateMachine_WhenSecondAcquireFails_ReleasesFirstAcquire` | StackStateMachine construction: first acquire succeeds but second fails; rolls back first acquire | Strategy README: SndStrategyPool |
| `RecoverStrategiesOnly_WithOnlyValidStrategies_Succeeds` | Index list containing only LifecycleStrategies recovers successfully | Strategy README: SndStrategyManager |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `GetStrategy_WrongBranchGeneric_ThrowsInvalidOperation` | Using LifecycleStrategyBase generic to acquire Active/StateMachine strategy | InvalidOperationException |
| `RecoverStrategiesOnly_WithNonLifecycleStrategy_Throws` | Recover list contains ActiveStrategyBase type | InvalidOperationException ("LifecycleStrategyBase") |
| `RecoverStrategiesOnly_DuplicateIndex_ThrowsBeforeAcquiring` | Recover list contains a duplicate lifecycle index | InvalidOperationException ("more than once"); no strategy reference acquired |
| `Recover_DuplicateActiveIndex_ThrowsBeforeAcquiring` | Recover list contains a duplicate active index | InvalidOperationException ("more than once"); no strategy reference acquired or leaked |
| `Register_AbstractStrategyType_Throws` | Registering an abstract strategy type | InvalidOperationException |
| `Register_DuplicateIndex_Throws` | Registering the same strategy index twice | InvalidOperationException ("already registered") |
| `GetStrategy_FactoryReturnsNull_ThrowsInvalidOperation` | Registered factory returns null | InvalidOperationException (contains "returned null"; must not degrade into an NRE) |

## SndStrategyPerformanceTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `StrategyPool_GetRelease_Throughput` | 100,000 Get+Release round-trips throughput and allocation within acceptable range (< 500MB) | — |
| `StrategyManager_Process_StrategyCountScaling` | 1/5/10/20 strategies × 10,000 frames Process throughput and allocation: asserts entities still alive after ProcessAll | — |
| `TriggerAll_AfterSpawn_AllocationByStrategyCount` | 1/10 strategies AfterSpawn TriggerAll ToArray allocation: asserts entity names are correct after AfterSpawn | — |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| `TestLifecycleStrategy` | LifecycleStrategyBaseTests.cs | Empty strategy with no hook overrides; verifies default implementation does not modify entity data |
| `TestLifecycleStrategyWithAdd` | LifecycleStrategyBaseTests.cs | Scenario strategy calling entity.AddStrategy during Process |
| `TestLifecycleStrategyKillSelf` | LifecycleStrategyBaseTests.cs | Calls RequestKillEntity(self) during Process |
| `TestLifecycleStrategyKillOther` | LifecycleStrategyBaseTests.cs | Target entity strategy calling RequestKillEntity("B") during Process |
| `KillSelfRecordingStrategy` | LifecycleStrategyBaseTests.cs | Kills itself during Process and records execution log; AsyncLocal isolated |
| `ProcessCalledStrategy` | LifecycleStrategyBaseTests.cs | Marks whether Process was called (AsyncLocal bool); verifies subsequent strategies execute after Kill |
| `ThrowOnAddStrategy` | LifecycleStrategyBaseTests.cs | AfterAdd hook throws InvalidOperationException; verifies rollback and confirms Process is not executed |
| `DuplicateAddTestStrategy` | LifecycleStrategyBaseTests.cs | Blank LifecycleStrategy; verifies duplicate AddStrategy of the same index is rejected |
| `QueryHpStrategy` | ActiveStrategyTests.cs | ActiveStrategy: returns 100 (int) or entity name (input="get_name") |
| `CmdDamageStrategy` | ActiveStrategyTests.cs | ActiveStrategy: when input is int, returns "dealt {n} damage" |
| `EntityOnlyStrategy` | ActiveStrategyTests.cs | LifecycleStrategy placeholder for distinguishing Entity/Active types |
| `StatefulActiveStrategy` | ActiveStrategyTests.cs | ActiveStrategy with instance field _counter; verifies registration rejection |
| `StatelessActiveStrategy` | ActiveStrategyTests.cs | Stateless ActiveStrategy; verifies auto-discovery |
| `UnannotatedActiveStrategy` | ActiveStrategyTests.cs | ActiveStrategy without [StrategyIndex] attribute; verifies registration rejection |
| `SelfWatchObserver` | ObserverStrategyTests.cs | Observer watching character.hp; AsyncLocal List<DataCall> records each OnDataChanged parameter |
| `MultiKeyObserver` | ObserverStrategyTests.cs | Observer watching character.hp + character.mp dual keys; records to separate lists |
| `NoDataKeyObserver` | ObserverStrategyTests.cs | Observer with no [ObserveData] attributes; verifies mount/unmount is possible |
| `MemoryObserver` | ObserverStrategyTests.cs | Records OnMounted/OnUnmounted calls (MountCall contains Entity + Target); AsyncLocal list isolated |
| `ThrowOnMountObserver` | ObserverStrategyTests.cs | OnMounted throws InvalidOperationException; verifies rollback and confirms subsequent SetData does not trigger |
| `ThrowOnUnmountObserver` | ObserverStrategyTests.cs | OnUnmounted throws InvalidOperationException; verifies the failed unmount still returns the pool reference |
| `StatefulObserver` | ObserverStrategyTests.cs | Observer with instance field _counter; verifies registration rejection |
| `UnannotatedObserver` | ObserverStrategyTests.cs | Observer without [StrategyIndex] attribute; verifies registration rejection |
| `Probe` (abstract) | StrategyOrderingTests.cs | Abstract base recording all eight lifecycle hook events for Producer / Bridge / Consumer |
| `Producer` / `Bridge` / `Consumer` | StrategyOrderingTests.cs | Verify A → B → C projection, deduplication, and Ordinal fallback using duplicate Before, equivalent After, and an unmounted intermediate |
| `MissingTarget` | StrategyOrderingTests.cs | Before points to an unregistered index; verifies the SealRegistration unknown-target error |
| `ActiveTarget` | StrategyOrderingTests.cs | ActiveStrategy target; verifies a lifecycle constraint referencing a non-lifecycle strategy is rejected |
| `ReferencesActive` | StrategyOrderingTests.cs | Lifecycle strategy referencing ActiveTarget; verifies the non-lifecycle reference error |
| `OrderedActive` | StrategyOrderingTests.cs | ActiveStrategy declaring Before; verifies rejection at registration |
| `CycleA` / `CycleB` / `CycleC` / `CycleRoot` | StrategyOrderingTests.cs | Form a real cycle with an unrelated predecessor; verifies closed-path diagnostics omit unrelated nodes |
| `AcyclicLeaf` | StrategyOrderingTests.cs | Acyclic node with no outgoing edges; verifies the cycle search completes its branch and reports only the closed path |
| `SelfReference` / `BlankReference` / `NullReference` | StrategyOrderingTests.cs | Invalid-registration cases: self reference, blank target, and null array |
| `OrderingAlpha` / `OrderingZulu` / `OrderingRemover` | StrategyOrderingIntegrationTests.cs | Real simulation host verifying Ordinal fallback order and identity-based removal under reentrant insertion in BeforeRemove |
| `ExtensionDomainStrategyBase` (abstract) | StrategyPoolTypeSafetyAndExtensionTests.cs | Third-domain abstract base class extending LifecycleStrategyBase; defines ProbeValue() abstract method |
| `ExtensionDomainConcreteStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | Concrete implementation of ExtensionDomainStrategyBase; ProbeValue() returns "ok" |
| `PoolEntityStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | LifecycleStrategyBase empty implementation for generic branch safety tests |
| `PoolStateMachineStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | StateMachineStrategyBase empty implementation for StackStateMachine tests |
| `PoolActiveStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | ActiveStrategyBase empty implementation for RecoverStrategiesOnly rejection test |
| `PerfPoolStrategy` | SndStrategyPerformanceTests.cs | LifecycleStrategyBase empty implementation for strategy pool Get/Release performance measurement |
| `PerfProcessBase` (abstract) | SndStrategyPerformanceTests.cs | Abstract LifecycleStrategy with empty Process method; performance strategies 1–20 all inherit this base |
| `PerfProcess1Strategy` ~ `PerfProcess20Strategy` | SndStrategyPerformanceTests.cs | 20 identically-named Process empty-implementation strategies for Process strategy count scaling and TriggerAll allocation measurement |

## SndStrategyPoolLeakDetectionTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `LogPoolLeaks_AllReleased_ProducesNoWarnings` | Get then Release returns refcount to zero; LogPoolLeaks emits no warnings | Strategy README: LogPoolLeaks |
| `LogPoolLeaks_NoStrategiesRegistered_ProducesNoWarnings` | LogPoolLeaks on an empty pool emits nothing | Strategy README: LogPoolLeaks |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `LogPoolLeaks_UnreleasedStrategy_LogsWarning` | Strategy acquired but not released; refcount non-zero | Warning containing strategy index and refCount |
| `LogPoolLeaks_MultipleLeaks_LogsWarningForEach` | Multiple strategies left unreleased | One warning per leaked strategy |


## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Effect of RequestKill during Process on same-entity ActiveStrategy | Only tested LifecycleStrategy remaining execution after Kill; ActiveStrategy scenarios not verified | snd-entity-model |
| ActiveStrategy's AfterSpawn/BeforeQuit/AfterLoad lifecycle behavior | ActiveStrategy only tested for Invoke + registration + Spawn/Load recovery; whether it responds to non-Invoke lifecycle hooks not covered | Strategy README: Strategy inheritance hierarchy |
| ObserverStrategy BeforeDead/BeforeSave hook integration | Observer only tested Dead/Quit release paths; observer behavior during BeforeDead/BeforeSave hooks not verified | Strategy README: Strategy lifecycle hook order |
| Thread safety of strategy pool under concurrent Get/Release | All current tests are single-threaded; multi-threaded scenario reference counting and pooling correctness not covered | Strategy README: SndStrategyPool |
| Cross-entity observer Save/Recover full chain (with resolveTarget via SessionManager.FindByName) | Only tested self-observation Save/Recover; cross-entity scenarios relying on SessionManager target lookup not covered | Strategy README: ObserverTopology |

---

[↑ Back to Origo.Core.Tests](README.en.md)
