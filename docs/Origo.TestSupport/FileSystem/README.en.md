<!-- docsync-pair: Origo.TestSupport/FileSystem/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# FileSystem

> [↑ Back to TestSupport](../README.en.md)

## Overview

Pure in-memory test double for `IFileSystem` with zero physical file system dependencies.

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestMemoryFileSystem.cs` | Full `IFileSystem` implementation (file/directory read/write, enumeration, deletion, copy, rename, path combine, parent directory). Stores file contents in a `Dictionary<string, string>` and directories in a `HashSet<string>`. Exposes invocation-count tracking properties for behavior verification. |

## Design Decisions

### Why an in-memory implementation instead of a mocking framework

`IFileSystem` methods form stateful collaborations (Write → Read → Exists → Delete → Enumerate). Per-method stubs from mocking frameworks cannot correctly simulate this cross-call state. The in-memory implementation preserves the same semantics as a real file system with zero I/O overhead.

---

[↑ Back to TestSupport](../README.en.md)
