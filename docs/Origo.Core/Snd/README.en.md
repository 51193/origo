<!-- docsync-pair: Origo.Core/Snd/README -->
<!-- docsync-revision: 24 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Snd (Shell Helpers)

> [↑ Back to Origo.Core](../README.en.md)

## Module Capability

Consumer-facing SND shell helpers. The SND context, world, entity, scene, and
strategy implementations live in
[Origo.Core.Kernel/Snd](../../Origo.Core.Kernel/Snd/README.en.md); stable
strategy/meta contracts live in
[Origo.Core.Contracts/Snd](../../Origo.Core.Contracts/Snd/README.en.md).

## Sub-Modules

| Sub-Module | Capability | Details |
|-----------|-----------|---------|
| [Strategy](Strategy/README.en.md) | Strategy shell extensions | `EnsureReplaceableStrategy` |
| [Archetype](Archetype/README.en.md) | Numeric recipe loading | `SndArchetypeLoader` key-value parsing and typed-data application |

## This Layer's Core Files

| File | Responsibility |
|------|---------------|
| `ActiveStrategyExtensions.cs` | Generic active-strategy invocation and idempotent `EnsureStrategy` |
| `EntityExtensions.cs` | Entity identity comparison across inner/wrapper references |
| `TryGetNumericExtensions.cs` | Numeric-coercion reads over `ISndDataAccess` |

## Architecture Notes

- These helpers compile against Contracts only and do not expose kernel implementation types.
- `ISndContext`, entity role interfaces, and metadata live in Contracts.

---
[↑ Back to Origo.Core](../README.en.md)
