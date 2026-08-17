<!-- docsync-pair: Origo.Core.Tests/StateMachine -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# State Machine Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/StateMachine](../Origo.Core/StateMachine/README.en.md)
> [↔ Behavior under test: usage/state-machine](../usage/state-machine.en.md)

## Behavior Overview

Validates StackStateMachine string stack operations: Push/PopRuntime/PopOnQuit triggering corresponding strategy hooks,
Snapshot/RestoreStackWithoutHooks/FlushAfterLoad two-phase recovery,
StateMachineContainer CreateOrGet/TryGet/serialization round-trip/batch Pop operations,
StateMachineStrategyBase default hook semantics, StateMachineStrategyContext snapshots, and session Dispose triggering PopAllOnQuit.

`RandomAndStateMachineTests.Random.cs`, though in the same test class partial file, only contains RandomNumberGenerator (XorShift128+) random number tests, belonging to the random number capability, recorded in [Random.en.md](Random.en.md), and not duplicated here.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `StateMachineStrategyBaseTests.cs` | Default hooks do not schedule actions, Push/Pop/Quit/AfterLoad hook triggers, container quit hooks |
| `StackStateMachineTests.cs` | StackStateMachine atomic operation boundaries: Push/Pop/Peek/Dispose/Restore all scenarios |
| `RandomAndStateMachineTests.StringStack.cs` | StringStack core operations: Snapshot/Restore round-trip, Push/Pop hook order, FlushAfterLoad |
| `RandomAndStateMachineTests.Container.cs` | Container: CreateOrGet/serialize/deserialize/batch Pop/atomic replace |
| `RandomAndStateMachineTests.SessionAndAdapter.cs` | Session Dispose triggers PopAllOnQuit, StateMachineStrategyContext snapshot |
| `RandomAndStateMachineTests.Random.cs` | RandomNumberGenerator random number tests (see [Random.en.md](Random.en.md), not recorded here) |
| `RandomAndStateMachineTests.TestStrategies.cs` (helper file, contains 0 `[Fact]`) | Test helper strategy class definitions, see [Test Helper Strategies](#test-helper-strategies) |

## StateMachineStrategyBaseTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Push_TriggersOnPushRuntime` | Push triggers OnPushRuntime (AfterTop=pushed value), does not trigger OnPushAfterLoad | state-machine: Push |
| `Pop_TriggersOnPopRuntime` | TryPopRuntime triggers OnPopRuntime (BeforeTop=popped value) | state-machine: TryPopRuntime |
| `Quit_PopTriggersOnPopBeforeQuit` | TryPopOnQuit triggers OnPopBeforeQuit | state-machine: TryPopOnQuit |
| `AfterLoad_TriggersOnPushAfterLoad_BottomToTop` | FlushAfterLoad triggers OnPushAfterLoad in bottom-to-top order | state-machine: Load Restoration |
| `Container_PopAllOnQuit_TriggersPopBeforeQuit_OnAllMachines` | Container PopAllOnQuit triggers OnPopBeforeQuit for all machines | state-machine: Container Operations |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `DefaultHooks_DoNotScheduleActions` | All 4 default hook calls | EnqueueCount = 0 |

## StackStateMachineTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Push_ValidValue_SetsPeek` | Push("state_a") → Peek returns (true, "state_a") | state-machine |
| `Push_MultipleValues_PeekReturnsLast` | Push a→b→c → Peek returns c | state-machine |
| `TryPopRuntime_AfterPush_ReturnsTrueAndPopsTop` | Push a→b → TryPop → Peek returns a | state-machine |
| `PushPopPush_RoundTrip_PreservesStackState` | Push→Pop→Push→Pop full round-trip, stack state correct | state-machine |
| `RestoreStackWithoutHooks_ThenPeek_ReturnsTop` | Restore {x,y} → Peek returns y | state-machine |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Push_NullValue_Throws` | Push(null) | ArgumentException |
| `Push_EmptyString_Throws` | Push("") | ArgumentException |
| `Push_WhitespaceString_Throws` | Push("   ") | ArgumentException |
| `Push_AfterDispose_Throws` | Push after Dispose | ObjectDisposedException |
| `TryPopRuntime_AfterDispose_Throws` | TryPopRuntime after Dispose | ObjectDisposedException |
| `Peek_AfterDispose_Throws` | Peek after Dispose | ObjectDisposedException |
| `RestoreStackWithoutHooks_NullList_Throws` | Restore(null) | ArgumentNullException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `TryPopRuntime_EmptyStack_ReturnsFalse` | TryPopRuntime on empty stack | false |
| `TryPopOnQuit_EmptyStack_ReturnsFalse` | TryPopOnQuit on empty stack | false |
| `Peek_EmptyStack_ReturnsNull` | Peek on empty stack | (false, null) |
| `Dispose_IsIdempotent` | Two consecutive Dispose calls | Does not throw |
| `RestoreStackWithoutHooks_EmptyList_ResultsInEmptyStack` | Restore(empty) | Peek = (false, null) |

## StringStack Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `StringStackStateMachine_Snapshot_RestoreStackWithoutHooks_RoundTrip` | Snapshot → Restore, stack snapshot identical | state-machine: Load Restoration (two-phase) |
| `StringStackStateMachine_PushPopRuntime_AfterAddAndBeforeRemove_OrderAndContext` | Push→Pop triggers correct hooks, BeforeTop/AfterTop context and order correct | state-machine: TryPopRuntime |
| `StringStackStateMachine_PushPopOnQuit_AfterAddAndBeforeQuit_OrderAndContext` | PopOnQuit triggers beforeQuit hook, order and context correct | state-machine: TryPopOnQuit |
| `StringStackStateMachine_FlushAfterLoad_CallsAfterLoadInPushOrder` | RestoreWithoutHooks → FlushAfterLoad replays afterload in push order | state-machine: Load Restoration |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `StringStackStateMachine_Throws_WhenStrategyNotRegistered` | Create state machine with unregistered strategy index | InvalidOperationException |

## Container Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `StateMachineContainer_PopAllRuntime_InvokesBeforeRemoveTopToBottom` | PopAllRuntime triggers runtime hooks in LIFO order | state-machine: Container Operations |
| `StateMachineContainer_PopAllOnQuit_InvokesBeforeQuitTopToBottom` | PopAllOnQuit triggers beforeQuit hooks in LIFO order | state-machine: Container Operations |
| `StateMachineContainer_PopAllOnQuit_TraversesMachinesInInsertionOrder` | Multiple state machines traversed in insertion order | state-machine |
| `StateMachineContainer_SerializeDeserialize_RoundTrip` | Serialize→Deserialize, state machine stacks identical | state-machine: Serialization Format |
| `StateMachineContainer_DeserializeWithoutHooks_SwapsAtomically` | Hook-free deserialization atomically replaces old state | state-machine |
| `StateMachineContainer_CreateOrGet_IdempotentForSameKeyAndIndices` | Same key + same indices CreateOrGet returns same instance | state-machine |
| `StateMachineContainer_FlushAllAfterLoad_NotifiesPushStrategy` | FlushAllAfterLoad replays afterload in push order | state-machine |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `StateMachineContainer_CreateOrGet_ConflictingIndices_Throws` | Same key, different indices for CreateOrGet | InvalidOperationException |
| `StateMachineContainer_DeserializeFromNode_DuplicateMachineKey_Throws` | Deserialization containing duplicate keys | InvalidOperationException |
| `StateMachineContainer_DeserializeFromNode_ThrowsOnNullNode` | null node deserialization | ArgumentNullException |
| `StateMachineContainer_DeserializeFromNode_ArrayRoot_Throws` | Structurally wrong payload (array root) deserialization | InvalidOperationException (fail-fast instead of silently clearing machines) |
| `StateMachineContainer_DeserializeFromNode_MissingMachinesKey_Throws` | Payload missing the machines key | InvalidOperationException |
| `StateMachineContainer_Clear_ReleasesAllMachines_WhenOneDisposeThrows` | One machine release throws | Exception propagates, but remaining machines are released and the container clears |

## SessionAndAdapter Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SessionRun_Dispose_InvokesPopAllOnQuit_TopToBottom` | Session Dispose triggers container PopAllOnQuit, push/beforeQuit event sequence in top-to-bottom order correct | state-machine: Container Operations |
| `StateMachineStrategyContext_HoldsMachineKeyAndStackSnapshot` | StateMachineStrategyContext correctly saves MachineKey, BeforeTop, AfterTop | state-machine: Strategy Context |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `SmPushStrategy` | RandomAndStateMachineTests.TestStrategies.cs | Push hook: records events in `push:runtime:before->after` and `push:afterload:before->after` formats |
| `SmPopStrategy` | RandomAndStateMachineTests.TestStrategies.cs | Pop hook: records `pop:runtime:...` and `pop:beforeQuit:...` events |
| `SmPopOrderProbeStrategy` | RandomAndStateMachineTests.TestStrategies.cs | OnPopBeforeQuit records MachineKey, verifying multi-machine traversal order |
| `SwapTestPushStrategy` | RandomAndStateMachineTests.Container.cs | Empty Push hook, used for serialization atomic replace tests |
| `SwapTestPopStrategy` | RandomAndStateMachineTests.Container.cs | Empty Pop hook, used for serialization atomic replace tests |
| `SmPushStub` | StackStateMachineTests.cs | Empty StateMachineStrategyBase Push stub, only used to drive stack operations |
| `SmPopStub` | StackStateMachineTests.cs | Empty StateMachineStrategyBase Pop stub, only used to drive stack operations |
| `TrackingPushStrategy` | StateMachineStrategyBaseTests.cs | Records OnPushRuntime/OnPushAfterLoad call counts and AfterTop |
| `TrackingPopStrategy` | StateMachineStrategyBaseTests.cs | Records OnPopRuntime/OnPopBeforeQuit call counts and BeforeTop |
| `TestSmStrategy` | StateMachineStrategyBaseTests.cs | Default StateMachineStrategyBase (no hooks overridden), verifying default implementation does not schedule actions |
| `StubStateMachineContext` | StateMachineStrategyBaseTests.cs | IStateMachineContext stub, counts EnqueueBusinessDeferred calls |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| Transactional behavior of container partial deserialization failure with multiple state machines | Whether original state is preserved on mid-deserialization exception | state-machine: Serialization Format |

---

[↑ Back to Origo.Core.Tests](README.en.md)
