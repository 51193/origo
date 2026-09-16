<!-- docsync-pair: Origo.Core/Utility/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Utility

> [↑ Back to Origo.Core](../README.en.md)

## Overview
General-purpose pure-function helpers: path normalization (`PathUtility`, consumed by adapter layers such as `GodotDirectoryOperations`), and string-to-typed inference (`ValueInference`, shared by the Archetype loader and console type inference).

## File List

| File | Responsibility |
|------|------|
| `PathUtility.cs` | Path operations: `Combine` (path joining + traversal attack detection — the guard runs **before every branch**, including an empty-string base and `scheme://` roots; `basePath` null throws `ArgumentNullException`, an empty-string base passes a non-traversal `relative` through directly), `GetParentDirectory` (parent extraction + root path boundary handling), `NormalizeDirectoryPath` (removes trailing slash; null throws `ArgumentNullException`), `ExtractGlobSuffix` (`"*.json"` → `".json"`). All three path functions recognize `scheme://` roots (e.g. `user://`): the root is never mangled by trailing-slash trimming, the parent of `user://x` is correctly `user://`, and a scheme root itself has no parent |
| `ValueInference.cs` | `internal` — unified string-to-typed inference (int → long → float → bool → string; float parsing rejects NaN/Infinity), shared by `SndArchetypeLoader` and console `bb_set` / `entity_set_data` |

## Design Decisions

### Why static utility classes
Stateless pure functions. Zero-cost calls, no DI or interface abstraction needed.

---
[↑ Back to Origo.Core](../README.en.md)
