<!-- docsync-pair: Origo.Core.Tests/Snd-ArchiveFileAccess -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Archive File Access Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.en.md)
> [↔ Behavior under test: usage/agent-reference](../usage/agent-reference.en.md)

## Behavior Under Test Overview

Validates the full behavior of `ISndArchiveFileAccess` on `SndContext`: DataSourceNode file read/write round-trips (paths relative to the archive active directory's `extra/` subdirectory), strongly-typed object read/write round-trips (including custom types with a registered converter), file existence checks, file deletion, overwrite semantics, Map format parsing, nested JSON parsing, error paths (non-existent files, path traversal, type mismatch, null nodes), and boundary paths (empty objects, Null nodes, Boolean values), as well as archive save/load round-trip persistence.

All file I/O uses the shared `TestMemoryFileSystem` (in-memory implementation); no real disk operations are involved.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `SndContextArchiveFileAccessTests.cs` | Complete behavior of ISndArchiveFileAccess on SndContext, including save/load round-trips |

## SndContextArchiveFileAccessTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `ReadFile_ReadsJsonFromExtraDirectory` | Reading a JSON file from `current/extra/` returns a parsed DataSourceNode tree; the path is automatically resolved relative to the `extra/` subdirectory | ISndArchiveFileAccess.ReadFile |
| `ReadFile_ReadsMapFileFromExtraDirectory` | Reading a .map file from `current/extra/` returns a parsed DataSourceNode tree (`key: value` format) | ISndArchiveFileAccess.ReadFile |
| `ReadFile_ReadsNestedJsonStructure` | Reading nested JSON structures (object containing arrays) supports multi-level object/array access | ISndArchiveFileAccess.ReadFile |
| `WriteFile_WritesToExtraAndCanBeReadBack` | Writing a DataSourceNode tree to `current/extra/` can be read back via ReadFile with data consistency | ISndArchiveFileAccess.WriteFile |
| `WriteFile_WithOverwriteTrue_OverwritesExistingFile` | overwrite=true overwrites an existing file in `extra/`; old data is replaced | ISndArchiveFileAccess.WriteFile |
| `WriteFile_WithOverwriteFalse_ThrowsWhenFileExists` | overwrite=false with an existing file throws IOException | ISndArchiveFileAccess.WriteFile |
| `WriteFile_WritesArrayNode` | Writing a DataSourceNode array node and reading it back in full | ISndArchiveFileAccess.WriteFile |
| `WriteFile_CreatesParentDirectories` | Writing a deep path automatically creates all parent directories under `extra/a/b/c/` | ISndArchiveFileAccess.WriteFile |
| `FileExists_ReturnsTrueForExistingFile` | Returns true when a file exists in `extra/` | ISndArchiveFileAccess.FileExists |
| `FileExists_ReturnsFalseForNonexistentFile` | Returns false when the file does not exist | ISndArchiveFileAccess.FileExists |
| `ReadObject_DeserializesTypedPrimitive` | Reads a JSON number and deserializes it as int via Converter | ISndArchiveFileAccess.ReadObject |
| `ReadWriteObject_RoundTrip_PreservesBool` | bool value round-trip is preserved correctly | ISndArchiveFileAccess.ReadObject/WriteObject |
| `ReadWriteObject_RoundTrip_PreservesString` | string value round-trip is preserved correctly | ISndArchiveFileAccess.ReadObject/WriteObject |
| `ReadWriteObject_RoundTrip_PreservesDouble` | double value round-trip preserves precision | ISndArchiveFileAccess.ReadObject/WriteObject |
| `ReadWriteObject_RoundTrip_PreservesCustomType` | After registering a custom type converter, a custom object round-trips through WriteObject → ReadObject | ISndArchiveFileAccess.ReadObject/WriteObject |
| `WriteObject_DisposesConverterNodeOnSuccess` | The DataSourceNode returned by the converter is disposed after a successful write; accessing `Kind` throws ObjectDisposedException | DataSourceNode ownership |
| `WriteObject_DisposesConverterNodeWhenWriteThrows` | When the gateway throws because overwrite=false, the converter node is still released in the finally path | DataSourceNode ownership |
| `DeleteFile_RemovesExistingFile` | Deleting an existing file in `extra/`; after deletion the file no longer exists | ISndArchiveFileAccess.DeleteFile |
| `FileExists_ReturnsFalseAfterDelete` | After deleting a file, FileExists returns false | ISndArchiveFileAccess.DeleteFile |
| `DeleteFile_ThenRead_Throws` | Reading after deletion throws an exception | ISndArchiveFileAccess.DeleteFile |
| `ArchiveFileAccess_IsAccessibleThroughRoleInterface` | ISndArchiveFileAccess can be obtained and used via ISndContext cast | ISndContext |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ReadFile_ThrowsForNonexistentFile` | File path does not exist | Throws exception |
| `ReadFile_ThrowsForPathTraversal` | `../escape.json` path traversal | ArgumentException |
| `WriteFile_ThrowsForPathTraversal` | `../escape.json` path traversal | ArgumentException |
| `DeleteFile_ThrowsForNonexistentFile` | Deleting a non-existent file | InvalidOperationException |
| `DeleteFile_ThrowsForPathTraversal` | `../escape.json` path traversal | ArgumentException |
| `ReadObject_ThrowsForTypeMismatch` | JSON string node deserialized as int | Throws exception (Converter mismatch) |
| `WriteFile_ThrowsForNullNode` | null DataSourceNode | ArgumentNullException |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `WriteFile_EmptyObject_RoundTrip` | Empty DataSourceNode object (zero keys) | Written and read back, contains no keys |
| `WriteFile_NullValueNode_RoundTrip` | Object containing a Null value node | After read back, IsNull is true |
| `WriteFile_BooleanValues_RoundTrip` | Object containing true/false nodes | After read back, AsBool() returns correct values |
| `ReadFile_DotDotInsideFileName_IsAllowed` | File name contains a `..` substring (e.g. `v1..2.map`, not a traversal segment) | Reads normally, not rejected |

### Save/Load Round-Trips

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `WriteFile_SurvivesSaveLoadRoundTrip` | Files written into `extra/` can still be read back after save → load, data consistent (verifies that `current/extra/` is snapshotted to `save_{id}/extra/` and restored on load) | ISndArchiveFileAccess |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| `NodeReturningConverter<T>` / `WriteProbe` | SndContextArchiveFileAccessTests.cs | Records the DataSourceNode returned by the converter and verifies deterministic release by WriteObject |
| `CustomPayloadConverter` / `CustomPayload` | SndContextArchiveFileAccessTests.cs | Custom type converter that verifies ReadObject/WriteObject round-trips after custom converter registration |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Thread safety of concurrent reads/writes on many files | Multi-threaded scenarios not covered | — |
| Cleanup behavior of `extra/` directory during Dispose/progress teardown | Lifecycle boundary cleanup not independently verified | ISndArchiveFileAccess |
| Lazy-deferred memory behavior of ReadFile for very large files | Performance characteristics of large JSON files not covered | DataSourceNode |

---

[↑ Back to Origo.Core.Tests](README.en.md)
