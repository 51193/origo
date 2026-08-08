<!-- docsync-pair: Origo.Core/Utility/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Utility

> [↑ Back to Origo.Core](../README.en.md)

## Overview
General-purpose pure-function helpers: collection diff (`DiffUtility`), path normalization (`PathUtility`, consumed by adapter layers such as `GodotDirectoryOperations`), and string-to-typed inference (`ValueInference`, shared by the Archetype loader and console type inference).

## File List

| File | Responsibility |
|------|------|
| `DiffUtility.cs` | Generic collection diff: (added, removed) from old vs new collections |
| `PathUtility.cs` | Path operations: `Combine` (path joining + traversal attack detection; `basePath` null throws `ArgumentNullException`, an empty-string base passes `relative` through directly), `GetParentDirectory` (parent extraction + root path boundary handling), `NormalizeDirectoryPath` (removes trailing slash), `ExtractGlobSuffix` (`"*.json"` → `".json"`). All three path functions recognize `scheme://` roots (e.g. `user://`): the root is never mangled by trailing-slash trimming, the parent of `user://x` is correctly `user://`, and a scheme root itself has no parent |
| `ValueInference.cs` | `internal` — unified string-to-typed inference (int → long → float → bool → string), shared by `SndArchetypeLoader` and console `bb_set` / `entity_set_data` |

## Design Decisions

### Why static utility classes
Stateless pure functions. Zero-cost calls, no DI or interface abstraction needed.

### Why no complex diff algorithm
Current needs only detect element additions/removals in collections. HashSet-based is sufficient.

---
[↑ Back to Origo.Core](../README.en.md)
