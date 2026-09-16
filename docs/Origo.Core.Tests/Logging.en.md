<!-- docsync-pair: Origo.Core.Tests/Logging -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Logging Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Logging](../Origo.Core/Logging/README.en.md)

## Behavior Overview

Validates LogMessageBuilder structured construction: plain messages, with timestamps (SetElapsedMs),
with context (AddContext), null/whitespace key skipping, combined usage, zero-value timestamps.

Validates the behavior of the `Logger<T>` generic wrapper: automatic tag generation from type name,
explicit interface implementation compatibility with manual tags, null inner guard, and composition with NullLogger.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `LogMessageBuilderTests.cs` | LogMessageBuilder structured construction |
| `LoggerGenericTests.cs` | `Logger<T>` type-aware logging wrapper |

## Test Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Build_PlainMessage` | Build("hello") returns "hello" | Logging |
| `SetElapsedMs_IncludesTimestamp` | SetElapsedMs → "[+Xms]" prefix | Logging |
| `AddContext_AppendsContext` | AddContext("key","val") → "test \| key=val" | Logging |
| `AddContext_MultipleEntries_AllIncluded` | Multiple AddContext → comma-separated | Logging |
| `Combined_ElapsedAndContext` | timestamp + context combined correctly | Logging |
| `SetElapsedMs_Zero_NotTruncated` | 0ms → "[+0.00ms]" | Logging |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `AddContext_NullKey_Skipped` | null key | Skipped |
| `AddContext_NullValue_Preserved` | null value | Preserved as `key=` (empty value, not skipped) |
| `AddContext_WhitespaceKey_Skipped` | Whitespace key | Skipped |

## Known Coverage Gaps

None — this module consists of a single simple utility class with complete test coverage.

## LoggerGenericTests Details

### Happy Path

| Test Method | Verified Behavior |
|-------------|-----------------|
| `LoggerT_TagDerived_FromTypeName` | `Logger<TestCategory>.Log()` automatically uses `typeof(T).Name` as the tag |
| `LoggerT_ExplicitInterface_UsesProvidedTag` | Calling through `ILogger` base interface uses the explicitly provided tag |
| `LoggerT_DifferentTypes_HaveDifferentTags` | `Logger<string>` and `Logger<int>` use `"String"` and `"Int32"` respectively |
| `LoggerT_WrapsNullLogger` | `new Logger<T>(NullLogger.Instance)` does not throw |
| `LoggerT_GenericType_TagUsesFriendlyName` | `Logger<GenericTest<int,string>>` uses CLR friendly name |

### Error Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `LoggerT_Constructor_NullInner_Throws` | `new Logger<T>(null)` | `ArgumentNullException` |

---

[↑ Back to Origo.Core.Tests](README.en.md)
