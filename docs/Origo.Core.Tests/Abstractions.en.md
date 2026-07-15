<!-- docsync-pair: Origo.Core.Tests/Abstractions -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Test Double Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Abstractions/FileSystem](../Origo.Core/Abstractions/FileSystem/README.en.md)
> [↔ Module under test: Origo.Core/Abstractions/Logging](../Origo.Core/Abstractions/Logging/README.en.md)

## Behavior Under Test Overview

Verifies the correctness of the test support facilities themselves — these facilities are the foundation
for all other tests. Covers `TestFileSystem` (in-memory IFileSystem implementation) with all 12 file/directory
operations, and `NullLogger`'s silent behavior.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `MemoryFileSystemTests.cs` | TestFileSystem: read/write/enumerate/copy/rename/delete/parent directory/path combination |
| `NullLoggerTests.cs` | NullLogger.Instance does not throw |
| `TestLoggerFilterTests.cs` (in `TestSupport/`) | TestLogger log level filtering behavior |
| `TestFileSystemAdditionalTests.cs` | TestFileSystem additional edge paths |

## MemoryFileSystemTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `MemoryFileSystem_BasicOperations` | Write→Exists→Read→Delete chain | IFileSystem |
| `MemoryFileSystem_EnumerateFiles` | Recursive/non-recursive enumeration, wildcard filtering | IFileSystem |
| `MemoryFileSystem_CombinePath` | Path combination, trailing slash handling | IFileSystem |
| `MemoryFileSystem_Rename` | File/directory rename | IFileSystem |
| `MemoryFileSystem_DeleteDirectory` | Recursive directory deletion | IFileSystem |
| `MemoryFileSystem_EnumerateFiles_CustomPatternAndBackslashNormalize` | Backslash path normalization, custom wildcards | IFileSystem |
| `MemoryFileSystem_Rename_FileAtRoot` | Root-level file rename | IFileSystem |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `MemoryFileSystem_ReadAllText_Missing_ThrowsFileNotFound` | Reading non-existent file | FileNotFoundException |
| `MemoryFileSystem_WriteAllText_NoOverwrite_ThrowsWhenExists` | Writing to existing file without overwrite | IOException |
| `MemoryFileSystem_Copy_SourceMissing_Throws` | Copying non-existent file | FileNotFoundException |
| `MemoryFileSystem_Copy_NoOverwrite_ThrowsWhenDestExists` | Copying to existing path without overwrite | IOException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `MemoryFileSystem_CreateDirectory_EmptyPath_NoOp` | Creating directory with empty path | Does not throw |
| `MemoryFileSystem_GetParentDirectory_EdgeCases` | File with no path separator / absolute path / normal path | Correctly returns parent directory |
| `MemoryFileSystem_EnumerateDirectories_FromExplicitDirectories` | Enumerating subdirectories from explicit directories | Includes subdirectories |

## NullLoggerTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `NullLogger_ImplementsILogger` | NullLogger.Instance can be referenced through ILogger interface, does not throw | ILogger |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `NullLogger_Instance_IsSingleton` | Accessing Instance twice | Returns same instance |

## TestFileSystemAdditionalTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `TestFileSystem_WriteAllText_And_ReadAllText` | WriteAllText followed by ReadAllText reads back consistently | IFileSystem |
| `TestFileSystem_WriteAllText_Overwrite` | overwrite=true overwrites existing file | IFileSystem |
| `TestFileSystem_Delete_RemovesFile` | Delete removes file, Exists returns false | IFileSystem |
| `TestFileSystem_CombinePath_CombinesCorrectly` | Path combination is correct | IFileSystem |
| `TestFileSystem_GetParentDirectory` | Extracting parent directory from file path | IFileSystem |
| `TestFileSystem_EnumerateDirectories` | Enumerating subdirectories from explicit directories | IFileSystem |
| `TestFileSystem_Rename_MovesAllFilesAndDirectories` | After directory rename, all files/subdirectories migrated, data unchanged | IFileSystem |
| `TestFileSystem_DeleteDirectory_RemovesAllContents` | Recursively deletes directory and all contents | IFileSystem |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `TestFileSystem_WriteAllText_NoOverwrite_Throws` | overwrite=false writing to existing file | IOException |

## TestLoggerFilterTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `MinimumLevel_SetToInfo_SuppressesDebug` | Debug level messages suppressed when MinimumLevel=Info | TestLogger |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `MinimumLevel_DefaultDebug_RecordsAllLevels` | Default MinimumLevel=Debug | All level messages recorded |
| `MinimumLevel_SetToError_OnlyRecordsError` | MinimumLevel=Error | Only Error level recorded |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| IFileSystem.Delete(path) behavior for directory paths | Semantics unclear | IFileSystem |

---

[↑ Back to Origo.Core.Tests](README.en.md)
