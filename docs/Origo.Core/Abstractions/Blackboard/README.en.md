<!-- docsync-pair: Origo.Core/Abstractions/Blackboard/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Blackboard (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Blackboard](../../Blackboard/README.en.md)

## Overview

Defines the generic key-value blackboard interface `IBlackboard`, providing global/progress/session-level shared state for the Core layer. All blackboard operations maintain type safety through generic methods, internally using `TypedData` to preserve type information and ensure types are not lost after serialization/deserialization.

## Included Files

| File | Responsibility |
|------|------|
| `IBlackboard.cs` | Core blackboard interface: SetValue/Get/Clear/Keys + Serialize/Deserialize |

## Interface Members

| Method | Description |
|------|------|
| `SetValue<T>(string key, T value)` | Write a key-value pair, preserving full type information |
| `TryGet<T>(string key)` | Safe read: returns a `(found, value)` tuple |
| `Clear()` | Clear all key-value entries |
| `GetKeys()` | Enumerate all key names |
| `SerializeAll()` | Export all entries as a `TypedData` dictionary for persistence |
| `DeserializeAll(...)` | Restore all entries from a persistence dictionary, replacing current contents |

## Design Decisions

### Why use TypedData to wrap values
Storing values directly as `object` would lose precise types during serialization (e.g., confusing `int` and `float`). `TypedData` is a `readonly partial struct` that carries type metadata as an inline value type during storage, avoiding heap allocation overhead while enabling precise restoration of original types after JSON deserialization. This is critical for numeric-sensitive game logic such as damage calculations.

### Why use generic TryGet rather than object
`TryGet<T>` forces the caller to declare the expected type, catching type mismatches at compile time. Additionally, the `(found, value)` tuple pattern avoids the ambiguity of `null` checking — `default(T)` for value types cannot be distinguished from "key not found," so the `found` flag is mandatory for disambiguation.

### Why not provide event subscriptions
The blackboard is a pure data container. Change notification responsibility belongs to upper-layer modules (such as `SndDataManager`'s data observers). Adding events at the blackboard layer would introduce unnecessary coupling and lifecycle management burden.

---
[↑ Back to Abstractions](../README.en.md)
