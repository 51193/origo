<!-- docsync-pair: Origo.Core/Abstractions/FileSystem/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# FileSystem (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: GodotAdapter/FileSystem](../../../Origo.GodotAdapter/FileSystem/README.en.md)

## Overview
Defines the platform-independent file system abstraction interface `IFileSystem`. The Core layer has no awareness of the underlying file system; all path operations are handled by the implementation to correctly process platform-specific path semantics.

## Included Files

| File | Responsibility |
|------|------|
| `IFileSystem.cs` | Complete file system abstraction: read/write, enumeration, directory management, path joining |

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
| `DeleteDirectory(path)` | Recursively delete a directory |
| `Rename(src, dst)` | Atomic rename/move |
| `CombinePath(base, relative)` | Platform-correct path joining |
| `GetParentDirectory(path)` | Get parent directory path |

## Design Decisions

### Why IFileSystem includes path operations
In Godot, `res://` and `user://` semantics differ from ordinary paths. Including path operations in the interface lets the implementation layer correctly handle virtual path prefixes.

### Why explicit overwrite parameter
The safety semantics of path operations should be explicitly readable. The explicit `overwrite` parameter forces the caller to make a deliberate choice.

### Why strategies do not directly use IFileSystem
`IFileSystem` is completely internalized. Strategies access files through `ISndFileAccess` and `ISndArchiveFileAccess`. All file content I/O is uniformly routed through `IDataSourceIoGateway` (the framework's hard boundary), while file metadata goes through `IFileMetaAccess` and path computation through `IPathResolver`.

---
[↑ Back to Abstractions](../README.en.md)
