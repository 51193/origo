<!-- docsync-pair: Origo.Core/DataSource/Converters/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Converters

> [↑ Back to DataSource](../README.en.md)

## Overview
Collection of registered `DataSourceConverter<T>` implementations. All converters are `internal`, managed by `DataSourceConverterRegistry`.

## Included Files

| File | Responsibility |
|------|------|
| `PrimitiveConverters.cs` | 14 primitive type converters (string, byte, int, float, bool, etc.) |
| `ArrayConverters.cs` | 14 primitive array converters (byte[], int[], float[], etc.) |
| `BlackboardDataConverter.cs` | Blackboard ↔ DataSourceNode |
| `DataMetaDataConverter.cs` | DataMetaData ↔ DataSourceNode |
| `NodeMetaDataConverter.cs` | NodeMetaData ↔ DataSourceNode |
| `SndMetaDataConverter.cs` | SndMetaData ↔ DataSourceNode |
| `SndMetaDataListConverter.cs` | SndMetaData list ↔ DataSourceNode |
| `StrategyMetaDataConverter.cs` | StrategyMetaData ↔ DataSourceNode |
| `StateMachineContainerPayloadConverter.cs` | State machine container payload ↔ DataSourceNode |
| `StringDictionaryConverter.cs` | String dictionary ↔ DataSourceNode |
| `TypedDataConverter.cs` | TypedData ↔ DataSourceNode, carrying type metadata |

## Converter Overview

### PrimitiveConverters
`string`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `char`, `bool`

### ArrayConverters
One per primitive type array. Read iterates `node.Elements`; Write builds `CreateArray()`. `string[]` element reads share the strict semantics of `Read<string>`: a null element throws `InvalidOperationException` (no silent drift to an empty string, so corrupt saves fail at the converter layer).

### Domain converters

| Converter | Handled Type |
|------|------|
| `NodeMetaDataConverter` | Node metadata |
| `StrategyMetaDataConverter` | Strategy index list |
| `DataMetaDataConverter` | Entity data (depends on TypedDataConverter) |
| `SndMetaDataConverter` | SND entity metadata |
| `SndMetaDataListConverter` | Entity metadata list |
| `BlackboardDataConverter` | Full blackboard data |

**Node-shape validation**: domain converters validate root and collection field shapes on read — object fields (`pairs`, string dictionaries, blackboard dictionaries, SndMetaData) must be Maps, and array fields (state-machine `stack`, strategy-index lists, `observer_indices`, SndMetaData lists) must be Arrays. A wrong shape throws `InvalidOperationException` instead of silently becoming an empty collection; an empty observer target is rejected rather than silently dropping the binding.

### TypedDataConverter
Reads/writes `"type"` and `"data"` fields. On read, resolves CLR type from `"type"` via `TypeStringMapping`, then uses corresponding converter. The registry backtracks along base class and interface chains when no exact type match exists — e.g. storing a `ReadOnlyDictionary<string,string>` with only an `IReadOnlyDictionary<string,string>` converter registered. This allows registering converters for interface types while still storing and reading their concrete implementations: `StringDictionaryConverter` returns a `ReadOnlyDictionary<string,string>` instance (compatible with the requested type). If the converter's returned instance is incompatible with the requested type (e.g. requesting `SortedDictionary`), the read throws `InvalidOperationException` immediately (fail-fast, naming both the converter and the requested type rather than an opaque `InvalidCastException`), preventing a silently type-drifted value from breaking a later serialization. String map values must be scalar strings: a null value throws `InvalidOperationException` (consistent with `Read<string>` rejecting null nodes — no silent drift to an empty string). The node-pair map (`NodeMetaDataConverter`) follows the same strict semantics: a null pair value fails the read instead of drifting into an empty resource path.

## Design Decisions

### Why each primitive type has its own converter
Avoids reflection-based generic instantiation. Each explicitly implemented, statically enumerable.

### Why array converters are separate
Arrays need traversal semantics (foreach over `Elements`), significantly different from scalars.

### Why domain converters are split into one file per type
One file per type keeps lookup and maintenance cheap; hierarchical dependencies stay visible through explicit constructor injection.

---
[↑ Back to DataSource](../README.en.md)
