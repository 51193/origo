<!-- docsync-pair: Origo.SourceGeneration.Tests/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.SourceGeneration.Tests

> [↑ Back to Origo.manual](../README.en.md) · [↔ Module Under Test: Origo.SourceGeneration](../Origo.SourceGeneration/README.en.md)

## Overview

`Origo.SourceGeneration.Tests` contains two categories of tests:

- **Generator behavior tests**: Directly drive `TypedDataGenerator`, run the generator on in-memory compilations and assert the generated sources, generator diagnostics, and the result of merged compilation of "original source + generated source". It references the generator as a regular library (not as an analyzer attachment), enabling instantiation and execution within tests.
- **Generated artifact performance benchmarks**: Obtain the generated `TypedData` by referencing `Origo.Core` (public API plus internal members accessed via `InternalsVisibleTo`), comparing throughput of inline storage/Kind dispatch against an unoptimized boxing implementation across multiple value types and the reference type `string`. Marked `[Trait("Category","Benchmark")]`, run once in a separate CI step (`scripts/benchmark.sh`) and printing comparison tables, separated from test runs subject to coverage gates.

## Files

| File | Responsibility |
|------|------|
| `GeneratorTestHarness.cs` | Constructs in-memory `CSharpCompilation`, runs `TypedDataGenerator`, exposes generated sources, generator diagnostics, merged compilation errors |
| `TypedDataGeneratorTests.cs` | Generator behavior tests: Home/Adapter mode output, two storage models, `ORIGOSG001`–`ORIGOSG004` diagnostics, generation determinism and incremental pipeline |
| `Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | Generated artifact performance benchmarks: write/read/mixed dispatch for multiple value types + `string`, generated inline `TypedData` vs unoptimized boxing; fixed pool + large iterations + multi-round min noise reduction, relaxed thresholds + comparison tables, and per-side measured allocation (`GC.GetAllocatedBytesForCurrentThread`, placed in separate `NoInlining` methods to avoid polluting timing) |
| `TestSupport/PerfReporter.cs` | Performance comparison table output (writes to both console and xUnit test output) |

## TypedDataGeneratorTests Test Details

### Happy Paths

| Test Method | Verified Behavior | Doc Source |
|---------|-----------|---------|
| `Home_Primitives_GeneratesExpectedMembers_AndCompiles` | Home mode registers system primitive types, generates `KindMap`/`TryGetInt32`/`AsInt32`/`explicit operator`/`TypedDataFactory<T>`/`TypedDataHomeKindRegistration` + `[ModuleInitializer]`, merged compilation zero errors | Origo.SourceGeneration |
| `Home_StringStoredViaRefSlot` | `string` accessed via `_ref` slot (`AsString() => (string?)_ref`, `case 13: return td._ref`) | Origo.SourceGeneration |
| `Adapter_ValueAndRefTypes_UseRefSlot_AndCompiles` | Adapter layer non-system value types and reference types uniformly go through `_ref`, generating `TypedDataLayeredExtensions`, `RegisterKind`, Converter/TypeMap branches, merged compilation zero errors | Origo.SourceGeneration |
| `Generation_IsDeterministic` | Same input run twice produces completely identical source text | Origo.SourceGeneration |
| `StartKind_OffsetIsHonored_AndNumberingIsSequential` | `StartKind` offset honored (128/129), numbering is sequential per declaration order | Origo.SourceGeneration |
| `OverlappingStartKinds_SameType_Deduplicated` | Same type declared redundantly in overlapping `StartKind` groups is deduplicated, no diagnostics, no compilation errors | Origo.SourceGeneration |
| `Incremental_SameInputTwice_ProducesIdenticalOutput` | Same input run twice consecutively, generated sources are item-by-item identical | Origo.SourceGeneration |
| `Incremental_SameInputTwice_NoAdditionalOutputs` | Same input run three times, first and third run generated source count and content match (no extra output) | Origo.SourceGeneration |
| `Incremental_UnrelatedCodeChange_GeneratedOutputUnchanged` | Appending an unrelated comment does not change generated output | Origo.SourceGeneration |
| `Incremental_NoAttribute_ThenAddAttribute_ProducesNewOutput` | From no attribute to adding attribute, output changes from empty to non-empty | Origo.SourceGeneration |
| `Incremental_HasAttribute_ThenRemoveAttribute_OutputDisappears` | From having attribute to removing attribute, output changes from non-empty to empty | Origo.SourceGeneration |
| `Incremental_AddTypeToExistingAttribute_OutputChanges` | Adding a type to an existing attribute, output gains corresponding type member (`Single`) | Origo.SourceGeneration |

### Error Paths

| Test Method | Triggered Error | Expected Behavior |
|---------|-----------|---------|
| `Home_UnsupportedValueType_ReportsORIGOSG002_ButStillGeneratesValidTypes` | Home group registers uninlinable value type (`decimal`) | Reports `ORIGOSG002` (Error), excludes only the unsupported type, `int` still generated normally with merged compilation zero errors |
| `Adapter_SystemPrimitive_ReportsORIGOSG001` | Adapter group registers system primitive type (`int`) | Reports `ORIGOSG001` (Error), excludes the primitive, does not produce inline accessors for it |
| `KindPastByteRange_ReportsORIGOSG003_IncludingWrapToNonZero` | `StartKind` offset causes Kind to exceed byte range (256/257, 257 wraps to 1 causing collision) | Reports `ORIGOSG003` (Error), excludes out-of-range types, in-range type (`Byte=255`) still generated |
| `OverlappingStartKindRanges_ReportORIGOSG004_AndDropCollidingTypes` | Two groups assign same Kind 1 to different types (`int`/`long`) | Reports `ORIGOSG004` (Error), excludes the two colliding types |
| `HomeAndAdapterCoexistence_HomeWins_NonSystemTypesRejected` | Same compilation has both Home attribute (system primitive types) and Adapter attribute (non-system value types) | Home mode takes effect, system types generated normally, non-system value types report `ORIGOSG002` (Error) |

### Edge Paths

| Test Method | Edge Condition | Expected Behavior |
|---------|---------|---------|
| `Home_NoAttribute_ProducesNoOutput` | No `SndInlineTypes` attribute | Produces no sources, no diagnostics |
| `MalformedAttribute_NoTypes_ProducesNoOutput` | Declares `SndInlineTypes` but passes no type arguments | Produces no sources, no diagnostics |
| `Home_OnlyReferenceTypes_NoInlineMethods` | Only registers reference types (`string`) | Generates `KindMap`/`AsString`, but does not produce `explicit operator`/`_inlineBits` inline mechanisms |
| `Home_DoesNotEmitSilentStubHelpers` | Home mode generation (regression guard) | Never generates `BitsFrom`/`ReadBitsAs`/`Pack`/`return default;` silent stub helpers |
| `Adapter_DoesNotEmitInlineHelpers` | Adapter mode generation (regression guard) | Does not produce `ReadBitsAs`/`BitsFrom`/`_inlineBits` |

## Benchmarks/TypedDataGeneratedBenchmarkTests Test Details

> See [Benchmarks.en.md](Benchmarks.en.md). Marked `[Trait("Category","Benchmark")]` (class-level), run only by `scripts/benchmark.sh`.

## Test Support Infrastructure

| Facility | Type | Purpose |
|------|------|------|
| `GeneratorTestHarness` | Internal static class | Constructs in-memory `CSharpCompilation` (with trusted platform assemblies as references, excluding `Origo.*`), runs `TypedDataGenerator`, exposes generated sources, generator diagnostics, merged compilation errors; also provides `CreateTrackedDriver`/`RunIncremental` for incremental pipeline assertions |
| `GeneratorOutput` | Internal record | Encapsulates generated sources array, generator diagnostics, merged compilation errors, provides `AllGeneratedText` and `HasGeneratorDiagnostic(id)` |
| `PerfReporter` | Public class | Performance comparison table output (writes to both console and xUnit test output), injected by benchmark cases via `PerfReporter.ForTest` |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|---------|------|---------|
| Performance benchmarks do not cover read/write throughput of adapter layer non-system value types (such as Godot types), only covering system primitives + `string` | Adapter layer `_ref` path performance characteristics are not benchmarked | Origo.SourceGeneration |

## Line Coverage Gate

Coverlet enforces `Origo.SourceGeneration` line coverage ≥ 90% (effective in CI and local `dotnet test` runs).

## Design Decisions

### Why use a generator driver instead of snapshot/Verifier frameworks

Directly using `CSharpGeneratorDriver` to drive the generator minimizes dependencies and allows asserting generated source text, generator diagnostics, and merged compilation results all within the same test. This seamlessly integrates with the repository's unified xUnit v3 usage without introducing additional verification framework dependencies.

### Why use runtime trusted platform assemblies as references while excluding Origo.* assemblies

The test compilation uses the current runtime's `TRUSTED_PLATFORM_ASSEMBLIES` as metadata references, enabling the in-memory compilation to resolve arbitrary BCL usages (`BitConverter`, `Unsafe`, `ModuleInitializer`, etc.) without fixed reference assembly packages. `Origo.*` assemblies are explicitly excluded: the performance benchmarks reference the real `Origo.Core`, which brings it into the test process's trusted platform assembly list, while the generator driver tests simulate `Origo.Core.Snd.Metadata` types with embedded source scaffolds — if the in-memory compilation also referenced the real `Origo.Core`, same-named types would conflict (CS0433).

### Why Adapter cases reference a separate host assembly and declare InternalsVisibleTo

The generator determines Home/Adapter mode by the assembly that defines `TypedData`. Adapter cases place the `TypedData` definition in a referenced host assembly, making the current compilation recognized as an adapter layer; the host assembly declares `InternalsVisibleTo` so that generated adapter layer code can access `TypedData`'s internal fields, matching the real relationship between Origo.Core/Origo.GodotAdapter.

### Why performance benchmarks use generated internal members, relaxed thresholds, and run independently

Performance benchmarks reference the generated artifacts of the real `Origo.Core`, covering public API (explicit conversion operators, `TryGetXxx`, `TryGetString`, `Data`, `FromObject`) and — via `InternalsVisibleTo` — the generator-produced internal members (`TypedData.KindMap`, the internal constructor, the `IsString` discriminator property) to compare the generated path against an unoptimized boxing implementation precisely; `TypedDataFactory<T>` and other non-benchmark types are out of scope.

Benchmarks are relaxed: they do not require the generated path to be faster than the unoptimized boxing baseline, only asserting it does not exceed a fixed multiple of the baseline (8×, skipping ratio for baselines below 1ms as unreliable) and that each single benchmark has a total elapsed upper limit. The goal is to guard against "severe performance regression / stalling" rather than pin down absolute performance numbers.

To resist measurement noise from OS time slice rotation and GC, each benchmark uses a fixed-capacity pool (bitmask addressing, constant memory), large iteration counts (making single-round elapsed span multiple time slices), one round of warmup plus multiple timed rounds, taking each side's minimum elapsed time (excluding preempted/GC outlier rounds).

Benchmarks are marked `[Trait("Category","Benchmark")]`, excluded from `test.sh`'s full test run via `--filter "Category!=Benchmark"`, and instead run once in a separate step `scripts/benchmark.sh` (with detailed logger): printing comparison tables while executing relaxed assertions, avoiding benchmarks being run twice. `scripts/benchmark.sh` also runs Core's [real-world simulation performance benchmarks](../Origo.Core.Tests/Benchmarks.en.md) (dictionary lookup/insertion, observer notification, heterogeneous dictionary iteration, and other use-case-close scenarios) in the same step.

---

> [↑ Back to Origo.manual](../README.en.md)
