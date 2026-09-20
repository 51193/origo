<!-- docsync-pair: Origo.Core.Contracts/Abstractions/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Abstractions

> [↑ Back to Origo.Core.Contracts](../README.en.md)

## Module Capability

Base abstraction layer of Origo.Core.Contracts: leaf interfaces implemented by
platform hosts. Every interface uses only `System.*` types and references no Core
runtime implementation or concrete adapter.

## Sub-modules

| Sub-module | Capability | Details |
|------------|------------|---------|
| [Console](Console/README.en.md) | Console input/output abstraction | `IConsoleInputSource` + `IConsoleOutputChannel` |
| [FileSystem](FileSystem/README.en.md) | Platform file-system and path abstraction | `IFileSystem` + `IPathResolver` |
| [Logging](Logging/README.en.md) | Engine-agnostic logging interfaces | `ILogger` + `ILogger<TCategory>` + `LogLevel` |

## Files at This Level

This directory contains only sub-directories and no direct `.cs` files.

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
