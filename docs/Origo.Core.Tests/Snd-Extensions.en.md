<!-- docsync-pair: Origo.Core.Tests/Snd-Extensions -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# SND Extensions Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd](../Origo.Core/Snd/README.en.md)

## Behavior Under Test Overview

Validates extension method behavior on `ISndEntity`: lazy strategy mounting and idempotent guard (`EnsureStrategy`), replaceable strategy mounting (`EnsureReplaceableStrategy`), cross-numeric-type compatible reading (`TryGetNumeric`/`GetNumeric`), and generic ActiveStrategy invocation (`InvokeStrategy<TInput, TOutput>`).

## Test File List

| File | Verification Focus |
|------|-------------------|
| `EnsureStrategyTests.cs` | EnsureStrategy first-time mount, idempotent skip, empty value override |
| `EntityStrategyExtensionsTests.cs` | EnsureReplaceableStrategy default/custom/empty override/idempotent/parameter validation |
| `TryGetNumericExtensionsTests.cs` | TryGetNumeric cross-type reads (int/float/long/double), non-numeric returns false, GetNumeric fallback |
| `ActiveStrategyExtensionsTests.cs` | InvokeStrategy generic overloads: with/without input serialization round-trip, null returns default |
| `EntityExtensionsTests.cs` | IsSameEntityAs entity identity comparison: same reference/wrapper, name+session dual check, unbound degenerate comparison, null argument validation |

## EnsureStrategyTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `EnsureStrategy_DataKeyMissing_SetsDataAndReturnsTrue` | When the data key does not exist, sets the dataKey and returns true | Snd README: ActiveStrategyExtensions |
| `EnsureStrategy_DataKeyExistsWithValue_ReturnsFalse` | When the data key already has a non-empty value, skips, returns false, and the value is unchanged | Snd README: ActiveStrategyExtensions |
| `EnsureStrategy_DataKeyExistsButEmpty_StillSetsAndReturnsTrue` | When the data key exists but the value is an empty string, still overwrites and returns true | Snd README: ActiveStrategyExtensions |

## EntityStrategyExtensionsTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `EnsureReplaceableStrategy_NoConfig_UsesDefault` | No configuration uses defaultStrategyIndex, returns true | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_CalledAgain_ReturnsFalse` | Second call returns false (idempotent) | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_ConfiguredOverride_UsesOverride` | When custom strategy index is configured, preserves it and returns false | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_EmptyOverride_UsesDefault` | When configured value is empty string, overwrites with defaultStrategyIndex, returns true | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_DifferentDefault_CalledAgain_ReturnsFalse` | First call mounted with A as default; second call with B as default still returns false and value stays A | Snd README: EnsureReplaceableStrategy |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `EnsureReplaceableStrategy_NullEntity_Throws` | entity is null | ArgumentNullException |
| `EnsureReplaceableStrategy_NullImplKey_Throws` | implKey is null | ArgumentNullException |
| `EnsureReplaceableStrategy_NullDefault_Throws` | defaultStrategyIndex is null | ArgumentNullException |

## TryGetNumericExtensionsTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `TryGetNumeric_FloatStored_ReturnsFloat` | Stored float is read back via TryGetNumeric<float> | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_IntStored_ReturnsFloat` | Stored int is read back via TryGetNumeric<float> as cross-type (42→42f) | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_LongStored_ReturnsFloat` | Stored long is read back via TryGetNumeric<float> as cross-type (123L→123f) | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_IntegerTypesStored_ReturnsFloat` | All seven integer types (byte/sbyte/short/ushort/char/uint/ulong) read back cross-type | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_BoolStored_ReturnsFalse` | Stored bool returns false | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_DoubleStored_ReturnsFloat` | Stored double is read back via TryGetNumeric<float> as cross-type (2.5d→2.5f) | Snd README: TryGetNumericExtensions |
| `GetNumeric_FloatStored_ReturnsValue` | GetNumeric directly reads a stored float value | Snd README: TryGetNumericExtensions |
| `GetNumeric_Missing_ReturnsFallback` | GetNumeric returns the specified fallback value for a missing key | Snd README: TryGetNumericExtensions |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `TryGetNumeric_StringStored_ReturnsFalse` | TryGetNumeric on a stored string returns false | Returns false, out value is 0f |
| `TryGetNumeric_MissingKey_ReturnsFalse` | Non-existent key returns false | Returns false, out value is 0f |

## ActiveStrategyExtensionsTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `InvokeStrategy_GenericWithInput_SerializesAndDeserializes` | InvokeStrategy<TInput,TOutput> serializes input, invokes strategy, deserializes result as strong type | Snd README: ActiveStrategyExtensions |
| `InvokeStrategy_GenericNoInput_CallsWithoutInput` | InvokeStrategy<TOutput> no-input overload still correctly invokes the strategy | Snd README: ActiveStrategyExtensions |
| `InvokeStrategy_NullResult_ReturnsDefault` | When strategy Invoke returns null, the generic method returns default(TOutput) | Snd README: ActiveStrategyExtensions |

## EntityExtensionsTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `IsSameEntityAs_SameReference_ReturnsTrue` | Same object reference comparison returns true | snd-entity-model: IsSameEntityAs |
| `IsSameEntityAs_DifferentWrappersSameEntity_ReturnsTrue` | Two distinct wrapper instances of the same entity (same name, same session) compare as equal | snd-entity-model: IsSameEntityAs |
| `IsSameEntityAs_SameNameDifferentSession_ReturnsFalse` | Same name but different owning session returns false | snd-entity-model: IsSameEntityAs |
| `IsSameEntityAs_DifferentNamesSameSession_ReturnsFalse` | Same session but different names returns false | snd-entity-model: IsSameEntityAs |
| `IsSameEntityAs_SameNameBothUnbound_ReturnsTrue` | When neither side is bound to a session, comparison degenerates to name equality; same name passes | snd-entity-model: IsSameEntityAs |
| `IsSameEntityAs_OneBoundOneUnbound_ReturnsFalse` | One side bound to a session and the other unbound returns false | snd-entity-model: IsSameEntityAs |
| `IsSameEntityAs_RealUnboundEntities_SameName_ReturnsTrue` | Real unbound entities (OwningSession access throws) degenerate to name equality without crashing | snd-entity-model: IsSameEntityAs |
| `IsSameEntityAs_RealUnboundEntities_DifferentName_ReturnsFalse` | Real unbound entities with different names return false | snd-entity-model: IsSameEntityAs |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `IsSameEntityAs_NullArgument_Throws` | other parameter is null | ArgumentNullException |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| `StubActiveStrategyEntity` | ActiveStrategyExtensionsTests.cs | ISndEntity stub implementation, injecting InvokeStrategy behavior via Func<object?, object?>, other members throw NotImplementedException |
| `TestNumericEntity` | TryGetNumericExtensionsTests.cs | ISndDataAccess test implementation with internal Dictionary<string, TypedData> storage; TryGetData converts via TypedDataObjectConverter |
| `TestResult` | ActiveStrategyExtensionsTests.cs | Simple POCO with a Result property, used as the return type for generic InvokeStrategy deserialization |
| `StubEntity` | EntityExtensionsTests.cs | ISndEntity stub with configurable OwningSession (init), used for IsSameEntityAs name+session dual check |
| `StubSession` | EntityExtensionsTests.cs | ISessionRun stub with LevelId fixed to "test"; other members throw NotImplementedException |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Actual strategy mount integration of EnsureReplaceableStrategy with AddStrategy | Current tests use StubSndEntity (does not store actual strategies); the linkage between EnsureReplaceableStrategy result and actual strategy Add is not verified | Snd README: EnsureReplaceableStrategy |
| TryGetNumeric compatibility with decimal/byte/short and other numeric types | Only int/long/float/double are tested; other CLR numeric types not covered | Snd README: TryGetNumericExtensions |
| InvokeStrategy generic serialization round-trip for complex nested types | Only simple anonymous types {Sx, Sz} → TestResult are tested; nested objects/arrays/enums not tested | Snd README: ActiveStrategyExtensions |
| Integration tests for EnsureStrategy/EnsureReplaceableStrategy in a real runtime environment (with strategy pool registration) | Current tests only manipulate the data layer of DummySndEntity/StubSndEntity; actual strategy Add and execution is not verified | Snd README: ActiveStrategyExtensions |

---

[↑ Back to Origo.Core.Tests](README.en.md)
