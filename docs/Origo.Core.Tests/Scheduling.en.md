<!-- docsync-pair: Origo.Core.Tests/Scheduling -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Scheduling Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Scheduling](../Origo.Core/Scheduling/README.en.md)

## Behavior Overview

Validates the ConcurrentActionQueue deferred action queue: enqueue/drain (ExecuteAll),
batch drain snapshot semantics (safe re-enqueue during ExecuteAll),
recursive depth protection (max re-entrant drain depth), concurrent enqueue safety, and empty queue idempotency.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `ConcurrentActionQueueTests.cs` | Basic chain: enqueue/drain/clear/re-entrant/empty queue |
| `ConcurrentActionQueueConcurrencyTests.cs` | Concurrency safety: multi-threaded enqueue, recursive depth protection, drain after Clear |

## ConcurrentActionQueueTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ConcurrentActionQueue_Enqueue_IncreasesCount` | Count increments after Enqueue | Scheduling |
| `ConcurrentActionQueue_ExecuteAll_RunsAllActions` | ExecuteAll executes all enqueued actions, Count returns to 0 | Scheduling |
| `ConcurrentActionQueue_ExecuteAll_ActionThatReenqueues` | Actions re-enqueued during ExecuteAll are executed in the same call | Scheduling |
| `ConcurrentActionQueue_ExecuteAll_PropagatesException` | Exception from single action propagates | Scheduling |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ConcurrentActionQueue_Enqueue_ThrowsOnNull` | Enqueue(null) | ArgumentNullException |
| `ConcurrentActionQueue_Constructor_ThrowsOnNullLogger` | new ConcurrentActionQueue(null) | ArgumentNullException |
| `ConcurrentActionQueue_ExecuteAll_DiscardCallbackThrows_RunsRemainingCallbacksAndAggregates` | A discarded action's cleanup callback throws | Remaining cleanup callbacks still run; AggregateException contains both the original action exception and the cleanup exception |
| `ConcurrentActionQueue_Clear_DiscardCallbackThrows_RunsRemainingCallbacksAndRethrowsFirst` | A cleanup callback throws during Clear | Remaining cleanup callbacks still run; the first cleanup exception propagates unchanged |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `ConcurrentActionQueue_ExecuteAll_EmptyQueue_ReturnsZero` | ExecuteAll on empty queue | Returns 0 |
| `ConcurrentActionQueue_Clear_EmptiesQueue` | Count=0 after Clear | Count returns to 0 |

## ConcurrentActionQueueConcurrencyTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Enqueue_FromManyThreads_ExecuteAllRunsAllActions` | 8 threads × 50 actions concurrent enqueue, ExecuteAll executes all 400 | Scheduling |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ExecuteAll_WhenActionsKeepReenqueueing_ThrowsAtMaxReentrantDepth` | Infinite re-enqueue | InvalidOperationException (contains "max re-entrant") |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `ExecuteAll_EmptyQueue_IsIdempotent` | 3 consecutive ExecuteAll calls | Each returns 0 |
| `ExecuteAll_AfterClear_DoesNotExecuteClearedActions` | ExecuteAll after Clear | Returns 0, action not executed |
| `ExecuteAll_ExactlyMaxDepthBatches_ThenQueueEmpty_DoesNotThrow` | Exactly 100 chained re-enqueue batches then queue empty (re-entrancy boundary) | Does not throw, all 100 actions executed |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| None | — | Tests for this capability do not define helper strategies; pure queue behavior tests |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| Exact recursive depth value verification (docs say max depth = 100) | Depth guard boundary offset | Scheduling |

---

[↑ Back to Origo.Core.Tests](README.en.md)
