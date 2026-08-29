<!-- docsync-pair: usage/architecture-overview -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Architecture Overview

> [↑ Back to usage](README.en.md)

## Design Principles

The Origo framework follows these core design constraints:

- **Platform-agnostic**: Core has zero engine dependencies; all I/O goes through `IDataSourceIoGateway` + `IFileMetaAccess` + `IPathResolver` (internalized by `IFileSystem`)
- **Adapter-layer isolation**: The adapter layer only provides capability encapsulation and bridging; it must not fire strategy hooks or manage strategy lifecycles
- **Interface Segregation (ISP)**: `ISndContext` exposes capabilities through 10 companion properties; `ISessionRun` returns an abstract `IStateMachineContainer`
- **Unidirectional dependency**: Adapter → Core → Abstractions; reverse is strictly forbidden
- **public whitelist**: Every public interface must have a clear cross-assembly consumer
- **Strategy as first-class citizen**: Strategies can access all 30+ members of `ISndContext` without restricting framework capabilities
- **Single-threaded frame model**: One frame = one logical atomic boundary
- **Single access path**: Every capability has exactly one external path; bypasses skip the orchestrated side effects (hooks, validation, lifecycle) and are extremely hard to diagnose

## Project Design

Origo is a **lightweight, platform-agnostic C# game framework** using the SND (Strategy + Node + Data) entity model. The project consists of two layers:

- **Origo.Core**: Platform-agnostic core — all game logic, persistence, and entity models live here
- **Origo.GodotAdapter**: Godot 4 adapter layer — bridges Core's abstraction interfaces with the Godot engine API

## Four-Layer Runtime

```
SystemRun (holds SystemRuntime, containing OrigoRuntime)
  ↓
ProgressRun (holds ProgressRuntime)
  ↓
SessionManager (holds SessionManagerRuntime)
  ↓
SessionRun (foreground + background)
```

### Capability Passing Rules

- Upper layers inject dependencies into lower layers; lower layers must not reference upper layers in reverse
- Each layer is constructed via structured parameter objects, not scattered parameters
- Each layer maintains its own aggregation container (`*Runtime`), centrally holding references

### Layer Responsibilities

| Layer | Container | Held Resources |
|-------|-----------|---------------|
| System | `SystemRuntime` | Logger, PathResolver, SaveRootPath, OrigoRuntime, SaveStorageService, SavePathPolicy, MetaAccess |
| Progress | `ProgressRuntime` | Logger, SaveStorageService, SndWorld, SceneHost, StateMachineContext, SndContext, SavePathPolicy |
| Session | `SessionManagerRuntime` | Logger, SaveStorageService, SndWorld, SceneHost, StateMachineContext, SndContext, ProgressBlackboard |
| Instance | `SessionRun` | SessionBlackboard, ISndSceneHost, StateMachineContainer (via RunStateScope) |

## SND Entity Model

```
Strategy + Node + Data
```

| Component | Description | Location |
|-----------|-------------|----------|
| **Strategy** | Behavioral logic, no instance-level mutable state | Strategy pool (globally shared) |
| **Node** | Presentation layer mapping, driven by engine implementation | Adapter layer (GodotNodeHandle) |
| **Data** | The single authoritative source for entity mutable state | SndDataManager (inline storage via TypedData struct) |

### Strategy Pool Constraints

- Strategies are explicitly registered via `[StrategyIndex("xxx")]`
- Missing index, null value, or type mismatch must throw an error
- Strategies are validated for statelessness at registration time via reflection (instance fields / writable properties are rejected)
- Strategies are sorted by priority; equal priority uses insertion order

## Session Model

- Both foreground and background are expressed through `ISessionRun` — no two parallel type systems
- The foreground unique key is `__foreground__`
- The only difference is `IsFrontSession` and the injected `ISndSceneHost`
- Business logic must not branch on session implementation type

## Persistence

### File Layout

```
{saveRoot}/
├── current/                          # Active save (temporary)
│   ├── .write_in_progress
│   ├── progress.json
│   ├── progress_state_machines.json
│   ├── meta.map
│   ├── extra/                     # Strategy archive files (follow save lifecycle)
│   └── level_{id}/
│       ├── snd_scene.json
│       ├── session.json
│       └── session_state_machines.json
└── save_{id}/                        # Snapshot save (persistent)
    ├── extra/                     # Strategy archive files (follow save lifecycle)
    └── ... (same as above)
```

### Two-Phase Write

1. Write to `current/` (create marker → write all files → delete marker)
2. Snapshot `current/` → `save_{id}/` (atomic rename via temp directory)

### Strict Read

- All three level files missing → treated as no save exists
- Partial presence → treated as corruption, throw exception
- `.write_in_progress` present under `current/` → throw exception (interrupted write)

## Concurrency Model

Origo uses a single-threaded frame loop model:
- Lifecycle actions are executed sequentially through deferred queues (`ActionScheduler` / `ConcurrentActionQueue`)
- A single frame is treated as a logical atomic boundary
- Cross-frame semantics fall into recoverable state (blackboard, state machines, entity Data)

> **Deferred direction**: Stateless strategies provide a foundation for entity-level concurrency, but in-entity strategy ordering, cross-entity calls, observer notifications, and container mutation still require serial semantics. The candidate design is an "entity concurrency" flag + concurrency-mode data + a parallel batch followed by a serial batch; deferred because there is no performance bottleneck today. See [Extension Directions and Deferred Designs](extension-directions.en.md) for the full trade-off.

## I/O Boundary

All file operations in the Core layer go through three interfaces:
- `IDataSourceIoGateway`: Content read/write, only 2 methods — `ReadTree(filePath)` and `WriteTree(filePath, node, overwrite)`. All file content I/O is routed through codecs, with zero bypass.
- `IFileMetaAccess`: File metadata operations (FileExists, directory management, enumeration, deletion, copy)
- `IPathResolver`: Platform path operations (CombinePath, GetParentDirectory)

`IFileSystem` is an implementation detail; the above three interfaces are its public facade. Business code should not directly depend on `IFileSystem`.

```
Business modules → DataSourceNode → IDataSourceIoGateway / IFileMetaAccess / IPathResolver → IFileSystem (internal) → File system
```

Suffix routing, codec strategy, and I/O error semantics are centrally governed on the Gateway side. Raw text files like `.sha` and `.write_in_progress` also go through the codec route via `RawStringDataSourceCodec` — there is no direct read/write bypass. The Gateway uses a fail-fast strategy: when codec decoding fails (e.g., `.map` file format error), the Gateway wraps the exception as an `InvalidOperationException` containing the file path and immediately throws — it does not swallow errors.

`IDataSourceIoGateway` is the framework's hard I/O content boundary: **any module within the system (including strategies) should access file content through this boundary**. File metadata operations (existence checks, directory management, etc.) go through `IFileMetaAccess`, and path operations through `IPathResolver`. The file operations exposed to strategies via `ISndFileAccess` and `ISndArchiveFileAccess` delegate to the above three interfaces; strategies do not need to handle raw text parsing or platform path differences themselves.

> **Deferred direction**: Mount the local file system, save directories, and network resources as `DataSourceNode` tree roots, replacing several file APIs with navigation such as `path -> to -> file -> entity -> health_point`. Synchronous tree reads are sufficient for local files today, but remote nodes would block the frame, so this is deferred. See [Extension Directions and Deferred Designs](extension-directions.en.md) for the full trade-off.

## Adapter Layer and Core Layer Separation Principles

The separation of the adapter layer and Core layer is the core architectural constraint of the Origo framework. The adapter layer's responsibilities are strictly limited to two items; everything else is implemented in the Core layer.

### Interface Abstraction Design

The Core layer follows the Interface Segregation Principle (ISP); `ISndContext` exposes capabilities through 10 companion properties:

| Role Interface | Responsibility |
|---------------|---------------|
| `ISndBlackboardAccess` | System-level + progress-level blackboard access |
| `ISndDeferredActions` | Deferred action queues |
| `ISndTemplateAccess` | Template cloning |
| `ISndConsoleAccess` | Console command submission/processing |
| `ISndStateMachineAccess` | Progress-level state machine container access |
| `ISndSaveOperations` | Save/load/level switch/continue |
| `ISndLifecycleOperations` | Lifecycle entry points (Continue/Initial/MainMenu) |
| `ISndFileAccess` | Static resource file access (via DataSource boundary + built-in parsing) |
| `ISndArchiveFileAccess` | In-save file access (paths relative to the save's active `extra/` subdirectory, following save lifecycle) |

Additionally, `ISessionManager` and `ISessionRun` live in the Abstractions layer. `ISessionRun.GetSessionStateMachines()` returns `IStateMachineContainer` (Abstractions layer interface) rather than a concrete `StateMachineContainer`, ensuring the interface layer is fully decoupled from the Runtime implementation.

### Adapter Layer Responsibilities (Only Two)

| Responsibility | Description | Example |
|---------------|-------------|---------|
| **Capability Provision** | Encapsulate engine-native capabilities as implementations of Core abstraction interfaces | `GodotFileSystem : IFileSystem`, `GodotLogger : ILogger`, `GodotNodeHandle : INodeHandle`, `GodotPackedSceneNodeFactory : INodeFactory` |
| **Bridging** | Inject adapter-layer implementations into the Core layer during startup to complete assembly | `OrigoAutoHost` (creates Runtime + SndManager), `OrigoDefaultEntry` (default entry orchestration) |

### Adapter Layer Forbidden Actions

| Forbidden | Reason | Counter-Example |
|-----------|--------|-----------------|
| **Fire strategy lifecycle hooks** | Strategies are a Core layer concept; hook firing timing and order must be centrally orchestrated by Core | `GodotSndManager` must not call `FireAfterSpawnHooks()`, `FireBeforeDeadHooks()`, etc. |
| **Manage strategy release/ref counting** | Strategy pool, ref counting, and priority sorting are managed in Core's `SndStrategyPool` and `SndStrategyManager` | `GodotSndManager` must not call `ReleaseStrategiesOnly()` |
| **Directly call Core pipeline methods** | The timing and order of frame boundary operations (entity processing → business queue → kill entities → system queue → console) are controlled by Core. The adapter layer should only call `IOrigoFrameDriver.DriveFrame(delta)` to hand over frame control | The adapter layer must not directly call the internal `FlushEndOfFrameDeferred` or `ProcessPending` |
| **Directly drive Core startup flow** | Strategy discovery, alias/template loading, and entry save loading are internal Core orchestration, uniformly executed in `SndContext.Bootstrap()`. The adapter layer only passes configuration via `SndContextParameters` | The adapter layer must not directly call `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`, `LoadSceneAliases()`, `LoadTemplates()`, `RequestLoadMainMenuEntrySave()` |
| **Hold Core orchestration state** | The state machine for entity lifecycle management (e.g., pending kill, teardown flow) is maintained by the Core layer | The adapter layer should not have methods like `QuitFromManager`, `DeadFromManager` |
| **Load engine-agnostic business configuration** | Template parsing, alias mapping, and strategy index resolution are all done in Core | The adapter layer should not read and parse business configurations like `snd_templates.map` |

### Core Layer Responsibilities

| Responsibility | Description |
|---------------|-------------|
| **Strategy system** | Strategy base classes, strategy pool, strategy manager, lifecycle hook definitions and firing |
| **Entity lifecycle orchestration** | `SndEntityFactory.Spawn`/`SpawnMany` (AfterSpawn), `SessionRun` load/save/quit and `KillPending` (via `SessionManager.KillPendingAllSessions`) uniformly orchestrate all hooks |
| **Scene host abstraction** | `ISndSceneHost` only defines container operations (create/lookup/remove), without hook semantics |
| **Deferred action pipeline** | `ActionScheduler` business queue + system queue, `IOrigoFrameDriver.DriveFrame` uniformly flushes |
| **Startup orchestration** | `SndContext.Bootstrap()` uniformly executes strategy discovery → alias/template loading → entry save loading |

### Responsibility Division in the Frame Loop

```
Godot._Process
  └── OrigoAutoHost._Process              ← Adapter layer (sole frame entry)
        └── IOrigoFrameDriver.DriveFrame(delta)  ← Core: unified frame boundary
              ├── SessionManager.ProcessAllSessions(delta, true) ← Core: entity frame processing (includes foreground)
              ├── FlushEndOfFrameDeferred() ← Core: deferred actions + KillPendingAllSessions
              └── Console.ProcessPending() ← Core: console
```

The frame loop entry is in the adapter layer (Godot's `_Process` callback), but control is immediately handed to the Core layer via `IOrigoFrameDriver.DriveFrame(delta)`. The adapter layer does not participate in any logical decisions within the frame, and is unaware of the three-phase ordering (entity processing, deferred queue, console) within Core.

## Project Structure

```
Origo.Core/           # Platform-agnostic core (211 .cs files)
├── Abstractions/     # Public interfaces (Blackboard/Entity/StateMachine/...)
├── Addons/           # External algorithm library (FastNoiseLite)
├── Blackboard/       # Blackboard implementation
├── DataSource/       # JSON/Map codec + type conversion
├── Grid/             # Grid coordinate system + A* pathfinding
├── Logging/          # Logging implementation
├── Planning/         # Planning/behavior strategy extensions
├── Random/           # Random numbers + noise
├── Runtime/          # Runtime four-layer lifecycle + console
├── Save/             # Persistent storage
├── Scheduling/       # Deferred queues
├── Serialization/    # Type ↔ string mapping
├── Snd/              # SND entity system (Strategy + Data + Node)
├── StateMachine/     # String-stack state machine
└── Utility/          # Utility classes (Diff/Path)

Origo.SourceGeneration/  # Roslyn source generator (5 .cs files)
└── TypedDataGenerator*.cs  # Home/Adapter dual-mode code generation (1 main file + 4 partial)

Origo.GodotAdapter/   # Godot 4 adapter layer (~23 .cs files)
├── Bootstrap/        # Startup orchestration
├── Console/          # Godot commands
├── FileSystem/       # Godot file system
├── Logging/          # Godot logging
├── Serialization/    # Godot type serialization
└── Snd/              # Godot entities + manager + node factory

Origo.ConsoleBridge/  # TCP remote console (~2 .cs files)
```

## Testing Strategy

- Core side: engine dependency leaks, lifecycle boundaries, persistence contracts, strategy pool constraints
- Adapter side: host assembly, path strategy, serialization registration runnable under the Godot environment
- `Origo.Core` total line coverage ≥ 90% (Coverlet)
- Test directory structure mirrors production code

---
[↑ Back to usage](README.en.md)
