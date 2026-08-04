<!-- docsync-pair: Origo.Core/Abstractions/FileSystem/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# FileSystem (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: GodotAdapter/FileSystem](../../../Origo.GodotAdapter/FileSystem/README.en.md)

## Overview
Defines the platform-independent file system abstraction interface `IFileSystem`. The Core layer has no awareness of the underlying file system (engine virtual paths, OS local paths); all path operations are handled by the implementation to correctly process platform-specific path semantics (such as Godot's `res://` and `user://` prefixes).

## Included Files

| File | Responsibility |
|------|------|
| `IFileSystem.cs` | Complete file system abstraction: read/write, enumeration, directory management, path joining |
| `IPathResolver.cs` | Platform path computation: CombinePath, GetParentDirectory |

## Interface Members

| Method | Description |
|------|------|
| `Exists(path)` | Check if a file exists |
| `DirectoryExists(path)` | Check if a directory exists |
| `ReadAllText(path)` | Read entire file text |
| `WriteAllText(path, content, overwrite)` | Write text; controllable overwrite behavior |
| `Copy(src, dst, overwrite)` | Copy a file or directory |
| `EnumerateFiles(dir, pattern, recursive)` | Enumerate files with search pattern and recursion |
| `EnumerateDirectories(dir)` | Enumerate directories (non-recursive) |
| `CreateDirectory(path)` | Create a directory (including parent directories) |
| `Delete(path)` | Delete a file; silently ignored if non-existent |
| `DeleteDirectory(path)` | Recursively delete a directory; silently ignored if non-existent |
| `Rename(src, dst)` | Atomic rename/move |
| `CombinePath(base, relative)` | Platform-correct path joining |
| `GetParentDirectory(path)` | Get parent directory path |

## Design Decisions

### Why IFileSystem includes path operations
Methods like `CombinePath` and `GetParentDirectory` look like pure string operations, but in Godot `res://` and `user://` semantics differ from ordinary paths. Including path operations in the interface lets the implementation layer correctly handle virtual path prefixes, avoiding wrong string-joining assumptions in the Core layer.

### Why explicit overwrite parameter
The safety semantics of path operations should be explicitly readable. Implicit overwrite or silent failure makes it hard for callers to judge the real outcome of a write. The explicit `overwrite` parameter forces the caller to make a deliberate choice.

### Why Rename's overwrite behavior is left to the implementation
Different platforms (Godot virtual file system vs OS local file system) handle renaming onto an existing target differently. The Core layer should not presume a behavior; the adapter layer implements the safest policy according to platform semantics.

### Why strategies do not directly use IFileSystem
`IFileSystem` is completely internalized — neither strategies nor infrastructure modules reference it directly. Strategies access files through `ISndFileAccess` (static resource file access, the `FileAccess` companion property of `ISndContext`) and `ISndArchiveFileAccess` (save-internal file access, the `ArchiveFileAccess` companion property). `ISndFileAccess` internally delegates to three base interfaces:

- `IDataSourceIoGateway`: content read/write (only `ReadTree`/`WriteTree`; all files are forced through the codec routing — including extension-less structured files like `.sha` and `.write_in_progress`, routed via `RawStringDataSourceCodec`), returning parsed `DataSourceNode` trees
- `IFileMetaAccess`: file metadata (FileExists, DirectoryExists, Enumerate, CreateDirectory, Delete, Copy, Rename)
- `IPathResolver`: platform path computation (CombinePath, GetParentDirectory)

`ISndArchiveFileAccess` additionally provides `DeleteFile`; its paths are relative to the `extra/` subdirectory of the save's active directory, and written files follow the save lifecycle. This ensures:

- All file content I/O uniformly passes through `IDataSourceIoGateway` (the framework's hard I/O boundary)
- File metadata operations go through `IFileMetaAccess`, path computation through `IPathResolver`
- Strategies never parse raw JSON/Map text themselves or deal with platform path differences
- Encoding/decoding policies are centrally managed; swapping engines requires no strategy changes
- The `IFileSystem` interface itself is implemented by the adapter layer; Core's built-in `MemoryFileSystem` serves as a zero-dependency reference implementation, public for tests and adapter reuse (see the DataSource module docs)

---
[↑ Back to Abstractions](../README.en.md)
