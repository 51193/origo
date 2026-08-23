<!-- docsync-pair: Origo.Core/DataSource/README -->
<!-- docsync-revision: 13 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
| `DataSourceNode.cs` | Tree data node: Map/Array/Text/Number/Bool/Null + lazy expansion (Lazy) + `As<T>()` generic value accessor (supports 14 types: string/char/byte/sbyte/short/ushort/int/uint/long/ulong/float/double/decimal/bool) + Builder `Add` (**allowed on Map/Array nodes only** — calling it on a scalar node throws `InvalidOperationException` immediately; null children are rejected immediately) + `Keys`/`Elements` (**shape-strict**: accessing them on a non-Map/non-Array node throws `InvalidOperationException`, preventing wrong shapes from silently becoming empty collections; they return read-only views, never the mutable backing storage) + `ComputeSha256Hash()` — iterative post-order traversal to generate deterministic string representation then compute SHA-256 hash, used for save idempotent dedup. `Dispose()` also uses iterative traversal to prevent stack overflow on deeply nested trees |
| `DataSourceNodeKind.cs` | Node type enum |
| `DataSourceCodecKind.cs` | Codec format enum (Json / Map / RawString) |
| `IDataSourceCodec.cs` | Codec interface: Decode/Encode |
| `IDataSourceIoGateway.cs` | I/O gateway interface: only `ReadTree` / `WriteTree` two methods; reads/writes files after routing codecs by suffix (Core's sole content contact point with files). All file content I/O is routed through codecs; zero bypass. |
| `DataSourceIoGateway.cs` | I/O gateway implementation: suffix → CodecKind mapping + read/write |
| `DataSourceIoOptions.cs` | I/O routing config: suffix → codec mapping (indentation is controlled by `DataSourceFactory.BuildDefaultCodecs(bool)`) |
| `DataSourceFactory.cs` | Factory: creates default Registry + IoGateway |
| `DataSourceConverter.cs` | Generic converter base class: `Read(DataSourceNode)` / `Write(T)` |
| `DataSourceConverterRegistry.cs` | Converter registry: look up Converter by Type + generic Read/Write. When an exact type is not registered, automatically backtracks along base class and interface chains. |
| `KeyValueFileParser.cs` | key:value format parser (for .map files) |
| `MemoryFileSystem.cs` | In-memory file system implementing `IFileSystem` (internal; test projects use it via InternalsVisibleTo, no production consumer) |
| `IFileMetaAccess.cs` | File metadata operation interface (public): FileExists / DirectoryExists / EnumerateFiles / EnumerateDirectories / CreateDirectory / Delete / DeleteDirectory / Copy / Rename; used alongside IDataSourceIoGateway — the Gateway handles content read/write (including codec routing), this interface handles file system structure operations |
| `FileMetaAccess.cs` | Default IFileMetaAccess implementation (internal), delegates to IFileSystem |
| `PathResolver.cs` | Default IPathResolver implementation (internal): CombinePath / GetParentDirectory, delegates to IFileSystem |

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
- **Strict reads**: archive payload converters (e.g. `StateMachineContainerPayloadConverter`) validate framework-mandatory fields (`key`/`pushIndex`/`popIndex` on each `machines` entry) and the node shape of array/object fields (stack, pairs, indices, etc.); `DataSourceNode.Keys`/`Count`/`Elements` reject wrong-shape access; `Keys`/`Elements` enumerate through read-only views. Array converters no longer silently return an empty array for null/scalar/object nodes. A corrupt archive immediately throws `InvalidOperationException`, never silently defaulting or becoming an empty collection (fail-fast, consistent with the Save strict-read contract)
- **Null values are never silently drifted**: `Read<string>` (including the runtime-typed overload) throws `InvalidOperationException` on a Null node — reading it as an empty string would silently drift null into `""`; callers must check `IsNull`/`TryGetValue` first (the pattern `TypedDataConverter` uses). `AsString()` returning `""` for a Null node stays as documented behavior (`DataSourceFactoryTests.AsString_OnNullNode_ReturnsEmpty` pins it)
- **Alternative direction: unified tree namespace (deferred)**: `DataSourceNode` already has the two foundations — tree shape and pluggable codecs. It could be promoted into a unified root mounting the local file system, save directories, and network resources, replacing several file APIs with path navigation such as `path -> to -> file -> entity -> health_point`; restricted subtrees would express access scopes structurally. The current synchronous read model is sufficient for local files, but remote nodes would block the frame, and the content/metadata boundary plus live-tree write-back semantics would need redefinition — hence deferred. See [Extension Directions and Deferred Designs](../../usage/extension-directions.en.md) for the full trade-off and re-evaluation signals

---
[↑ Back to Origo.Core](../README.en.md)
