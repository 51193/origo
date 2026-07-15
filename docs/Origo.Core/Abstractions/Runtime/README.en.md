<!-- docsync-pair: Origo.Core/Abstractions/Runtime/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Runtime (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Scheduling](../../Scheduling/README.en.md)

## Overview
Defines the frame-driven abstract interfaces. `IOrigoFrameDriver` is the frame boundary between the host environment and Core — the adapter layer transfers frame control via `DriveFrame(delta)`. `IScheduler` is Core's internal scheduling queue, marked `internal`.

## Included Files

| File | Responsibility |
|------|------|
| `IOrigoFrameDriver.cs` | External frame boundary: `DriveFrame(double delta)` |
| `IScheduler.cs` | **internal** — Core internal scheduling: Enqueue / Tick / Clear |

## Interface Members

### IOrigoFrameDriver

| Member | Description |
|------|------|
| `DriveFrame(double delta)` | Frame boundary entry. Core internal order: entity processing → business queue → kill entities → system queue → console pump |

### IScheduler (internal)

| Method | Description |
|------|------|
| `Enqueue(Action)` | Schedule action for current frame or later |
| `Tick()` | Execute queued actions |
| `Clear()` | Clear not-yet-executed actions |

## Design Decisions

### Why IOrigoFrameDriver is separate from IScheduler
Orthogonal responsibilities: `IScheduler` manages queues (internal); `IOrigoFrameDriver` defines the frame boundary (external).

### Why IScheduler is internal
No cross-assembly consumers outside Core. Marking internal avoids unnecessary API surface.

---
[↑ Back to Abstractions](../README.en.md)
