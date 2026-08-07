<!-- docsync-pair: README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo Manual

The complete documentation manual for the Origo framework. Uses a **bottom-up** structure — aggregating upward from source code directories level by level, ensuring any question can reach its target via multi-level directory indexing without reading source code from scratch.

> **Development Loop (mandatory order)**: ① Develop source → ② Extend/adapt tests → ③ Execute tests → ④ Fix source + re-test until all pass → ⑤ Changelog → ⑥ Docs sync.
> Before modifying source code, you must read the documentation of its upstream, downstream, and related facilities. Never misdiagnose cross-module collaborative design as defects. Full guidelines and document master index at repo root [AGENTS.md](../AGENTS.md).

## Design Principles

The Origo framework follows these core design constraints; all module implementations and interface designs are guided by them:

| Principle | Description |
|-----------|-------------|
| **Platform-agnostic** | Origo.Core has zero engine dependencies. All game logic, persistence, and entity models use only `System.*` types |
| **Adapter-layer isolation** | Engine integration is exclusively through `Origo.GodotAdapter` implementing Core abstraction interfaces. The adapter layer must not fire strategy hooks, manage strategy lifecycles, flush deferred pipelines, or hold Core orchestration state |
| **Interface Segregation (ISP)** | `ISndContext` is split into 10 narrow-role companion interfaces; `ISessionRun` returns an abstract `IStateMachineContainer` rather than a concrete type |
| **Unidirectional dependency** | Abstractions → Core implementations → Adapter; reverse dependencies are strictly forbidden |
| **public whitelist** | Do not expose interfaces preemptively for "maybe useful in the future"; every public interface must have a clear cross-assembly consumer |
| **Explicit failure first** | Throw exceptions rather than silent degradation when interface contracts are violated; save/load strictly validate integrity |
| **Strategy as first-class citizen** | Game logic is strategy-driven; `ISndContext` acts as a god object exposing all capabilities to strategies without restricting what framework features a strategy can access |
| **Single-threaded frame model** | One frame = one logical atomic boundary; deferred actions are executed sequentially via queues |
| **Single access path** | Every capability must have exactly one external access path. Any object or method that can simulate a dedicated interface must be sealed `internal`. Callers must not hand-stitch low-level operations to bypass interface encapsulation (which would skip the orchestrated side effects within the interface, making bugs extremely hard to diagnose) |

## How to Use This Manual

```
Root (this file)
  ├── I need to know "what capabilities the framework provides overall"
  │   └── usage/capabilities.md → browse all capabilities by functional domain
  │
  ├── I need to know "how to use Origo"
  │   └── usage/README.md → choose docs by scenario
  │
  ├── I need to know "a module's capabilities and design decisions"
  │   ├── Origo.Core/README.md → subsystem overview → dive into specific sub-modules
  │   │   └── Snd/README.md → Entity/README.md → ...
  │   ├── Origo.GodotAdapter/README.md → adapter layer sub-modules
  │   └── Origo.ConsoleBridge/README.md → TCP bridge
  │
  ├── I need to know "what capabilities are covered by tests"
  │   ├── Origo.Core.Tests/README.md → view Core tests by capability
  │   ├── Origo.GodotAdapter.Tests/README.md → adapter layer 6 capability tests
  │   ├── Origo.ConsoleBridge.Tests/README.md → TCP bridge tests
  │   └── Origo.SourceGeneration.Tests/README.md → source generator tests
  │
  └── I need to know "how this manual itself is maintained"
      └── META.md
```

Each directory's `README.md` contains:
- **Sub-module links** (downward navigation)
- **Parent module link** (upward navigation, marked `↑`)
- **Related module links** (horizontal associations, marked `↔`)

## Project Module Index

| Module | Location | Description |
|--------|----------|-------------|
| **Origo.Core** | [README](Origo.Core/README.en.md) | Platform-agnostic core: SND entity system, runtime, persistence, state machines |
| **Origo.SourceGeneration** | [README](Origo.SourceGeneration/README.en.md) | Roslyn incremental source generator: TypedData multi-layer inline storage + strongly-typed accessors |
| **Origo.GodotAdapter** | [README](Origo.GodotAdapter/README.en.md) | Godot 4 adapter layer: file system, logging, serialization, bootstrap |
| **Origo.ConsoleBridge** | [README](Origo.ConsoleBridge/README.en.md) | TCP remote console bridge (port 9876) |
| **Usage Docs** | [README](usage/README.en.md) | Usage guide from quick start to deep reference |
| **Tests: Core** | [README](Origo.Core.Tests/README.en.md) | Behavioral test documentation for Core layer's 31 capabilities |
| **Tests: GodotAdapter** | [README](Origo.GodotAdapter.Tests/README.en.md) | Adapter layer 6 capability tests + 18 integration test classes (86 tests) |
| **Tests: ConsoleBridge** | [README](Origo.ConsoleBridge.Tests/README.en.md) | TCP bridge server behavioral test documentation |
| **Tests: SourceGeneration** | [README](Origo.SourceGeneration.Tests/README.en.md) | TypedData source generator driver behavioral test documentation |
| **Manual Meta-Instructions** | [META.md](META.en.md) | Writing and maintenance conventions for this manual |
| **Agent Workflow** | [AGENTS.md](../AGENTS.md) | Mandatory development loop (source → test extension → test execution → fix & re-test → Changelog → docs), core principles, and document master index |
| **Performance Baselines** | [benchmarks/baseline.md](benchmarks/baseline.en.md) | TypedData inline storage + framework subsystem performance baseline and design trade-offs |

## Origo.Core Subsystems

| Subsystem | Responsibility |
|-----------|---------------|
| [Abstractions](Origo.Core/Abstractions/README.en.md) | 11 groups of public interfaces (IBlackboard, IFileSystem, ISndEntity, ISessionManager, IStateMachineContainer...) |
| [Snd](Origo.Core/Snd/README.en.md) | SND entity system (Strategy + Node + Data) |
| [Runtime](Origo.Core/Runtime/README.en.md) | Four-layer runtime lifecycle + console |
| [Save](Origo.Core/Save/README.en.md) | Persistence (two-phase write + strict read) |
| [DataSource](Origo.Core/DataSource/README.en.md) | Data source abstraction layer (JSON/Map codec + type conversion) |
| [Grid](Origo.Core/Grid/README.en.md) | Grid coordinate system, A* pathfinding, coordinate parsing |
| [StateMachine](Origo.Core/StateMachine/README.en.md) | String-stack state machine |
| [Planning](Origo.Core/Planning/README.en.md) | Intent-driven plan execution |
| [Scheduling](Origo.Core/Scheduling/README.en.md) | Deferred action scheduling |
| [Blackboard](Origo.Core/Blackboard/README.en.md) | In-memory blackboard implementation |
| [Random](Origo.Core/Random/README.en.md) | Random number + noise maps |
| [Utility](Origo.Core/Utility/README.en.md) | General utilities: collection diff comparison |
| [Serialization](Origo.Core/Serialization/README.en.md) | Type ↔ string mapping |
| [Logging](Origo.Core/Logging/README.en.md) | Log builder + NullLogger |
| [Addons](Origo.Core/Addons/README.en.md) | FastNoiseLite noise library |

## Quick Navigation

| I want to... | Go here |
|-------------|---------|
| Browse all framework capabilities | [usage/capabilities](usage/capabilities.en.md) |
| Quickly integrate Origo | [usage/quick-start](usage/quick-start.en.md) |
| Understand the overall architecture | [usage/architecture-overview](usage/architecture-overview.en.md) |
| Write game strategies | [usage/snd-entity-model](usage/snd-entity-model.en.md) |
| Understand the lifecycle loop | [usage/strategy-lifecycle](usage/strategy-lifecycle.en.md) |
| Learn design patterns | [usage/design-patterns](usage/design-patterns.en.md) |
| Test strategies | [usage/strategy-testing](usage/strategy-testing.en.md) |
| Use the save system | [usage/persistence-flow](usage/persistence-flow.en.md) |
| Use state machines | [usage/state-machine](usage/state-machine.en.md) |
| Use console commands | [usage/console-commands](usage/console-commands.en.md) |
| View interface signatures | [usage/agent-reference](usage/agent-reference.en.md) |
| Understand Core module implementations | [Origo.Core/](Origo.Core/README.en.md) |
| Understand Source Generation | [Origo.SourceGeneration/](Origo.SourceGeneration/README.en.md) |
| Understand Godot adapter | [Origo.GodotAdapter/](Origo.GodotAdapter/README.en.md) |

## Version

Current Origo framework version: **0.0.9-nightly** (in development). Documentation is co-located with source code in the same repository; versions are naturally synchronized. When code directory structure changes, the manual's directory mirror and indexes should be updated accordingly.

- Framework source and docs: this repository [origo](https://github.com/51193/origo) (docs under `docs/`)
- Example project: [origo.demo](https://github.com/51193/origo.demo)

Manual maintenance rules: see [META.md](META.en.md). Top-level agent workflow entry: see [AGENTS.md](../AGENTS.md).

[↑ Back to AGENTS.md](../AGENTS.md)
