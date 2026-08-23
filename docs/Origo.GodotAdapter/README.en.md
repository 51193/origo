<!-- docsync-pair: Origo.GodotAdapter/README -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.GodotAdapter

> [↑ Back to Origo.manual](../README.en.md)

## Module Overview

**Origo.GodotAdapter** is the Godot 4 adapter layer for the Origo framework. It bridges the platform-agnostic abstractions in the Core layer with Godot engine's concrete APIs, including the file system (via `FileAccess`/`DirAccess`), logging output (via `GD.Print`), node lifecycle (via `Node`/`PackedScene`), and engine type serialization (14 types including `Vector2`, `Transform3D`, etc.).

## Subsystem Overview

| Subsystem | Capability | Details |
|--------|------|------|
| [Bootstrap](Bootstrap/README.en.md) | Startup orchestration | OrigoAutoHost → OrigoDefaultEntry → Runtime creation + strategy discovery + Context binding |
| [Console](Console/README.en.md) | Godot console commands | press_button / tree_debug / camera_view commands + adapter layer CommandHandlerBase |
| [FileSystem](FileSystem/README.en.md) | Godot file system | IFileSystem implementation: FileAccess/DirAccess + res:// and user:// support |
| [Logging](Logging/README.en.md) | Godot logging | ILogger implementation: delegate-injected GD.Print/PushWarning/PushError |
| [Serialization](Serialization/README.en.md) | Godot type serialization | 14 Godot types → DataSourceNode converters |
| [Snd](Snd/README.en.md) | Godot SND entities | ISndSceneHost implementation: GodotSndManager + GodotSndEntity + PackedSceneNodeFactory |
| — | TypedData inline | Source Generator generates extension methods and Kind registrations for 14 Godot types |

## Startup Flow

```
OrigoDefaultEntry._Ready()
  ├── base._Ready()                          // OrigoAutoHost
  │   └── CreateRuntime()
  │       ├── GodotFileSystem
  │       ├── CreateAndSetupSndManager()
  │       │    ├── GodotSndManager
  │       │    ├── GodotJsonConverterRegistry registrations
  │       │    └── OrigoRuntime
  │       ├── ConsoleInput/Output
  │       └── OrigoRuntime
  ├── RegisterConsoleCommandHandlers()       // Adapter commands
  ├── new SndContext(...)                    // Pass startup config
  ├── SndManager.BindContext(sndContext)
  └── sndContext.Bootstrap()                 // Core-internal sequence:
        ├── Strategy discovery (reflection scan, skip Godot assemblies)
        ├── LoadSceneAliases / LoadTemplates
        └── RequestLoadMainMenuEntrySave
```

## Architectural Constraints

- **No core business rules**: All business logic resides in the Core layer; the adapter layer only "translates"
- **No reverse dependencies**: Core never references any types from GodotAdapter
- **Godot types only appear in the adapter layer**: `Godot.Vector*`, `Godot.Node`, etc. never appear in the Core layer

### Strategy Lifecycle Isolation

The adapter layer does not participate in any aspect of strategy lifecycle management:

- **Does not trigger strategy hooks**: `GodotSndManager.CreateEntity` only creates the entity and Godot node, without calling hooks like `AfterSpawn` / `AfterLoad` / `BeforeDead`
- **Does not manage strategy teardown**: `RemoveEntity` only removes the Godot node and collection reference, without calling `ReleaseStrategiesOnly`
- **Does not flush the deferred pipeline**: The frame loop does not bypass Core to call the internal `FlushEndOfFrameDeferred` directly
- **`OrigoAutoHost._Process` is the sole frame entry point**: Within it, Core's `ProcessAll` → `FlushEndOfFrameDeferred` → `Console.ProcessPending` are delegated in order; the adapter layer only schedules, never makes decisions

All this orchestration is the unified responsibility of the Core layer's session lifecycle (`SessionManager` / `SessionRun`). For detailed separation principles, see [Architecture Overview](../usage/architecture-overview.en.md#adapter-layer-and-core-layer-separation-principles).

### Bridge Pattern

`GodotSndEntity` embodies the bridge pattern: it implements both `ISndEntity` (Core public interface) and `IEntityLifecycle` (Core internal interface), internally holding a `SndEntity` instance and transparently delegating to it. It contains no business logic of its own — it serves solely as an adapter between the Godot Node and the Core SndEntity.


### Usage notes (common pitfalls)

Verified integration notes for embedding Origo into a Godot project:

- **Export override timing**: Setting Export properties (e.g. `ConfigPath`) on an
  `OrigoDefaultEntry` subclass inside the constructor has no effect — Godot re-assigns
  defaults when instantiating the scene. Assign them inside `_Ready`, before calling
  `base._Ready()`.
- **Command line runs do not rebuild C#**: `godot --path .` loads the previously built
  DLL. The editor rebuilds automatically; from the command line run `dotnet build` first
  (or use `dotnet build && godot --path .`).
- **Full-screen UI swallows 3D clicks**: A Control covering the whole screen defaults to
  `mouse_filter = Stop`, blocking all mouse events so 3D interactions (e.g. board clicks)
  stop working. Set `MouseFilter = Ignore` on the UI root; keep child panels at Stop so
  their buttons respond.
- **Input stage selection**: Right-button presses can be consumed by the GUI system before
  reaching `_UnhandledInput`. Use `_Input` for global fallback input (camera drag/zoom)
  and `_UnhandledInput` for scene-object interaction.
- **`LookAt` colinear warning**: With the camera directly above the target,
  `LookAt(target, Vector3.Up)` warns "Target and up vectors are colinear" every frame.
  Use a horizontal up vector (e.g. `Vector3.Back`) when the pitch is near vertical.
- **Anchor presets**: `SetAnchorsPreset(RightWide)` only sets anchors, leaving offsets at
  zero (zero-width panel). Use `SetAnchorsAndOffsetsPreset` and set `OffsetLeft` etc.
- **Headless viewport**: In headless mode the viewport size is 0 and screen↔world
  conversions (`Camera3D.UnprojectPosition`) return invalid values. Tests that depend on
  a real viewport (e.g. click chains) must run in GUI mode.

## Bridges with Core

| Core Interface | Adapter Implementation | File |
|-----------|------------|------|
| `IFileSystem` | `GodotFileSystem` | [FileSystem/](FileSystem/README.en.md) |
| `ILogger` | `GodotLogger` | [Logging/](Logging/README.en.md) |
| `ISndSceneHost` (internal) / `ISndSceneReadAccess` (public) | `GodotSndManager` | [Snd/](Snd/README.en.md) |
| `INodeFactory` | `GodotPackedSceneNodeFactory` | [Snd/](Snd/README.en.md) |
| `INodeHandle` | `GodotNodeHandle` | [Snd/](Snd/README.en.md) |
| `IConsoleCommandHandler` | `CommandHandlerBase` + subclasses | [Console/](Console/README.en.md) |

## TypedData Multi-Layer Inlining

Origo.GodotAdapter references the `Origo.SourceGeneration` source generator and registers 14 Godot engine types at the assembly level via `[assembly: SndInlineTypes(startKind: 128, ...)]`. At compile time, the SG automatically generates extension methods (`TryGetVector2` / `AsVector3`, etc., inside the `internal` `TypedDataLayeredExtensions` class), `[ModuleInitializer]` registration logic, and KindResolver/Converter bridges.

- **Assembly load registers**: When the GodotAdapter assembly is referenced or loaded, its generated `[ModuleInitializer]` methods automatically perform Kind / Converter / TypeMap registration; no extra initialization entry point is needed. Tests force assembly loading by referencing a public type.
- **Kind range 128–141**: Does not conflict with Core layer's 1–13, ensuring that `(TypedData)42` created in Core won't be misinterpreted as `Vector2` in GodotAdapter.

See [Origo.SourceGeneration documentation](../Origo.SourceGeneration/README.en.md) for details.

---
[↑ Back to Origo.manual](../README.en.md)
