# Scheduling

> [↑ Back to Origo.Core.Kernel](../README.en.md) · [↔ Frame-driver contract: Origo.Core.Contracts/Abstractions/Runtime](../../Origo.Core.Contracts/Abstractions/Runtime/README.en.md)

## Overview
Deferred scheduling implementation: an `ActionScheduler` over the thread-safe `ConcurrentActionQueue`. The host environment is responsible for calling `Tick` at the right time to execute queued actions.

## Included Files

| File | Responsibility |
|------|------|
| `ActionScheduler.cs` | Internal scheduler wrapping `ConcurrentActionQueue` (Enqueue / Tick / Clear) |
| `ConcurrentActionQueue.cs` | Thread-safe deferred execution queue with batch drain and reentrancy protection |

## Implementation Details

### ActionScheduler
Thin wrapper over `ConcurrentActionQueue`:
- `Enqueue(action)` → enqueue
- `Tick()` → `ExecuteAll()`, returns count executed this frame
- `Clear()` → discard every queued action, running each action's `onDiscard` cleanup; used by `SndContext.Shutdown()` to cancel pending business/system deferred requests (see [Lifecycle](../Runtime/Lifecycle/README.en.md#shutdown))

### ConcurrentActionQueue
Core implementation using `List<Action>` + `lock`:
- **Batch drain**: Lock → snapshot all actions → clear → release lock → invoke
- **Reentrancy protection**: Actions enqueuing new actions continue draining. `MaxReentrantDrainDepth=100`
- **Explicit clear**: `Clear()` snapshots and clears the pending queue and runs every action's `onDiscard` cleanup; failures from discard callbacks are aggregated and rethrown after all callbacks ran. `ExecuteAll` keeps its current batch: only queued actions are cancelled, while actions already dequeued into the running batch (including later ones) still run (if the current action throws, the fail-fast rule below abandons the rest)
- **Exception handling**: Single action failure → log Error → rethrow (let-it-crash semantics); **the remaining actions in the same batch are dropped** (actions newly enqueued by earlier actions stay queued and run on the next `Tick`). Dropped actions registered with an `onDiscard` cleanup callback run every callback before the rethrow; if a cleanup callback itself throws, the remaining callbacks still run and the failures are aggregated with the original action exception (resource accounting such as the persistence-request counter does not leak). When the business queue throws in `OrigoRuntime.FlushEndOfFrameDeferred`, that frame's `KillPendingAllSessions` and system queue execution are postponed to the next frame (the cascading effect of fail-fast)

## Design Decisions

### Why snapshot-style drain rather than drain-while-executing
Drain-while-executing (e.g. `while(Dequeue()) invoke()`) requires holding the lock continuously and cannot enqueue new actions during execution. Snapshot-style first copies all pending actions out of the lock region, releases the lock, then invokes them one by one — reducing lock contention while allowing actions to enqueue new actions internally.

### Why ActionScheduler is internal
The scheduler is used only by runtime internals (construction, frame flushing, and shutdown cancellation). External code uses the scheduling capability indirectly through upper-level APIs (such as `SndContext`'s `EnqueueBusinessDeferred`) and does not interact with `ActionScheduler` directly.

### Why exceptions are rethrown rather than swallowed
Actions in the deferred queue are part of the frame model. If one action fails, the system should crash rather than silently skip, so business logic does not keep running in an unknown corrupted state. Exception details are logged, then thrown.

- **Entity frame processing stays serial (deferred direction)**: `ConcurrentActionQueue` thread safety only covers deferred-action enqueue/dequeue; in-entity lifecycle strategies are ordered by relative constraints, and frame processing as a whole still runs serially under the single-threaded model. Entity-level concurrency has been discussed as an alternative direction and is deferred because there is no performance bottleneck today. See [#45](https://github.com/51193/origo/issues/45) for the full trade-off

### Why shutdown cancels queued deferred actions
Queued business/system actions were scheduled against the runtime that is being torn down. Leaving them queued keeps the persistence-request counter non-zero (`ISndDeferredActions.IsPersistenceIdle` stays false) and a later frame would run a save/load/switch action against the terminal context, throwing `InvalidOperationException` from an otherwise unrelated frame. `OrigoRuntime.CancelPendingDeferredRequests` therefore clears both queues during shutdown before the console-state reset and again after the quit-hook teardown (hooks may enqueue new work before terminal state is committed); `Clear()` runs every registered discard callback so accounting (for example the tracked persistence-request counter) is released. Both queues are cleared even when a discard callback throws, and the first failure is rethrown afterwards so shutdown stays fail-fast; only still-queued work is cancelled — a batch that is already executing keeps running, including its remaining actions, so a shutdown requested from inside a batch action cannot stop that batch (unless that action throws; fail-fast then abandons the rest).

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
