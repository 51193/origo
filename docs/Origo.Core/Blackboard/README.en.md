<!-- docsync-pair: Origo.Core/Blackboard/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Blackboard

> [↑ Back to Origo.Core](../README.en.md) · [↔ Abstractions: Blackboard](../Abstractions/Blackboard/README.en.md) · [Related Tests: Blackboard](../../Origo.Core.Tests/Blackboard.en.md)

## Overview

The default in-memory implementation of the `IBlackboard` interface. Uses `Dictionary<string, TypedData>` as the underlying storage with `StringComparer.Ordinal` key comparison, ensuring case-sensitive exact matching of key names.

## Included Files

| File | Responsibility |
|------|---------------|
| `Blackboard.cs` | `sealed` implementation of `IBlackboard`; key-value pairs preserve type through `TypedData` |

## Implementation Details

- **Key validation**: Both `SetValue` and `TryGet` perform defensive checks on empty/null keys, throwing `ArgumentException`
- **SetValue**: Creates a value type instance via `TypedDataFactory<T>.Create(value)` and stores it in the dictionary; the generic type is captured at compile time
- **TryGet**: First looks up the `TypedData`, then extracts and validates the runtime type via `TypedDataFactory<T>.TryExtract(td, out var value)`
- **SerializeAll/DeserializeAll**: Full export/import, no incremental merging. `DeserializeAll` clears first then populates — replacement semantics

## Design Decisions

### Why sealed class

`TypedData` is a `readonly partial struct`; as a value type, it is stored inline in the dictionary, avoiding heap allocation and GC pressure. The struct only contains a type metadata handle and an `object` reference for the actual data — copy cost is minimal. Scenarios requiring customized blackboard behavior should use composition (wrapping a new `IBlackboard` implementation) or the decorator pattern rather than inheritance.

### Why use Ordinal comparison

Key names are constants hardcoded in code (e.g., `"core.player.health"`), with no culture-related sorting involved. `Ordinal` provides the fastest string comparison performance.

### Why DeserializeAll is replacement rather than merge

Save recovery has "restore from a known state" semantics, not "incremental update." Residual keys would pollute a new session with old save data; replacement semantics are safer.

---
[↑ Back to Origo.Core](../README.en.md)
