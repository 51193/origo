<!-- docsync-pair: Origo.GodotAdapter.Tests/FileSystem -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# File System Tests (Adapter Layer)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Module under test: Origo.GodotAdapter/FileSystem](../Origo.GodotAdapter/FileSystem/README.en.md)

## Behavior Under Test Overview

Verifies `GodotFileSystem`'s handling of `res://` (read-only) and `user://` (writable) virtual path prefixes:
path combination and parent directory resolution are delegated to `Origo.Core.Utility.PathUtility`.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `GodotFileSystemPathTests.cs` | res:// / user:// path combination, parent directory resolution, and boundary inputs |

## GodotFileSystemPathTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `GodotFileSystem_CombinePath_UsesHelperRules` | `GodotFileSystem.CombinePath` delegates to `PathUtility` rules | GodotAdapter FileSystem |
| `GodotFileSystem_GetParentDirectory_UsesHelperRules` | `GodotFileSystem.GetParentDirectory` delegates to `PathUtility` rules | GodotAdapter FileSystem |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `GodotFileSystem_CombinePath_NullSecondArg_ReturnsFirst` | Second argument is null | Returns first argument |

## Test Support Strategy

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| None | — | This test file defines no support strategies; path logic correctness is covered by `Origo.Core.Tests/PathUtilityTests` |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Actual I/O operations of `GodotFileSystem` (`ReadAllText` / `WriteAllText` / `Exists` / `EnumerateFiles`, etc.) not covered (depends on Godot engine `FileAccess` / `DirAccess`, related production files excluded by coverlet) | Real file read/write and directory enumeration behavior not directly verified in tests | Origo.GodotAdapter/FileSystem |
| `res://` read-only constraint (writing to `res://` should be rejected) behavior not covered | Read-only semantics not verified | Origo.GodotAdapter/FileSystem |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
