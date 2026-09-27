# Snd

> [↑ Back to Origo.Core.Contracts](../README.en.md)

## Module Capability

SND contract layer. It contains the TypedData inline-storage/access model,
the entity metadata model, the stable strategy base classes, internal entity
capability contracts, and the `ISndContext` unified business facade interface.

## Sub-modules

| Sub-module | Capability | Details |
|------------|------------|---------|
| [Entity](Entity/README.en.md) | Internal entity capability contracts | `ISndEntityRawSubscription` + `ISndEntityStrategyQuery` |
| [Metadata](Metadata/README.en.md) | TypedData and entity metadata model | TypedData / SndMetaData / NodeMetaData / StrategyMetaData / DataMetaData / SndMetaFluentBuilder |
| [Strategy](Strategy/README.en.md) | Stable strategy base classes and attributes | BaseStrategy / LifecycleStrategyBase / ActiveStrategyBase / ActiveStrategyJsonBase / ObserverStrategyBase / ObserveDataAttribute / StrategyIndexAttribute |

## Files at This Level

| File | Responsibility |
|------|----------------|
| `ISndContext.cs` | Unified SND business facade: Bootstrap + 10 companion properties |

This directory also contains the Entity, Metadata, and Strategy sub-directories.

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
