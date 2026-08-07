<!-- docsync-pair: benchmarks/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Performance Baselines

> [↑ Back to Origo manual](../README.en.md)

## Overview

Performance baseline data and design trade-off analysis for the Origo framework, covering subsystems including TypedData inline storage, entity lifecycle, Observer topology, DataSourceNode trees, Blackboard, Save persistence, concurrent queues, random number generation, Strategy performance, and more.

## Files

| File | Description |
|------|------|
| `baseline.en.md` / `baseline.zh.md` | Performance baseline data and design trade-off analysis for Origo framework subsystems |
| `README.md` | Performance benchmark document overview (this file) |

## Benchmark Test Files

| File | Module Under Test | Description |
|------|---------|------|
| `Origo.SourceGeneration.Tests/Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | Source Generation | Pure micro-benchmark: TypedData inline vs boxing for value type / reference type read/write, mixed dispatch |
| `Origo.Core.Tests/Benchmarks/TypedDataRealWorldBenchmarkTests.cs` | TypedData | Real SND path simulation: dictionary lookup/insertion, numeric cast chains, observer notification, heterogeneous iteration |
| `Origo.Core.Tests/Benchmarks/EntityLifecycleBenchmarkTests.cs` | Entity Lifecycle | Entity creation+AfterSpawn, frame processing scaling, BuildMetaData serialization throughput |
| `Origo.Core.Tests/Benchmarks/ObserverTopologyBenchmarkTests.cs` | Observer Topology | Mount/Unmount binding count scaling |
| `Origo.Core.Tests/Benchmarks/DataSourceNodeBenchmarkTests.cs` | DataSourceNode | Tree construction, SHA-256 hashing, As\<T\> type dispatch |
| `Origo.Core.Tests/Benchmarks/BlackboardBenchmarkTests.cs` | Blackboard | Batch SetValue/TryGet, serialization round-trip |
| `Origo.Core.Tests/Benchmarks/SavePayloadBenchmarkTests.cs` | Save Persistence | Payload hash computation, Write/Read round-trip, Snapshot round-trip |
| `Origo.Core.Tests/Benchmarks/ConcurrentActionQueueBenchmarkTests.cs` | Concurrent Queue | Enqueue+ExecuteAll scaling, Enqueue throughput |
| `Origo.Core.Tests/Benchmarks/RandomGeneratorBenchmarkTests.cs` | Random | XorShift128+ NextUInt64/NextInt64/NextInt32 throughput |
| `Origo.Core.Tests/Snd/Strategy/SndStrategyPerformanceTests.cs` | Strategy | Strategy pool Get/Release, Process frame processing scaling, TriggerAll allocation |
| `Origo.GodotAdapter.Tests/Serialization/GodotTypedDataPerformanceTests.cs` | Godot Adapter | Godot registered type TypedData read/write/conversion throughput, mixed entity simulation |

## Running

```bash
bash scripts/benchmark.sh
```

This script runs three benchmark suites sequentially (all marked `[Trait("Category","Benchmark")]`, excluded from `test.sh`):

- **SG Pure Micro-Benchmarks** — `Origo.SourceGeneration.Tests`
- **Core Benchmarks** — `Origo.Core.Tests` (TypedData real-world simulation + entity lifecycle + Observer topology + DataSourceNode + Blackboard + Save + concurrent queue + random + Strategy performance)
- **Godot Adapter Benchmarks** — `Origo.GodotAdapter.Tests`

---

[↑ Back to Origo manual](../README.en.md)
