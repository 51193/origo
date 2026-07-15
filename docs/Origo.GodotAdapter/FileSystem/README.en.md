<!-- docsync-pair: Origo.GodotAdapter/FileSystem/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# FileSystem

> [↑ Back to Origo.GodotAdapter](../README.en.md) · [↔ Core abstraction: Abstractions/FileSystem](../../Origo.Core/Abstractions/FileSystem/README.en.md)

## Overview

The Godot implementation of the `IFileSystem` interface. Built on Godot's `FileAccess` and `DirAccess` APIs, supporting both `res://` (read-only project resources) and `user://` (writable user data) virtual path schemes. All path operations are implemented in this layer, correctly handling Godot virtual path semantics.

## Files

| File | Responsibility |
|------|------|
| `GodotFileSystem.cs` | Godot implementation of `IFileSystem`, delegating to segmented static classes |
| `GodotFileOperations.cs` | File-level operations: Exists/ReadAllText/WriteAllText/Copy/Delete |
| `GodotDirectoryOperations.cs` | Directory-level operations: Exists/Create/EnumerateFiles/EnumerateDirectories/Rename/DeleteRecursive (path operations directly call `Origo.Core.Utility.PathUtility`) |

## Module Details

### GodotFileSystem

A thin facade that delegates all `IFileSystem` methods to the appropriate static utility classes. For example, `Exists` → `GodotFileOperations.Exists`, `DirectoryExists` → `GodotDirectoryOperations.Exists`.

### GodotFileOperations

- **ReadAllText**: `FileAccess.Open(path, Read)` → `GetAsText()`
- **WriteAllText**: `FileAccess.Open(path, Write)` → `StoreString(content)`
- **Copy**: ReadAllText + WriteAllText (simple copy, suitable for small files; large file copies can be optimized at a higher level)
- **Delete**: `DirAccess.RemoveAbsolute(path)`

### GodotDirectoryOperations

- **Create**: `DirAccess.MakeDirRecursiveAbsolute`
- **EnumerateFiles**: Supports `*pattern` suffix filtering and recursive mode
- **DeleteRecursive**: Delete files first, then recursively delete subdirectories, finally delete the current directory
- **Rename**: Opens the parent directory then calls `DirAccess.Rename`

## Design Decisions

### Why file operations are split into File/Directory

A single `GodotFileSystem` class containing all implementation details would be too long (expected 200+ lines). Splitting into two static classes by file vs. directory operations reduces navigation cost. Path handling logic resides in `Origo.Core.Utility.PathUtility` (already extracted from the GodotAdapter layer); `GodotFileSystem` and `GodotDirectoryOperations` call it directly without an intermediate wrapper layer.

### Why Rename is not implemented with FileAccess

Godot directory rename/move operations require opening the target's parent directory, then executing `DirAccess.Rename` on the full path. File-level rename also requires directory operations under the hood, hence it resides in `GodotDirectoryOperations`.

### Why Copy uses read-then-write instead of streaming

Current save files (JSON, map) are small (KB-level), and read-then-write is simple and reliable. If large resource file copying is needed in the future, streaming can be introduced at a higher level (e.g., `SaveStorageFacade`) without modifying the low-level interface.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
