<!-- docsync-pair: Origo.Core.Contracts/Runtime/Console/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Console (Contracts)

> [↑ Back to Runtime](../README.en.md) · [↔ Implementation: Origo.Core/Runtime/Console](../../../Origo.Core/Runtime/Console/README.en.md)

## Module Capability

Tooling extension contracts for console command handling: the handler interface,
the invocation model, and the argument-validation base. Core routing, game
code, and GodotAdapter command handlers compile against these contracts; the
actual command pump remains driven by `IOrigoFrameDriver.DriveFrame`.

## Included Files

| File | Responsibility |
|------|----------------|
| `IConsoleCommandHandler.cs` | Handler interface: Name / HelpText / argument bounds / TryExecute |
| `CommandInvocation.cs` | Invocation model: Command + PositionalArgs + NamedArgs |
| `ConsoleCommandHandlerBase.cs` | Handler base: positional-argument validation and ExecuteCore dispatch |
| `ConsoleMessages.cs` | internal constants: shared user-facing error messages |

## Design Decisions

### Why argument validation lives in the base class

`ConsoleCommandHandlerBase.TryExecute` validates positional argument counts
before execution and returns a clear error including the help text. Centralizing
validation avoids repeating it in every handler and keeps fail-fast behavior
consistent for all derived commands.

### Why the invocation model is pure data

`CommandInvocation` carries only the parsed command name, positional arguments,
and named arguments; it does not parse, route, or execute command side effects.
Parsing remains in the Core parser and routing in the Core router, keeping the
contract layer dependency-free.

---
[↑ Back to Runtime](../README.en.md)
