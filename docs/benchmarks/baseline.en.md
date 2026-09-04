<!-- docsync-pair: benchmarks/baseline -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Origo Performance Baseline

> [↑ Back to Origo.manual](../README.en.md)

> A snapshot of the performance status of Origo framework subsystems, serving as the authoritative reference for future performance optimization.
> Values are tightly coupled to the runtime environment and runtime version; cross-machine comparison is not meaningful. Before-and-after optimization comparisons must be retested **in the same environment with the same runtime**.

> The current `baseline.json` keeps only entries whose metric keys are unchanged and still correspond to the same measurement. Renamed or redesigned measurements have no baseline entry yet; run `bash scripts/benchmark.sh --update-baseline` on the baseline machine to refill and commit them.

## Reproduction

Run from the Origo source repository root:

```bash
bash scripts/benchmark.sh
```

This script runs three benchmark suites sequentially (all marked `[Trait("Category","Benchmark")]`, excluded from `test.sh`, run only once here):

- **SG Pure Micro-Benchmarks** — `Origo.SourceGeneration.Tests/Benchmarks/TypedDataGeneratedBenchmarkTests.cs`
- **Core Subsystem Benchmarks** — `Origo.Core.Tests` (TypedData real-world simulation + entity lifecycle + Observer topology + DataSourceNode + Blackboard + Save + concurrent queue + random + Strategy performance)
- **Godot Adapter Benchmarks** — `Origo.GodotAdapter.Tests` (Godot registered type TypedData read/write/conversion throughput)

## Regression Gate (benchmark.sh comparison)

`scripts/benchmark.sh` compares every run against `docs/benchmarks/baseline.json`
(a machine-readable baseline generated from the `BENCH|kind|label|side|ops|alloc`
lines emitted by `PerfReporter.EmitMetric`):

- **Regression gates run only when the run machine matches the baseline's
  `machine_id`**: throughput drop over 50% (min-of-rounds measurements
  `CompareTable` / `Compare` / `Report` only) and allocation growth over 20%
  both fail at that point
- **On a machine mismatch all numeric gates are skipped** (CI runners are
  random machines / fresh VMs; both throughput and allocation depend on CPU
  frequency scaling, tiered-JIT inlining decisions, and runtime builds, so
  neither is comparable across machines): the benchmark step acts as a smoke
  test that the benchmarks still run
- Local `scripts/ci.sh` runs on the baseline machine and catches real
  throughput and allocation regressions
- After a confirmed improvement or an environment change, run
  `bash scripts/benchmark.sh --update-baseline` and commit the refreshed baseline

## Sampling Metadata

| Item | Value |
|----|----|
| CPU | AMD Ryzen 7 9700X (8C/16T, 3.80 GHz base) |
| Memory / OS | 30 GiB / Ubuntu 26.04 LTS (Linux 7.0.0-30-generic) |
| .NET | SDK 10.0.400, runtime 10.0.11 (test target `net10.0`) |
| Build | `Release` |
| Sampling | Single run (TypedData real-world simulation uses 10 warmup rounds; min-of-rounds internally takes `min of 5`) |

> The tables below are a reference snapshot of the current baseline values;
> **the regression gate's data source is `docs/benchmarks/baseline.json`**
> (refreshed via `--update-baseline` as machine and runtime change).

## Methodology

- **Timing**: Fixed-capacity pool + bitmask addressing (constant memory), large iteration counts (single round spans multiple OS time slices), 10 warmup rounds + multiple rounds taking each side's minimum elapsed time (excluding preempted/GC outlier rounds).
- **Allocation**: Each benchmark runs a dedicated `[MethodImpl(NoInlining)]` measurement round per side, taking the difference of `GC.GetAllocatedBytesForCurrentThread()` before and after. Measurement is placed in a separate method so its loop body does not share code generation with the timing loop and does not affect throughput.
- **Alloc column**: Measured allocation per round (i.e., corresponding to the iteration count in the table), in "Generated / Boxed" format.

> **"Alloc" and relative trends are the most reliable criteria**: they are verbatim consistent across 4 rounds. Absolute throughput is subject to jitter from CPU frequency scaling and code alignment effects on trivial loops (see validity limitations at the end).

## SG Pure Micro-Benchmarks

### Write Throughput — Generated operator vs Boxed class (2,000,000 iterations)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner | Alloc Gen/Boxed |
|------|--------------:|--------------:|:----:|:----:|----------------|
| Int32 | 920.05 | 72.32 | 12.72x | Generated | 0 B / 106.81 MB |
| Int64 | 899.36 | 71.11 | 12.65x | Generated | 0 B / 106.81 MB |
| Single | 883.70 | 72.01 | 12.27x | Generated | 0 B / 106.81 MB |
| Double | 888.49 | 71.61 | 12.41x | Generated | 0 B / 106.81 MB |
| Boolean | 905.63 | 72.01 | 12.58x | Generated | 0 B / 106.81 MB |
| Char | 921.40 | 72.11 | 12.78x | Generated | 0 B / 106.81 MB |
| String (ref slot) | 602.90 | 130.03 | 4.64x | Generated | 0 B / 61.04 MB |

### Read Throughput — Generated TryGet/Kind vs Boxed `is T` (10,000,000 iterations, 0 B Alloc on both sides)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner | Stability |
|------|--------------:|--------------:|:----:|:----:|--------|
| Int32 | 551.94 | 656.18 | 0.84x | Boxed | Stable |
| Int64 | 554.01 | 652.83 | 0.85x | Boxed | Stable |
| Single | 564.36 | 654.69 | 0.86x | Boxed | Stable |
| Double | 560.50 | 597.51 | 0.94x | Boxed | Stable |
| Boolean | 527.18 | 651.17 | 0.81x | Boxed | Stable |
| Char | 551.92 | 595.73 | 0.93x | Boxed | Stable |
| String (`TryGetString`) | 416.40 | 659.57 | 0.63x | Boxed | High variance |
| String (`IsString`) | 572.32 | 660.44 | 0.87x | Boxed | Stable |

### Mixed Dispatch — Generated Kind switch vs Boxed `is T` (10,000,000 iterations, 0 B on both sides)

| Scenario | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| Mixed dispatch (int/float/bool/string/double) | 1249.41 | 815.57 | 1.53x | Generated |

## Core Real-World Simulation Benchmarks

### Heterogeneous Dictionary Iteration (2,048,000 `.Data` reads)

| Scenario | Generated .Data (Mops/s) | Boxed Iteration (Mops/s) | Ratio | Winner | Alloc Gen/Boxed |
|------|--------------------:|------------------:|:----:|:----:|----------------|
| Heterogeneous dict `.Data` iteration | 418.27 | 3024.22 | 7.2x | Boxed | 37.49 MB / 0 B |

> This is a **synthetic worst case**: `.Data` returns `object`, value types re-box every read through `ToObject` (dataset ~80% value types → 37.49 MB per round). This calling pattern does not exist in production (see "Design Trade-offs").

### Factory Construction + Dictionary Insertion (500,000 iterations)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner | Alloc Gen/Boxed |
|------|--------------:|--------------:|:----:|:----:|----------------|
| String | 177.12 | 194.73 | 0.91x | Boxed | 23.53 MB / 14.97 MB |
| Int32 | 209.13 | 138.85 | 1.51x | Generated | 23.53 MB / 26.42 MB |
| Single | 206.48 | 129.38 | 1.60x | Generated | 23.53 MB / 26.42 MB |
| Boolean | 207.56 | 130.85 | 1.59x | Generated | 23.53 MB / 26.42 MB |

> Value type insertion boxed side uses ~12 MB more boxing allocation (26.42 vs 23.53); generated is both faster and leaner. String insertion generated side has slightly more allocation (23.53 vs 14.97 MB): `Dictionary<string,TypedData>` embeds a 24-byte struct per entry, so the backing array is larger than `Dictionary<string,object>`'s 8-byte references.

### Observer Notification (2,000,000 iterations, 0 B on both sides)

| Scenario | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| Observer notify (old,new) + type check | 1863.06 | 1842.30 | 1.01x | Generated |

> Passed via `TypedData` (not `object`), using `TryGetString` for type check, zero boxing, on par with boxed `is string`.

### Dictionary Lookup TryExtract (2,000,000 iterations, 0 B on both sides)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner | Stability |
|------|--------------:|--------------:|:----:|:----:|--------|
| String | 196.00 | 185.94 | 1.05x | Generated | Stable |
| Int32 | 125.16 | 140.90 | 0.89x | Boxed | Stable |
| Single | 125.27 | 127.55 | 0.98x | Boxed | Stable |
| Boolean | 125.17 | 122.13 | 1.02x | Generated | Stable |

### Multi-type Cast Chain float→int→long→double (2,000,000 iterations, int payload, 0 B on both sides)

| Scenario | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| Numeric cast chain | 261.27 | 251.89 | 1.04x | Generated |

## Performance Status Overview

- **Value types**: Writes about 12.3–12.8x, mixed dispatch about 1.53x, dictionary construction+insertion 1.51–1.60x (String generated slightly slower than boxing); pure micro-benchmark single reads are currently boxed-faster (0.81–0.94x), while dictionary lookup is close to parity. The generated path remains zero-boxing, but the relative advantage varies with CPU generation.
- **string**: Writes 4.64x faster than boxing; `TryGetString` array reads about 1.58x slower, and the `IsString` path is also about 1.15x slower than boxing on the current CPU (structural, see below).
- **DictLookup**: String/Boolean generated slightly faster (1.05x/1.02x), Int32/Single close to parity (0.89x/0.98x).
- **Observer notification**: Generated 1.01x better than boxing.
- Both string-read paths are currently slightly faster with boxing; the gap comes from the cache behavior of the 24-byte `TypedData` layout rather than from generated-code instruction overhead (see below).

## Design Trade-offs and Evaluated Directions

Documents why the current state is as it is, what trade-offs were made, and directions **verified ineffective and should not be retried**, to avoid future iterations revisiting dead ends.

- **`TryGetString` uses `Unsafe.As<string>` (guarded by `_kind == String`) rather than `(string)_ref`**: The guard already proves `_ref` is a string; `castclass` is redundant. Moreover, `castclass` can throw exceptions, which blocks the JIT's elimination and hoisting optimizations for TryGetString calls whose results are discarded or loop-invariant. On **operation-bound** `TryGetString` paths (such as observer notifications), removing it brings the generated side to parity with boxed `is string`.

- **[Verified Ineffective, Do Not Retry] Removing `castclass` yields no measurable benefit for "array string reads"**: The same change on cache-bound array reads only yields noise-level variation (median ~+2%), not altering the boxed-is-about-1.58x-faster conclusion. The bottleneck on this path is **cache, not instructions** — `_ref` at struct offset 16, with a 24-byte stride on a 64-byte cache line, crosses cache line boundaries more easily than value type reads from `_inlineBits` (offset 8). Instruction-level micro-tuning cannot improve this; do not repeatedly attempt in this direction.

- **[Evaluated Not Worthwhile, Do Not Attempt] Squeezing the `TypedData` struct to 16 bytes**: The current 24 bytes (`byte _kind` + `long _inlineBits` + `object? _ref`) is the lower bound of GC-safe design — `long` and managed reference slots cannot overlap (GC must independently scan references), and the `_kind` byte has no spare bits to tuck. Squeezing to 16 bytes would require sacrificing `long`/`double`'s full 64-bit inlining, or introducing extra branching/type lookups (most likely net negative), and would change the `internal` layout (depended on by generated code and tests via `InternalsVisibleTo`). The structural gap of value type single reads and DictLookup values, which fluctuate between 0.89x and 1.24x and are mostly close to parity, originates from this and is accepted.

- **`.Data` (object?) boxing only occurs on cold paths**: `.Data` boxes value types through `ToObject`, serving cold paths where the type is unknown at compile time (serialization by `DataType`, console, `ToString`); these paths inherently require `object`. Framework-internal hot/warm paths (data change signal handling, load validation, etc.) uniformly use zero-boxing `TryGetXxx`. **The "heterogeneous `.Data` iteration" benchmark (about 7.2x, 37.49 MB) is a synthetic worst case and does not correspond to any real production hot path**; removing `.Data` would only move the same boxing inside and add complexity (zero downstream dependencies, ~60 test locations depend on its convenience access). Trade-offs and recommended usage are documented in [Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.en.md).

## Validity Limitations

1. **CPU dynamic frequency scaling (current scaling ≈ 88%)** introduces run-to-run jitter; for marginal items ≤ 1.3x and the fastest loops (boxed side heterogeneous iteration 6.3–8.4x, writes), retesting at fixed frequency/performance mode is recommended.
2. **Absolute throughput is affected by code alignment**: writes and other ultra-trivial loops are sensitive to method layout/alignment, with ~±8% jitter; **allocation counts and relative trends are unaffected by this** and should be the primary criteria.
3. **Runtime is .NET 10.0.11**, matching the `net10.0` test target; conclusions must be retested under the same runtime.
4. **Micro-benchmarks use min-of-rounds**, favoring ideal JIT steady state; real-world simulation suites are more representative.
5. High-variance items: String `TryGetString` read, mixed dispatch, boxed side heterogeneous iteration, boxed side value type insertion; for these items, increase sampling rounds (≥ 8 rounds) to converge before drawing conclusions.

## Subsystem Performance Baselines

> Below are performance snapshots of the framework's core subsystems, grouped by module. Each benchmark uses min-of-rounds or a single measurement.

### Entity Lifecycle

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| 100 entities create+spawn | 100 | 180.60 us | 553.71 Kops/s | 374.75 KB |
| 500 entities create+spawn | 500 | 867.80 us | 576.17 Kops/s | 1.82 MB |
| 2000 entities create+spawn | 2,000 | 3.47 ms | 575.71 Kops/s | 7.29 MB |

### Frame Processing

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| 10 entities × 1 strategy, 200 frames | 2,000 | 114.50 us | 17.47 Mops/s | 600 B |
| 50 entities × 5 strategies, 200 frames | 50,000 | 1.27 ms | 39.52 Mops/s | 3.16 KB |
| 200 entities × 10 strategies, 200 frames | 400,000 | 8.21 ms | 48.70 Mops/s | 20.35 KB |

### Entity Save (SaveSingle)

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| 10 entities SaveSingle | 10 | 46.30 us | 215.98 Kops/s | 9.57 KB |
| 100 entities SaveSingle | 100 | 104.00 us | 961.54 Kops/s | 95.35 KB |
| 500 entities SaveSingle | 500 | 264.90 us | 1.89 Mops/s | 476.60 KB |

### Observer Topology

#### Mount

| Scenario | Operations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Mount × 10 | 10 | 59.20 us | 168.92 Kops/s | 23.60 KB |
| Mount × 50 | 50 | 71.40 us | 700.28 Kops/s | 112.49 KB |
| Mount × 200 | 200 | 435.30 us | 459.45 Kops/s | 450.70 KB |

#### Unmount

| Scenario | Operations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Unmount × 10 | 10 | 105.20 us | 95.06 Kops/s | 19.79 KB |
| Unmount × 50 | 50 | 62.50 us | 800.00 Kops/s | 94.29 KB |
| Unmount × 200 | 200 | 389.70 us | 513.22 Kops/s | 374.63 KB |

### DataSourceNode Tree

| Scenario | Node Count | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Tree build d=2 w=5 | 31 | 159.00 us | 194.97 Kops/s | 40.16 KB |
| Tree build d=3 w=8 | 585 | 284.00 us | 2.06 Mops/s | 763.25 KB |
| Tree build d=4 w=8 | 4,681 | 2.69 ms | 1.74 Mops/s | 6.07 MB |
| SHA-256 hash d=3 w=8 | 585 | 4.52 ms | 129.33 Kops/s | 1.76 MB |
| SHA-256 hash d=4 w=8 | 4,681 | 7.36 ms | 635.74 Kops/s | 18.37 MB |
| As<T> dispatch | 50,000,000 | 613.23 ms | 81.54 Mops/s | 40 B |

### Blackboard

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| SetValue Int32 × 100k | 100,000 | 10.26 ms | 9.75 Mops/s | 16.70 MB |
| SetValue Single × 100k | 100,000 | 9.95 ms | 10.05 Mops/s | 18.76 MB |
| SetValue String × 100k | 100,000 | 15.61 ms | 6.41 Mops/s | 25.62 MB |
| SetValue Boolean × 100k | 100,000 | 9.15 ms | 10.93 Mops/s | 18.76 MB |
| TryGet Int32 × 500k | 500,000 | 54.89 ms | 9.11 Mops/s | 19.07 MB |
| TryGet Single × 500k | 500,000 | 42.41 ms | 11.79 Mops/s | 19.07 MB |
| TryGet String × 500k | 500,000 | 43.10 ms | 11.60 Mops/s | 19.07 MB |
| TryGet Boolean × 500k | 500,000 | 35.19 ms | 14.21 Mops/s | 19.07 MB |
| Serialize+Deserialize 100 keys | 200 | 40.80 us | 4.90 Mops/s | 4.84 KB |
| Serialize+Deserialize 500 keys | 1,000 | 32.70 us | 30.58 Mops/s | 22.62 KB |
| Serialize+Deserialize 1000 keys | 2,000 | 62.50 us | 32.00 Mops/s | 47.63 KB |

### Save Persistence

| Scenario | Entities | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| ComputePayloadHash 10e | 10 | 59.00 us | 169.49 Kops/s | 53.10 KB |
| ComputePayloadHash 100e | 100 | 253.20 us | 394.94 Kops/s | 503.36 KB |
| ComputePayloadHash 500e | 500 | 1.29 ms | 388.98 Kops/s | 2.31 MB |
| Write+Read 10e | 10 | 129.40 us | 77.28 Kops/s | 27.30 KB |
| Write+Read 100e | 100 | 143.10 us | 698.81 Kops/s | 135.49 KB |
| Write+Read 300e | 300 | 383.10 us | 783.09 Kops/s | 347.25 KB |
| Snapshot Write+Read 10e | 10 | 11.70 us | 854.70 Kops/s | 6.27 KB |
| Snapshot Write+Read 100e | 100 | 7.20 us | 13.89 Mops/s | 6.27 KB |
| Snapshot Write+Read 300e | 300 | 7.40 us | 40.54 Mops/s | 6.27 KB |

### Concurrent Queue

| Scenario | Operations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Enqueue+ExecuteAll × 100 | 100 | 30.30 us | 3.30 Mops/s | 896 B |
| Enqueue+ExecuteAll × 1,000 | 1,000 | 15.50 us | 64.52 Mops/s | 7.91 KB |
| Enqueue+ExecuteAll × 10,000 | 10,000 | 1.25 ms | 7.98 Mops/s | 78.22 KB |
| Enqueue × 1,000 | 1,000 | 33.50 us | 29.85 Mops/s | 47.53 KB |
| Enqueue × 10,000 | 10,000 | 4.34 ms | 2.31 Mops/s | 568.81 KB |
| Enqueue × 50,000 | 50,000 | 976.30 us | 51.21 Mops/s | 2.53 MB |

### Random (XorShift128+)

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| NextUInt64 | 5,000,000 | 5.43 ms | 920.22 Mops/s | 0 B |
| NextInt64 | 5,000,000 | 5.43 ms | 920.06 Mops/s | 0 B |
| NextInt32 | 5,000,000 | 5.43 ms | 920.01 Mops/s | 0 B |
| Standalone NextUInt64 | 10,000,000 | 10.87 ms | 919.77 Mops/s | 0 B |

### Strategy Performance

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Pool Get+Release × 100k | 100,000 | 105.32 ms | 949.50 Kops/s | 211.52 MB |
| Process 10k frames, 1 strategy | 10,000 | 8.66 ms | 1.15 Mops/s | 96 B |
| Process 10k frames, 5 strategies | 50,000 | 1.49 ms | 33.65 Mops/s | 104 B |
| Process 10k frames, 10 strategies | 100,000 | 2.02 ms | 49.49 Mops/s | 144 B |
| Process 10k frames, 20 strategies | 200,000 | 3.40 ms | 58.80 Mops/s | 224 B |

## Godot Adapter Benchmarks

### Vector3 Read/Write and Conversion

| Scenario | Generated (Mops/s) | Baseline (Mops/s) | Ratio | Winner | Alloc Gen/Baseline |
|------|--------------:|--------------:|:----:|:----:|----------------|
| Read Vector3: TryGet vs ToObject | 445.83 | 102.94 | 4.33x | Generated | 40 B / 6.10 MB |
| Write Vector3: factory vs fallback | 13.56 | 425.71 | 31.39x | Baseline | 33.57 MB / 6.10 MB |
| FromObject Color: kind-switch vs fallback | 54.88 | 62.50 | 1.14x | Baseline | 12.21 MB / 12.21 MB |

### Single Measurements

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| ToObject: Registered Vector3 conversion | 200,000 | 1.90 ms | 105.46 Mops/s | 6.10 MB |
| Create+Extract Vector3 | 400,000 | 16.90 ms | 23.68 Mops/s | 39.67 MB |
| EntitySim 500 entities × 60 frames | 150,000 | 13.12 ms | 11.44 Mops/s | 10.07 MB |

---
[↑ Back to Origo.manual](../README.en.md)