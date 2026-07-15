<!-- docsync-pair: Origo.Core/Abstractions/Console/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Console (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Runtime/Console](../../Runtime/Console/README.en.md)

## Overview
Defines the input/output abstraction between the Core layer and external console systems. Core does not directly depend on a concrete console implementation. The adapter layer dispatches commands via `Enqueue`, and Core consumes them per-frame via `TryDequeueCommand`.

## Included Files

| File | Responsibility |
|------|------|
| `IConsoleInputSource.cs` | Bidirectional command input abstraction: adapter dispatches commands (Enqueue), Core consumes per-frame (TryDequeueCommand) |
| `IConsoleOutputChannel.cs` | Publish-subscribe output channel, supporting multiple listeners |

## Interface Details

### IConsoleInputSource

| Member | Description |
|------|------|
| `TryDequeueCommand(out string? line)` | Non-blocking retrieval of a command line; returns false when queue is empty |
| `Enqueue(string line)` | Append a command line to the queue; blank lines are silently ignored |
| `Clear()` | Clear all pending commands in the queue |

### IConsoleOutputChannel

| Member | Description |
|------|------|
| `Subscribe(Action<string>)` | Register an output listener, returns a subscription ID |
| `Unsubscribe(long subscriptionId)` | Unsubscribe; returns false if ID does not exist |
| `Publish(string line)` | Publish an output message to all listeners |

## Design Decisions

### Why Enqueue and Clear are also on the interface
Originally `IConsoleInputSource` only exposed `TryDequeueCommand`, forcing adapters and ConsoleBridge to depend on the concrete class `ConsoleInputBuffer` to call `Enqueue` and `Clear`, causing an abstraction leak. Promoting these two methods to the interface eliminates compile-time dependency on the concrete class.

### Why input uses polling rather than events
Origo adopts a single-threaded frame loop model. The polling-style `TryDequeueCommand` processes commands at a deterministic point within the frame, avoiding unpredictable execution order and potential recursion introduced by event callbacks.

### Why output uses publish-subscribe
Console output may have multiple consumers (log file writing, screen rendering, remote forwarding). The publish-subscribe pattern lets Core operate without knowing the number or types of consumers.

---
[↑ Back to Abstractions](../README.en.md)
