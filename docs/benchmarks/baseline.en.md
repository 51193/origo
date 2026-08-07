<!-- docsync-pair: benchmarks/baseline -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo Performance Baseline

> [↑ Back to Origo.manual](../README.en.md)

> A snapshot of the performance status of Origo framework subsystems, serving as the authoritative reference for future performance optimization.
> Values are tightly coupled to the runtime environment and runtime version; cross-machine comparison is not meaningful. Before-and-after optimization comparisons must be retested **in the same environment with the same runtime**.

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
| CPU | Intel Core i7-11800H (8C/16T, 2.30 GHz base) |
| Memory / OS | 15.4 GiB / Ubuntu 26.04 LTS (Linux 7.0.0-28-generic) |
| .NET | SDK 10.0.302, runtime 10.0.10 (test target `net10.0`) |
| Build | `Release` |
| Sampling | Single run (min-of-rounds internally takes `min of 5`) |

> The tables below are a reference snapshot of the current baseline values;
> **the regression gate's data source is `docs/benchmarks/baseline.json`**
> (refreshed via `--update-baseline` as machine and runtime change).

## Methodology

- **Timing**: Fixed-capacity pool + bitmask addressing (constant memory), large iteration counts (single round spans multiple OS time slices), 1 round warmup + multiple rounds taking each side's minimum elapsed time (excluding preempted/GC outlier rounds).
- **Allocation**: Each benchmark runs a dedicated `[MethodImpl(NoInlining)]` measurement round per side, taking the difference of `GC.GetAllocatedBytesForCurrentThread()` before and after. Measurement is placed in a separate method so its loop body does not share code generation with the timing loop and does not affect throughput.
- **Alloc column**: Measured allocation per round (i.e., corresponding to the iteration count in the table), in "Generated / Boxed" format.

> **"Alloc" and relative trends are the most reliable criteria**: they are verbatim consistent across 4 rounds. Absolute throughput is subject to jitter from CPU frequency scaling and code alignment effects on trivial loops (see validity limitations at the end).

## SG Pure Micro-Benchmarks

### Write Throughput — Generated operator vs Boxed class (2,000,000 iterations)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner | Alloc Gen/Boxed |
|------|--------------:|--------------:|:----:|:----:|----------------|
| Int32   | 226 | 12.3 | 18.5x | Generated | **0 B / 106.81 MB** |
| Int64   | 229 | 11.6 | 19.7x | Generated | 0 B / 106.81 MB |
| Single  | 238 | 13.7 | 17.5x | Generated | 0 B / 106.81 MB |
| Double  | 227 | 12.9 | 17.6x | Generated | 0 B / 106.81 MB |
| Boolean | 239 | 13.6 | 17.5x | Generated | 0 B / 106.81 MB |
| Char    | 232 | 12.7 | 18.3x | Generated | 0 B / 106.81 MB |
| String (ref slot) | 168 | 27.1 | 6.2x | Generated | 0 B / 61.04 MB |

### Read Throughput — Generated TryGet/Kind vs Boxed `is T` (10,000,000 iterations, 0 B Alloc on both sides)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner | Stability |
|------|--------------:|--------------:|:----:|:----:|--------|
| Int32   | 248 | 185 | 1.34x | Generated | Stable |
| Int64   | 231 | 216 | 1.07x | Generated | Stable |
| Single  | 254 | 189 | 1.35x | Generated | Stable |
| Double  | 257 | 214 | 1.20x | Generated | Stable |
| Boolean | 249 | 200 | 1.24x | Generated | Stable |
| Char    | 256 | 225 | 1.14x | Generated | Stable |
| String (`TryGetString`) | 191 | 313 | ~1.64x | Boxed | High variance |
| String (`IsString`) | 313 | 201 | 1.56x | Generated | Stable |

### Mixed Dispatch — Generated Kind switch vs Boxed `is T` (10,000,000 iterations, 0 B on both sides)

| Scenario | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| Mixed dispatch (int/float/bool/string/double) | 366 | 116 | ~3.15x | Generated |

## Core Real-World Simulation Benchmarks

### Heterogeneous Dictionary Iteration (2,048,000 `.Data` reads)

| Scenario | Generated .Data (Mops/s) | Boxed Iteration (Mops/s) | Ratio | Winner | Alloc Gen/Boxed |
|------|--------------------:|------------------:|:----:|:----:|----------------|
| Heterogeneous dict `.Data` iteration | 74 | 520 | ~7.0x | Boxed | 37.49 MB / 0 B |

> This is a **synthetic worst case**: `.Data` returns `object`, value types re-box every read through `ToObject` (dataset ~80% value types → 37.49 MB per round). This calling pattern does not exist in production (see "Design Trade-offs").

### Factory Construction + Dictionary Insertion (500,000 iterations)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner | Alloc Gen/Boxed |
|------|--------------:|--------------:|:----:|:----:|----------------|
| String  | 49.1 | 54.0 | 1.10x | Boxed | 23.53 MB / 14.97 MB |
| Int32   | 50.1 | 15.2 | 3.30x | Generated | 23.53 MB / 26.42 MB |
| Single  | 60.0 | 18.6 | 3.24x | Generated | 23.53 MB / 26.42 MB |
| Boolean | 53.1 | 18.7 | 2.84x | Generated | 23.53 MB / 26.42 MB |

> Value type insertion boxed side uses ~12 MB more boxing allocation (26.42 vs 23.53); generated is both faster and leaner. String insertion generated side has slightly more allocation (23.53 vs 14.97 MB): `Dictionary<string,TypedData>` embeds a 24-byte struct per entry, so the backing array is larger than `Dictionary<string,object>`'s 8-byte references.

### Observer Notification (2,000,000 iterations, 0 B on both sides)

| Scenario | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| Observer notify (old,new) + type check | 386 | 361 | ~1.07x | Generated |

> Passed via `TypedData` (not `object`), using `TryGetString` for type check, zero boxing, on par with boxed `is string`.

### Dictionary Lookup TryExtract (2,000,000 iterations, 0 B on both sides)

| Type | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| String  | 55.5 | 44.5 | 1.25x | Generated |
| Int32   | 39.2 | 39.4 | 1.00x | Boxed |
| Single  | 42.1 | 34.9 | 1.21x | Generated |
| Boolean | 41.4 | 31.5 | 1.32x | Generated |

### Multi-type Cast Chain float→int→long→double (2,000,000 iterations, int payload, 0 B on both sides)

| Scenario | Generated (Mops/s) | Boxed (Mops/s) | Ratio | Winner |
|------|--------------:|--------------:|:----:|:----:|
| Numeric cast chain | 102 | 85.9 | 1.19x | Generated |

## Performance Status Overview

- **Value types**: Writes ~17–20x, mixed dispatch ~3.15x, dictionary construction+insertion ~3x far exceed boxing; single reads 1.07–1.35x (all surpass boxing). **Goal achieved.**
- **string**: Writes 6.2x, DictLookup 1.25x, IsString 1.56x surpass boxing; `TryGetString` array reads ~1.64x slower (structural, see below).
- **DictLookup value types**: Int32 nearly tied (1.00x), Single/Boolean generated faster (1.21–1.32x). Compared to the previous baseline round (Ryzen 7 9700X), the relative advantage of generated code is more pronounced on lower-end CPUs, because the boxing path's GC write barrier overhead is relatively heavier under weaker cache/lower base frequency.
- **Observer notification**: Generated 1.07x better than boxing.
- The only "boxed faster" item — `TryGetString` array reads — is structural (see below), and the `IsString` path generated is 1.56x faster than boxing.

## Design Trade-offs and Evaluated Directions

Documents why the current state is as it is, what trade-offs were made, and directions **verified ineffective and should not be retried**, to avoid future iterations revisiting dead ends.

- **`TryGetString` uses `Unsafe.As<string>` (guarded by `_kind == String`) rather than `(string)_ref`**: The guard already proves `_ref` is a string; `castclass` is redundant. Moreover, `castclass` can throw exceptions, which blocks the JIT's elimination and hoisting optimizations for TryGetString calls whose results are discarded or loop-invariant. On **operation-bound** `TryGetString` paths (such as observer notifications), removing it brings the generated side to parity with boxed `is string`.

- **[Verified Ineffective, Do Not Retry] Removing `castclass` yields no measurable benefit for "array string reads"**: The same change on cache-bound array reads only yields noise-level variation (median ~+2%), not altering the ~1.40x conclusion. The bottleneck on this path is **cache, not instructions** — `_ref` at struct offset 16, with a 24-byte stride on a 64-byte cache line, crosses cache line boundaries more easily than value type reads from `_inlineBits` (offset 8). Instruction-level micro-tuning cannot improve this; do not repeatedly attempt in this direction.

- **[Evaluated Not Worthwhile, Do Not Attempt] Squeezing the `TypedData` struct to 16 bytes**: The current 24 bytes (`byte _kind` + `long _inlineBits` + `object? _ref`) is the lower bound of GC-safe design — `long` and managed reference slots cannot overlap (GC must independently scan references), and the `_kind` byte has no spare bits to tuck. Squeezing to 16 bytes would require sacrificing `long`/`double`'s full 64-bit inlining, or introducing extra branching/type lookups (most likely net negative), and would change the `internal` layout (depended on by generated code and tests via `InternalsVisibleTo`). The structural gap of value type single reads ≤1.10x and DictLookup value types 1.31–1.38x originates from this and is accepted.

- **`.Data` (object?) boxing only occurs on cold paths**: `.Data` boxes value types through `ToObject`, serving cold paths where the type is unknown at compile time (serialization by `DataType`, console, `ToString`); these paths inherently require `object`. Framework-internal hot/warm paths (data change signal handling, load validation, etc.) uniformly use zero-boxing `TryGetXxx`. **The "heterogeneous `.Data` iteration" benchmark (~6.9x, 37.49 MB) is a synthetic worst case and does not correspond to any real production hot path**; removing `.Data` would only move the same boxing inside and add complexity (zero downstream dependencies, ~60 test locations depend on its convenience access). Trade-offs and recommended usage are documented in [Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.en.md).

## Validity Limitations

1. **CPU dynamic frequency scaling (scaling ≈ 75%)** introduces run-to-run jitter; for marginal items ≤ 1.3x and the fastest loops (boxed side heterogeneous iteration 6.3–8.4x, writes), retesting at fixed frequency/performance mode is recommended.
2. **Absolute throughput is affected by code alignment**: writes and other ultra-trivial loops are sensitive to method layout/alignment, with ~±8% jitter; **allocation counts and relative trends are unaffected by this** and should be the primary criteria.
3. **Runtime is .NET 10.0.10**, matching the `net10.0` test target; conclusions must be retested under the same runtime.
4. **Micro-benchmarks use min-of-rounds**, favoring ideal JIT steady state; real-world simulation suites are more representative.
5. High-variance items: String `TryGetString` read, mixed dispatch, boxed side heterogeneous iteration, boxed side value type insertion; for these items, increase sampling rounds (≥ 8 rounds) to converge before drawing conclusions.

## Subsystem Performance Baselines

> Below are performance snapshots of the framework's core subsystems, grouped by module. Each benchmark uses min-of-rounds or a single measurement.

### Entity Lifecycle

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| 100 entities create+spawn | 100 | 815.40 us | 122.64 Kops/s | 392.72 KB |
| 500 entities create+spawn | 500 | 2.43 ms | 205.47 Kops/s | 1.91 MB |
| 2000 entities create+spawn | 2,000 | 22.41 ms | 89.26 Kops/s | 7.64 MB |

### Frame Processing

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| 10 entities × 1 strategy, 200 frames | 2,000 | 360.40 us | 5.55 Mops/s | 600 B |
| 50 entities × 5 strategies, 200 frames | 50,000 | 4.18 ms | 11.97 Mops/s | 3.16 KB |
| 200 entities × 10 strategies, 200 frames | 400,000 | 20.56 ms | 19.45 Mops/s | 20.35 KB |

### Entity Save (SaveSingle)

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| 10 entities SaveSingle | 10 | 167.40 us | 59.74 Kops/s | 9.96 KB |
| 100 entities SaveSingle | 100 | 173.20 us | 577.37 Kops/s | 99.26 KB |
| 500 entities SaveSingle | 500 | 1.39 ms | 360.65 Kops/s | 496.13 KB |

### Observer Topology

#### Mount

| Scenario | Operations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Mount × 10 | 10 | 198.60 us | 50.35 Kops/s | 22.31 KB |
| Mount × 50 | 50 | 153.00 us | 326.80 Kops/s | 105.89 KB |
| Mount × 200 | 200 | 705.10 us | 283.65 Kops/s | 424.18 KB |

#### Unmount

| Scenario | Operations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Unmount × 10 | 10 | 217.40 us | 46.00 Kops/s | 18.50 KB |
| Unmount × 50 | 50 | 405.30 us | 123.37 Kops/s | 87.69 KB |
| Unmount × 200 | 200 | 1.97 ms | 101.30 Kops/s | 348.11 KB |

### DataSourceNode Tree

| Scenario | Node Count | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Tree build d=2 w=5 | ~31 | 186.50 us | 166.22 Kops/s | 40.16 KB |
| Tree build d=3 w=8 | ~585 | 1.79 ms | 327.09 Kops/s | 763.25 KB |
| Tree build d=4 w=8 | ~4,681 | 21.98 ms | 212.98 Kops/s | 6.07 MB |
| SHA-256 hash d=3 w=8 | ~585 | 8.14 ms | 71.85 Kops/s | 1.58 MB |
| SHA-256 hash d=4 w=8 | ~4,681 | 45.74 ms | 102.35 Kops/s | 16.04 MB |
| As\<T\> dispatch | 50,000,000 | 2.30 s | 21.73 Mops/s | 40 B |

### Blackboard

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| SetValue Int32 × 100k | 100,000 | 47.77 ms | 2.09 Mops/s | 16.70 MB |
| SetValue Single × 100k | 100,000 | 103.39 ms | 967.17 Kops/s | 18.76 MB |
| SetValue String × 100k | 100,000 | 171.10 ms | 584.44 Kops/s | 25.62 MB |
| SetValue Boolean × 100k | 100,000 | 73.20 ms | 1.37 Mops/s | 18.76 MB |
| TryGet Int32 × 500k | 500,000 | 115.50 ms | 4.33 Mops/s | 19.07 MB |
| TryGet Single × 500k | 500,000 | 120.19 ms | 4.16 Mops/s | 19.07 MB |
| TryGet String × 500k | 500,000 | 111.47 ms | 4.49 Mops/s | 19.07 MB |
| TryGet Boolean × 500k | 500,000 | 114.01 ms | 4.39 Mops/s | 19.07 MB |
| Serialize+Deserialize 100 keys | 200 | 145.90 us | 1.37 Mops/s | 4.84 KB |
| Serialize+Deserialize 500 keys | 1,000 | 104.70 us | 9.55 Mops/s | 22.62 KB |
| Serialize+Deserialize 1000 keys | 2,000 | 351.00 us | 5.70 Mops/s | 47.63 KB |

### Save Persistence

| Scenario | Entity Count | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| ComputePayloadHash 10e | 10 | 332.20 us | 30.10 Kops/s | 40.76 KB |
| ComputePayloadHash 100e | 100 | 2.30 ms | 43.42 Kops/s | 350.30 KB |
| ComputePayloadHash 500e | 500 | 5.43 ms | 92.07 Kops/s | 1.67 MB |
| Write+Read 10e | 10 | 316.10 us | 31.64 Kops/s | 25.42 KB |
| Write+Read 100e | 100 | 489.10 us | 204.46 Kops/s | 129.40 KB |
| Write+Read 300e | 300 | 1.70 ms | 176.13 Kops/s | 331.78 KB |
| Snapshot Write+Read 10e | 10 | 121.80 us | 82.10 Kops/s | 6.30 KB |
| Snapshot Write+Read 100e | 100 | 145.20 us | 688.71 Kops/s | 6.30 KB |
| Snapshot Write+Read 300e | 300 | 77.50 us | 3.87 Mops/s | 6.30 KB |

### Concurrent Queue

| Scenario | Operations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Enqueue+ExecuteAll × 100 | 100 | 75.10 us | 1.33 Mops/s | 896 B |
| Enqueue+ExecuteAll × 1,000 | 1,000 | 86.70 us | 11.53 Mops/s | 7.91 KB |
| Enqueue+ExecuteAll × 10,000 | 10,000 | 2.32 ms | 4.31 Mops/s | 78.22 KB |
| Enqueue × 1,000 | 1,000 | 137.90 us | 7.25 Mops/s | 16.28 KB |
| Enqueue × 10,000 | 10,000 | 12.31 ms | 812.66 Kops/s | 256.31 KB |
| Enqueue × 50,000 | 50,000 | 3.13 ms | 15.98 Mops/s | 1.00 MB |

### Random (XorShift128+)

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| NextUInt64 | 10,000,000 | 19.62 ms | 509.63 Mops/s | 0 B |
| NextInt64 | 5,000,000 | 9.37 ms | 533.53 Mops/s | 0 B |
| NextInt32 | 5,000,000 | 9.26 ms | 539.95 Mops/s | 0 B |

### Strategy Performance

| Scenario | Iterations | Elapsed | Throughput | Allocation |
|------|--------|------|------|------|
| Pool Get+Release × 100k | 100,000 | 455.76 ms | 219.41 Kops/s | 219.15 MB |
| Process 10k frames, 1 strategy | 10,000 | 22.35 ms | 447.43 Kops/s | 96 B |
| Process 10k frames, 5 strategies | 50,000 | 820.30 us | 60.95 Mops/s | 104 B |
| Process 10k frames, 10 strategies | 100,000 | 1.01 ms | 98.89 Mops/s | 144 B |
| Process 10k frames, 20 strategies | 200,000 | 1.70 ms | 117.88 Mops/s | 224 B |
