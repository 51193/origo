<!-- docsync-pair: Origo.SourceGeneration/README -->
<!-- docsync-revision: 9 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.SourceGeneration

> [↑ Back to Origo.manual](../README.en.md) · [↔ Core: Snd/Metadata](../Origo.Core/Snd/Metadata/README.en.md)

## Overview

**Origo.SourceGeneration** is a Roslyn incremental source generator (`IIncrementalGenerator`) that generates type-specialized inline storage and strongly-typed accessors for the `TypedData` partial struct. It supports **Home/Adapter dual-mode** generation, allowing the Core layer and downstream adapter layers to declare their own type sets independently while automatically coordinating the global Kind value space.

> For the complete performance reasoning, step-by-step analysis, and extension guide, see **[pipeline.md](pipeline.en.md)**.

## Files

| File | Responsibility |
|------|------|
| `TypedDataGenerator.cs` | Roslyn `IIncrementalGenerator`: dual-mode code generator (main entry + attribute parsing + generation input extraction) |
| `TypedDataGenerator.AdapterGeneration.cs` | partial — Adapter mode code generation (Godot engine type extension methods) |
| `TypedDataGenerator.HomeGeneration.cs` | partial — Home mode code generation (Core BCL type extension methods) |
| `TypedDataGenerator.FactoryGeneration.cs` | partial — `TypedDataFactory<T>` Create/TryExtract branch generation (type mapping `TypedDataTypeMap` and Kind allocation live in HomeGeneration.cs) |
| `TypedDataGenerator.Diagnostics.cs` | partial — Diagnostics definitions (ORIGOSG001-005) |
| `AnalyzerReleases.Shipped.md` | Analyzer release tracking (shipped rules, currently empty) |
| `AnalyzerReleases.Unshipped.md` | Analyzer release tracking (unshipped rules: `ORIGOSG001`, `ORIGOSG002`, `ORIGOSG003`, `ORIGOSG004`, `ORIGOSG005`) |
| `pipeline.en.md` | Full-pipeline performance analysis: complete reasoning and benchmark notes from boxing problems to compile-time optimization |

## Dual-Mode Architecture

The Source Generator detects at compile time whether the current assembly is the "home" assembly for TypedData (i.e., the assembly defining the `TypedData` struct), and automatically switches the generation strategy:

| Mode | Applicable Assembly | Generated Content |
|------|-----------|---------|
| **Home** | Origo.Core | `partial struct TypedData`'s KindMap, AsXxx/TryGetXxx methods, explicit operators; `TypedDataTypeMap`, `TypedDataObjectConverter`, `TypedDataFactory<T>`; `[ModuleInitializer]` KindTypeMap registration |
| **Adapter** | Origo.GodotAdapter and other adapter layers | Extension methods (`AsXxx` / `TryGetXxx`); `[ModuleInitializer]` KindTypeMap + KindResolver + FromObject/ToObject conversion bridges |

### Home Mode Generated Content

| Category | Generated Content | Notes |
|---------|---------|------|
| **KindMap** | `partial struct TypedData { internal static class KindMap { const byte Int32 = 5; ... } }` | One `const byte` discriminator per registered type, numbered from `StartKind` |
| **Kind registration** | `TypedDataHomeKindRegistration` + `[ModuleInitializer]` | Calls `TypedData.RegisterKind()` to populate the global `KindTypeMap[]`, independent of any static constructor, allowing multiple assemblies to each register their own types |
| **Strongly-typed construction** | `explicit operator TypedData(...)` | Explicit conversion operators for each system type, writing values inline into struct fields |
| **Strongly-typed reads** | `AsXxx()` (`internal`) / `TryGetXxx()` (`public`) | Accessor methods, direct field reads via Kind discriminator; `AsXxx` has no Kind guard (framework-internal, called only after a switch match), business reads use `TryGetXxx` |
| **Generic factory** | `TypedDataFactory<T>` | `Create(T)` / `TryExtract(TypedData, out T)`, if-else chain with JIT constant folding |
| **Serialization bridge** | `TypedDataObjectConverter` | `ToObject` / `FromObject`, switch dispatch + `TypedDataLayeredRegistry` fallback chain |
| **Type mapping** | `TypedDataTypeMap` | `GetKindForType(Type)`, if-else chain + `TypedDataLayeredRegistry.ResolveKind` fallback |

### Adapter Mode Generated Content

| Category | Generated Content | Notes |
|---------|---------|------|
| **Extension methods** | `TypedDataLayeredExtensions` static class (`internal`) | `this TypedData` extension methods: `TryGetVector3`, `AsVector3`, etc., reading adapter-layer types through `_ref` (non-system value type size cannot be determined at compile time, see below). Consistent with the home assembly's internal `AsXxx`, the whole class is `internal` — unguarded read paths are not exposed to business code, which reads via `ISndEntity.TryGetData<T>` or `TryGetXxx` |
| **Kind registration** | `TypedDataAdapterKindRegistration` + `[ModuleInitializer]` | Calls `TypedData.RegisterKind(startKind + i, typeof(T))` |
| **Conversion bridges** | `TypedDataAdapterConverterRegistration` + `[ModuleInitializer]` | Calls `TypedDataLayeredRegistry.RegisterFromObjectFallback` / `RegisterToObjectFallback` |
| **Type resolution** | `TypedDataAdapterTypeMapRegistration` + `[ModuleInitializer]` | Calls `TypedDataLayeredRegistry.RegisterKindResolver`, providing a `Type → kind` if-else chain |

### Kind Value Segmentation

Kind values are `byte`s, with each layer's starting value controlled by `SndInlineTypesAttribute`'s `StartKind` parameter:

| Layer | StartKind | Kind Range | Type Count |
|----|-----------|----------|--------|
| Core | 1 (default) | 1–13 | 13 BCL primitive types |
| GodotAdapter | 128 | 128–141 | 14 Godot engine types |
| Reserved (future adapters) | 192 | 192–254 | — |
| Fallback | — | `TypedData.UnregisteredKind` | Fallback for unregistered types |

### Type Inlining Strategy

There are only two storage paths: system primitive value types in the home assembly go into `_inlineBits`, everything else goes into `_ref`.

| Type | Storage | Notes |
|------|---------|------|
| System primitive value types declared in the home (Origo.Core) assembly (`byte`/`sbyte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`/`float`/`double`/`bool`/`char`) | Inlined in `_inlineBits : long` field | Zero heap allocation, zero boxing; inlining limited to home assembly |
| Reference types (`string`) | Stored in `_ref : object?` field | Built-in KindMap fallback |
| Adapter-registered types (non-system value types) | Stored in `_ref : object?` field | Non-system value type size cannot be reliably determined at compile time, fall back to `_ref` |
| Unregistered types | `_ref : object?` fallback | Kind=`TypedData.UnregisteredKind`, restored through `TypedDataObjectConverter.FromObject` during deserialization |

> Discriminator `0` is fixed as the `Null` sentinel value (`default(TypedData)`). Inlining candidates are determined precisely by a `SpecialType` whitelist (the 12 system primitive types enumerated in the first row of the table above), not by type display name matching.

## Type Validation and Diagnostics

The generator validates the storage model for each registered type before generation; violations are reported as compile errors (fail-fast) rather than generating silently-returning-`0`/`default` accessors. Invalid types are excluded from generation, and the reported errors cause the build to fail.

Diagnostic messages carry the corresponding `SndInlineTypesAttribute` syntax location, marking the offending attribute line with a red squiggle in the IDE, with click-to-navigate support to the source location.

| Diagnostic ID | Severity | Trigger Condition |
|---------|---------|---------|
| `ORIGOSG001` | Error | A system primitive type is registered in a non-home (adapter layer) assembly's `SndInlineTypes` group. Inlined primitive types are exclusive to Origo.Core; adapter layers may only register reference types or non-system value types (going through `_ref`). |
| `ORIGOSG002` | Error | An uninlinable and unsupported value type (such as `decimal` or a custom struct) is registered in the home assembly. The home assembly only permits registering supported system primitive types and reference types. |
| `ORIGOSG003` | Error | A registered type's Kind value (`startKind` + position within group) falls outside the `byte` valid range `[1, 254]`. This includes cases where a Kind overflow wraps around to an already-occupied value, silently conflicting with another type. |
| `ORIGOSG004` | Error | Multiple `SndInlineTypes` groups have overlapping `startKind` ranges, causing the same Kind byte to be assigned to multiple different types. Each inlined type must map to a unique Kind. |
| `ORIGOSG005` | Error | Multiple registered types produce the same generated identifier (KindName): same-named types from different namespaces, generic instantiations whose names collapse to one identifier, and the same type registered more than once with different kind values (re-registering the same type with the same kind is idempotent and silently deduplicated, matching the runtime `RegisterKind` semantics). The reserved identifier `Null` is rejected too — `KindMap` always emits the sentinel `Null = 0` (and value types would also collide with the handwritten `IsNull` property), so a type named `Null` reports ORIGOSG005 and is dropped. Generated accessor identifiers derive from the type name; any identifier collision would emit uncompilable duplicate members. |

## Registration Mechanism

```csharp
// Core layer (default StartKind=1)
[assembly: SndInlineTypes(
    typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
    typeof(int), typeof(uint), typeof(long), typeof(ulong),
    typeof(float), typeof(double), typeof(bool), typeof(char), typeof(string)
)]

// Adapter layer (specified StartKind)
[assembly: SndInlineTypes(startKind: 128,
    typeof(Vector2), typeof(Vector2I),
    typeof(Vector3), typeof(Vector3I), typeof(Vector4),
    typeof(Quaternion), typeof(Basis),
    typeof(Transform2D), typeof(Transform3D),
    typeof(Color), typeof(Rect2), typeof(Rect2I),
    typeof(Aabb), typeof(Plane)
)]
```

`SndInlineTypesAttribute` is defined in the `Origo.Core.Snd.Metadata` namespace. The `StartKind` parameter (default `1`) controls the Kind start offset.

## Multi-Layer Runtime Extensions

### TypedDataLayeredRegistry

Located in `Origo.Core.Snd.Metadata`, provides a chained callback registration mechanism that allows multiple adapter layers to concurrently contribute their own type mappings:

| Registration Method | Registered Delegate Shape | Called From |
|---------|--------------|------------|
| `RegisterKindResolver(Func<Type, byte>)` | `Type → kind`, returns 0 for not handled | `TypedDataTypeMap.GetKindForType` |
| `RegisterFromObjectFallback(Func<byte, object, (long, object?)?>)` | `(kind, value) → (inlineBits, refValue)?`, returns null for not handled | `TypedDataObjectConverter.FromObject` |
| `RegisterToObjectFallback(Func<TypedData, object?>)` | `TypedData → object?`, returns null for not handled | `TypedDataObjectConverter.ToObject` |

### TypedData.RegisterKind

`TypedData` provides the `internal static void RegisterKind(byte kind, Type type)` method, allowing external assemblies (via `InternalsVisibleTo`) to write kind → Type mappings into the global `KindTypeMap[256]` array. Each layer's `[ModuleInitializer]` calls this method in assembly load order. Validation rules: Kind `0` (Null sentinel) is ignored; Kind `255` (`UnregisteredKind` sentinel) is rejected with `ArgumentOutOfRangeException`; a null type throws `ArgumentNullException`; registering a different type to an already-occupied kind throws `InvalidOperationException` (idempotent re-registration of the same type is allowed).

## Design Decisions

### Why Home/Adapter dual mode instead of single centralized generation

Single centralized generation would require the SG to scan all downstream adapter assemblies when compiling Core, but Core compiles before adapter layers — at that point, adapter layer metadata does not yet exist. Dual mode allows each layer to compile and generate independently, assembling at runtime via `TypedDataLayeredRegistry` + `ModuleInitializer`.

### Why ModuleInitializer instead of static TypedData()

`static TypedData()` can only exist once; adapter layers cannot append `KindTypeMap` entries. `ModuleInitializer` allows multiple assemblies to each register their own Kind mappings, executing in assembly load order (Core before GodotAdapter).

### Why adapter layer types go through _ref instead of _inlineBits

Adapter layer types (such as Godot's `Vector3`, `Color`) are externally defined non-system value types whose actual byte sizes cannot be reliably inferred at source generation time. Safety strategy: types registered by adapter layers uniformly go through the `_ref` path; the Kind discriminator still guarantees fast dispatch for `DataType` lookups and `TryGet` calls.

### Why out-of-range registration is an error, not a silent degradation

The storage model has only two paths: inlining (home system primitives) and `_ref` (everything else). If out-of-range cases like "registering a system primitive in an adapter layer" or "registering an uninlinable value type in the home assembly" were silently accepted, the generator could only produce accessors returning `0`/`default`, causing data to be silently corrupted across round-trips. Following the framework's "explicit failure preferred" constraint, the generator reports `ORIGOSG001`/`ORIGOSG002` compile errors for such out-of-range registrations and excludes the offending type, exposing the problem at build time rather than at runtime.

### Why Kind space range and uniqueness are validated at compile time

Kind is a `byte`; at runtime `TypedData.RegisterKind` throws `InvalidOperationException` when a kind is registered to a different type than an existing mapping (idempotent re-registration of the same type is allowed), so types can never silently overwrite each other. Kinds are computed as `startKind` plus intra-group position; if `startKind` is too large or there are too many types, the Kind value overflows past 255 and wraps around to an already-occupied small value, conflicting with existing types. The generator therefore enforces three invariants at compile time: Kind must fall within `[1, 254]` (`ORIGOSG003`, evaluated on the true value, not relying on `byte` truncation), each Kind must be uniquely mapped (`ORIGOSG004`, detecting overlapping `startKind` ranges), and every generated identifier must be unique (`ORIGOSG005`, detecting same-named types across namespaces, collapsing generic instantiations, duplicate registrations of the same type with different kinds, and reserved identifiers that collide with the `KindMap` sentinel `Null`). Violating types are excluded and build errors are reported, so Kind conflicts are exposed at build time rather than corrupting saves at runtime.

---

[↑ Back to Origo.manual](../README.en.md)
