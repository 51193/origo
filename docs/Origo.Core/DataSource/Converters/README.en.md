<!-- docsync-pair: Origo.Core/DataSource/Converters/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
One per primitive type array. Read iterates `node.Elements`; Write builds `CreateArray()`.

### Domain converters

| Converter | Handled Type |
|------|------|
| `NodeMetaDataConverter` | Node metadata |
| `StrategyMetaDataConverter` | Strategy index list |
| `DataMetaDataConverter` | Entity data (depends on TypedDataConverter) |
| `SndMetaDataConverter` | SND entity metadata |
| `SndMetaDataListConverter` | Entity metadata list |
| `BlackboardDataConverter` | Full blackboard data |

### TypedDataConverter
Reads/writes `"type"` and `"data"` fields. On read, resolves CLR type from `"type"` via `TypeStringMapping`, then uses corresponding converter. The registry backtracks along base class and interface chains when no exact type match exists.

## Design Decisions

### Why each primitive type has its own converter
Avoids reflection-based generic instantiation. Each explicitly implemented, statically enumerable.

### Why array converters are separate
Arrays need traversal semantics (foreach over `Elements`), significantly different from scalars.

### Why domain converters are split into one file per type
One file per type keeps lookup and maintenance cheap; hierarchical dependencies stay visible through explicit constructor injection.

---
[↑ Back to DataSource](../README.en.md)
