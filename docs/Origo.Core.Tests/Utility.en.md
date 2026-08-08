<!-- docsync-pair: Origo.Core.Tests/Utility -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Utility Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Utility](../Origo.Core/Utility/README.en.md)

## Verified Capabilities

Behavior of `DiffUtility.Diff<T>()` and `PathUtility` static path operations.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `Utility/DiffUtilityTests.cs` | Diff collection difference comparison correct/error/boundary paths |
| `Utility/PathUtilityTests.cs` | Path joining, traversal attack detection, parent directory extraction, glob suffix parsing |

## Test Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Diff_AddedItems_Detected` | Added items correctly detected | `DiffUtility` public API |
| `Diff_RemovedItems_Detected` | Removed items correctly detected | `DiffUtility` public API |
| `Diff_AddedAndRemoved` | Mixed additions and removals | `DiffUtility` public API |
| `Diff_Duplicates_TreatedAsSingle` | Duplicates are deduplicated before comparison | `DiffUtility` public API |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Diff_EmptyBoth_ReturnsEmpty` | Both collections are empty | added, removed are both empty lists |
| `Diff_EmptyOld_NewHasItems_ReturnsAdded` | Old collection is empty | All new items counted as added |
| `Diff_EmptyNew_OldHasItems_ReturnsRemoved` | New collection is empty | All old items counted as removed |
| `Diff_NoChange_ReturnsEmpty` | Both collections have the same content | added, removed are both empty lists |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Diff_NullOld_Throws` | oldItems is null | `ArgumentNullException` |
| `Diff_NullNew_Throws` | newItems is null | `ArgumentNullException` |

### PathUtility Correct Paths

| Test Method | Behavior Verified |
|-------------|------------------|
| `NormalizeDirectoryPath_StripsTrailingSlashes` | Trailing slashes stripped |
| `ExtractGlobSuffix_ReturnsSuffix` | `"*.json"` → `".json"` |
| `ExtractGlobSuffix_ReturnsNull_WhenNoGlob` | No wildcard pattern returns null |
| `Combine_EmptyBase_ReturnsRelative` | Empty-string base path returns relative path directly (passthrough) |
| `Combine_NullBase_Throws` | Base path is null | `ArgumentNullException` (fail-fast) |
| `Combine_NullOrEmptyRelative_ReturnsBase` | Relative path empty returns base path |
| `Combine_JoinsPaths` | Normal path joining (redundant slashes removed) |
| `GetParentDirectory_ReturnsParent` | Parent directory extracted |
| `GetParentDirectory_NullOrEmpty_ReturnsEmpty` | null/empty input returns string.Empty |
| `GetParentDirectory_SingleSegment_ReturnsEmpty` | Single-segment path with no parent returns string.Empty |
| `NormalizeDirectoryPath_SchemePath_TrimsTrailingSlash` | `user://dir/` trailing slash trimmed |
| `NormalizeDirectoryPath_SchemeRoot_IsPreserved` | `user://`/`res://` scheme roots keep double slash |
| `Combine_SchemeRootBase_KeepsDoubleSlash` | Combining with `user://` root keeps double slash |
| `GetParentDirectory_SchemeFile_ReturnsSchemeRoot` | File under scheme returns its scheme root (`user://foo.map` → `user://`) |
| `GetParentDirectory_BackslashPath_ReturnsParent` | Windows backslash path parent extraction (`C:\base\sub` → `C:\base`) |

### PathUtility Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Combine_RejectsPathTraversal` | `..` path traversal sequences | `ArgumentException` |
| `GetParentDirectory_AtRoot_Throws` | Root path with no parent directory | `InvalidOperationException` |
| `GetParentDirectory_SchemeRoot_Throws` | `user://`/`res://` scheme root has no parent | `InvalidOperationException` |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Diff dedup/comparison for custom reference types that are not IEquatable<T> | Reference equality vs value equality semantics | DiffUtility |

---

[↑ Back to Origo.Core.Tests](README.en.md)
