<!-- docsync-pair: Origo.GodotAdapter/Bootstrap/README -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Bootstrap

> [↑ Back to Origo.GodotAdapter](../README.en.md)

## Overview

Startup and orchestration for the Godot adapter layer. Creates the complete runtime stack (kernel `OrigoRuntime` + internal `GodotSndManager`) through `Origo.Core.Kernel.Ports.AdapterHostKernelPort`, and registers Godot-specific type mappings, serialization converters, and command handlers. All Godot-specific dependencies are injected at the adapter layer; the Core layer remains unaware of them.

## Files

| File | Responsibility |
|------|------|
| `OrigoAutoHost.cs` | Godot Node; prepares GodotFileSystem, type mappings, and the scene host, then calls `AdapterHostKernelPort` to create the runtime, system blackboard, console channels, and observer topology. The public `Runtime` surface is `IOrigoRuntime`; `_Process` delegates to `IOrigoFrameDriver.DriveFrame(delta)` |
| `OrigoDefaultEntry.cs` | Inherits OrigoAutoHost, holds startup configuration properties (`AutoDiscoverStrategies`, `_godotSkipPrefixes` (`private static readonly` field), `SceneAliasMapPath`, etc.), exposes `Context` to presentation code, and provides protected startup hooks such as `ConfigureStrategies` |
| `OrigoDefaultEntry.Bootstrap.cs` | Partial class, `_Ready` implementation: ConfigureStrategies(`ISndWorldAccess`) → register command handlers through the port → create and bind SndContext through `AdapterHostKernelPort.CreateContext` → call `Bootstrap()`. Any step failure marks the bootstrap failed (`MarkBootstrapFailed`) so the next frame fails fast |

## Startup Flow

```
OrigoDefaultEntry._Ready()
  └── base._Ready()                          // OrigoAutoHost
       └── AdapterHostKernelPort.CreateRuntime(...)
            ├── GodotFileSystem + GodotJsonConverterRegistry callback
            ├── kernel creates TypeStringMapping/Registry/IO/Meta/Path
            ├── PersistentBlackboard(...) → LoadFromDisk()
            ├── ConsoleInputBuffer / ConsoleOutputChannel (options may inject custom channels)
            ├── kernel creates OrigoRuntime
            └── ISndSceneHostRuntimeBinder.BindRuntimeDependencies(...)  // binds world/logger + observer topology
  ├── ConfigureStrategies(Runtime.SndWorld)  // ISndWorldAccess; manual registration before Bootstrap freeze
  ├── RegisterConsoleCommandHandlers()       // adapter handlers registered through the port
  ├── AdapterHostKernelPort.CreateContext(...)  // pass startup config
  ├── Context = sndContext                   // exposed to presentation/game code
  └── sndContext.Bootstrap()                 // Core-internal orchestration:

SndContext.Bootstrap() internal sequence:
  1. Strategy discovery       (OrigoAutoInitializer.DiscoverAndRegisterStrategies)
  2. Ordering validation and registration freeze (SndStrategyPool.SealRegistration)
  3. Scene alias loading      (SndWorld.LoadSceneAliases)
  4. SND template loading     (SndWorld.LoadTemplates)
  5. Entry save loading       (RequestLoadMainMenuEntrySave)
```

## Design Decisions

### Why the kernel port builds the runtime before the context

`SndWorld` hides concrete runtime construction. `AdapterHostKernelPort.CreateRuntime` creates the runtime and immediately binds world/logger plus the per-scene observer topology through `ISndSceneHostRuntimeBinder`; the same port then creates and binds `ISndContext`. The adapter never duplicates Core construction order and the scene host stays usable before the context exists.

### Why OrigoDefaultEntry is a partial class

Startup logic (`OrigoDefaultEntry.Bootstrap.cs`) is separated from exported property definitions (`OrigoDefaultEntry.cs`). Godot's [Export] attributes shown in the scene editor are clearer in the main file, while the orchestration logic in a separate file improves maintainability.

### Why strategy discovery filters Godot prefixes

`OrigoAutoInitializer.DiscoverAndRegisterStrategies` scans all assemblies in the current AppDomain. Godot and GodotSharp assemblies contain a large number of non-strategy classes; filtering prefixes avoids pointless scanning and registration errors. The prefix is passed into Core via `SndContextParameters.DiscoverySkipPrefixes`, rather than being hardcoded in the adapter layer.


### Why strategy registration must finish before Bootstrap

Lifecycle strategy `Before` / `After` constraints are validated over the complete registration graph, and the registry must be frozen before any entity is created. `SndContext.Bootstrap()` calls `SndStrategyPool.SealRegistration()` after strategy discovery: unknown targets, non-lifecycle targets, self references, or cycles throw immediately, and later `SndWorld.RegisterStrategy` calls throw. `AutoDiscoverStrategies` scans `[StrategyIndex]`-annotated types by default; strategies that need manual registration can override `ConfigureStrategies(ISndWorldAccess)` in a derived entry, which runs before `Bootstrap()`. The full ordering contract is in [Snd/Strategy](../../Origo.Core/Snd/Strategy/README.en.md).

### Why Context Is Public

`OrigoAutoHost` exposes the stable `IOrigoRuntime`. Common presentation needs (save listing, continue availability, lifecycle entry points, template and blackboard queries) are concentrated on `ISndContext`. `Context` shares the host entry lifecycle: it is assigned during `_Ready()` and is the same instance passed to `ConfigureSaveMetadataContributors`.

### Why startup orchestration is centralized in SndContext.Bootstrap()

The adapter layer should not directly call `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`, `LoadSceneAliases()`, `LoadTemplates()`, or `RequestLoadMainMenuEntrySave()`; strategy discovery and JSON entity-list spawning are now compiler-level `internal` and reachable only by `SndContext.Bootstrap`. Runtime template/alias map reloads should use the public companion: `ctx.Template.LoadTemplates(...)` / `ctx.Template.LoadSceneAliases(...)`. These are Core-internal orchestration operations — strategy discovery and ordering validation must execute in the Core layer, alias/template loading is Core configuration parsing, and entry save loading is the Core lifecycle entry point. The adapter layer only passes configuration through `AdapterContextOptions`; the port constructs and binds the context, and `Bootstrap()` ensures these operations complete in the correct layer with the correct dependency order.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
