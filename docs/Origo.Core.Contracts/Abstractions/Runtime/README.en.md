<!-- docsync-pair: Origo.Core.Contracts/Abstractions/Runtime/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Runtime (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.en.md)

## Overview
Defines the frame-driven and stable host/runtime abstractions. `IOrigoFrameDriver` is the frame boundary between the host environment and Core — the adapter layer transfers frame control via `DriveFrame(delta)`. The kernel-internal scheduling contract and implementation live in [Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.en.md).

## Included Files

| File | Responsibility |
|------|------|
| `IOrigoFrameDriver.cs` | External frame boundary: `DriveFrame(double delta)` |
| `IOrigoRuntime.cs` | Stable host/runtime contract: metadata, logger, world, blackboards, console channels, and sessions |
| `ISndWorldAccess.cs` | Stable SND world access: strategy registration, type mappings, metadata conversion, and data-source gateway |

## Interface Members

### IOrigoFrameDriver

| Member | Description |
|------|------|
| `DriveFrame(double delta)` | Frame boundary entry. Core internal order: entity processing → business queue → kill entities → system queue → console pump |

### IOrigoRuntime

| Member | Description |
|--------|-------------|
| `Meta` | Framework metadata |
| `Logger` | Runtime logger |
| `SndWorld` | Stable SND world access surface |
| `ConsoleInput` | Console input queue; null when the host did not inject one |
| `ConsoleOutputChannel` | Console output channel; null when the host did not inject one |
| `SessionManager` | Current session manager |
| `RegisterConsoleCommandHandler(handler)` | Registers an `IConsoleCommandHandler` through the runtime console router; fails explicitly when either console channel is missing |

### ISndWorldAccess

| Member | Description |
|--------|-------------|
| `ConverterRegistry` | Typed data-source converter registry |
| `DataSourceIo` | Data-source I/O gateway |
| `GetRegisteredStrategyIndices()` | Returns all registered strategy indices |
| `IsStrategyRegistered(index)` | Returns whether a strategy index is registered |
| `RegisterStrategy<TStrategy>(factory)` | Registers a strategy factory; fails after Bootstrap freezes registration |
| `RegisterTypeMappings(registerMappings)` | Adds stable type-name mappings |
| `CloneMetaData(meta)` | Deep-clones entity metadata |
| `ResolveTemplate(templateAlias)` | Resolves a template alias |
| `ReadMetaNode(node)` / `ReadMetaListNode(node)` | Reads one entity or a metadata list |
| `WriteMetaNode(meta)` / `WriteMetaListNode(metaDataList)` | Writes one entity or a metadata list |
| `ReadTypedDataMap(node)` | Reads a typed-data map |

## Design Decisions

### Why the system blackboard has one shell access path

`IOrigoRuntime` does not duplicate the system blackboard; the public entry is
`ISndContext.Blackboard.SystemBlackboard` (and its `IStateMachineContext` view).
The kernel runtime keeps the same instance internally, so state changes,
persistence, and lifecycle side effects are implemented once and every view
observes the same state.

### Why metadata-list resolution lives only on `ISndTemplateAccess`

`ISndWorldAccess` does not duplicate `ResolveMetaListFromJsonArray`; template and
entity-list resolution stay on `ISndContext.Template`. The kernel `SndWorld`
implementation remains the single underlying implementation rather than a
second public entry point.

### Why console-handler registration lives on `IOrigoRuntime`

`IConsoleCommandHandler` and `ConsoleCommandHandlerBase` are shell tooling extensions; registration must go through the runtime console router so strategies or adapters cannot parse and execute commands outside the console pump and its argument validation. Core and adapter hosts share the stable `IOrigoRuntime.RegisterConsoleCommandHandler` entry point, and registration fails fast when the host did not inject both console channels.

### Why the frame driver is independent of the scheduling implementation

`IOrigoFrameDriver` is the externally exposed frame-boundary abstraction;
adapter layers hand over frame control through it without knowing the internal
queue order or entity-processing pipeline. The scheduling queue contract and
implementation are kernel-internal and live in
[Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.en.md).
The two responsibilities are orthogonal: frame boundary and queue management.

### Why there is no ability to cancel a single action
In the single-threaded frame-loop model, actions queued within a frame are generally one-shot lightweight transactions that do not need cancellation. Conditional execution should be decided by the strategy before enqueuing, or handled by an early exit inside the action. A cancellation mechanism would significantly increase queue implementation complexity without solving a real business problem.

---
[↑ Back to Abstractions](../README.en.md)
