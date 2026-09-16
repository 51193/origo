<!-- docsync-pair: Origo.Core/Runtime/Console/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Console

> [↑ Back to Runtime](../README.en.md)

## Module Capabilities
Origo's runtime console command system. Provides command parsing (positional + named args), command routing, and input/output channels (queue + publish-subscribe).

## Sub-modules

| Sub-module | Capability | Details |
|--------|------|------|
| [CommandHandlers](CommandHandlers/README.en.md) | 11 built-in command handlers | help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy |

## Core Files

| File | Responsibility |
|------|------|
| `OrigoConsole.cs` | Console facade: Router + Input + OutputChannel + Parser |
| `ConsoleCommandRouter.cs` | Command routing: name → IConsoleCommandHandler registration and lookup |
| `ConsoleCommandParser.cs` | Command parsing: string → CommandInvocation (positional + named args), `internal` |
| `ConsoleCommandHandlerBase.cs` | Handler base: Name/HelpText/arg validation/execution |
| `ConsoleMessages.cs` | User-facing console message constants (English, `internal`), referenced by both production handlers and test assertions to avoid hardcoded literals. Currently contains `InvalidArgumentCount` |
| `CommandInvocation.cs` | Invocation model: Command + PositionalArgs + NamedArgs |
| `IConsoleCommandHandler.cs` | Handler interface: Name + HelpText + TryExecute |
| `ConsoleInputBuffer.cs` | Thread-safe input queue (Enqueue/TryDequeue/Clear) |
| `ConsoleOutputChannel.cs` | Subscribe/publish output channel |
| `ConsoleCommandHelper.cs` | internal utility class: entity lookup `TryFindEntity`, blackboard layer resolution, type inference |

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
- **Named arg support**: besides positional args, `key=value` named args are supported (e.g. `spawn name=x template=y`). The two modes cannot be mixed. Duplicate named args (e.g. `name=a name=b`) are rejected with an error (fail-fast, no silent override)
- **Pre-validation**: `ConsoleCommandHandlerBase.TryExecute` validates the argument count before execution, returning a clear error on failure
- **Thread-safe input**: `ConsoleInputBuffer` is `lock`-protected, supporting concurrent enqueue from the TCP bridge thread
- **Immediate exception propagation**: internal `ProcessPending()` does not catch exceptions thrown by command handlers. If a handler throws while executing (e.g. `InvalidOperationException`), the exception propagates directly to the frame-driver caller — not degraded to a log or error message. This ensures bugs surface early in development
- **Unique command names**: `ConsoleCommandRouter.Register` requires globally unique command names. Registering a handler with a name already taken by an existing handler throws `InvalidOperationException`
- **Output listener isolation**: `ConsoleOutputChannel.Publish()` wraps each subscriber's invocation in try-catch. If a single listener throws, subsequent subscribers still receive the output line. After all subscribers have been invoked, the first exception is rethrown to preserve fail-fast. This ensures output is never silently lost because of a single faulty listener

---
[↑ Back to Runtime](../README.en.md)
