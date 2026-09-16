<!-- docsync-pair: Origo.Core.Tests/Snd-FileAccess -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# File Access Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.en.md)
> [↔ Behavior under test: usage/agent-reference](../usage/agent-reference.en.md)

## Behavior Under Test Overview

Validates the full behavior of `ISndFileAccess` on `SndContext`: DataSourceNode file read/write round-trips, strongly-typed object read/write round-trips, file existence checks, overwrite semantics, Map format parsing, nested JSON parsing, error paths (non-existent files, null paths, invalid JSON), and boundary paths (empty objects, Null nodes, Boolean values).

All file I/O uses the shared `TestMemoryFileSystem` (in-memory implementation); no real disk operations are involved.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `SndContextFileAccessTests.cs` | Complete behavior of ISndFileAccess on SndContext |

## SndContextFileAccessTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `ReadFile_ReadsJsonAndReturnsParsedTree` | Reading a JSON file returns a parsed DataSourceNode tree, supports `["key"]` access | ISndFileAccess.ReadFile |
| `ReadFile_ReadsMapFileAndReturnsParsedTree` | Reading a .map file returns a parsed DataSourceNode tree (`key: value` format) | ISndFileAccess.ReadFile |
| `ReadFile_ReadsNestedJsonStructure` | Reading nested JSON structures supports multi-level object/array access | ISndFileAccess.ReadFile |
| `WriteFile_WritesNodeAndCanBeReadBack` | Writing a DataSourceNode tree can be read back via ReadFile with data consistency | ISndFileAccess.WriteFile |
| `WriteFile_WithOverwriteTrue_OverwritesExistingFile` | overwrite=true overwrites an existing file; old data is replaced | ISndFileAccess.WriteFile |
| `WriteFile_WithOverwriteFalse_ThrowsWhenFileExists` | overwrite=false with an existing file throws IOException | ISndFileAccess.WriteFile |
| `WriteFile_WritesArrayNode` | Writing a DataSourceNode array node and reading it back in full | ISndFileAccess.WriteFile |
| `FileExists_ReturnsTrueForExistingFile` | Returns true when the file exists | ISndFileAccess.FileExists |
| `FileExists_ReturnsFalseForNonexistentFile` | Returns false when the file does not exist | ISndFileAccess.FileExists |
| `ReadObject_DeserializesJsonToTypedPrimitive` | Reads a JSON number and deserializes it as int via Converter | ISndFileAccess.ReadObject |
| `ReadObject_DeserializesJsonToString` | Reads a JSON string and deserializes it as string via Converter | ISndFileAccess.ReadObject |
| `WriteObject_SerializesTypedValueAndCanBeReadBack` | Writing an int and reading it back; value is consistent | ISndFileAccess.WriteObject |
| `WriteObject_WithOverwrite_ReplacesExisting` | Strongly-typed writing supports overwrite semantics | ISndFileAccess.WriteObject |
| `ReadWriteObject_RoundTrip_PreservesBool` | bool value round-trip is preserved correctly | ISndFileAccess.ReadObject/WriteObject |
| `ReadWriteObject_RoundTrip_PreservesDouble` | double value round-trip preserves precision | ISndFileAccess.ReadObject/WriteObject |
| `FileAccess_IsAccessibleThroughRoleInterface` | ISndFileAccess can be obtained and used via ISndContext cast | ISndContext |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ReadFile_ThrowsForNonexistentFile` | File path does not exist | Throws exception (type depends on IFileSystem implementation) |
| `ReadFile_ThrowsForNullPath` | null path | ArgumentException |
| `ReadFile_ThrowsForInvalidJson` | Invalid JSON syntax (e.g., `{broken`) | Throws exception on lazy evaluation |
| `WriteFile_ThrowsForNullNode` | null DataSourceNode | ArgumentNullException |
| `ReadObject_ThrowsForNonexistentFile` | File does not exist | Throws exception |
| `ReadObject_ThrowsForTypeMismatch` | JSON string node read as int | Throws exception (Converter mismatch) |
| `FileExists_ReturnsFalseForEmptyPath` | Empty string path | ArgumentException (Gateway rejects empty paths) |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `WriteFile_EmptyObject_RoundTrip` | Empty DataSourceNode object | Written and read back, contains no keys |
| `WriteFile_NullValueNode_RoundTrip` | Object containing a Null value node | After read back, IsNull is true |
| `WriteFile_BooleanValues_RoundTrip` | Object containing true/false nodes | After read back, AsBool() returns correct values |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| None | — | This test file defines no helper strategies; pure interface behavior tests |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| ReadObject/WriteObject round-trips for complex custom types | Currently only testing BCL primitives (int/string/bool/double); user-defined types not tested | ISndFileAccess |
| Thread safety of concurrent reads/writes on many files | Multi-threaded scenarios not covered | — |
| Lazy-deferred memory behavior of ReadFile for large files | Performance characteristics of large JSON files not covered | DataSourceNode |

---

[↑ Back to Origo.Core.Tests](README.en.md)
