<!-- docsync-pair: Origo.Core.Tests/Blackboard -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Blackboard Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core.Contracts/Blackboard](../Origo.Core.Contracts/Blackboard/README.en.md)
> [↔ Abstraction: Origo.Core.Contracts/Abstractions/Blackboard](../Origo.Core.Contracts/Abstractions/Blackboard/README.en.md)

## Behavior Under Test Overview

Verifies all behavior of the `IBlackboard` interface's default in-memory implementation: SetValue/Get
returning type-safe tuples, key validation (null/whitespace key rejection), type mismatch detection,
null semantics (registered reference kinds support null; unregistered reference types reject null),
Clear/GetKeys/SerializeAll/DeserializeAll full lifecycle.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `BlackboardTests.cs` | Blackboard CRUD + serialization + key validation + type safety |

## Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Blackboard_SetValue_And_TryGet_Int` | SetValue(100) → TryGet<int> returns (true, 100) | Blackboard Abstraction |
| `Blackboard_SetValue_And_TryGet_String` | SetValue("player") → TryGet<string> returns (true, "player") | Blackboard Abstraction |
| `Blackboard_SetValue_NullString_RemainsSupported` | Registered reference kind `string` can store null; TryGet still returns found=true | Blackboard Abstraction |
| `Blackboard_Clear_RemovesAll` | GetKeys is empty after Clear | Blackboard Abstraction |
| `Blackboard_GetKeys_ReturnsAllKeys` | GetKeys returns all key names | Blackboard Abstraction |
| `Blackboard_SerializeAll_And_DeserializeAll_RoundTrip` | Data consistent after serialize then deserialize | Blackboard Abstraction |
| `Blackboard_SetValue_OverwriteExisting` | Overwriting returns the latest value | Blackboard Abstraction |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `Blackboard_TryGet_MissingKey_ReturnsFalse` | Non-existent key | found=false |
| `Blackboard_TryGet_WrongType_ReturnsFalse` | int key read as string type | found=false |
| `Blackboard_SetValue_ThrowsOnNullKey` | null key | ArgumentException |
| `Blackboard_TryGet_ThrowsOnNullKey` | null key | ArgumentException |
| `Blackboard_SetValue_ThrowsOnWhitespaceKey` | Whitespace key | ArgumentException |
| `Blackboard_SetValue_NullUnregisteredReference_Throws` | Unregistered reference type `object?` written as null | ArgumentNullException; no unfindable entry is stored |

| `Blackboard_DeserializeAll_Null_Throws` | null data passed in | Throws Exception |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Performance with large key-value counts (10k+) | When blackboard is large-scale key-value store | — |

---

[↑ Back to Origo.Core.Tests](README.en.md)
