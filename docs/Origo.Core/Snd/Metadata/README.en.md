<!-- docsync-pair: Origo.Core/Snd/Metadata/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Metadata

> [↑ Back to Snd](../README.en.md)

## Overview

The data model for SND entity metadata. These models are pure data containers with no business logic, serving solely as the standard contract for passing entity information between subsystems of the Core layer. Used for serialization/deserialization, network transmission, and save storage.

## Included Files

| File | Responsibility |
|------|---------------|
| `TypedData.cs` | Inline storage struct with type information (`readonly partial struct`), includes `RegisterKind()` method |
| `SndMetaFluentBuilder.cs` | SND entity metadata fluent builder, eliminating `??= new DataMetaData()` and manual `new TypedData(...)` boilerplate |
| `SndInlineTypesAttribute.cs` | Assembly-level attribute declaring which types are stored inline in TypedData + optional `StartKind` offset |
| `TypedDataLayeredRegistry.cs` | Multi-layer adapter extension points: KindResolver / FromObject / ToObject delegate chain registration |
| `DataMetaData.cs` | Entity data dictionary container |
| `NodeMetaData.cs` | Node metadata (logical name → resource ID mapping) |
| `StrategyMetaData.cs` | Strategy index lists |
| `SndMetaData.cs` | Entity metadata aggregate: Name + Node + Strategy + Data |

## Model Details

### TypedData

```csharp
public readonly partial struct TypedData : IEquatable<TypedData>
{
    internal byte _kind;          // Type discriminant value (0 = Null)
    internal long _inlineBits;    // Value type inline storage (≤ 8 bytes)
    internal object? _ref;        // Reference type or large value type fallback

    public Type DataType { get; }
    public object? Data { get; }
    public bool IsNull { get; }
}
```

The core type-preserving inline storage mechanism of the SND system. Value types (`int`, `float`, `bool`, etc.) are stored directly in the struct's `_inlineBits` field, with zero boxing and zero heap allocation. Reference types (`string`) are stored in the `_ref` field with no additional wrapping.

`TypedData` is a partial struct enhanced by a Source Generator — compile-time generated code provides strongly-typed accessors for each registered type (`AsInt32()` / `TryGetSingle(out v)` etc.), explicit conversion operators, and generic factory classes (`TypedDataFactory<T>`). See [Origo.SourceGeneration](../../../Origo.SourceGeneration/README.en.md).

Type discrimination is implemented via the `_kind` field. Discriminant value `0` is the `Null` sentinel (`default(TypedData)`). The `DataType` property returns the corresponding `System.Type` from the generated lookup table based on `_kind`.

`TypedData.RegisterKind(byte kind, Type type)` allows adapter layers to register their own types into the global `KindTypeMap[256]` via `[ModuleInitializer]`. The Core layer's 13 primitive types and the GodotAdapter layer's 14 engine types reside in different Kind ranges (1–13 and 128–141) with no conflict.

At serialization boundaries (during deserialization), construction occurs through internal pipelines of `TypedDataConverter.Read()`, `TypedDataTypeMap.GetKindForType`, and `TypedDataObjectConverter.FromObject`. Registered types go through inline storage; unregistered types and adapter-layer types fall back to `_ref`. `TypedDataLayeredRegistry` provides a chained callback mechanism, enabling adapter-layer conversion logic to be inserted into `TypedDataObjectConverter.ToObject` / `FromObject` switch dispatch.

### TypedData Access Modes and Recommended Usage

`TypedData` provides the following read modes with different performance characteristics:

| Mode | Prerequisite | Boxing | Applicable |
|------|-------------|--------|------------|
| Generated strongly-typed accessors like `TryGetInt32(out int)` / `TryGetString(out string)` / `AsXxx()` | Known target type at compile time | **Zero boxing** | **Hot paths, known-type reads and type checks** (data change handling, per-frame reads/writes, replacing `is T` checks) |
| `TypedDataObjectConverter.ToObject(td)` | Type-erased | Value types **boxed** | **Framework-internal cold paths only**: serialization, console/debug output, `ToString`. `internal`, external code cannot access |

**Recommended usage**:

- Always use `TryGetXxx` / `TypedDataFactory<T>.TryExtract()` for value reads — the former is zero-boxing and does not route through `ToObject`'s switch dispatch.
- Always use explicit operators (e.g., `(TypedData)42`) or `TypedDataFactory<T>.Create()` for construction.
- **Do not manually write kind dispatch or if-else chains to handle different TypedData types** — type discrimination logic is already encapsulated in generated code and `TypedDataObjectConverter`.

### SndMetaData

Aggregates all entity metadata:

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Entity unique identifier name |
| `NodeMetaData` | `NodeMetaData?` | Node mapping; may be null for background entities |
| `StrategyMetaData` | `StrategyMetaData?` | Strategy index list |
| `DataMetaData` | `DataMetaData?` | Data dictionary; defaults to empty container |

Provides a `DeepClone()` method: shallow copy of value references (no recursive object graph copy), but new dictionary/list containers are created.

### DataMetaData

Wraps a `Dictionary<string, TypedData>`. Each key corresponds to a typed entity data point. Since `TypedData` is a value type (struct), dictionary values cannot be null; missing data points are represented by not containing the corresponding key.

### NodeMetaData

Wraps `Dictionary<string, string>`, storing logical node name to resource identifier mappings. The semantics of specific resource IDs are defined by the adapter layer (e.g., `res://` paths in Godot).

### StrategyMetaData

Wraps `List<string>`, storing the list of strategy indices associated with this entity (e.g., `["core.health", "gameplay.movement"]`).

### SndMetaFluentBuilder

```csharp
var meta = new SndMetaFluentBuilder("Player")
    .SetInt("hp", 100)
    .SetFloat("speed", 200f)
    .SetBool("alive", true)
    .AddLifecycleStrategy("game.player_move")
    .SetNode("scene", "res://player.tscn")
    .Build();
```

Fluent chained API for building `SndMetaData`, eliminating the manual `meta.DataMetaData ??= new DataMetaData()` + `meta.DataMetaData.Pairs["key"] = new TypedData(typeof(T), value)` boilerplate.

Provides a `SndMetaFluentBuilder.From(SndMetaData)` static factory for fluent data addition after `ctx.Template.CloneTemplate`:

```csharp
var meta = SndMetaFluentBuilder.From(ctx.Template.CloneTemplate("player_template", "Player"))
    .SetInt("hp", 100)
    .Build();
```

Typed Set methods include `SetInt`, `SetFloat`, `SetDouble`, `SetLong`, `SetBool`, `SetString`, and `SetBytes`. `Build()` returns the completed `SndMetaData`.

## Design Decisions

### Why TypedData is a value type (readonly partial struct)

TypedData stores entity runtime mutable state (e.g., `hp = 100`, `speed = 3.5f`), the vast majority of which are value types. Using a class (reference type) would mean every `SetData` requires a heap allocation of `new TypedData(typeof(T), value)`, generating significant GC pressure for frequently updated game entities (multiple reads/writes per frame).

As a value type, inline storage eliminates heap allocations. `Dictionary<string, TypedData>` values are directly embedded in dictionary entries; value types live on the stack or inline in collections without producing standalone GC objects.

### Why use Source Generator to generate type members

Each BC primitive type needs consistent constructor, accessor, factory method, and serialization bridge code. Manually maintaining boilerplate for 13+ types is error-prone and leads to inter-type inconsistency. The Source Generator reads `[assembly: SndInlineTypes]` attributes and auto-generates optimal implementations for all registered types, ensuring zero-boxing reads and writes.

### Why TypedData.DataType is a Type rather than a string

`System.Type` enables direct runtime type checking and conversion (e.g., `ConverterRegistry.Read(type, node)` during serialization). The cost of recovering a `Type` from a `string` is paid only once at deserialization. For registered types, `DataType` reads from a compile-time-generated static lookup table (zero reflection); for unregistered types, the runtime type is obtained via `_ref?.GetType()`.

### Why DeepClone does not recursively copy the object graph

Entity Data values may be complex reference types (strings, arrays, nested objects). Full recursive deep copy is expensive and may incorrectly copy engine-internal references (via `INodeHandle.Native`, etc.). The current shallow copy semantics are consistent with JSON round-trip serialization behavior — if JSON serialization would not copy, DeepClone also does not copy.

### Why TypedData uses ModuleInitializer + RegisterKind

`static TypedData()` static constructor can only exist in the defining assembly, whereas in a multi-adapter architecture, downstream assemblies like GodotAdapter need to register their own types into the same `KindTypeMap` array. `ModuleInitializer` allows multiple assemblies to each call `TypedData.RegisterKind()` at load time, executing in dependency order (Core before Adapter), enabling composition of global type registrations.

### Why TypedData does not expose an `object?` boxing accessor

Returning value types like `int`/`float` as `object?` on the CLR **inevitably triggers a boxing allocation**. If TypedData exposed such a property, regardless of its name (`Data` or otherwise), it would become the most natural way for secondary developers to extract values from TypedData — because it "works for all types" without needing to learn generics or `TryGetXxx`. This is precisely the **backdoor** prohibited by §1.4: a fully-featured, simpler access path that obscures the existence of zero-boxing `TryGetXxx` / `TypedDataFactory<T>.TryExtract()`.

TypedData deliberately does not expose a boxing accessor. There is only one path to extract a value: known type at compile time → `TryGetXxx` or `TypedDataFactory<T>.TryExtract()`, JIT-folded to zero-overhead field reads. Framework-internal cold paths that require type erasure (serialization, console debugging, `ToString`) are handled by `internal` `TypedDataObjectConverter.ToObject`, inaccessible to external code.

Similarly, TypedData does not expose a `new TypedData(Type, object?)` universal constructor. There is only one path to construct: `TypedDataFactory<T>.Create()` or explicit operators (e.g., `(TypedData)42`), JIT-folded to zero-overhead field writes.

---

[↑ Back to Snd](../README.en.md)
