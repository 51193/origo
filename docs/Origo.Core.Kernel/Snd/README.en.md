# Snd (Kernel)

> [↑ Back to Origo.Core.Kernel](../README.en.md)

Kernel implementation of the SND context/world and its internal file companions.

## Included Files

| File | Responsibility |
|------|----------------|
| `SndContext.cs` | Unified SND runtime context facade: owns `SystemRun` and `ProgressRun` lifecycle transitions and exposes the ten `ISnd*` capability companions to strategies |
| `SndContextArchiveFileAccess.cs` | `internal` save-archive file access companion: resolves relative paths into the current save's `extra/` subdirectory and rejects path traversal |
| `SndContextFileAccess.cs` | `internal` static-resource file access companion: tree I/O through `IDataSourceIoGateway`, existence checks through `IFileMetaAccess`, typed conversion through `DataSourceConverterRegistry` |
| `SndContextParameters.cs` | Construction parameters for `SndContext`: runtime, I/O gateway, storage wiring, and optional bootstrap configuration (strategy discovery, alias/template maps, custom converters) |
| `SndDefaults.cs` | `internal` convention values for built-in initial saves and default level IDs, shared between kernel and adapter layers |
| `SndMappings.cs` | `internal` scene-alias → engine-resource-path and template-alias → `SndMetaData` mappings, attached to `SndWorld` and managed with the runtime lifecycle |
| `SndTemplateResolver.cs` | `internal` resolves `SndMetaData` against the loaded template path mapping with in-memory caching |
| `SndWorld.cs` | Unified SND entry point for upper-layer game code, encapsulating the strategy pool, serialization configuration, and mappings |

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
