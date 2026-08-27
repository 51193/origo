<!-- docsync-pair: Origo.TestSupport/FileSystem/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# FileSystem

> [↑ Back to TestSupport](../README.en.md)

## Overview

Pure in-memory test double for `IFileSystem` with zero physical file system dependencies.

## File Inventory

| File | Responsibility |
|------|---------------|
| `MemoryFileSystem.cs` | Pure in-memory `IFileSystem` reference implementation: stores file contents in a `Dictionary<string, string>` and directories in a `HashSet<string>`; implements file/directory read/write, enumeration, deletion, copy, rename, path combine, and parent directory. |
| `TestMemoryFileSystem.cs` | Test decorator over `MemoryFileSystem` adding the `SeedFile` convenience method and `ReadAllTextCallCount` tracking. |

## Design Decisions

### Why an in-memory implementation instead of a mocking framework

`IFileSystem` methods form stateful collaborations (Write → Read → Exists → Delete → Enumerate). Per-method stubs from mocking frameworks cannot correctly simulate this cross-call state. The in-memory implementation preserves the same semantics as a real file system with zero I/O overhead.

### Why MemoryFileSystem lives in TestSupport rather than Core

`MemoryFileSystem` has no production consumers; only test projects use it, either through `TestMemoryFileSystem` or directly. Per AGENTS §1.2, production assemblies should not carry internal implementations solely for test convenience, so the reference implementation lives in `Origo.TestSupport`. Core keeps only the `IFileSystem` abstraction and the path/data-source components needed by production code.

---

[↑ Back to TestSupport](../README.en.md)
