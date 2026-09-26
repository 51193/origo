<!-- docsync-pair: Origo.Core.Kernel/Runtime/Console/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Console

> [↑ Back to Runtime](../README.en.md) · [↔ Tooling contracts: Origo.Core.Contracts/Runtime/Console](../../../Origo.Core.Contracts/Runtime/Console/README.en.md)

## Module Capabilities
Origo's runtime console command-system implementation. Provides command parsing
(positional + named args), command routing, and input/output channels (queue +
publish-subscribe). The handler interface, invocation model, and argument
validation base live in
[Origo.Core.Contracts/Runtime/Console](../../../Origo.Core.Contracts/Runtime/Console/README.en.md).

## Sub-modules

| Sub-module | Capability | Details |
|--------|------|------|
| [CommandHandlers](CommandHandlers/README.en.md) | 11 base command handlers + 5 persistence command handlers | OrigoConsole registers help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy; SndContext registers list_saves / save / load / delete_save / switch_level |

## Core Files

| File | Responsibility |
|------|----------------|
| `OrigoConsole.cs` | Console facade: Router + Input + OutputChannel + Parser |
| `ConsoleCommandRouter.cs` | Command routing: name → IConsoleCommandHandler registration and lookup |
| `ConsoleCommandParser.cs` | Command parsing: string → CommandInvocation, `internal` |
| `ConsoleInputBuffer.cs` | Thread-safe input queue (Enqueue/TryDequeue/Clear) |
| `ConsoleOutputChannel.cs` | Subscribe/publish output channel |
| `ConsoleCommandHelper.cs` | internal utility: entity lookup, blackboard layer resolution, type inference |

## Command Lifecycle

```
External input (Godot console / TCP bridge)
    │
    ▼
ConsoleInputBuffer.Enqueue(line)
    │
    ▼
IOrigoFrameDriver.DriveFrame(delta) → OrigoConsole.ProcessPending() (internal)
    ├── TryDequeueCommand → line
    ├── ConsoleCommandParser.Parse(line)
    │   └── CommandInvocation { Command, PositionalArgs, NamedArgs }
    ├── ConsoleCommandRouter.TryExecute(invocation, outputChannel)
    │   └── handler.TryExecute(invocation, outputChannel)
    └── outputChannel.Publish(result)
```

## Design Principles
- **Named arg support**: besides positional args, `key=value` named args are supported; the two modes cannot be mixed, and duplicate named args are rejected.
- **Pre-validation**: the base class lives in Contracts and validates argument counts before execution.
- **Thread-safe input**: `ConsoleInputBuffer` is `lock`-protected.
- **Immediate exception propagation**: internal `ProcessPending()` does not catch handler exceptions; they propagate to the frame-driver caller.
- **Unique command names**: duplicate registration throws `InvalidOperationException`.
- **Output listener isolation**: `ConsoleOutputChannel.Publish()` isolates each subscriber, then rethrows the first failure after all subscribers ran, preserving fail-fast without silently losing output.

---
[↑ Back to Runtime](../README.en.md)
