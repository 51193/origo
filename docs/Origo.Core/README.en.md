<!-- docsync-pair: Origo.Core/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Origo.Core

> [↑ Back to Origo Manual](../README.en.md)

## Module Overview

**Origo.Core** is the platform-agnostic core of the Origo framework. It does not depend on any engine types (Godot, Unity, etc.) and uses only `System.*` and the .NET BCL. All game logic, save systems, entity models, and state machines are implemented in this layer, with differences injected through interfaces into the adapter layer.

## Subsystem Overview

| Subsystem | Capability | Details |
|-----------|-----------|---------|
| [Abstractions](Abstractions/README.en.md) | 11 groups of public abstraction interfaces | IBlackboard / IFilesystem / ILogger / ISndEntity / IStateMachine ... |
| [Addons](Addons/README.en.md) | Vendor third-party libraries | FastNoiseLite v1.1.1 (noise generation) |
| [Blackboard](Blackboard/README.en.md) | Default IBlackboard implementation | In-memory blackboard based on Dictionary + TypedData |
| [DataSource](DataSource/README.en.md) | Data source abstraction layer | DataSourceNode tree model + JSON/Map codec + type converter registration |
| [Grid](Grid/README.en.md) | Grid coordinate system utilities | GridCoordinateSystem: bidirectional grid ↔ world coordinate conversion |
| [Logging](Logging/README.en.md) | Logging system | LogMessageBuilder (structured construction) + NullLogger (test silence) |
| [Planning](Planning/README.en.md) | Behavior planning system | PlanExecutionStrategyBase: intent-driven plan execution + EnsureReplaceableStrategy extension |
| [Random](Random/README.en.md) | Random number system | XorShift128+ PRNG + PersistentRandom + Simplex/Worley noise maps |
| [Runtime](Runtime/README.en.md) | Runtime core | Four-layer lifecycle + console + state machine container + OrigoRuntime |
| [Save](Save/README.en.md) | Persistence system | Two-phase write + strict read + path policy + meta.map |
| [Scheduling](Scheduling/README.en.md) | Deferred scheduling | ActionScheduler + thread-safe ConcurrentActionQueue |
| [Serialization](Serialization/README.en.md) | Type mapping | TypeStringMapping (CLR types ↔ stable string identifiers) |
| [Snd](Snd/README.en.md) | SND entity system | Strategy→Entity→Data→Scene Host→Numeric Recipe Loading — full stack |
| [StateMachine](StateMachine/README.en.md) | String-stack state machine | StackStateMachine + strategy hooks + persistence model |
| [Utility](Utility/README.en.md) | General utilities | Path normalization (PathUtility) and string-to-value inference (ValueInference) |

> The TypedData source generator is a standalone project [Origo.SourceGeneration](../Origo.SourceGeneration/README.en.md), not part of Core.

## This Layer's Files

| File | Responsibility |
|------|---------------|
| `OrigoMeta.cs` | Framework metadata: name, version number, default banner |
| `AssemblyAttributes.cs` | `[assembly: SndInlineTypes(...)]` home inline-type registration declaring the system primitives and string supported by Core |

## Architectural Constraints

- **No Godot references**: The Origo.Core `.csproj` and source code must not contain `Godot` or `GodotSharp` namespaces or assembly references
- **I/O via Gateway**: All file read/write must go through `IDataSourceIoGateway`; direct `File.*` is forbidden
- **Core testability**: Can core business logic run completely in unit tests without mocking anything other than the file system/clock?

## Dependency Direction

```
Origo.Core (platform-agnostic)
    ↑ implements interfaces
Origo.GodotAdapter (engine adapter)
    ↑ injects differences
Origo.ConsoleBridge (standalone service)
```

The adapter layer depends on Core's abstraction interfaces and injects concrete implementations; Core never depends on the adapter layer in reverse.

---
[↑ Back to Origo Manual](../README.en.md)
