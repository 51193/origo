# Bootstrap

> [↑ Back to Origo.GodotAdapter](../README.en.md)

## Overview

Startup and orchestration for the Godot adapter layer. Creates the complete runtime stack (kernel `OrigoRuntime` + internal `GodotSndManager`) through `Origo.Core.Kernel.Ports.AdapterHostKernelPort`, and registers Godot-specific type mappings, serialization converters, and command handlers. All Godot-specific dependencies are injected at the adapter layer; the Core layer remains unaware of them.

## Files

| File | Responsibility |
|------|------|
| `OrigoAutoHost.cs` | Godot Node; prepares GodotFileSystem, type mappings, and the scene host, then calls `AdapterHostKernelPort` to create the runtime, system blackboard, console channels, and observer topology. The public `Runtime` surface is `IOrigoRuntime`; `_Process` delegates to `IOrigoFrameDriver.DriveFrame(delta)` |
| `OrigoDefaultEntry.cs` | Inherits OrigoAutoHost, holds startup configuration properties (`AutoDiscoverStrategies`, `_godotSkipPrefixes` (`private static readonly` field), `SceneAliasMapPath`, etc.), exposes `Context` to presentation code, and provides protected startup hooks such as `ConfigureStrategies` |
| `OrigoDefaultEntry.Bootstrap.cs` | Partial class, `_Ready` implementation: ConfigureStrategies(`ISndWorldAccess`) → register command handlers through `IOrigoRuntime.RegisterConsoleCommandHandler` → create and bind SndContext through `AdapterHostKernelPort.CreateContext` → call `Bootstrap()`. Any step failure marks the bootstrap failed (`MarkBootstrapFailed`) so the next frame fails fast |

## Startup Flow

The ordered startup sequence — runtime creation through `AdapterHostKernelPort`,
`ConfigureStrategies`, handler registration, context creation, and the internal
`SndContext.Bootstrap()` steps — is owned by
[Origo.GodotAdapter Startup Flow](../README.en.md#startup-flow), which also
records the failure semantics and how the Core host path differs. This page
keeps the per-file responsibilities above and the design decisions below.

## Design Decisions

### Why the kernel port builds the runtime before the context

`SndWorld` hides concrete runtime construction. `AdapterHostKernelPort.CreateRuntime` creates the runtime and immediately binds world/logger plus the per-scene observer topology through `ISndSceneHostRuntimeBinder`; the same port then creates and binds `ISndContext`. The adapter never duplicates Core construction order and the scene host stays usable before the context exists.

### Why OrigoDefaultEntry is a partial class

Startup logic (`OrigoDefaultEntry.Bootstrap.cs`) is separated from exported property definitions (`OrigoDefaultEntry.cs`). Godot's [Export] attributes shown in the scene editor are clearer in the main file, while the orchestration logic in a separate file improves maintainability.

### Why strategy discovery filters Godot prefixes

`OrigoAutoInitializer.DiscoverAndRegisterStrategies` scans all assemblies in the current AppDomain. Godot and GodotSharp assemblies contain a large number of non-strategy classes; filtering prefixes avoids pointless scanning and registration errors. The prefix is passed into Core via `SndContextParameters.DiscoverySkipPrefixes`, rather than being hardcoded in the adapter layer.


### Why strategy registration must finish before Bootstrap

Lifecycle strategy `Before` / `After` constraints are validated over the complete registration graph, and the registry must be frozen before any entity is created. `SndContext.Bootstrap()` calls `SndStrategyPool.SealRegistration()` after strategy discovery: unknown targets, non-lifecycle targets, self references, or cycles throw immediately, and later `SndWorld.RegisterStrategy` calls throw. `AutoDiscoverStrategies` scans every non-skipped loaded assembly for `[StrategyIndex]`-annotated types by default and registers each one; a non-parameterless constructor or duplicate index fails fast. Strategies that need manual registration must set `AutoDiscoverStrategies = false` and override `ConfigureStrategies(ISndWorldAccess)` in a derived entry, which runs before `Bootstrap()`. The full ordering contract is in [Snd/Strategy](../../Origo.Core/Snd/Strategy/README.en.md).

### Why the startup registry hook exists

`ConfigureStartupRegistry(TypeStringMapping, DataSourceConverterRegistry)` runs
inside `CreateRuntime` before `PersistentBlackboard.LoadFromDisk()`. A custom
type stored in `system.json` must be registered here (mapping plus converter);
`RegisterCustomConverters` runs during `Bootstrap`, after that initial read.
Godot calls the child `GodotSndManager._ExitTree` before the entry's: the manager calls `Context.Shutdown()` while the entity collection is still populated, so normal scene teardown runs the same quit hooks/current-directory cleanup as `OrigoHost.Dispose`. `OrigoDefaultEntry._ExitTree` calls `Shutdown()` again as an idempotent backstop.

### Why Context Is Public

`OrigoAutoHost` exposes the stable `IOrigoRuntime`. Common presentation needs (save listing, continue availability, lifecycle entry points, template and blackboard queries) are concentrated on `ISndContext`. `Context` shares the host entry lifecycle: it is assigned during `_Ready()` and is the same instance passed to `ConfigureSaveMetadataContributors`.

### Why startup orchestration is centralized in SndContext.Bootstrap()

The adapter layer should not directly call `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`, `LoadSceneAliases()`, `LoadTemplates()`, or `RequestLoadMainMenuEntrySave()`; strategy discovery and JSON entity-list spawning are compiler-level `internal` and reachable only by `SndContext.Bootstrap`. Runtime template/alias map reloads should use the public companion: `ctx.Template.LoadTemplates(...)` / `ctx.Template.LoadSceneAliases(...)`. These are Core-internal orchestration operations — strategy discovery and ordering validation must execute in the Core layer, alias/template loading is Core configuration parsing, and entry save loading is the Core lifecycle entry point. The adapter layer only passes configuration through `AdapterContextOptions`; the port constructs and binds the context, and `Bootstrap()` ensures these operations complete in the correct layer with the correct dependency order.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
