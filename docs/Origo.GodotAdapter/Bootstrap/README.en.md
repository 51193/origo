<!-- docsync-pair: Origo.GodotAdapter/Bootstrap/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Bootstrap

> [↑ Back to Origo.GodotAdapter](../README.en.md)

## Overview

Startup and orchestration for the Godot adapter layer. Responsible for creating the complete runtime stack (`OrigoRuntime` + `GodotSndManager`), registering Godot-specific type mappings, serialization converters, and command handlers. All Godot-specific dependencies are injected at the adapter layer; the Core layer remains unaware of them.

## Files

| File | Responsibility |
|------|------|
| `OrigoAutoHost.cs` | Godot Node, creates the runtime: GodotFileSystem + TypeStringMapping + ConverterRegistry + PersistentBlackboard + ConsoleInput/Output. `_Process` delegates to `IOrigoFrameDriver.DriveFrame(delta)` |
| `OrigoDefaultEntry.cs` | Inherits OrigoAutoHost, holds startup configuration properties (`AutoDiscoverStrategies`, `_godotSkipPrefixes` (`private static readonly` field), `SceneAliasMapPath`, etc.) |
| `OrigoDefaultEntry.Bootstrap.cs` | Partial class, `_Ready` implementation: register command handlers → create SndContext → call `Bootstrap()` |

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

### Why startup orchestration is centralized in SndContext.Bootstrap()

The adapter layer should not directly call `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`, `LoadSceneAliases()`, `LoadTemplates()`, or `RequestLoadMainMenuEntrySave()`. These are Core-internal orchestration operations — strategy discovery must execute in the Core layer, alias/template loading is Core configuration parsing, and entry save loading is the Core lifecycle entry point. The adapter layer only passes configuration parameters via `SndContextParameters`; `Bootstrap()` ensures these operations complete in the correct layer with the correct dependency order.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
