<!-- docsync-pair: Origo.Core/Scheduling/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Scheduling

> [↑ Back to Origo.Core](../README.en.md) · [↔ Abstractions: Runtime](../Abstractions/Runtime/README.en.md)

## Overview
Concrete implementation of `IScheduler`. Provides `ActionScheduler` based on `ConcurrentActionQueue`, a thread-safe deferred execution queue.

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
- **Exception handling**: Single action failure → log Error → rethrow (let-it-crash)

## Design Decisions

### Why snapshot-style drain
Reduces lock contention and allows in-action re-enqueuing.

### Why ActionScheduler is internal
Only used internally within Runtime layer. External code uses higher-level APIs.

### Why exceptions rethrown
Deferred queue actions are part of the frame model. Failure must crash rather than silently skip.

---
[↑ Back to Origo.Core](../README.en.md)
