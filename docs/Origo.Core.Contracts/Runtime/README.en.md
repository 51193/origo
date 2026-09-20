<!-- docsync-pair: Origo.Core.Contracts/Runtime/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Runtime

> [↑ Back to Origo.Core.Contracts](../README.en.md)

## Module Capability

Runtime extension-contract layer. It currently contains console command
tooling extensions: the handler interface, invocation model, and argument
validation base. Core implementations, GodotAdapter, and game code implement or
derive from these contracts without depending on runtime implementation.

## Sub-modules

| Sub-module | Capability | Details |
|------------|------------|---------|
| [Console](Console/README.en.md) | Console tooling extension contracts | `IConsoleCommandHandler` + `CommandInvocation` + `ConsoleCommandHandlerBase` |

## Files at This Level

This directory contains only sub-directories and no direct `.cs` files.

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
