# Snd (Kernel)

> [↑ Back to Origo.Core.Kernel](../README.en.md)

Kernel implementation of the SND context/world, entity aggregate, scene host,
strategy/observer machinery, and their internal file companions.

## Sub-modules

| Sub-module | Capability | Details |
|------------|------------|---------|
| [Companions](Companions/README.en.md) | Internal `SndContext` role companions | Blackboard / deferred / template / console / state-machine / save / lifecycle / state-machine-context views |
| [Entity](Entity/README.en.md) | SND entity aggregate implementation | Entity data, node, passive/active strategy managers, and raw-subscription plumbing |
| [Scene](Scene/README.en.md) | Scene host and entity factory | Full-memory scene host, entity naming policy, spawn rollback, and node factory |
| [Strategy](Strategy/README.en.md) | Strategy pool, managers, and observer topology | Lifecycle ordering, active/passive managers, pool, and observer binding topology |

## Included Files

| File | Responsibility |
|------|----------------|
| `SndContext.cs` | Unified SND runtime context facade: owns `SystemRun` and `ProgressRun` lifecycle transitions and exposes nine `ISnd*` role companions plus `IStateMachineContext` to strategies |
| `SndContextArchiveFileAccess.cs` | `internal` save-archive file access companion: resolves relative paths into the current save's `extra/` subdirectory and rejects path traversal |
| `SndContextFileAccess.cs` | `internal` static-resource file access companion: tree I/O through `IDataSourceIoGateway`, existence checks through `IFileMetaAccess`, typed conversion through `DataSourceConverterRegistry` |
| `SndContextParameters.cs` | Construction parameters for `SndContext`: runtime, I/O gateway, storage wiring, and optional bootstrap configuration (strategy discovery, alias/template maps, custom converters) |
| `SndDefaults.cs` | `internal` convention values for built-in initial saves and default level IDs, shared between kernel and adapter layers |
| `SndMappings.cs` | `internal` scene-alias → engine-resource-path and template-alias → `SndMetaData` mappings, attached to `SndWorld` and managed with the runtime lifecycle |
| `SndShutdownState.cs` | `internal` owner of shutdown terminal/in-progress flags, nested-request consumption, and the shared cleanup-commit/retry transitions used by direct shutdown and workflow teardown |
| `SndTemplateResolver.cs` | `internal` resolves `SndMetaData` against the loaded template path mapping with in-memory caching |
| `SndWorld.cs` | Unified SND entry point for upper-layer game code, encapsulating the strategy pool, serialization configuration, and mappings |

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
