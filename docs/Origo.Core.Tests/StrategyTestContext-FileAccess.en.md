<!-- docsync-pair: Origo.Core.Tests/StrategyTestContext-FileAccess -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Strategy Test Context File Access Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.en.md)
> [↔ Behavior under test: usage/strategy-testing](../usage/strategy-testing.en.md)

## Behavior Under Test Overview

Validates the behavior of `ISndFileAccess` on `StrategyTestContext` — in-memory file system support in strategy unit tests. `StrategyTestContext` automatically creates the full file I/O pipeline (`MemoryFileSystem` → `DataSourceIoGateway` → `DataSourceConverterRegistry`) at construction time, allowing strategy unit tests to verify file read/write logic in a completely disk-free environment.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `StrategyTestContextFileAccessTests.cs` | Behavior of ISndFileAccess on StrategyTestContext |

## StrategyTestContextFileAccessTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `FileExists_ReturnsFalseForNonexistentFile` | File does not exist initially | ISndFileAccess.FileExists |
| `FileExists_ReturnsTrueAfterWrite` | File exists after writing | ISndFileAccess.FileExists |
| `WriteThenReadFile_RoundTrip_PreservesData` | DataSourceNode written then read back; keys and values are consistent | ISndFileAccess.ReadFile/WriteFile |
| `WriteThenReadFile_RoundTrip_ForArrayNode` | Array node written then read back; length and elements are consistent | ISndFileAccess.ReadFile/WriteFile |
| `WriteFile_OverwriteTrue_ReplacesExisting` | overwrite=true overwrites an existing file | ISndFileAccess.WriteFile |
| `WriteFile_OverwriteFalse_ThrowsWhenFileExists` | overwrite=false throws when file exists | ISndFileAccess.WriteFile |
| `WriteThenReadObject_RoundTrip_Int` | int value written then read back; consistent | ISndFileAccess.ReadObject/WriteObject |
| `WriteThenReadObject_RoundTrip_String` | string value written then read back; consistent | ISndFileAccess.ReadObject/WriteObject |
| `WriteThenReadObject_RoundTrip_Bool` | bool value written then read back; consistent | ISndFileAccess.ReadObject/WriteObject |
| `WriteThenReadObject_RoundTrip_Double` | double value written then read back; consistent | ISndFileAccess.ReadObject/WriteObject |
| `WriteObject_OverwriteTrue_ReplacesExisting` | Strongly-typed writing supports overwrite semantics | ISndFileAccess.WriteObject |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `ReadFile_ThrowsForNonexistentFile` | File path does not exist | Throws exception (MemoryFileSystem throws FileNotFoundException) |
| `ReadObject_ThrowsForNonexistentFile` | File does not exist | Throws exception |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| None | — | This test file defines no helper strategies; pure interface behavior tests |

## Design Decisions

### Why StrategyTestContext has a built-in MemoryFileSystem

Strategy unit tests often require lightweight file I/O support (reading configurations, writing state). `StrategyTestContext` automatically creates the full `MemoryFileSystem` → `DataSourceIoGateway` → `DataSourceConverterRegistry` pipeline in its constructor, enabling strategy tests to use all methods of `ISndFileAccess` with zero configuration. All file operations complete in memory; no temporary directory creation or real filesystem mocking is needed.

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Strategy integration test using ISndFileAccess via ISndContext within StrategyTestScenario | Currently only testing interface behavior directly via StrategyTestContext; file read/write during full strategy workflows not tested | ISndFileAccess |
| ReadObject/WriteObject behavior after custom Converter registration | Correctness of file read/write after users register custom type converters in tests not verified | DataSourceConverterRegistry |

---

[↑ Back to Origo.Core.Tests](README.en.md)
