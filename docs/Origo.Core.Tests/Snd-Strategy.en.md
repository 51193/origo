<!-- docsync-pair: Origo.Core.Tests/Snd-Strategy -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# SND Strategy Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd/Strategy](../Origo.Core/Snd/Strategy/README.en.md)
> [↔ Behavior under test: usage/snd-entity-model](../usage/snd-entity-model.en.md)

## Behavior Under Test Overview

Validates the full behavior of the SND strategy system: strategy priority ordering, pool reference counting/recycling, 8 lifecycle hooks for entity strategies, ActiveStrategy Invoke calls, observer strategy mount/unmount/data change notifications/persistence/topology queries, and type-safety checks during strategy registration.

The three performance tests in `SndStrategyPerformanceTests` use `Stopwatch` + `PerfReporter` to measure throughput/allocation with accompanying correctness assertions, do not carry `[Trait("Category","Benchmark")]` tags, and execute alongside the functional test pipeline.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `ActiveStrategyTests.cs` | ActiveStrategy Invoke calls, Spawn/Load recovery, Quit/Dead release, dynamic add/remove, serialization, registration validation, Entity/Active mixed scenarios |
| `LifecycleStrategyBaseTests.cs` | Default hooks do not mutate data; concurrent semantics of Add/Kill/SelfKill/OtherKill during Process; AfterAdd failure rollback; safe handling of non-existent strategy operations |
| `ObserverStrategyTests.cs` | Observer registration & statelessness enforcement; Mount/Unmount lifecycle and parameter correctness; data change notifications (correct key/non-observed key/after unmount) and old/new values; multi-key observation; serialization (ObserverIndices population/empty bindings/grouping); Dead/Quit release and OnUnmounted; attribute reflection extraction; cross-entity mount rejection; null/empty/unknown parameter defenses; RecoverBindings fault tolerance; Has/Remove topology queries; Teardown/KillPending/ClearAll cleanup paths |
| `StrategyPriorityTests.cs` | Strategies sorted ascending by Priority, same priority preserves insertion order FIFO, all lifecycle hooks respect priority, serialization/recovery preserves order |
| `StrategyPoolTypeSafetyAndExtensionTests.cs` | Strategy pool type-branch safety (generic GetStrategy type mismatch does not leak ref count), StackStateMachine two-phase acquisition failure rollback, third-domain base class extension, RecoverStrategiesOnly rejects non-Lifecycle strategies |
| `SndStrategyPoolLeakDetectionTests.cs` | Strategy pool leak detection: refcounts return to zero on normal release / mid-failure teardown; LogPoolLeaks emits no residual warnings |
| `SndStrategyPerformanceTests.cs` | Strategy pool Get/Release throughput, Process strategy count scaling, TriggerAll ToArray allocation (performance measurement, not benchmark-tagged) |

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

## LifecycleStrategyBaseTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `DefaultHooks_DoNotMutateEntityData` | All 8 default lifecycle hooks do not change entity data | snd-entity-model: Strategy lifecycle hooks |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `AddStrategy_WhenAfterAddThrows_RollsBackInsertionAndPoolReference` | Strategy AfterAdd hook throws InvalidOperationException | Strategy insertion rolled back, pool reference returned, subsequent Process does not execute this strategy |

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
| `HasObserverBindingTargeting_ExistingTarget_ReturnsTrue` | HasBindingTargetingFrom returns true when observer is mounted | Strategy README: ObserverTopology |
| `HasObserverBindingTargeting_NonexistentTarget_ReturnsFalse` | Returns false when target binding does not exist | Strategy README: ObserverTopology |
| `RemoveAllObserverBindingsTargeting_ClearsBindings` | RemoveBindingsTargetingFor clears all bindings for a specified target | Strategy README: ObserverTopology |
| `TeardownOutgoingObserverBindings_TriggersOnUnmounted` | TeardownOutgoingFor triggers OnUnmounted | Strategy README: ObserverTopology |
| `DataChange_OnlyTargetEntityNotified` | Data changes only notify observers of the target entity (EntityName and TargetName are both the target entity) | snd-entity-model: Observer |
| `BuildObserverBindings_TwoTargets_GroupsCorrectly` | BuildBindingsFor groups correctly by target | Strategy README: ObserverTopology |
| `OnDataChanged_OldAndNewValues_Correct` | OnDataChanged parameters oldValue=100, newValue=50 | snd-entity-model: Observer |

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

## StrategyPriorityTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Pool_GetPriority_ReturnsExplicitPriorityFromAttribute` | Priority=100 attribute correctly parsed | snd-entity-model: Priority |
| `Pool_GetPriority_ReturnsDefault6205WhenNotSpecified` | Returns default value 6205 when priority not specified | snd-entity-model: Priority |
| `Add_DifferentPriorities_SortedAscending` | Different priorities sorted ascending | snd-entity-model: Strategy execution order |
| `Add_SamePriority_MaintainsInsertionFifoOrder` | Same priority maintains FIFO insertion order | snd-entity-model |
| `Add_MixedPriorities_SortedAscWithStableFifoInSamePriority` | Mixed priorities sorted correctly; same priority maintains insertion order | snd-entity-model |
| `Add_InsertBetweenExisting_PositionsCorrectly` | Inserting between existing entries goes to the correct position | snd-entity-model |
| `Process_ExecutesInPriorityAscendingOrder` | Process executes in ascending priority order | snd-entity-model |
| `Process_SamePriority_ExecutesInInsertionOrder` | Same priority executes in insertion order | snd-entity-model |
| `Spawn_DifferentPriorities_SortedAscending` | On Spawn, sorted by priority | snd-entity-model |
| `Spawn_SamePriority_MaintainsInputOrder` | On Spawn, same priority maintains input order | snd-entity-model |
| `Load_DifferentPriorities_ResortedAscending` | On Load recovery, re-sorted ascending | snd-entity-model |
| `SerializeIndices_ReturnsIndicesInPriorityOrder` | Serialized indices are in priority order | snd-entity-model |
| `SaveLoadRoundtrip_MaintainsProcessingOrder` | After serialization → recovery, Process order is consistent | snd-entity-model |
| `AfterSpawn_ExecutesInPriorityAscendingOrder` | AfterSpawn hooks respect priority | snd-entity-model |
| `BeforeQuit_ExecutesInPriorityAscendingOrder` | BeforeQuit hooks respect priority | snd-entity-model |
| `AfterLoad_ExecutesInPriorityAscendingOrder` | AfterLoad hooks respect priority | snd-entity-model |
| `Remove_Middle_RemainingOrderPreserved` | Removing a middle strategy preserves remaining order | — |
| `Remove_First_RemainingOrderPreserved` | Removing the first strategy preserves remaining order | — |
| `Remove_Last_RemainingOrderPreserved` | Removing the last strategy preserves remaining order | — |
| `AddAfterRemove_InsertsAtCorrectPosition` | Inserting after removal goes to the correct position | — |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Pool_GetPriority_ReturnsZeroForUnknownIndex` | Querying priority for unknown index | Returns 0 |
| `EmptyList_ProcessDoesNotThrow` | Process on empty strategy list | No exception |
| `EmptyList_SerializeIndicesReturnsEmpty` | Serialize empty strategy list | Returns empty |
| `SingleStrategy_Works` | Single strategy | Works normally |
| `NegativePriorities_SortedCorrectly` | Negative priorities sorted correctly (-10, -5, 0, 50) | — |
| `IntMinAndIntMaxPriority_SortedCorrectly` | int.MinValue and int.MaxValue sorted correctly | — |
| `DescendingPriorityInsertion_SortedAscending` | Descending priority insertion auto-sorted ascending | — |
| `AscendingPriorityInsertion_SortedAscending` | Ascending priority insertion stays ascending | — |
| `AlternatingPriorityInsertion_SortedCorrectly` | Alternating priority insertion sorted ascending afterwards | — |
| `Remove_NonexistentStrategy_Throws` | Removing non-existent strategy throws; mounted strategies unaffected | — |
| `AllDefaultPriority6205_MaintainsInsertionOrder` | All default priority 6205 maintains insertion order | snd-entity-model |

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
| `SP50 / SP100 / SP200` | StrategyPriorityTests.cs | Strategies with different Priority (50/100/200); record execution log in Process |
| `S5 / S10A / S10B / S10C / S15 / S20 / S25 / S30 / S40 / S60 / S80 / S10` | StrategyPriorityTests.cs | Strategy group covering full priority range (5–80); some override Process to log |
| `SDemo` | StrategyPriorityTests.cs | Strategy without explicit Priority attribute (default 6205) |
| `SA / SB / SC` | StrategyPriorityTests.cs | Three strategies with same default priority (6205) to observe FIFO insertion order |
| `SN10 / SN5 / SN0` | StrategyPriorityTests.cs | Negative priority strategies (-10/-5/0) |
| `S0 / SMin / SMax` | StrategyPriorityTests.cs | int.Zero / int.MinValue / int.MaxValue priority strategies |
| `LC10 / LC20 / LC30` | StrategyPriorityTests.cs | Override AfterSpawn hook (Priority=10/20/30); verify lifecycle hook priority |
| `Q10 / Q20 / Q30` | StrategyPriorityTests.cs | Override BeforeQuit hook (Priority=10/20/30) |
| `LD10 / LD20 / LD30` | StrategyPriorityTests.cs | Override AfterLoad hook (Priority=10/20/30) |
| `Rec` (AsyncLocal recorder) | StrategyPriorityTests.cs | Execution order log collector; BeginTest/Add/Reset/Log; AsyncLocal isolates parallel tests |
| `TestLifecycleStrategy` | LifecycleStrategyBaseTests.cs | Empty strategy with no hook overrides; verifies default implementation does not modify entity data |
| `TestLifecycleStrategyWithAdd` | LifecycleStrategyBaseTests.cs | Scenario strategy calling entity.AddStrategy during Process |
| `TestLifecycleStrategyKillSelf` | LifecycleStrategyBaseTests.cs | Calls RequestKillEntity(self) during Process |
| `TestLifecycleStrategyKillOther` | LifecycleStrategyBaseTests.cs | Target entity strategy calling RequestKillEntity("B") during Process |
| `KillSelfRecordingStrategy` | LifecycleStrategyBaseTests.cs | Kills itself during Process and records execution log; AsyncLocal isolated |
| `ProcessCalledStrategy` | LifecycleStrategyBaseTests.cs | Marks whether Process was called (AsyncLocal bool); verifies subsequent strategies execute after Kill |
| `ThrowOnAddStrategy` | LifecycleStrategyBaseTests.cs | AfterAdd hook throws InvalidOperationException; verifies rollback and confirms Process is not executed |
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
| `StatefulObserver` | ObserverStrategyTests.cs | Observer with instance field _counter; verifies registration rejection |
| `UnannotatedObserver` | ObserverStrategyTests.cs | Observer without [StrategyIndex] attribute; verifies registration rejection |
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
