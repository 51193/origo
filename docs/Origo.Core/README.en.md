<!-- docsync-pair: Origo.Core/README -->
<!-- docsync-revision: 21 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Origo.Core

> [↑ Back to Origo Manual](../README.en.md)

## Module Overview

**Origo.Core** is the consumer shell package. It contains the platform-agnostic
shell facade (`OrigoHost`), grid/random utilities, and SND
extension helpers; runtime construction, SND internals, persistence, codecs,
and console routing live in [Origo.Core.Kernel](../Origo.Core.Kernel/README.en.md).
Stable contracts and shared pure helpers live in
[Origo.Core.Contracts](../Origo.Core.Contracts/README.en.md).

## Subsystem Overview

| Subsystem | Capability | Details |
|-----------|------------|---------|
| [Contracts](../Origo.Core.Contracts/README.en.md) | Stable contracts | Interfaces, pure data, strategy bases, metadata, and shared shell helpers |
| [Kernel](../Origo.Core.Kernel/README.en.md) | Kernel implementation | Runtime, SND, save/storage, data source, console routing, and kernel ports |
| [Grid](Grid/README.en.md) | Grid utilities | Grid coordinate conversion, A* pathfinding, and coordinate parsing; `GridPos` is a Contracts type |
| [Random](Random/README.en.md) | Random utilities | XorShift128+ PRNG, persistent random, and noise map generation |
| [Snd](Snd/README.en.md) | SND shell helpers | Active-strategy, entity-identity, numeric-read, and archetype extensions |

## This Layer's Files

| File | Responsibility |
|------|----------------|
| `OrigoHost.cs` | Consumer shell host facade; creates the kernel runtime/SND context through internal kernel ports and exposes stable interfaces |

## Core Shell Workflow

A consumer that references only the Core shell package can start a host and run
a strategy without compiling against kernel implementation types:

```csharp
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;

var host = OrigoHost.Create(new OrigoHostOptions
{
    Meta = new OrigoMeta("MyGame", "1.0.0", OrigoMeta.DefaultBanner),
    Logger = NullLogger.Instance,
    AutoDiscoverStrategies = false,
});

host.Runtime.SndWorld.RegisterStrategy(() => new CounterStrategy());
host.DriveFrame(1.0 / 60.0);
```

`host.Context` exposes the stable `ISndContext` capability facets; `host.Runtime`
exposes `IOrigoRuntime` and `ISndWorldAccess`. Supply
`OrigoHostOptions.FileSystem` and call `host.Bootstrap()` when the workflow
needs entry-config or save files; strategy registration and frame driving work
without file access.

## Architecture Constraints

- **No engine dependency**: Core references no Godot or adapter assembly.
- **Stable compile surface**: public signatures use Contracts or Core shell types only; kernel compile assets never flow to consumers.
- **Runtime-only kernel dependency**: `Origo.Core` references `Origo.Core.Kernel` with `PrivateAssets="compile"`.
- **Single access path**: consumers start through `OrigoHost` and use Contracts interfaces; kernel ports stay internal.

## Dependency Direction

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel ◄── runtime-only ── Origo.Core shell
```

---
[↑ Back to Origo Manual](../README.en.md)
