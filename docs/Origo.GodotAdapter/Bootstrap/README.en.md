<!-- docsync-pair: Origo.GodotAdapter/Bootstrap/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Bootstrap

> [↑ Back to Origo.GodotAdapter](../README.en.md)

## Overview

Startup and orchestration for the Godot adapter layer. Responsible for creating the complete runtime stack (`OrigoRuntime` + `GodotSndManager`), registering Godot-specific type mappings, serialization converters, and command handlers. All Godot-specific dependencies are injected at the adapter layer; the Core layer remains unaware of them.

## Files

| File | Responsibility |
|------|------|
| `OrigoAutoHost.cs` | Godot Node, creates the runtime: GodotFileSystem + TypeStringMapping + ConverterRegistry + PersistentBlackboard + ConsoleInput/Output. `_Process` delegates to `IOrigoFrameDriver.DriveFrame(delta)` |
| `OrigoDefaultEntry.cs` | Inherits OrigoAutoHost, holds startup configuration properties (`AutoDiscoverStrategies`, `_godotSkipPrefixes` (`private static readonly` field), `SceneAliasMapPath`, etc.), and exposes `Context` so presentation code can reach the unified business facade |
| `OrigoDefaultEntry.Bootstrap.cs` | Partial class, `_Ready` implementation: register command handlers → create SndContext → call `Bootstrap()`. Any step failure marks the bootstrap failed (`MarkBootstrapFailed`) so the next frame fails fast |

## Startup Flow

```
OrigoDefaultEntry._Ready()
  └── base._Ready()                          // OrigoAutoHost
       └── CreateRuntime()
            ├── new GodotFileSystem()
            ├── CreateAndSetupSndManager()
            │    ├── new GodotSndManager()
            │    ├── GodotJsonConverterRegistry.RegisterTypeMappings(...)
            │    ├── DataSourceFactory.CreateDefaultRegistry(...)
            │    ├── GodotJsonConverterRegistry.RegisterDataSourceConverters(...)
            │    └── new PersistentBlackboard(...) → LoadFromDisk()
            ├── new ConsoleInputBuffer()
            ├── new ConsoleOutputChannel()
            └── new OrigoRuntime(...)
       └── sndManager.BindRuntimeDependencies(world, logger)
  ├── RegisterConsoleCommandHandlers()       // Adapter layer command handlers
  ├── new SndContext(new SndContextParameters(...) {  // Pass startup config
  │       AutoDiscoverStrategies = ...,
  │       DiscoverySkipPrefixes = ...,
  │       SceneAliasMapPath = ...,
  │       SndTemplateMapPath = ...
  │   })
  ├── Context = sndContext                   // Exposed to presentation/game code
  ├── SndManager.BindContext(sndContext)
  └── sndContext.Bootstrap()                 // Core-internal orchestration:

SndContext.Bootstrap() internal sequence:
  1. Strategy discovery       (OrigoAutoInitializer.DiscoverAndRegisterStrategies)
  2. Scene alias loading      (SndWorld.LoadSceneAliases)
  3. SND template loading     (SndWorld.LoadTemplates)
  4. Entry save loading       (RequestLoadMainMenuEntrySave)
```

## Design Decisions

### Why bind in two steps (RuntimeDependencies then Context)

`SndWorld` is created during `OrigoRuntime` construction, but `ISndContext` is created at a later stage (after configuration and strategy discovery). Two-step binding allows GodotSndManager to interact with the World immediately after Runtime creation (e.g., preloading), and then gain persistence and scene capabilities once the Context is ready.

### Why OrigoDefaultEntry is a partial class

Startup logic (`OrigoDefaultEntry.Bootstrap.cs`) is separated from exported property definitions (`OrigoDefaultEntry.cs`). Godot's [Export] attributes shown in the scene editor are clearer in the main file, while the orchestration logic in a separate file improves maintainability.

### Why strategy discovery filters Godot prefixes

`OrigoAutoInitializer.DiscoverAndRegisterStrategies` scans all assemblies in the current AppDomain. Godot and GodotSharp assemblies contain a large number of non-strategy classes; filtering prefixes avoids pointless scanning and registration errors. The prefix is passed into Core via `SndContextParameters.DiscoverySkipPrefixes`, rather than being hardcoded in the adapter layer.


### Why Context Is Public

`OrigoAutoHost` already exposes `Runtime` and `SndManager`. Common presentation needs (save listing, continue availability, lifecycle entry points, template and blackboard queries) are concentrated on `ISndContext`. `Context` shares the host entry lifecycle: it is assigned during `_Ready()` and is the same instance passed to `ConfigureSaveMetadataContributors`.

### Why startup orchestration is centralized in SndContext.Bootstrap()

The adapter layer should not directly call `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`, `LoadSceneAliases()`, `LoadTemplates()`, or `RequestLoadMainMenuEntrySave()`; strategy discovery and JSON entity-list spawning are now compiler-level `internal` and reachable only by `SndContext.Bootstrap`. Runtime template/alias map reloads should use the public companion: `ctx.Template.LoadTemplates(...)` / `ctx.Template.LoadSceneAliases(...)`. These are Core-internal orchestration operations — strategy discovery must execute in the Core layer, alias/template loading is Core configuration parsing, and entry save loading is the Core lifecycle entry point. The adapter layer only passes configuration parameters via `SndContextParameters`; `Bootstrap()` ensures these operations complete in the correct layer with the correct dependency order.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
