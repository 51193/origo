<!-- docsync-pair: Origo.SourceGeneration.Tests/Benchmarks -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# TypedData Generated Product Performance Benchmarks

> [↑ Back to Origo.SourceGeneration.Tests](README.en.md)
> [↔ Module under test: Origo.SourceGeneration](../Origo.SourceGeneration/README.en.md)
> [↔ Performance baseline: baseline](../benchmarks/baseline.en.md)

## Behavior Under Test Overview

Verifies the performance of `TypedData`'s generated inline storage model against
an unoptimized boxing baseline. Benchmarks reference real `Origo.Core` but use
only `TypedData`'s public API. Marked `[Trait("Category","Benchmark")]` and run
independently of coverage gates.

## Test Files

| File | Verification Focus |
|------|-------------------|
| `Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | Multi-value-type + `string` write/read/mixed-dispatch throughput and memory allocation |

## Happy Paths (Performance Benchmarks)

| Test Method | Verified Behavior | Doc Source |
|------------|-------------------|------------|
| `ValueTypes_WriteThroughput_GeneratedOperator_vs_BoxedClass` | Value type (`int`/`long`/`float`/`double`/`bool`/`char`) writes: generated `explicit operator` vs boxed class, within budget | Origo.SourceGeneration |
| `ValueTypes_ReadThroughput_GeneratedKind_vs_BoxedIsT` | Value type reads: generated `TryGetXxx` (Kind dispatch) vs boxed `Data is T`, hit counts match and within budget | Origo.SourceGeneration |
| `ReferenceType_String_GeneratedRefSlot_vs_BoxedClass` | `string` write and read via `_ref` slot vs boxed class | Origo.SourceGeneration |
| `StringRead_IsString_vs_BoxedIsT` | `string` `IsString` check vs boxed `Data is string` | Origo.SourceGeneration |
| `MixedDispatch_GeneratedKind_vs_BoxedIsT` | Mixed type pool (int/float/bool/string/double) Kind dispatch vs boxed `is T`, hit counts match and within budget | Origo.SourceGeneration |

## Test Support Infrastructure

| Facility | Type | Purpose |
|---------|------|--------|
| `PerfReporter` | Public class | Performance comparison table output (writes to both console and xUnit test output), injected by benchmark cases via `PerfReporter.ForTest` |
| `OldTypedData` | Internal class | Boxing baseline comparison object: stores as `Type` + `object?`, simulating the unoptimized scenario |

## Benchmark Design Decisions

Each benchmark uses a fixed-capacity pool (bitmask addressing, constant memory),
large iteration counts, one warmup round plus multiple timed rounds taking each
side's minimum elapsed time (excluding preempted/GC outlier rounds). Relaxed
thresholds (generated path ≤ 8× baseline, single benchmark total elapsed below
upper cap) guard against "severe performance regression / stalling" rather than
pinning absolute performance numbers.

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Performance benchmarks do not cover read/write throughput of adapter layer non-system value types (such as Godot types), only covering system primitives + `string` | Adapter layer `_ref` path performance characteristics are not benchmarked | Origo.SourceGeneration |

---

[↑ Back to Origo.SourceGeneration.Tests](README.en.md)
