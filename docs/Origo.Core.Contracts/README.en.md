<!-- docsync-pair: Origo.Core.Contracts/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Origo.Core.Contracts

> [↑ Back to Origo Manual](../README.en.md)

## Module Overview

**Origo.Core.Contracts** is Origo's stable consumer contract package: platform-
independent public interfaces, pure data, and extension contracts without
runtime construction, persistence, console routing, or Godot dependencies.
Shell and kernel packages reference this contract layer so consumers can compile
against it.

## Subsystems

| Subsystem | Capability | Details |
|-----------|------------|---------|
| [Abstractions](Abstractions/README.en.md) | Platform-independent base abstractions | Logging, console input/output, file-system/path, node, and frame-driver contracts |
| [Runtime](Runtime/README.en.md) | Runtime tooling extension contracts | Console handler, invocation model, and argument-validation base |

## Files at This Level

| File | Responsibility |
|------|----------------|
| `OrigoMeta.cs` | Framework metadata: name, version, and default banner text |

## Architecture Constraints

- **No engine dependency**: uses only `System.*` and the .NET BCL; references no Godot or adapter assembly.
- **Stable contracts only**: interfaces, pure data, extension bases, and tooling contracts; concrete implementations stay in kernel or shell packages.
- **One dependency direction**: `Origo.Core.Contracts` references neither `Origo.Core`, `Origo.Core.Kernel`, `Origo.GodotAdapter`, nor `Origo.ConsoleBridge`.

## Dependency Direction

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel
        ▲
        │
Origo.Core ──► Origo.SourceGeneration (analyzer)
        ▲
        │
Origo.GodotAdapter
```

Adapters and implementations depend on the contract layer; the contract layer
never depends on an implementation.

---
[↑ Back to Origo Manual](../README.en.md)
