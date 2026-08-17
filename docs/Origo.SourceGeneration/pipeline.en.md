<!-- docsync-pair: Origo.SourceGeneration/pipeline -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# TypedData Compile-Time Optimization: Full Pipeline Analysis

> [↑ Back to Origo.SourceGeneration](README.en.md) ·
> [↔ Baseline Data](../benchmarks/baseline.en.md) ·
> [↔ TypedData Documentation](../Origo.Core/Snd/Metadata/README.en.md)

## Overview

This document is a systematic analysis of the **why, how, and why it works** for the TypedData source generator. It walks through the complete reasoning chain from problem → solution → pipeline → performance data → boundary limitations, serving as a reference for future maintenance and extension development.

**Target audience**: Developers who need to further extend TypedData capabilities on the Origo framework, adapt to new runtimes, or understand performance trade-off details. Before reading this document, it is recommended to first read the [Origo.SourceGeneration README](README.en.md) for an overview of the dual-mode architecture and generated content.

---

## 1. Problem Origin: The Performance Trap of Generic Data Dictionaries

### 1.1 Business Requirements

Game entities carry various attributes: HP `int`, movement speed `float`, name `string`, coordinates `Vector3`, and so on. The framework needs a generic mechanism to accommodate these arbitrary-typed data. The direct approach is:

```csharp
Dictionary<string, object> _data;
_data["hp"] = 100;      // int boxed
_data["speed"] = 3.5f;  // float boxed
```

### 1.2 The Cost of Boxing

C# value types (`int`, `float`, `bool`, etc.) are not heap objects and cannot be directly stored in an `object` slot. When assigned to `object`, the CLR must perform **boxing**:

1. Allocate a new object on the managed heap (for an `int`, typically a 24-byte heap object containing syncblk + method table pointer + 4 bytes of data + padding)
2. Copy the value type's contents into the heap object
3. Store a reference to that object in the Dictionary's entries array

The reverse operation on reads is **unboxing**: runtime type check + heap-to-stack/register copy.

### 1.3 Impact on Game Engines

| Metric | Boxing Approach |
|------|------------|
| Per `int` write | 1 heap allocation (~24 bytes) + GC object tracking |
| 2 million `int` writes | ~107 MB heap allocation |
| Per GC trigger | Scan all live boxed objects, STW (Stop-The-World) |
| Thousands of reads/writes per frame | GC latency accumulation → frame rate jitter |

In a 60 FPS game, GC consuming 2-5ms out of a 16ms per-frame budget is enough to cause frame drops. Offline batch processing might not care, but for real-time frame loops, boxing on the hot path must be eliminated.

---

## 2. Prerequisites: Why Only C# Can Do This

The TypedData approach relies on three C# / .NET-specific mechanisms. From the perspective of Java or other runtimes, this approach would seem impossible. These prerequisites are explained first.

### 2.1 Generics Do Not Erase Types

C# generics fully preserve type parameter information in IL and runtime metadata after compilation. `List<int>` and `List<float>` are different types. The key expression `typeof(T)` is not compile-time syntactic sugar — it is a real `System.Type` instance at runtime:

```csharp
static byte LookupKind<T>()
{
    // This is legal in C# because T is not erased
    if (typeof(T) == typeof(int))  return 5;
    if (typeof(T) == typeof(float)) return 9;
    return 0;
}
```

**Java comparison**: Generics are erased to `Object` after compilation; the `T.class` expression simply does not exist in bytecode.

### 2.2 Value Type Generics Produce Separate Machine Code

When the CLR's JIT compiler encounters `TypedDataFactory<int>` and `TypedDataFactory<float>`, they generate **two completely independent native machine code** fragments, because `int` (4 bytes) and `float` (IEEE 754 single precision) have completely different register layouts and read/write instruction sequences.

This is equivalent to C++ template code expansion (`std::vector<int>` and `std::vector<float>` are two separate assembly outputs). Reference type generics (like `TypedDataFactory<string>`) share the same code because all references are 8-byte pointers.

### 2.3 JIT Constant Folding + Dead Code Elimination

When compiling `TypedDataFactory<int>`, the JIT encounters:

```csharp
if (typeof(T) == typeof(byte))   // typeof(int) == typeof(byte) → false
    ...
if (typeof(T) == typeof(int))    // typeof(int) == typeof(int)  → true
    ...
```

`typeof(T)` for a concrete `T = int` is a JIT-time constant. The JIT can determine at compile time that `typeof(int) == typeof(byte)` is always `false`, and directly eliminate that branch from the generated machine code. Only the `typeof(T) == typeof(int)` branch, where the condition is `true`, is retained.

**This is the source of the performance**: what appears in source code as a giant if-else chain becomes, after JIT, machine code containing only the single branch for the target type.

### 2.4 Roslyn Source Generator

The C# compiler allows inserting source generators — during compilation, the generator can read all metadata of the current assembly (types, attributes, method signatures, etc.), then dynamically produce new `.cs` source files appended to the compilation. Generated code is fully equivalent to hand-written code.

---

## 3. TypedData Struct Design

```csharp
public readonly partial struct TypedData : IEquatable<TypedData>
{
    internal readonly byte _kind;        // Type tag
    internal readonly long _inlineBits;  // Raw storage for ≤8-byte value types
    internal readonly object? _ref;      // Reference types or large structs
}
```

Physical layout (24 bytes, including 7 bytes of alignment padding):

```
Offset  0  [_kind]       1 byte
Offset  1  [padding]     7 bytes (alignment to long boundary)
Offset  8  [_inlineBits] 8 bytes
Offset 16  [_ref]        8 bytes (managed reference pointer)
```

### 3.1 Field Responsibilities

| Field | What It Holds | How It Is Used |
|------|--------|--------|
| `_kind` | `0` = null, `1-254` = registered types, `255` = unregistered | All type discrimination and `switch` dispatch is based on it |
| `_inlineBits` | `int`, `float`, `double`, `bool`, `long`, and other ≤8-byte primitive types | Direct type-cast read; `float`/`double` reinterpret bit patterns via `BitConverter` |
| `_ref` | `string`, Godot `Vector3`, arbitrary unregistered types | Managed reference, no extra wrapping |

### 3.2 Why 24 Bytes

`_inlineBits` (`long` field) and `_ref` (managed reference) cannot share memory — the GC must independently scan every managed reference pointer to determine object liveness. If the reference were embedded in the high 8 bytes of `long`, the GC could not distinguish whether it is a number or a pointer. So the three must be placed sequentially:

```
byte (1B) + padding (7B) + long (8B) + object? (8B) = 24B
```

Could it be squeezed to 16 bytes? That would require sacrificing `long`/`double`'s full 8-byte inlining capability, falling back to the `_ref` path (which for value types effectively means boxing to the heap every time). This was evaluated as a net negative for performance and has been abandoned.

---

## 4. Full Pipeline Breakdown

### 4.1 Registration Phase: Declaring Type Sets

The framework applies `[assembly: SndInlineTypes(...)]` at the assembly level:

```csharp
// Origo.Core assembly — StartKind defaults to 1
[assembly: SndInlineTypes(
    typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
    typeof(int), typeof(uint), typeof(long), typeof(ulong),
    typeof(float), typeof(double), typeof(bool), typeof(char),
    typeof(string)
)]

// Origo.GodotAdapter assembly — StartKind = 128
[assembly: SndInlineTypes(startKind: 128,
    typeof(Vector2), typeof(Vector2I), typeof(Vector3),
    typeof(Vector3I), typeof(Vector4), typeof(Quaternion),
    typeof(Basis), typeof(Transform2D), typeof(Transform3D),
    typeof(Color), typeof(Rect2), typeof(Rect2I),
    typeof(Aabb), typeof(Plane)
)]
```

Kind allocation rules:

| Layer | StartKind | Kind Range | Type Count |
|----|-----------|----------|--------|
| Core | 1 | 1–13 | 13 BCL primitive types |
| GodotAdapter | 128 | 128–141 | 14 Godot engine types |
| Reserved (future adapters) | 192 | 192–254 | — |

Compile-time validation (fail-fast):

| Diagnostic ID | Trigger Condition |
|---------|---------|
| `ORIGOSG001` | System primitive type registered in a non-home (adapter) assembly |
| `ORIGOSG002` | Home assembly registers an uninlinable value type (e.g., `decimal`) |
| `ORIGOSG003` | Kind out of range (not in `[1, 254]`) |
| `ORIGOSG004` | Kind range overlap, multiple types mapped to the same Kind |
| `ORIGOSG005` | Generated identifier (KindName) collisions: same-named types from different namespaces, collapsing generic instantiations, or the same type registered more than once with different kind values (same-kind re-registration is idempotent and silently deduplicated) |
| `ORIGOSG006` | Sanitized KindName is not a valid C# identifier (e.g. pointer type names containing `*`) |

These diagnostics are reported as Errors at compile time, causing build failure. Issues like Kind conflicts or out-of-range values **never reach runtime**.

#### Why Validate Kind at Compile Time

At runtime `TypedData.RegisterKind` already detects conflicts — registering a different type to an occupied kind throws `InvalidOperationException` (idempotent re-registration of the same type is allowed). But relying on runtime detection alone would let the corruption risk reach runtime. Compile-time enforcement (range, numeric uniqueness, and generated-identifier uniqueness) means Kind conflicts are caught at build time.

---

### 4.2 Linking Phase: Source Generator Produces Code

The Source Generator is invoked at compile time through the Roslyn `IIncrementalGenerator` pipeline. It scans the assembly for all `[assembly: SndInlineTypes]` attributes, then generates a `TypedData.g.cs` source file appended to the current compilation.

The generated content falls into two sets based on **whether the current compilation assembly is the home assembly for TypedData**:

#### Home Mode (Origo.Core)

| Category | Output |
|---------|------|
| **KindMap** | `partial struct TypedData { KindMap { const byte Int32 = 5; ... } }` |
| **Inline accessors** | `AsInt32()` / `TryGetInt32(out v)`, etc., directly operating on `_inlineBits` |
| **Explicit conversions** | `explicit operator TypedData(int value)` |
| **Generic factory** | `TypedDataFactory<T>`: includes `Create` (T → TypedData) and `TryExtract` (TypedData → T) |
| **Type mapping** | `TypedDataTypeMap.GetKindForType(Type)` |
| **Object converter** | `TypedDataObjectConverter`: `ToObject` / `FromObject` |
| **Kind registration** | `[ModuleInitializer]` calls `TypedData.RegisterKind()` |

#### Adapter Mode (Origo.GodotAdapter, etc.)

| Category | Output |
|---------|------|
| **Extension methods** | `td.TryGetVector3(out Vector3 v)`, etc. (via `_ref` path) |
| **Kind registration** | `[ModuleInitializer]` calls `TypedData.RegisterKind()` |
| **Conversion fallbacks** | Registers `FromObject`/`ToObject` fallback delegates with `TypedDataLayeredRegistry` |
| **Type resolution** | Registers a `Type → kind` if-else chain with `TypedDataLayeredRegistry` |

#### Source Generator Code Location

The generator source lives under `Origo.SourceGeneration/` as 5 partial files (~1085 lines total): `TypedDataGenerator.cs` (pipeline and input extraction), `TypedDataGenerator.HomeGeneration.cs` (home-assembly generation), `TypedDataGenerator.AdapterGeneration.cs` (adapter generation), `TypedDataGenerator.FactoryGeneration.cs` (`TypedDataFactory<T>` branch generation), and `TypedDataGenerator.Diagnostics.cs` (diagnostic definitions). The core `GenerateTypedDataFactory` (`FactoryGeneration.cs`) iterates all registered types and generates `typeof(T) == typeof(...)`-style branches one by one.

---

### 4.3 Write Path: T → TypedData

Taking `entity.SetData("hp", 100)` as an example:

```
Caller → SndDataManager.SetData<int>("hp", 100)
       → TypedDataFactory<int>.Create(100)
```

**Pre-JIT** code of `TypedDataFactory<int>.Create`:

```csharp
public static TypedData Create(T value)
{
    if (typeof(T) == typeof(byte))
    {
        byte local = Unsafe.As<T, byte>(ref value);
        return new TypedData(1, local, null);
    }
    if (typeof(T) == typeof(sbyte))
    {
        sbyte local = Unsafe.As<T, sbyte>(ref value);
        return new TypedData(2, local, null);
    }
    // ... 9 similar branches ...
    if (typeof(T) == typeof(int))
    {
        int local = Unsafe.As<T, int>(ref value);       // JIT: typeof(T)==typeof(int) → true
        return new TypedData(5, local, null);            // _kind=5, _inlineBits=100, _ref=null
    }
    // ... uint, long, ulong, float, double, bool, char ...
    if (typeof(T) == typeof(string))
    {
        return new TypedData(13, 0, value);
    }
    // fallback: unregistered types
    var kind = TypedDataTypeMap.GetKindForType(typeof(T));
    if (kind != 0)
    {
        var result = TypedDataObjectConverter.FromObject(kind, value!);
        return new TypedData(kind, result.inlineBits, result.refValue);
    }
    return new TypedData(TypedData.UnregisteredKind, 0, value);
}
```

**Post-JIT** actual machine code executed (for `TypedDataFactory<int>`):

```
store _kind=5, store _inlineBits=100, store _ref=null
```

#### Why It Is Fast

| Dimension | Generated (TypedData struct) | Boxed (OldTypedData class) |
|------|------------------------|---------------------------|
| Heap allocation | 0 (value type embedded in dictionary entries array) | 1 gen-0 heap allocation per `int` write |
| Write | Two `stfld` (store _kind + _inlineBits) | `newobj` + `stfld` + GC accounting |
| GC pressure | 0 (no independent objects) | Each gen-0 GC must scan all boxes |
| Assignment to dictionary's `object` key | Bypassed — `TypedData` is a struct, directly embedded | Boxed reference stored in dictionary |

**Benchmark data** (2 million writes):

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Allocation (Gen/Boxed) |
|------|--------------:|--------------:|:----:|----------------|
| Int32 | 908 | 69.6 | **13.0x** | 0 B / 106.81 MB |
| Int64 | 893 | 70.9 | **12.6x** | 0 B / 106.81 MB |
| Single | 866 | 71.4 | **12.1x** | 0 B / 106.81 MB |
| Double | 881 | 72.3 | **12.2x** | 0 B / 106.81 MB |
| Boolean | 891 | 71.3 | **12.5x** | 0 B / 106.81 MB |
| Char | 910 | 71.6 | **12.7x** | 0 B / 106.81 MB |
| String | 563 | 110 | **5.1x** | 0 B / 61.04 MB |

Value type writes achieve **12–13x throughput** with **zero byte allocation**. String writes are also 5.1x (reference types don't need boxing into another reference type object, but still require constructing the `OldTypedData` wrapper class).

---

### 4.4 Read Path: TypedData → T

#### TryGetXxx Path (Hot Path)

```csharp
// Caller:
entity.TryGetData<int>("hp", out int hp);
// → SndDataManager.TryGetData<int>
// → _data.TryGetValue("hp", out TypedData td)
// → TypedDataFactory<int>.TryExtract(td, out hp)
```

**Pre-JIT** `TryExtract`:

```csharp
public static bool TryExtract(TypedData source, out T value)
{
    if (typeof(T) == typeof(byte) && source._kind == 1)
    {
        byte local = (byte)source._inlineBits;
        value = Unsafe.As<byte, T>(ref local);
        return true;
    }
    // ... 10 similar branches ...
    if (typeof(T) == typeof(int) && source._kind == 5)     // ← JIT only keeps this line
    {
        int local = (int)source._inlineBits;                 // Direct field read
        value = Unsafe.As<int, T>(ref local);                // no-op (T is already int)
        return true;
    }
    // ... float, double, bool, char, string ...
    if (typeof(T) == typeof(string) && source._kind == 13)
    {
        object? rawRef = source._ref;
        value = Unsafe.As<object?, T>(ref rawRef)!;   // bit reinterpretation (no castclass); null yields found=true + null
    }
    // fallback: registered but non-inline types → ToObject + cast
    if (source._kind != 0 && source._kind != TypedData.UnregisteredKind)
    {
        var obj = TypedDataObjectConverter.ToObject(source);
        if (obj is T t1) { value = t1; return true; }
    }
    // last resort: unregistered types
    if (source._ref is T t2) { value = t2; return true; }
    value = default!;
    return false;
}
```

**Post-JIT** (for `TypedDataFactory<int>`):

```csharp
if (source._kind == 5) { value = (int)source._inlineBits; return true; }
// fallback ...
```

Two instructions: byte comparison + register read.

#### Why Single Reads Are Nearly Tied (≤ 1.10x)

**Benchmark data** (10 million reads, 0 byte allocation on both sides):

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio |
|------|--------------:|--------------:|:----:|
| Int32 | 550 | 581 | 1.06x (boxed slightly faster) |
| Int64 | 551 | 580 | 1.06x (boxed slightly faster) |
| Single | 551 | 568 | 1.03x (nearly tied) |
| Double | 560 | 584 | 1.04x (boxed slightly faster) |
| Boolean | 530 | 583 | 1.10x (boxed slightly faster) |
| Char | 545 | 584 | 1.07x (boxed slightly faster) |
| String (IsString) | 586 | 576 | 1.02x (generated slightly faster) |

Analysis: The boxed side's code path is also: take `object` reference → check type metadata (method table pointer) → unbox copy. The work on both sides is similar — both are type discrimination + field reads. The ~10% slower generated side is not due to instruction count, but to cache affinity differences caused by struct size (see Chapter 5).

#### Why Mixed Dispatch Pulls Ahead

```csharp
// Generated: each TryGetXxx checks _kind + reads field, zero boxing
td.TryGetInt32(out _);
td.TryGetSingle(out _);
td.TryGetBoolean(out _);
td.TryGetString(out _);
td.TryGetDouble(out _);

// Boxed: .Data each time goes through ToObject → switch → value types must be boxed → then is T unboxed
data is int;
data is float;
data is bool;
data is string;
data is double;
```

**Benchmark**: Mixed dispatch generated **1.54x** faster than boxed (~1250 vs ~812 Mops/s), 0 byte allocation on both sides (generated side is truly zero-allocation; boxed side allocations are masked by loop reuse, but the repeated boxing logic overhead is still higher).

#### Special Handling of TryGetString

```csharp
// Generated: guarded by _kind == String, uses Unsafe.As<string> instead of (string)_ref
if (_kind == KindMap.String) { value = Unsafe.As<string>(_ref)!; return true; }
```

Why not use `(string)_ref` (castclass): `_kind == String` already proves `_ref` is a `string` instance, making castclass redundant. More critically, castclass may throw exceptions — this blocks the JIT's elimination and hoisting optimizations for TryGetString calls whose results are discarded. On the observer notification path, removing castclass brings the generated side to parity with boxed `is string` (~5390 vs ~5170 Mops/s).

---

### 4.5 Object Boundary Paths: ToObject / FromObject / Data Property

`TypedDataObjectConverter.ToObject` serves cold paths where the type is unknown at compile time (serialization, console output, `ToString`, `Data` property reads):

```csharp
public static object? ToObject(TypedData td)
{
    switch (td._kind)
    {
        case 0:  return null;
        case 1:  return td.AsByte();          // _inlineBits → byte → boxed as object
        case 5:  return td.AsInt32();         // _inlineBits → int → boxed as object
        case 13: return td._ref;              // Reference type returned directly
        // ... adapter cases provided by TypedDataLayeredRegistry ...
    }
    var obj = TypedDataLayeredRegistry.ResolveToObject(td);
    if (obj is not null) return obj;
    return td._ref;
}
```

Value types read through `ToObject` **inevitably incur boxing** — because the return type is `object`.

#### Performance Trade-offs of `TypedDataObjectConverter.ToObject` Iteration

`TypedDataObjectConverter.ToObject` is a framework-internal facility for handling type-erased scenarios. It accepts `TypedData` and returns `object?` — value types are necessarily boxed. TypedData deliberately exposes no public boxing value-access property, to avoid creating the backdoor prohibited by §1.4. This path is only accessible via `internal`. Here is its performance data:

**Benchmark** (2048-key heterogeneous dictionary `ToObject` iteration, 80% value types):

| Metric | Generated Side | Boxed Side |
|------|-------|-------|
| Throughput | ~404 Mops/s | ~2800 Mops/s |
| Ratio | **0.14x** (boxed 6.9x faster) | — |
| Allocation | 37.49 MB | 0 B |

This is the inherent cost of the type-erased path. **But it is not a real hot path**:

- Framework-internal hot/warm paths (data change signal handling, load validation, entity observation) uniformly use zero-boxing `TryGetXxx`
- `TypedDataObjectConverter.ToObject` is only used for framework-internal serialization and cold paths, not exposed to external callers
- **Even the worst case will not appear on a hot path** — this path is only open to internal serialization and debug code

---

### 4.6 Adapter Layer Extension Chain: From Compilation to Runtime

#### Why a Two-Layer Architecture Is Needed

Origo.Core defines `TypedData`; Origo.GodotAdapter is a separate, independent DLL compiled **after** Core. A single centralized code generation cannot work — when Core is being generated, adapter layer metadata does not yet exist.

Solution: Let each layer compile and generate independently, assembling via `ModuleInitializer` at runtime.

#### Runtime Assembly Flow

```
Program start
  ├─ Origo.Core.dll loaded
  │    └─ ModuleInitializer runs
  │         ├─ RegisterKind(1, typeof(byte))
  │         ├─ RegisterKind(5, typeof(int))
  │         └─ ... 13 primitive types ...
  │
  └─ Origo.GodotAdapter.dll loaded (depends on Core, therefore always after)
       └─ ModuleInitializer runs
            ├─ RegisterKind(128, typeof(Vector2))
            ├─ RegisterKind(130, typeof(Vector3))
            └─ ... 14 Godot types ...
            ├─ RegisterKindResolver(if-else chain for Godot types)
            ├─ RegisterFromObjectFallback(switch for Godot types)
            └─ RegisterToObjectFallback(switch for Godot types)
```

#### Adapter Layer Read Resolution Flow

```csharp
// When calling TypedDataFactory<Vector3>.TryExtract(td, out v3)
// 1. All 13 typeof(T)==... branches miss (T is Vector3, not in Core's registration list)
// 2. Fall into fallback: TypedDataObjectConverter.ToObject(td)
// 3. switch(td._kind): cases 0-13 miss (kind is not 130)
// 4. TypedDataLayeredRegistry.ResolveToObject(td)
//    → iterate delegate chain
//    → Adapter's ToObject callback hits case 130 → return td._ref
//    → Vector3 extracted from _ref
// 5. obj is T t1 → true → return
```

#### TypedDataLayeredRegistry

```csharp
internal static class TypedDataLayeredRegistry
{
    // Multi-layer delegate chain, each layer registers one callback
    private static Func<Type, byte>? _kindResolverChain;
    private static Func<byte, object, (long, object?)?>? _fromObjectChain;
    private static Func<TypedData, object?>? _toObjectChain;

    // ResolveXxx iterates GetInvocationList() calling chain, returns first non-null/0 result
}
```

The delegate assembly order is the `ModuleInitializer` execution order (Core before Adapter, matching DLL load dependency direction).

#### Why Adapter Layer Types Cannot Be Inlined

Adapter-registered Godot types (`Vector3` 12 bytes, `Color` 16 bytes, etc.) exceed `_inlineBits`'s 8-byte capacity, and the Source Generator runs during Core compilation — at that point, the byte layout of external engine types on different platforms cannot be reliably inferred. Safety strategy: adapter layer types uniformly go through `_ref`; the Kind byte still provides zero-overhead dispatch (avoiding `is T` virtual checks).

---

## 5. Cache Effects and Structural Limitations

### 5.1 Struct Size vs. Cache Line

CPU cache lines are 64 bytes. `TypedData` is 24 bytes, so one cache line holds only 2.66 elements; whereas the value slot in a `Dictionary<string, object>` entries array is an 8-byte reference, with one cache line holding 8.

This means that during array traversal, `TypedData` has a lower cache hit rate. This is the **structural reason for the 1.06-1.10x single-read and 1.31-1.38x DictLookup ratios** — not instruction count, but cache affinity.

### 5.2 Internal Field Offsets

```
offset 0:  _kind (1B)
offset 1:  padding (7B)
offset 8:  _inlineBits (8B)
offset 16: _ref (8B)
```

`_ref` at offset 16, with a 24-byte stride, is more prone to crossing two cache line boundaries in loops (offset 16 + stride 24 × N → frequently hitting offsets 40, 64, etc.), further degrading `TryGetString`'s array traversal performance (~1.40x, high variance, approximately 391–485 Mops/s).

### 5.3 Why It Cannot Be Squeezed to 16 Bytes

`long` and managed reference `object?` cannot share memory — the GC must be able to independently scan each reference pointer to determine object liveness. If `_ref` overlapped with the high 8 bytes of `_inlineBits`, the GC could not tell whether it is a number or a pointer.

Thus the minimum safe layout is: `byte (1B) + padding (7B) + long (8B) + reference (8B) = 24B`. After evaluation, sacrificing `double`/`long`'s full 64-bit inlining capability or introducing additional branching to bring a 1.10x marginal gap to parity is a net negative.

---

## 6. Performance Panorama

> Data from [benchmarks/baseline.en.md](../benchmarks/baseline.en.md), sampling environment: AMD Ryzen 7 9700X / .NET 10.0.9

### Writes (Generated 12-13x, 0 Byte Allocation)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Allocation |
|------|--------------:|--------------:|:----:|------|
| Int32 | 908 | 69.6 | 13.0x | 0 B / 107 MB |
| Single | 866 | 71.4 | 12.1x | 0 B / 107 MB |
| Boolean | 891 | 71.3 | 12.5x | 0 B / 107 MB |
| String | 563 | 110 | 5.1x | 0 B / 61 MB |

### Reads (Single reads nearly tied, multi-type dispatch pulls ahead)

| Scenario | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| Int32 single read | 550 | 581 | 1.06x | Boxed |
| Mixed dispatch (5 types) | ~1250 | ~812 | **1.54x** | Generated |
| Cast chain (4 types) | 351 | 251 | **1.40x** | Generated |
| Observer notification | ~5390 | ~5170 | ~1.0x | Tie |
| String IsString | 586 | 576 | 1.02x | Generated |

### Dictionary Construction + Insertion (Generated 2.9x, 3 MB Less Allocation)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Allocation (Gen/Boxed) |
|------|--------------:|--------------:|:----:|----------------|
| String | 193 | 144 | 1.34x | 23.53 MB / 14.97 MB |
| Int32 | 218 | 75.9 | **2.88x** | 23.53 MB / 26.42 MB |
| Boolean | 226 | 76.7 | **2.94x** | 23.53 MB / 26.42 MB |

> String insertion generated side slightly more allocation: `Dictionary<string, TypedData>`'s entries array embeds 24-byte structs, requiring a larger backing array than `Dictionary<string, object>`'s 8-byte references. For value type insertion, the 2.9x throughput gain fully covers this overhead.

### Conclusion

- **Writes** are 12-13x faster than boxing, with zero heap allocation. This is the greatest benefit of the generated approach.
- **Reads** are not inferior to boxing (≤ 1.10x), with mixed dispatch pulling ahead (1.54x) and cast chains pulling ahead (1.40x).
- The only losing item (`TypedDataObjectConverter.ToObject` iteration at 0.14x) is unavoidable boxing overhead in the type-erased path, limited to framework-internal cold path calls with no public entry point.

---

## 7. How to Extend

### 7.1 Adding a New System Primitive Type (Register in Core Layer)

If the framework needs to support a new type (e.g., C# `nint`, a future BCL ≤8-byte value type):

1. Append `typeof(...)` to the `[SndInlineTypes]` array in `Origo.Core/AssemblyAttributes.cs`
2. Add the corresponding `SpecialType` match in `TypedDataGenerator.cs`'s `IsInlineCandidate` and `GenerateKindName`
3. If the type has special read/write logic (e.g., `float`'s `BitConverter`), add handling in the `InlineTypeExprs` helper in `TypedDataGenerator.cs` (`Pack` / `Unpack` / `FromObject`) — the single source for all bit-pattern expressions, shared by the home accessor, conversion, and factory generation
4. Run `bash scripts/test.sh` to pass full test suite + coverage gate
5. Update Changelog and performance data tables in this document

### 7.2 Adding Adapter Layer Types (Register in a New Adapter Assembly)

1. Add `[assembly: SndInlineTypes(startKind: <unoccupied range>, typeof(NewType), ...)]` to the new assembly
2. Choose Kind range: check the validation range in `TypedDataGenerator.cs`'s `KindValue` (1-254), ensure no overlap with other adapters
3. Source Generator will auto-detect this assembly is not Home → use Adapter mode → generate complete extension methods + ModuleInitializer registration chain
4. Adapter layer types go through `_ref` path (unless they are ≤8-byte system primitive types, but such types should not be registered by adapters — `ORIGOSG001` will block them)

### 7.3 Adding New Source Generator Diagnostic Rules

Append `static readonly DiagnosticDescriptor` in the field area of `TypedDataGenerator.cs`, and append validation logic in `ValidateAndFilter`. Note: for each added validation, a corresponding `ORIGOSG00X` test case must also be added (in the `Origo.SourceGeneration.Tests` project).

---

## 8. Related Documents

| Document | Content |
|------|------|
| [Origo.SourceGeneration README](README.en.md) | Dual-mode architecture, generated content catalog, registration mechanism, design decisions |
| [TypedData documentation](../Origo.Core/Snd/Metadata/README.en.md) | TypedData struct, access patterns, recommended usage |
| [Performance baseline](../benchmarks/baseline.en.md) | All benchmark data, methodology, validity limitations |
| [Origo.Core.Tests / Benchmarks](../Origo.Core.Tests/Benchmarks.en.md) | Real-world simulation benchmark notes |

---

[↑ Back to Origo.SourceGeneration](README.en.md)
