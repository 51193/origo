<!-- docsync-pair: Origo.Core.Kernel/DataSource/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# DataSource

> [↑ Back to Origo.Core](../README.en.md)

## Module Capability

Origo's data source abstraction layer — the codec bridge between Core and external formats (JSON, .map). Provides a unified `DataSourceNode` tree data model, an I/O Gateway that auto-routes codecs by file suffix, and a bidirectional converter registry between CLR types and node data.

## Sub-Modules

| Sub-Module | Capability | Details |
|-----------|-----------|---------|
| [Codec](Codec/README.en.md) | Format codecs | `JsonDataSourceCodec` (lazy expansion) + `MapDataSourceCodec` (key:value, strict fail-fast; **keys/values that cannot round-trip and non-Text children are rejected on encode** — they would produce files the strict decoder cannot read back or silently drift; duplicate-key warnings on decode are observable through the injected logger) + `RawStringDataSourceCodec` (`.sha`/`.write_in_progress` raw text) |
| [Converters](Converters/README.en.md) | Type conversion | 14 basic types + 14 array types + 8 domain types + TypedData |

## This Layer's Core Files

| File | Responsibility |
|------|---------------|
| `DataSourceCodecKind.cs` | Codec format enum (Json / Map / RawString) |
| `IDataSourceCodec.cs` | Codec interface: Decode/Encode |
| `DataSourceIoGateway.cs` | I/O gateway implementation: suffix → CodecKind mapping + read/write |
| `DataSourceIoOptions.cs` | I/O routing config: suffix → codec mapping (indentation is controlled by `DataSourceFactory.BuildDefaultCodecs(bool)`) |
| `DataSourceFactory.cs` | Factory: creates default Registry + IoGateway |
| `DataSourceConverterRegistry.cs` | Converter registry: look up Converter by Type + generic Read/Write. When an exact type is not registered, automatically backtracks along base class and interface chains. |
| `KeyValueFileParser.cs` | key:value format parser (for .map files) |
| `FileMetaAccess.cs` | Default IFileMetaAccess implementation (internal), delegates to IFileSystem |
| `PathResolver.cs` | Default IPathResolver implementation (internal): CombinePath / GetParentDirectory, delegates to IFileSystem |

> Data-source leaf contracts (DataSourceNode, DataSourceNodeKind, converter bases, IDataSourceIoGateway, IFileMetaAccess) live in [Origo.Core.Contracts/DataSource](../../Origo.Core.Contracts/DataSource/README.en.md).

## Data Flow

```
External file (.json / .map / .sha / .write_in_progress / ...)
    │
    ▼
IDataSourceIoGateway.ReadTree / WriteTree (suffix routing → Codec, zero bypass)
    │                          ├── .json  → JsonDataSourceCodec
    │                          ├── .map   → MapDataSourceCodec (strict, fail-fast)
    │                          └── .sha / .write_in_progress → RawStringDataSourceCodec
    ▼
DataSourceNode (tree data)
    │
    ▼
DataSourceConverterRegistry (type conversion)
    │
    ▼
CLR objects (TypedData / SndMetaData / etc.)
```

## Design Decisions

- **IDataSourceIoGateway hard boundary**: All file content I/O in Core must go through the Gateway's `ReadTree`/`WriteTree`; direct `File.*` API is forbidden; zero bypass
- **Fail-fast**: On codec decode failure, the Gateway wraps the exception as an `InvalidOperationException` containing the file path and immediately throws. Note: `.json` decoding uses lazy expansion (see below), so a `JsonException` is thrown only when the node is first accessed — outside the Gateway's try/catch, without file-path context. Load paths supplement level/file context at first access (e.g. `ProgressRun`'s `ValidateLevelPayload`); `.map`/`.sha` are eagerly decoded, and parse errors are always wrapped by the Gateway
- **Lazy expansion**: Large JSON nodes expand children only on access, avoiding full parsing
- **Zero reflection**: All converters are explicitly registered; no reflection-based auto-discovery is used
- **Runtime type container**: `DataSourceNode` is a universal serialization container — the entire Save system and DataSource flow passes data through it, deferring type safety to `DataSourceConverterRegistry` lookups. This is a deliberate design trade-off ("simplicity over strict typing"), allowing all subsystems to share a single data tree at the cost of exposing conversion errors at runtime rather than compile time.
- **Strict reads**: archive payload converters (e.g. `StateMachineContainerPayloadConverter`) validate framework-mandatory fields (`key`/`pushIndex`/`popIndex` on each `machines` entry) and the node shape of array/object fields (stack, pairs, indices, etc.); `DataSourceNode.Keys`/`Count`/`Elements` reject wrong-shape access; `Keys`/`Elements` enumerate through read-only views. Array converters reject null/scalar/object nodes instead of silently returning an empty array. A corrupt archive immediately throws `InvalidOperationException`, never silently defaulting or becoming an empty collection (fail-fast, consistent with the Save strict-read contract)
- **Null values are never silently drifted**: `Read<string>` (including the runtime-typed overload) throws `InvalidOperationException` on a Null node — reading it as an empty string would silently drift null into `""`; callers must check `IsNull`/`TryGetValue` first (the pattern `TypedDataConverter` uses). `AsString()` returning `""` for a Null node stays as documented behavior (`DataSourceFactoryTests.AsString_OnNullNode_ReturnsEmpty` pins it)
- **Alternative direction: unified tree namespace (deferred)**: `DataSourceNode` already has the two foundations — tree shape and pluggable codecs. It could be promoted into a unified root mounting the local file system, save directories, and network resources, replacing several file APIs with path navigation such as `path -> to -> file -> entity -> health_point`; restricted subtrees would express access scopes structurally. The current synchronous read model is sufficient for local files, but remote nodes would block the frame, and the content/metadata boundary plus live-tree write-back semantics would need redefinition — hence deferred. See [#44](https://github.com/51193/origo/issues/44) for the full trade-off and re-evaluation signals

---
[↑ Back to Origo.Core](../README.en.md)
