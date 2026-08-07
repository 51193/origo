<!-- docsync-pair: Origo.Core.Tests/TypeStringMapping -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Type Serialization Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Serialization](../Origo.Core/Serialization/README.en.md)

## Behavior Under Test Overview

Validates TypeStringMapping's CLR type ↔ stable string identifier bidirectional mapping: all BCL primitive types and array types pre-registered, custom type registration with bidirectional lookup, conflict detection (name conflict/type conflict), null/whitespace key validation. Also covers SndMappings scene alias/template loading and parsing, and JSON ↔ TypedData/SndMetaData codec integration.

`SchedulingAndTypeMappingTests.cs` hosts tests for both ActionScheduler and TypeStringMapping capabilities: this document only records its TypeStringMapping-related methods (`TypeStringMapping_HasDefaultTypes_AndSupportsCustomRegistration`); its ActionScheduler methods are recorded in [Runtime-Core.md](Runtime-Core.en.md) and are not duplicated here.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `TypeStringMappingTests.cs` | Basic conflict detection |
| `TypeStringMappingExtendedTests.cs` | BCL pre-registration verification, custom registration round-trips, ReadOnlyDictionary pre-registration, conflict detection, null/whitespace key validation |
| `JsonAndMappingsTests.cs` | JSON and type mapping integration: SndMetaData/TypedData round-trips, SndMappings scene alias and template parsing, Blackboard SerializeAll, JSON array root decoding |
| `SchedulingAndTypeMappingTests.cs` | TypeStringMapping default types and custom registration (ActionScheduler methods see [Runtime-Core.md](Runtime-Core.en.md)) |

## TypeStringMappingTests Details

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `RegisterType_ThrowsOnConflictingMapping` | DateTime→"MyDateTime" then register long→"MyDateTime" or DateTime→"OtherName" | InvalidOperationException |

## TypeStringMappingExtendedTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `TypeStringMapping_RegisterType_DuplicateSameType_NoThrow` | Duplicate registration of the same mapping does not throw | Serialization |
| `TypeStringMapping_BclTypes_AllPreregistered` | Int32/String/Boolean/Single/Double/Int64/Int16/Byte/ArrayString etc. are obtainable | Serialization |
| `TypeStringMapping_RegisterCustomType_RoundTrips` | Register Guid → bidirectional lookup is correct | Serialization |
| `TypeStringMapping_ReadOnlyDictionaryTypes_Preregistered` | ReadOnlyDictionary / IReadOnlyDictionary types are bidirectionally pre-registered | Serialization |
| `TypeStringMapping_RegisterManyCustomTypes_AllResolvable` | Register DateTime/Uri/Version/TimeSpan sequentially → all bidirectionally queryable | Serialization |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `TypeStringMapping_RegisterType_ConflictingNameToType_Throws` | "Int32" already mapped to int; remap to long | InvalidOperationException |
| `TypeStringMapping_RegisterType_ConflictingTypeToName_Throws` | int already mapped to "Int32"; remap to "MyInt" | InvalidOperationException |
| `TypeStringMapping_RegisterType_WhitespaceName_Throws` | "" or "  " name | ArgumentException |
| `TypeStringMapping_GetTypeByName_UnregisteredType_Throws` | Unregistered name | InvalidOperationException |
| `TypeStringMapping_GetNameByType_UnregisteredType_Throws` | Unregistered type | InvalidOperationException |
| `TypeStringMapping_RegisterType_NullName_Throws` | null name | ArgumentNullException |
| `TypeStringMapping_GetTypeByName_NullName_Throws` | null name | ArgumentNullException |
| `TypeStringMapping_GetNameByType_NullType_Throws` | null type | ArgumentNullException |

## JsonAndMappingsTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `SndMetaData_RoundTripPreservesTypedData` | SndMetaData via registry.Write → JSON encode → decode → Read round-trip preserves name/nodes/strategies/TypedData | Serialization |
| `SndMappings_LoadSceneAliases_DuplicateKey_LastWins` | Duplicate key in scene alias map; latter overwrites former | Serialization |
| `SndMappings_LoadSceneAliasesAndTemplates_ResolveExpectedValues` | After loading scene aliases and templates, resolve correctly; template resolution uses cache and does not re-read files | Serialization |
| `SndMappings_ResolveMetaListFromJsonArray_SupportsTemplateAndInlineMix` | JSON array mixing templateKey references and inline definitions resolved to meta list | Serialization |
| `Blackboard_SerializeAll_ReturnsDetachedCopy` | SerializeAll returns a detached copy; modifying the copy does not affect the blackboard original | Serialization |
| `JsonCodec_DecodeJsonArrayRoot_ReadsElements` | Top-level JSON array decoded to Array node; elements readable by index | Serialization |
| `SndWorld_ResolveTemplate_MutationDoesNotPolluteTemplateCache` | Mutating the copy returned by ResolveTemplate does not pollute the template cache; resolving again returns original content | Serialization |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `SndMappings_ResolveTemplate_BeforeLoadTemplates_Throws` | ResolveTemplate before LoadTemplates has been called | InvalidOperationException |
| `SndMappings_ResolveTemplate_AfterLoadTemplatesWithEmptyMap_Throws` | ResolveTemplate after loading empty template map | InvalidOperationException (contains "empty") |
| `SndMappings_ResolveTemplate_InvalidJson_Throws` | Template points to a JSON file with invalid syntax | Throws exception |

### Boundary Paths

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `TypedDataJson_DataPropertyBeforeType_DeserializesCorrectly` | JSON has data field before type field | Still correctly deserialized as TypedData<int> |

## SchedulingAndTypeMappingTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `TypeStringMapping_HasDefaultTypes_AndSupportsCustomRegistration` | Default types (Int32, ArrayString) are obtainable; after registering Guid, bidirectional lookup works | Serialization |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| None | — | Tests for this capability define no helper strategies; pure mapping/serialization behavior tests |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Name stability of generic types (e.g., `List<int>` vs `List<string>`) | Identifier strategy for generic types | Serialization |

---

[↑ Back to Origo.Core.Tests](README.en.md)
