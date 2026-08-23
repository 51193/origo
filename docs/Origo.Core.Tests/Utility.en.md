<!-- docsync-pair: Origo.Core.Tests/Utility -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Utility Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Utility](../Origo.Core/Utility/README.en.md)

## Verified Capabilities

Behavior of `PathUtility` static path operations and `ValueInference` string-to-typed value inference.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `Utility/PathUtilityTests.cs` | Path joining, traversal attack detection, parent directory extraction, glob suffix parsing |
| `Utility/ValueInferenceTests.cs` | int → long → float → bool → string inference order and type precision |

## Test Details

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
| `Combine_EmptyBase_RejectsTraversal` | Empty base path + traversal sequence (`../`, `..\\`) | ArgumentException (verifies the traversal guard applies to the empty-base branch) |
| `Combine_SchemeRootBase_RejectsTraversal` | Scheme root + traversal sequence | ArgumentException |
| `GetParentDirectory_SchemeFile_ReturnsSchemeRoot` | File under scheme returns its scheme root (`user://foo.map` → `user://`) |
| `GetParentDirectory_BackslashPath_ReturnsParent` | Windows backslash path parent extraction (`C:\base\sub` → `C:\base`) |

### PathUtility Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Combine_RejectsPathTraversal` | `..` path traversal sequences | `ArgumentException` |
| `GetParentDirectory_AtRoot_Throws` | Root path with no parent directory | `InvalidOperationException` |
| `GetParentDirectory_SchemeRoot_Throws` | `user://`/`res://` scheme root has no parent | `InvalidOperationException` |

### ValueInference Inference Order

| Test Method | Behavior Verified |
|-------------|------------------|
| `Infer_ReturnsFirstMatchingTypedValue` | Returns the first parseable type in int → long → float → bool → string order; `"42"`→int, `"3000000000"`→long, `"3.14"`→float, `"true"`→bool, everything else→string (including empty string and `"12abc"` returned verbatim) |

---

[↑ Back to Origo.Core.Tests](README.en.md)
