<!-- docsync-pair: Origo.Core/Abstractions/Runtime/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Runtime (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.en.md)

## Overview
Defines the frame-driven abstract interface. `IOrigoFrameDriver` is the frame boundary between the host environment and Core — the adapter layer transfers frame control via `DriveFrame(delta)`. The kernel-internal scheduling contract and implementation live in [Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.en.md).

## Included Files

| File | Responsibility |
|------|------|
| `IOrigoFrameDriver.cs` | External frame boundary: `DriveFrame(double delta)` |

## Interface Members

### IOrigoFrameDriver

| Member | Description |
|------|------|
| `DriveFrame(double delta)` | Frame boundary entry. Core internal order: entity processing → business queue → kill entities → system queue → console pump |

## Design Decisions

### Why the frame driver is independent of the scheduling implementation

`IOrigoFrameDriver` is the externally exposed frame-boundary abstraction;
adapter layers hand over frame control through it without knowing the internal
queue order or entity-processing pipeline. The scheduling queue contract and
implementation are kernel-internal and live in
[Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.en.md).
The two responsibilities are orthogonal: frame boundary and queue management.

### Why there is no ability to cancel a single action
In the single-threaded frame-loop model, actions queued within a frame are generally one-shot lightweight transactions that do not need cancellation. Conditional execution should be decided by the strategy before enqueuing, or handled by an early exit inside the action. A cancellation mechanism would significantly increase queue implementation complexity without solving a real business problem.

---
[↑ Back to Abstractions](../README.en.md)
