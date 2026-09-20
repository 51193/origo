<!-- docsync-pair: Origo.Core.Kernel/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Origo.Core.Kernel

> [↑ Back to Origo Manual](../README.en.md)

## Module Overview

**Origo.Core.Kernel** is Origo's kernel implementation package. It references
only the stable contract package `Origo.Core.Contracts` and carries
implementation that must not enter the consumer compilation surface. It
currently contains the vendored noise implementation and the deferred
scheduling implementation; runtime construction, SND internals, persistence,
and console routing remain in `Origo.Core`.

## Subsystems

| Subsystem | Capability | Details |
|-----------|------------|---------|
| [Addons](Addons/README.en.md) | Vendored third-party libraries | FastNoiseLite noise implementation |
| [Scheduling](Scheduling/README.en.md) | Deferred scheduling implementation | `IScheduler` + `ActionScheduler` + `ConcurrentActionQueue` |

## Files at This Level

This directory contains only sub-directories and no direct `.cs` files.

## Architecture Constraints

- **Contracts-only dependency**: Kernel references neither `Origo.Core`, `Origo.GodotAdapter`, `Origo.ConsoleBridge`, nor any Godot assembly.
- **No consumer compile assets**: kernel types do not enter the shell consumer compilation surface; shell reaches them through Contracts or documented kernel-shell ports.
- **Internal bridge**: scheduling implementations stay internal and are exposed to `Origo.Core` and test assemblies through `InternalsVisibleTo`; runtime construction currently remains in `Origo.Core`.
- **Package dependency boundary**: `Origo.Core` references Kernel with `PrivateAssets="compile"`, so the NuGet dependency carries runtime/build/native assets but no compile assets.

## Dependency Direction

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel
        ▲
        │
Origo.Core (shell / current host facade)
```

---
[↑ Back to Origo Manual](../README.en.md)
