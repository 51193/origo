<!-- docsync-pair: Origo.Core.Kernel/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Origo.Core.Kernel

> [↑ Back to Origo Manual](../README.en.md)

## Module Overview

**Origo.Core.Kernel** is Origo's kernel implementation package. It references
only `Origo.Core.Contracts` and carries runtime construction, SND internals,
persistence/storage, data-source codecs and factory, console routing, vendored
noise, deferred scheduling, and the internal `Origo.Core.Kernel.Ports`.
Kernel compile assets never enter the Core shell consumer compilation surface.

## Subsystems

| Subsystem | Capability | Details |
|-----------|------------|---------|
| [Abstractions](Abstractions/README.en.md) | Internal framework contracts | Entity lifecycle, node host, scene host/access, and session binding |
| [DataSource](DataSource/README.en.md) | Data-source implementation | JSON/Map codecs, factory, I/O gateway, converters, and path/file access |
| [Runtime](Runtime/README.en.md) | Runtime lifecycle and console | System/Progress/Session layers, frame queues, and console routing |
| [Save](Save/README.en.md) | Persistence implementation | Save coordinator, payloads, strict readers, atomic writes, and storage layout |
| [Snd](Snd/README.en.md) | SND implementation | Context/world, entity aggregate, scene host, strategy pool/managers, and observer topology |
| [StateMachine](StateMachine/README.en.md) | State-machine implementation | Stack machine and persistence models |
| [Ports](Ports/README.en.md) | Kernel-shell ports | Internal host-construction ports exposed to shell assemblies via `InternalsVisibleTo` |
| [Scheduling](Scheduling/README.en.md) | Deferred scheduling implementation | `IScheduler` + `ActionScheduler` + `ConcurrentActionQueue` |
| [Addons](Addons/README.en.md) | Vendored third-party libraries | FastNoiseLite noise implementation |

## Files at This Level

This directory contains only sub-directories and no direct `.cs` files.

## Architecture Constraints

- **Contracts-only dependency**: Kernel references neither `Origo.Core`, `Origo.GodotAdapter`, `Origo.ConsoleBridge`, nor any Godot assembly.
- **No consumer compile assets**: kernel types do not enter the Core shell consumer compilation surface; consumers use Contracts and the Core shell facade.
- **Internal ports**: `Origo.Core.Kernel.Ports` members are internal and exposed to shell assemblies and tests only through `InternalsVisibleTo`.
- **Package dependency boundary**: `Origo.Core` references Kernel with `PrivateAssets="compile"`, so the NuGet dependency carries runtime/build/native assets but no compile assets.

## Dependency Direction

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel ◄── runtime-only ── Origo.Core shell
```

---
[↑ Back to Origo Manual](../README.en.md)
