<!-- docsync-pair: Origo.Core/Utility/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Utility

> [↑ Back to Origo.Core](../README.en.md)

## Overview
General-purpose utility functions providing cross-module helper capabilities.

## File List

| File | Responsibility |
|------|------|
| `DiffUtility.cs` | Generic collection diff: (added, removed) from old vs new collections |
| `PathUtility.cs` | Path operations: `Combine` (join + traversal attack detection), `GetParentDirectory` (parent + root boundary), `NormalizeDirectoryPath`, `ExtractGlobSuffix` |

## Design Decisions

### Why static utility classes
Stateless pure functions. Zero-cost calls, no DI or interface abstraction needed.

### Why no complex diff algorithm
Current needs only detect element additions/removals in collections. HashSet-based is sufficient.

---
[↑ Back to Origo.Core](../README.en.md)
