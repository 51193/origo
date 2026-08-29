<!-- docsync-pair: Origo.Core.Tests/Benchmarks -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Performance Benchmarks

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.en.md) · [↔ SG Pure Micro-Benchmarks: Origo.SourceGeneration.Tests](../Origo.SourceGeneration.Tests/README.en.md)

## Behavior Under Test Overview

This benchmark suite covers throughput and allocation characteristics of Origo framework's core subsystems.
TypedData-related benchmarks compare source-generated `TypedData` (inline storage + Kind dispatch) against
an unoptimized "boxed into `Dictionary<string, object>`" implementation on paths close to real usage;
remaining benchmarks measure performance characteristics of entity lifecycle, Observer topology,
DataSourceNode tree construction/traversal, Blackboard read/write, Save persistence, concurrent queues,
random number generation, and Strategy pool subsystems.

Benchmarks are marked `[Trait("Category","Benchmark")]`, excluded from `test.sh`'s full test run via
`--filter "Category!=Benchmark"`, and instead run as a separate step by `scripts/benchmark.sh`.
This script runs this suite, [SG pure micro-benchmarks](../Origo.SourceGeneration.Tests/README.en.md),
and [Godot adapter benchmarks](../Origo.GodotAdapter.Tests/Serialization.en.md) together, and verifies that every `BENCH` metric key is unique within a run before comparing baselines; duplicate keys fail immediately.

> **Performance numbers are not recorded in this document.** Volatile absolute throughput and ratio
> snapshots live in the authoritative baseline [benchmarks/baseline.md](../benchmarks/baseline.en.md);
> this document only describes the capabilities under test and design intent.

## Included Files

| File | Responsibility |
|------|---------------|
| `Benchmarks/TypedDataRealWorldBenchmarkTests.cs` | Five TypedData real-world simulation benchmarks: dictionary lookup/insert, numeric coercing chain, observer notification, heterogeneous dictionary iteration |
| `Benchmarks/EntityLifecycleBenchmarkTests.cs` | Entity creation + AfterSpawn scaling, frame processing (ProcessAll) scaling, BuildMetaData serialization throughput |
| `Benchmarks/ObserverTopologyBenchmarkTests.cs` | ObserverTopology Mount/Unmount binding count scaling |
| `Benchmarks/DataSourceNodeBenchmarkTests.cs` | DataSourceNode tree construction, SHA-256 hash computation, As\<T\> type dispatch throughput |
| `Benchmarks/BlackboardBenchmarkTests.cs` | In-memory Blackboard SetValue/TryGet bulk throughput, SerializeAll+DeserializeAll round-trip |
| `Benchmarks/SavePayloadBenchmarkTests.cs` | SavePayload hash computation, WriteToCurrent+ReadFromCurrent round-trip, Snapshot round-trip |
| `Benchmarks/ConcurrentActionQueueBenchmarkTests.cs` | ConcurrentActionQueue Enqueue+ExecuteAll scaling, Enqueue throughput |
| `Benchmarks/RandomGeneratorBenchmarkTests.cs` | XorShift128+ NextUInt64/NextInt64/NextInt32 throughput |
| `Snd/Strategy/SndStrategyPerformanceTests.cs` | Strategy pool Get/Release round-trip, Process frame handling scaling, TriggerAll allocation |
| `Origo.TestSupport/Reporting/PerfReporter.cs` | Performance comparison table outputter (`Report`, `Compare`, `CompareTable`, `ReportTable`), writes to both console and xUnit test output; duplicate `BENCH` metric keys within one process throw `InvalidOperationException` |
| `TestSupport/PerfReporterMetricKeyUniquenessTests.cs` | Pins the PerfReporter metric-key uniqueness guard: duplicate keys must fail immediately |

## Benchmark Methods

### TypedDataRealWorldBenchmarkTests

| Benchmark Method | Simulated Real Path | Comparison Content |
|-----------------|---------------------|-------------------|
| `DictLookup_TryExtract_vs_BoxedDict` | `SndDataManager.TryGetData<T>`: dictionary lookup hit then extract via `TryGetXxx` | `Dictionary<string,TypedData>` + `TryGetXxx` vs `Dictionary<string,object>` + `is T` |
| `DictInsert_FactoryCreate_vs_BoxedDict` | `SndDataManager.SetData<T>`: construct value and write to dictionary | Generated factory `Create` / implicit conversion + insert vs boxed insert |
| `MultiTypeExtractionChain_Generated_vs_Boxed` | `TryGetNumeric` cross-numeric type sequential try-read | Generated `TryGetSingle→TryGetInt32→TryGetInt64→TryGetDouble` chain vs `is float→is int→is long→is double` chain |
| `ObserverNotify_Generated_vs_Boxed` | Observer callback passing old/new values with type check | Passing `(TypedData, TypedData)` + `TryGetString` vs passing `(object?, object?)` + `is string` |
| `HeterogeneousDictIteration_GeneratedData_vs_BoxedDict` | Iterating heterogeneous data dictionary, converting each entry to `object` via `TypedDataObjectConverter.ToObject` | Generated `ToObject` (object-ification) vs pure `object` pass-through |

### EntityLifecycleBenchmarkTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `EntityCreation_ScalingByEntityCount` | 100/500/2000 entity creation + `FireAfterSpawnHooks` throughput and allocation |
| `FrameProcessing_ScalingByEntityAndStrategyCount` | 200-frame ProcessAll throughput under 10e×1s / 50e×5s / 200e×10s configurations |
| `EntitySaveSingle_ScalingByEntityCount` | 10/100/500 entity `BuildMetaData` (serialization metadata construction) throughput |

### ObserverTopologyBenchmarkTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `ObserverMount_ScalingByBindingCount` | 10/50/200 binding Mount throughput |
| `ObserverUnmount_ScalingByBindingCount` | 10/50/200 binding Unmount throughput |

### DataSourceNodeBenchmarkTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `TreeBuild_ScalingByDepthAndWidth` | d2w5/d3w8/d4w8 tree construction throughput and allocation |
| `TreeTraversalAndHashCompute` | d3w8/d4w8 tree `ComputeSha256Hash` throughput |
| `AsT_TypeDispatchThroughput` | Number/Text/Bool type dispatch on 500k iterations × 100-element array |

### BlackboardBenchmarkTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `SetValue_BulkWrite_ThroughputByType` | Int32/Single/String/Boolean 100k SetValue each, throughput and allocation |
| `TryGet_BulkRead_ThroughputByType` | Int32/Single/String/Boolean 500k TryGet each, throughput and allocation |
| `SerializeAllDeserializeAll_Roundtrip` | 100/500/1000 key SerializeAll+DeserializeAll round-trip throughput |

### SavePayloadBenchmarkTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `PayloadHashCompute_ScalingByEntityCount` | 10/100/500 entity `ComputePayloadHash` throughput |
| `PayloadWriteAndRead_Roundtrip` | `WriteToCurrent` + `ReadFromCurrent` round-trip |
| `PayloadSnapshotWriteAndRead_Roundtrip` | `WriteSavePayloadToCurrentThenSnapshot` + `ReadSavePayloadFromSnapshot` round-trip |

### ConcurrentActionQueueBenchmarkTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `EnqueueAndExecuteAll_ScalingByActionCount` | 100/1000/10000 action `Enqueue`+`ExecuteAll` throughput |
| `EnqueueThroughput_BulkInsert` | 1000/10000/50000 action `Enqueue` bulk throughput |

### RandomGeneratorBenchmarkTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `NextUInt64_Throughput` | 10M `NextUInt64` throughput |
| `NextFunctions_ThroughputComparison` | 5M `NextUInt64`/`NextInt64`/`NextInt32` comparison |

### SndStrategyPerformanceTests

| Benchmark Method | Test Content |
|-----------------|-------------|
| `StrategyPool_GetRelease_Throughput` | 100k Get+Release round-trips |
| `StrategyManager_Process_StrategyCountScaling` | 1/5/10/20 strategies × 10k frames ProcessAll |
| `TriggerAll_AfterSpawn_AllocationByStrategyCount` | 1/10 strategies AfterSpawn TriggerAll allocation |

Each benchmark dataset mixes five types (`int`/`float`/`bool`/`string`/`double`, rotated via `i % 5`)
to reflect the real distribution of heterogeneous SND data.

## Test Support Facilities

`PerfReporter` (see [Origo.Core.Tests README — Test Support Facilities](README.en.md)) prints in a unified
table format "Method / Iterations / Time / Throughput / Allocation" with dual-channel output to both
`Console.Out` and `ITestOutputHelper`, ensuring results visible in both CI and local.

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Does not cover real-world throughput of GodotAdapter registered types (`Vector2`/`Vector3`, etc.) | Adapter-layer multi-layer dispatch performance not verified in this suite | Covered separately by `GodotTypedDataPerformanceTests` in [Origo.GodotAdapter.Tests/Serialization](../Origo.GodotAdapter.Tests/Serialization.en.md) |
| Does not cover concurrent/multi-threaded read/write | Contention and visibility under multi-threading not tested | Framework uses single-threaded frame model (see [manual root README — Design Principles](../README.en.md)) |
| Does not cover Kind-dispatch alternative iteration paths | Only tests the `ToObject` (object-ification) iteration path | [Snd/Metadata](../Origo.Core/Snd/Metadata/README.en.md) Kind dispatch |
| Absolute throughput/ratio/allocation not asserted (only prints + single-benchmark time cap) | Performance degradations cannot be auto-captured, require manual baseline comparison | [benchmarks/baseline.md](../benchmarks/baseline.en.md) |

## Design Decisions

### Why benchmarks are loose: only time cap, no "faster" assertions

This suite only asserts each benchmark's total time below an 8-second cap (`AssertInCap`), without
fixed ratio caps or requiring the generated path to be faster than the boxed baseline. Real paths
layer dictionary lookup, object conversion, etc. costs; the generated path is intentionally allowed
to be slower than the boxed baseline in some read scenarios (see baseline file for details). The
benchmark's purpose is "guard against hanging and enable long-term relative trend tracking", not to
lock absolute numbers or enforce no-regression — the latter would trigger excessive false alarms
due to machine and runtime variance.

> This is slightly different from [SG pure micro-benchmarks](../Origo.SourceGeneration.Tests/README.en.md):
> the SG suite additionally asserts the generated path is within 8× of the baseline; this suite only
> keeps the time cap because the real path baselines are more complex.

### Why mark `Category=Benchmark` and run independently

Benchmarks have large iteration counts and long run times. Running them together with coverage-gated
tests would slow down regular CI, and they should not count toward coverage. Marking
`[Trait("Category","Benchmark")]` lets `test.sh` exclude them via `--filter "Category!=Benchmark"`,
running them separately in `scripts/benchmark.sh` as an independent step, both printing comparison
tables and executing loose assertions, avoiding being run twice.

### Why use fixed datasets + warmup + min(reps) for noise reduction

To resist measurement noise from OS time-slice rotation and GC, each benchmark uses a fixed-capacity
dictionary/dataset (constant memory), large iteration counts (so a single round spans multiple time
slices), and takes the minimum time across rounds for each side (generated & boxed), discarding
outlier rounds from preemption/GC.

### Why allocation measurement lives in separate NoInlining methods

Each benchmark, outside the timing rounds, does one dedicated measurement round for each of the
generated/boxed sides, taking the difference of `GC.GetAllocatedBytesForCurrentThread()` before and
after as the allocation for that round. The measurement loop is extracted into a separate
`[MethodImpl(NoInlining)]` method: if inlined into the timing method body (or using a lambda that
captures locals), it would change the codegen of the timing loop (closure field indirection, loop
alignment), contaminating throughput numbers. Keeping it in a separate method preserves the
uninstrumented codegen of the timing loop, with allocation and time not interfering.

### Why the benchmarks go through TypedDataObjectConverter

All of `TypedData`'s `AsXxx` accessors and the object converter are `internal` (the test
projects reach them via `InternalsVisibleTo`). The heterogeneous-iteration benchmark measures
the "compile-time-unknown-type object-ification" cold path through internal
`TypedDataObjectConverter.ToObject` — the real call shape of serialization, console, and
`ToString` cold paths; hot/warm paths (data-change signal handling, load validation) use the
zero-allocation `TryGetXxx` accessors (see [Snd/Metadata](../Origo.Core/Snd/Metadata/README.en.md)).
needing to expose Core internal members to the test project, keeping the benchmark's calling shape
consistent with real downstream consumers.

---

> [↑ Back to Origo.Core.Tests](README.en.md)
