<!-- docsync-pair: Origo.Core/Runtime/Console/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
| `ConsoleCommandParser.cs` | Command parsing: string → CommandInvocation |
| `ConsoleCommandHandlerBase.cs` | Handler base: Name/HelpText/arg validation/execution |
| `ConsoleMessages.cs` | User-facing message constants (English) |
| `CommandInvocation.cs` | Invocation model: CommandName + PositionalArgs + NamedArgs |
| `IConsoleCommandHandler.cs` | Handler interface: Name + HelpText + TryExecute |
| `ConsoleInputBuffer.cs` | Thread-safe input queue (Enqueue/TryDequeue/Clear) |
| `ConsoleOutputChannel.cs` | Subscribe/publish output channel |
| `ConsoleCommandHelper.cs` | internal: entity lookup, blackboard layer resolution, type inference |

## Command Lifecycle

```
External input (Godot console / TCP bridge)
    │
    ▼
ConsoleInputBuffer.Enqueue(line)
    │
    ▼
OrigoConsole.ProcessPending()
    ├── TryDequeueCommand → line
    ├── ConsoleCommandParser.Parse(line)
    ├── ConsoleCommandRouter.TryExecute(invocation, outputChannel)
    └── outputChannel.Publish(result)
```

## Design Principles
- **Named arg support**: `key=value` named args (not mixable with positional)
- **Pre-validation**: Arg count validated before execution
- **Thread-safe input**: `lock` protected for TCP bridge concurrent enqueue
- **Immediate exception propagation**: Handler exceptions propagate directly, no degradation
- **Unique command names**: Duplicate handler registration throws `InvalidOperationException`
- **Output listener isolation**: Try-catch per subscriber; first exception rethrown after all invoked

---
[↑ Back to Runtime](../README.en.md)
