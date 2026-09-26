<!-- docsync-pair: Origo.Core.Contracts/README -->
<!-- docsync-revision: 10 -->
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
| [Abstractions](Abstractions/README.en.md) | Platform-independent base abstractions | Logging, console I/O, file-system/path, node, frame-driver, lifecycle, entity, scene, and state-machine contracts |
| [Runtime](Runtime/README.en.md) | Runtime contracts and tooling | `IOrigoRuntime`/`ISndWorldAccess`, console handler/invocation model, and argument validation |
| [Blackboard](Blackboard/README.en.md) | Shared blackboard implementation | In-memory `IBlackboard` implementation used by shell and kernel |
| [Grid](Grid/README.en.md) | Grid value types | `GridPos` |
| [Logging](Logging/README.en.md) | Shared logging implementation | `Logger<T>`, `LogMessageBuilder`, and `NullLogger` |
| [Planning](Planning/README.en.md) | Planning strategy base | `PlanExecutionStrategyBase` |
| [Serialization](Serialization/README.en.md) | Shared type mapping | `TypeStringMapping` |
| [DataSource](DataSource/README.en.md) | Data-source contracts | Tree data node, I/O gateway, file-meta contract, converter bases, and registry |
| [Snd](Snd/README.en.md) | SND data contracts | TypedData, metadata, ISndContext, strategy bases, and internal entity query contracts |
| [Save](Save/README.en.md) | Save contracts | Display-meta contributors and save-slot entries |
| [StateMachine](StateMachine/README.en.md) | State-machine contracts | Strategy base and per-operation context |
| [Utility](Utility/README.en.md) | Shared pure utilities | `PathUtility` and internal value inference |

## Files at This Level

| File | Responsibility |
|------|----------------|
| `OrigoMeta.cs` | Framework metadata: name, version, and default banner text |
| `OrigoHostOptions.cs` | Stable configuration model for the Core shell host facade |
| `AssemblyAttributes.cs` | `[assembly: SndInlineTypes(...)]` home-host inline type registration: system primitives and string supported by TypedData |

## Architecture Constraints

- **No engine dependency**: uses only `System.*` and the .NET BCL; references no Godot or adapter assembly.
- **Stable contracts only**: interfaces, pure data, extension bases, and tooling contracts; concrete implementations stay in kernel or shell packages.
- **One dependency direction**: `Origo.Core.Contracts` references neither `Origo.Core`, `Origo.Core.Kernel`, `Origo.GodotAdapter`, nor `Origo.ConsoleBridge`.

## Dependency Direction

```
Origo.Core.Contracts ──► Origo.SourceGeneration (analyzer packaging)
        ▲
        ├──────────────────────┐
        │                      │
Origo.Core.Kernel      Origo.ConsoleBridge shell
        ▲
        │ runtime-only
Origo.Core shell
        ▲
        │
Origo.GodotAdapter
```

Adapters, the console bridge, and implementations depend on the contract
layer; the contract layer never depends on an implementation.

---
[↑ Back to Origo Manual](../README.en.md)
