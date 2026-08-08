<!-- docsync-pair: Origo.Core/Scheduling/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Scheduling

> [↑ Back to Origo.Core](../README.en.md) · [↔ Abstractions: Runtime](../Abstractions/Runtime/README.en.md)

## Overview
Concrete implementation of the `IScheduler` interface. Provides a simple scheduler based on `ConcurrentActionQueue`, plus a thread-safe deferred execution queue. The host environment is responsible for calling `Tick` at the right time to execute queued actions.

## Included Files

| File | Responsibility |
|------|------|
| `ActionScheduler.cs` | Internal `IScheduler` implementation wrapping `ConcurrentActionQueue` |
| `ConcurrentActionQueue.cs` | Thread-safe deferred execution queue with batch drain and reentrancy protection |

## Implementation Details

### ActionScheduler
Thin wrapper adapting `ConcurrentActionQueue` to `IScheduler`:
- `Enqueue(action)` → enqueue
- `Tick()` → `ExecuteAll()`, returns count executed this frame
- `Clear()` → clear queue

### ConcurrentActionQueue
Core implementation using `List<Action>` + `lock`:
- **Batch drain**: Lock → snapshot all actions → clear → release lock → invoke
- **Reentrancy protection**: Actions enqueuing new actions continue draining. `MaxReentrantDrainDepth=100`
- **Exception handling**: Single action failure → log Error → rethrow (let-it-crash semantics); **the remaining actions in the same batch are dropped** (including actions newly enqueued by earlier actions). When the business queue throws in `OrigoRuntime.FlushEndOfFrameDeferred`, that frame's `KillPendingAllSessions` and system queue execution are postponed to the next frame (the cascading effect of fail-fast)

## Design Decisions

### Why snapshot-style drain rather than drain-while-executing
Drain-while-executing (e.g. `while(Dequeue()) invoke()`) requires holding the lock continuously and cannot enqueue new actions during execution. Snapshot-style first copies all pending actions out of the lock region, releases the lock, then invokes them one by one — reducing lock contention while allowing actions to enqueue new actions internally.

### Why ActionScheduler is internal
The scheduler is used only inside the Runtime layer. External code uses the scheduling capability indirectly through upper-level APIs (such as `SndContext`'s `EnqueueBusinessDeferred`) and does not interact with `ActionScheduler` directly.

### Why exceptions are rethrown rather than swallowed
Actions in the deferred queue are part of the frame model. If one action fails, the system should crash rather than silently skip, so business logic does not keep running in an unknown corrupted state. Exception details are logged, then thrown.

---
[↑ Back to Origo.Core](../README.en.md)
