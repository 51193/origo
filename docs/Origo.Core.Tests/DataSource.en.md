<!-- docsync-pair: Origo.Core.Tests/DataSource -->
<!-- docsync-revision: 12 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Data Source Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/DataSource](../Origo.Core/DataSource/README.en.md)
> [↔ Behavior under test: usage/architecture-overview](../usage/architecture-overview.en.md)

## Behavior Overview

Validates the DataSourceNode tree model and its encode/decode, conversion, and hashing capabilities: node factory (Map/Array/Text/Number/Bool/Null), strongly-typed value accessors (AsString/AsChar plus the generic As<T>: As<int>/As<long>/As<float>/As<double>/As<byte>/As<sbyte>/As<short>/As<ushort>/As<uint>/As<ulong>/As<decimal>), object/array access (indexer, TryGetValue, ContainsKey, Keys, Count, Elements), Builder chained Add, lazy expansion (expander not called before access, expand only once, expansion failure keeps Lazy with retry), JSON codec round-trip (complex nested tree/top-level array/empty object/empty array, nested objects lazy, primitives not lazy), Map codec (comments/empty lines skipped, colons in values, skip null values, empty value, line without colon throws), `DataSourceConverterRegistry` (register/get, generic and runtime-typed read/write, unregistered type throws, null write, type hierarchy fallback), complete round-trip for 14 primitive types + 14 array types + domain types (TypedData/SndMetaData/BlackboardData/StateMachineContainerPayload/StringDictionary), `TypeStringMapping` type name registration, `IDisposable` recursive disposal with deep-tree stack overflow prevention, `ComputeSha256Hash` canonical hashing, and strict/lenient parse behavior of `KeyValueFileParser`.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `DataSourceFactoryTests.cs` | Factory methods and basic accessors: node creation, value/object/array access, Builder chained Add, Lazy expansion |
| `DataSourceCodecTests.cs` | JSON codec round-trip (complex nested tree, top-level array, lazy expansion) and Map codec edge cases (comments/empty lines skipped, colons in values, line without colon throws) |
| `DataSourceConverterTests.cs` | ConverterRegistry registration and read/write, complete round-trip for 14 primitive types + 14 array types + 5 domain types (TypedData/SndMetaData/BlackboardData/StateMachineContainerPayload/StringDictionary), TypeStringMapping extension |
| `DataSourceTests.cs` | Remaining items: IDisposable recursive disposal with deep-tree stack overflow prevention, new accessor methods, TypeStringMapping new type registration, DataSourceConverterRegistry type hierarchy fallback, ReadOnlyDictionary round-trip |
| `DataSourceNodeSha256Tests.cs` | DataSourceNode `ComputeSha256Hash` canonical hash computation |
| `KeyValueFileParserTests.cs` | `KeyValueFileParser.Parse` key-value file parsing (strict/lenient mode, comments, duplicate keys, null/empty content) |
| `CorruptObserverIndicesStrictReadTests.cs` (in Save/) | Verifies: when `observer_indices` contains a non-object element, **multiple target keys**, or an **empty object** (corrupted save), `LoadFromPayload` throws `InvalidOperationException` (containing "observer_indices") instead of silently dropping bindings |

## DataSourceTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `CreateObject_ReturnsObjectNode` | CreateObject returns Map node | DataSource |
| `CreateArray_ReturnsArrayNode` | CreateArray returns Array node, Count=0 | DataSource |
| `CreateString_ReturnsStringNode` | CreateString("hello") → Text node, AsString()="hello" | DataSource |
| `CreateNumber_IntOverloads_ReturnNumberNode` | int/long/float/double/string overloads all return Number nodes | DataSource |
| `CreateBoolean_ReturnsBooleanNode` | true/false → Bool node, AsBool() correct | DataSource |
| `CreateNull_ReturnsNullNode` | CreateNull returns Null node, IsNull=true | DataSource |
| `AsString_OnStringNode_ReturnsValue` | String node AsString returns original value | DataSource |
| `AsString_OnNumberNode_ReturnsStringRepresentation` | Number node AsString returns string representation | DataSource |
| `AsInt_ParsesCorrectly` | Number node `As<int>()` parses to int | DataSource |
| `AsLong_ParsesCorrectly` | Number node `As<long>()` parses to long | DataSource |
| `AsFloat_ParsesCorrectly` | Number node `As<float>()` parses to float | DataSource |
| `AsDouble_ParsesCorrectly` | Number node `As<double>()` parses to double | DataSource |
| `ObjectNode_IndexerByKey_ReturnsChild` | obj["x"] returns child node | DataSource |
| `ObjectNode_TryGetValue_ReturnsTrueForExistingKey` | TryGetValue for existing key returns true and outputs child node | DataSource |
| `ObjectNode_ContainsKey_WorksCorrectly` | ContainsKey returns true/false for existing/missing keys | DataSource |
| `ObjectNode_Keys_ReturnsInsertionOrder` | Keys returned in insertion order | DataSource |
| `ArrayNode_IndexerByIndex_ReturnsChild` | arr[0]/arr[1] indexer returns child nodes | DataSource |
| `ArrayNode_Count_ReflectsElements` | Count reflects element count | DataSource |
| `ArrayNode_Elements_EnumeratesAll` | Elements enumerates all elements | DataSource |
| `ObjectNode_Add_ReturnsSameNodeForChaining` | Object Add returns self for chaining | DataSource |
| `ArrayNode_Add_ReturnsSameNodeForChaining` | Array Add returns self for chaining | DataSource |
| `CreateLazy_DoesNotCallExpanderUntilAccessed` | Lazy node does not call expander until accessed | DataSource |
| `CreateLazy_ExpandsOnlyOnce` | Lazy node expands only once across multiple accesses | DataSource |
| `JsonCodec_RoundTrip_ComplexTree` | Complex tree with string/number/boolean/null/array/nested object JSON round-trip | DataSource |
| `JsonCodec_RoundTrip_TopLevelArray` | Top-level array JSON round-trip, element order preserved | DataSource |
| `JsonCodec_Decode_NestedObjectsAreLazy` | Decoded nested objects are lazy, accessing inner keys evaluates correctly | DataSource |
| `JsonCodec_Decode_PrimitivesAreNotLazy` | Decoded primitives are the corresponding Kind directly, not lazy | DataSource |
| `MapCodec_RoundTrip_FlatObject` | Flat object Map codec round-trip | DataSource |
| `MapCodec_Decode_IgnoresCommentsAndEmptyLines` | Map decode skips `#` comments and empty lines | DataSource |
| `MapCodec_Decode_HandlesColonsInValues` | Map decode preserves colons in values (e.g. URLs) | DataSource |
| `MapCodec_Encode_SkipsNullValues` | Map encode skips null-valued keys | DataSource |
| `MapCodec_Encode_RejectsMultilineValue` | Value contains newline characters | InvalidOperationException (encode would produce a file that its own strict decode cannot read back, so it must be rejected) |
| `MapCodec_DuplicateKey_WarningIsObservable` | Duplicate keys on decode | Warning logged (observable through the injected logger) |
| `Registry_RegisterAndGet_RoundTrips` | Register→Get→Write→Read round-trip | DataSource |
| `Registry_ReadWrite_ByGenericType` | Write/Read round-trip by generic type | DataSource |
| `Registry_ReadWrite_ByRuntimeType` | Write/Read round-trip by runtime Type | DataSource |
| `PrimitiveConverters_RoundTrip_AllTypes` | string/int/long/float/double/bool all types round-trip | DataSource |
| `TypedDataConverter_RoundTrip_IntValue` | TypedData(int) round-trip preserves DataType=int | DataSource |
| `TypedDataConverter_RoundTrip_StringValue` | TypedData(string) round-trip preserves DataType=string | DataSource |
| `TypedDataConverter_RoundTrip_NullData` | TypedData(string, null) round-trip preserves type with null data | DataSource |
| `TypedDataConverter_NullDataForNullReferenceType_StillReturnsNullString` | Reference-kind (string) null data reads back as found-but-null, does not throw | DataSource |
| `SndMetaDataConverter_RoundTrip_FullStructure` | SndMetaData full fields (Node/Strategy/Data substructures) round-trip | DataSource |
| `SndMetaDataConverter_RoundTrip_NullSubStructures` | Null substructures round-trip, DataMetaData falls back to empty dict | DataSource |
| `BlackboardDataConverter_RoundTrip_MixedEntries` | Blackboard dict (int/string/bool mixed) round-trip | DataSource |
| `StateMachineContainerPayloadConverter_RoundTrip` | State machine container Payload (multiple machines/stacks) round-trip | DataSource |
| `StringDictionaryConverter_RoundTrip` | IReadOnlyDictionary<string,string> round-trip | DataSource |
| `Read_String_FromNullNode_Throws` | Reading a Null node as string | InvalidOperationException (silent null drift to empty string must be rejected; callers check IsNull/TryGetValue first) |
| `RuntimeRead_String_FromNullNode_Throws` | Runtime-typed overload reads Null node as string | InvalidOperationException |
| `ObjectNode_Add_OnScalarNode_Throws` | Adding a child by key onto a scalar node | InvalidOperationException (children would be silently dropped by all codecs) |
| `ArrayNode_Add_OnScalarNode_Throws` | Appending a child onto a scalar node | InvalidOperationException |
| `FileMetaAccess_DirectoryExists_RejectsNullOrWhitespacePath` | DirectoryExists with empty/whitespace path | ArgumentException (aligned with FileExists) |
| `CreateDefaultRegistry_RegistersAllExpectedTypes` | Default registry registers all primitive/array/domain type converters | DataSource |
| `SndMetaDataConverter_JsonIntegration_FullRoundTrip` | SndMetaData → node → JSON → node → SndMetaData | DataSource |
| `ByteConverter_RoundTrip` | byte 0/255/128 round-trip | DataSource |
| `SByteConverter_RoundTrip` | sbyte -128/0/127 round-trip | DataSource |
| `Int16Converter_RoundTrip` | short -32768/0/32767 round-trip | DataSource |
| `UInt16Converter_RoundTrip` | ushort 0/65535 round-trip | DataSource |
| `UInt32Converter_RoundTrip` | uint 0/4294967295 round-trip | DataSource |
| `UInt64Converter_RoundTrip` | ulong 0/18446744073709551615 round-trip | DataSource |
| `DecimalConverter_RoundTrip` | decimal 0/max/negative round-trip | DataSource |
| `CharConverter_RoundTrip` | char 'A'/space/Chinese round-trip | DataSource |
| `ByteArrayConverter_RoundTrip` | byte[] round-trip | DataSource |
| `SByteArrayConverter_RoundTrip` | sbyte[] round-trip | DataSource |
| `Int16ArrayConverter_RoundTrip` | short[] round-trip | DataSource |
| `UInt16ArrayConverter_RoundTrip` | ushort[] round-trip | DataSource |
| `Int32ArrayConverter_RoundTrip` | int[] round-trip | DataSource |
| `UInt32ArrayConverter_RoundTrip` | uint[] round-trip | DataSource |
| `Int64ArrayConverter_RoundTrip` | long[] round-trip | DataSource |
| `UInt64ArrayConverter_RoundTrip` | ulong[] round-trip | DataSource |
| `SingleArrayConverter_RoundTrip` | float[] round-trip (with precision tolerance) | DataSource |
| `DoubleArrayConverter_RoundTrip` | double[] round-trip (with precision tolerance) | DataSource |
| `DecimalArrayConverter_RoundTrip` | decimal[] round-trip | DataSource |
| `BooleanArrayConverter_RoundTrip` | bool[] round-trip | DataSource |
| `CharArrayConverter_RoundTrip` | char[] (including Chinese) round-trip | DataSource |
| `StringArrayConverter_RoundTrip` | string[] (including empty string) round-trip | DataSource |
| `IntArrayConverter_JsonIntegration_RoundTrip` | int[] → node → JSON → node → int[] | DataSource |
| `ByteArrayConverter_JsonIntegration_RoundTrip` | byte[] → node → JSON → node → byte[] | DataSource |
| `TypedDataConverter_RoundTrip_ByteValue` | TypedData(byte) round-trip preserves DataType=byte | DataSource |
| `TypedDataConverter_RoundTrip_DecimalValue` | TypedData(decimal) round-trip preserves DataType=decimal | DataSource |
| `TypedDataConverter_RoundTrip_CharValue` | TypedData(char) round-trip preserves DataType=char | DataSource |
| `TypedDataConverter_RoundTrip_IntArrayValue` | TypedData(int[]) round-trip preserves DataType=int[] | DataSource |
| `TypedDataConverter_RoundTrip_ByteArrayValue` | TypedData(byte[]) round-trip preserves DataType=byte[] | DataSource |
| `AsByte_ParsesCorrectly` | Number node `As<byte>()` parses 0/255 | DataSource |
| `AsSByte_ParsesCorrectly` | Number node `As<sbyte>()` parses -128/127 | DataSource |
| `AsShort_ParsesCorrectly` | Number node `As<short>()` parses -32768/32767 | DataSource |
| `AsUShort_ParsesCorrectly` | Number node `As<ushort>()` parses 0/65535 | DataSource |
| `AsUInt_ParsesCorrectly` | Number node `As<uint>()` parses 0/4294967295 | DataSource |
| `AsULong_ParsesCorrectly` | Number node `As<ulong>()` parses 0/18446744073709551615 | DataSource |
| `AsDecimal_ParsesCorrectly` | Number node `As<decimal>()` parses 3.14159/-99.99 | DataSource |
| `AsChar_ParsesCorrectly` | String node AsChar parses 'A'/Chinese | DataSource |
| `TypeStringMapping_RegistersAllNewTypes` | TypeStringMapping registers all primitive and array type names | DataSource |
| `TypeStringMapping_RegistersReadOnlyDictionary` | TypeStringMapping registers ReadOnlyDictionary and interface type names | DataSource |
| `ConverterRegistry_TypeHierarchyFallback_FindsInterfaceConverterForConcreteType` | ReadOnlyDictionary concrete type falls back to IReadOnlyDictionary interface converter | DataSource |
| `ConverterRegistry_TypeHierarchyFallback_ExactTypeMatchStillWorks` | Exact type match still takes priority | DataSource |
| `ReadOnlyDictionary_BlackboardRoundTrip_SurvivesSerialization` | ReadOnlyDictionary values restored after blackboard serialize/deserialize | DataSource |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `AsInt_OnNonNumericString_Throws` | CreateString("hello") → `As<int>()` | FormatException |
| `CreateString_Null_Throws` | CreateString(null) | ArgumentNullException (Null nodes are CreateNull's responsibility; null must not drift into an empty Text node) |
| `CreateNumber_NullString_Throws` | CreateNumber(string) with null | ArgumentNullException |
| `Add_NullChild_Throws` | Map/Array Add with a null child | ArgumentNullException (deferring the error to encode time becomes an NRE or empty data) |
| `ObjectNode_IndexerByKey_ThrowsOnMissingKey` | obj["missing"] | KeyNotFoundException |
| `Registry_Get_ThrowsForUnregisteredType` | Get<DateTime>() unregistered | InvalidOperationException |
| `Registry_RuntimeRead_ThrowsForUnregisteredType` | Read(typeof(DateTime), …) unregistered | InvalidOperationException |
| `TypedDataConverter_NullDataForRegisteredValueType_Throws` | data field null for registered value kind (int) | InvalidOperationException (contains "value type") |
| `StateMachineContainerPayloadConverter_EntryMissingKey_Throws` | Machine entry missing key field | InvalidOperationException (contains "key") |
| `StateMachineContainerPayloadConverter_EntryMissingPushIndex_Throws` | Machine entry missing pushIndex field | InvalidOperationException (contains "pushIndex") |
| `StateMachineContainerPayloadConverter_EntryMissingPopIndex_Throws` | Machine entry missing popIndex field | InvalidOperationException (contains "popIndex") |
| `StateMachineContainerPayloadConverter_EntryNullOrNonStringKey_Throws` | Machine entry key is null | InvalidOperationException |
| `StateMachineContainerPayloadConverter_EntryNullOrNonStringKey_Throws` | Machine entry key is null | InvalidOperationException |
| `StateMachineContainerPayloadConverter_StackNotArray_Throws` | Machine entry stack is an object instead of an array | InvalidOperationException (contains "array"; must not silently become an empty stack) |
| `StrategyMetaDataConverter_LifecycleIndicesNotArray_Throws` | lifecycle_indices is an object instead of an array | InvalidOperationException (contains "array") |
| `StrategyMetaDataConverter_ObserverIndicesNotArray_Throws` | observer_indices is an object instead of an array | InvalidOperationException (contains "array") |
| `StrategyMetaDataConverter_BlankObserverTarget_Throws` | observer_indices entry target is empty | InvalidOperationException (contains "target"; binding must not be silently dropped) |
| `NodeMetaDataConverter_PairsNotMap_Throws` | node.pairs is an array instead of an object | InvalidOperationException (contains "object") |
| `DataMetaDataConverter_PairsNotMap_Throws` | data.pairs is an array instead of an object | InvalidOperationException (contains "object") |
| `StringDictionaryConverter_Read_NonMap_Throws` | String dictionary root node is an array | InvalidOperationException (contains "object") |
| `BlackboardDataConverter_Read_NonMap_Throws` | Blackboard dictionary root node is an array | InvalidOperationException (contains "object") |
| `SndMetaDataConverter_Read_NonMap_Throws` | SndMetaData root node is an array | InvalidOperationException (contains "object") |
| `SndMetaDataListConverter_Read_NonArray_Throws` | SndMetaData list root node is an object | InvalidOperationException (contains "array") |
| `MapCodec_Encode_ThrowsForNonObjectNode` | Map encode on Array node | InvalidOperationException |
| `MapCodec_Encode_RejectsColonInKey` | Key contains a colon | InvalidOperationException (the first colon is the decode separator and would silently split into another key/value pair) |
| `MapCodec_Encode_RejectsCommentKey` | Key starts with `#` | InvalidOperationException (the strict decoder would treat the whole line as a comment and drop it) |
| `MapCodec_Encode_RejectsUntrimmedKeyOrValue` | Key or value has leading/trailing whitespace | InvalidOperationException (the strict decoder trims both fields) |
| `MapCodec_Encode_RejectsNonTextChild` | Number/Bool child | InvalidOperationException (`.map` only carries strings; decoding would silently drift the type) |
| `MapCodec_Encode_EmptyKey_Throws` | Empty key | InvalidOperationException |
| `ArrayConverter_Read_NullNode_Throws` | Array converter reads a Null root node | InvalidOperationException (must not silently become an empty array) |
| `ArrayConverter_Read_ScalarNode_Throws` | Array converter reads a scalar root node | InvalidOperationException |
| `ArrayConverter_Read_ObjectNode_Throws` | Array converter reads an object root node | InvalidOperationException |
| `DataSourceNode_Keys_OnNonMap_Throws` | Keys accessed on a non-Map node | InvalidOperationException (wrong shape must not silently become an empty key set) |
| `DataSourceNode_CountAndElements_OnNonArray_Throw` | Count/Elements accessed on a non-Array node | InvalidOperationException |
| `LazyNode_WhenExpanderThrows_NodeStaysLazy_AndCanRetrySuccessfully` | First expansion throws InvalidOperationException | After first access throws, node stays Lazy, second access succeeds (callCount=2) |
| `LazyNode_WhenExpanderThrows_NodeCanStillBeDisposed` | Expander always throws InvalidOperationException | Can still Dispose after failed expansion, subsequent access throws ObjectDisposedException |
| `MapCodec_Decode_LineWithoutColon_Throws` | Map text with line missing colon | FormatException |
| `Dispose_PreventsSubsequentAccess` | Access Kind/AsString/IsNull after Dispose | ObjectDisposedException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `AsString_OnNullNode_ReturnsEmpty` | Null node AsString | Returns string.Empty |
| `ObjectNode_TryGetValue_ReturnsFalseForMissingKey` | TryGetValue for missing key | Returns false |
| `Registry_RuntimeWrite_NullReturnsNullNode` | Write(typeof(int), null) | Returns Null node |
| `Registry_GenericWrite_NullReturnsNullNodeLikeRuntimeOverload` | Write<string>(null) consistent with runtime-typed overload | Both return Null node with identical hash |
| `StateMachineContainerPayloadConverter_EntryStackMissing_DefaultsToEmpty` | Machine entry missing stack field | Treated as empty stack, reads normally (only identity fields mandatory) |
| `JsonCodec_RoundTrip_EmptyObject` | Empty object JSON round-trip | Map node, no keys |
| `JsonCodec_RoundTrip_EmptyArray` | Empty array JSON round-trip | Array node, Count=0 |
| `MapCodec_Decode_EmptyValueAfterColon_ReturnsEmptyString` | `emptyval:` empty value line | Key exists, value is empty string |
| `MapCodec_Decode_OnlyCommentsAndEmptyLines_ReturnsEmptyObject` | Only comments and empty lines | Map node, no keys |
| `ByteArrayConverter_RoundTrip_Empty` | Empty byte[] round-trip | Returns empty array |
| `StringArrayConverter_RoundTrip_Empty` | Empty string[] round-trip | Returns empty array |
| `Dispose_RecursivelyDisposesChildren` | Parent object Dispose | Recursively disposes children, accessing children throws ObjectDisposedException |
| `Dispose_RecursivelyDisposesArrayChildren` | Parent array Dispose | Recursively disposes array children |
| `Dispose_CanBeCalledMultipleTimes` | Multiple Dispose | Idempotent, no throw, subsequent access throws ObjectDisposedException |
| `Dispose_LazyNodeReleasesExpander` | Dispose unexpanded Lazy node | expander not called, subsequent access throws ObjectDisposedException |
| `UsingStatement_DisposesAfterScope` | using scope exit | Access outside scope throws ObjectDisposedException |
| `Dispose_DeeplyNestedTree_DoesNotStackOverflow` | 2000-level nested tree Dispose | No stack overflow |
| `ComputeSha256Hash_DeeplyNestedTree_DoesNotStackOverflow` | 2000-level nested tree ComputeSha256Hash | No stack overflow, returns non-empty hash |

## DataSourceNodeSha256Tests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ScalarString_HashIsDeterministic` | Same string node produces consistent hash | DataSource |
| `ScalarNumber_HashIsDeterministic` | Same number node produces consistent hash | DataSource |
| `ScalarBoolean_HashIsDeterministic` | Same boolean node produces consistent hash | DataSource |
| `NullNode_HashIsDeterministic` | Null node produces consistent hash | DataSource |
| `ObjectNode_HashDependsOnKeys` | Different key names produce different hashes | DataSource |
| `ObjectNode_HashEscapesSpecialCharactersInKeys` | Keys with special chars (`=`/`,`/`{}`/`[]`/quotes/backslash) hash deterministically, distinct from same char in value position | DataSource |
| `ObjectNode_HashIndependentOfInsertionOrder` | Object hash is independent of key insertion order | DataSource |
| `ArrayNode_HashOrderDependent` | Array hash depends on element order | DataSource |
| `DeepNested_HashWorks` | Deep nesting (object containing array containing string) computes without throwing and is non-empty | DataSource |
| `DifferentValues_DifferentHashes` | Different string values produce different hashes | DataSource |
| `EmptyObjectVsEmptyArray_DifferentHashes` | Empty object and empty array produce different hashes | DataSource |
| `StringWithSpecialChars_HashWorks` | String with quotes/backslashes hash is deterministic and reproducible | DataSource |
| `StringWithSpecialChars_DoesNotCollideWithUnescapedEquivalent` | Different values before/after escaping do not collide | DataSource |
| `BooleanTrueAndFalse_HaveDifferentHashes` | true and false have different hashes | DataSource |
| `NumberIntegerVsFloatWithSameValue_HaveDifferentHashes` | Integer 1 and float 1.0 canonicalized to same hash (value-equivalent) | DataSource |
| `HashIsHexString` | Hash is 64-character lowercase hex string | DataSource |
| `SameComplexTree_DifferentInstances_SameHash` | Different instances of same structure produce same hash | DataSource |
| `DecodedDeeplyNestedTree_AllLevelsExpanded_DepthThreeHashDiffers` | Decoded tree expanded to depth 3 with changed leaf value produces different hash (verifies lazy subtrees are included in hash) | DataSource |
| `DecodedNestedTree_ArrayDeepChange_ProducesDifferentHash` | Deep object value change inside decoded tree array produces different hash | DataSource |
| `DecodedNestedTree_DeepKeyChange_ProducesDifferentHash` | Deep key change in decoded tree produces different hash | DataSource |
| `DecodedNestedTree_DeepValueChange_ProducesDifferentHash` | Deep leaf value change in decoded tree produces different hash | DataSource |
| `DecodedNestedTree_SameContent_ProducesSameHash` | Decoded trees with same content produce same hash | DataSource |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `DisposedNode_ComputeSha256Hash_Throws` | ComputeSha256Hash on disposed node | ObjectDisposedException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `EmptyString_HashWorks` | Empty string node | Hash deterministic and reproducible |

## KeyValueFileParserTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `KeyValueFileParser_Parse_BasicKeyValue` | Basic `key: value` multi-line parsed to dict | DataSource |
| `KeyValueFileParser_Parse_SkipsCommentsAndBlanks` | Skips `#` comments and empty lines, retains valid keys | DataSource |
| `KeyValueFileParser_Parse_ValueContainsColon_PreservesFullValue` | Colons in values (e.g. URLs) fully preserved | DataSource |

## CorruptObserverIndicesStrictReadTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `LoadFromPayload_WhenObserverIndicesEntryIsNotAnObject_Throws` | `observer_indices` array contains a non-object element (corrupted save; the write side only ever produces object elements) | `LoadFromPayload` throws `InvalidOperationException` (message contains "observer_indices"), does not silently drop corrupted bindings |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `KeyValueFileParser_Parse_StrictMode_ThrowsOnInvalidLine` | Strict mode encounters line without colon | FormatException |
| `KeyValueFileParser_Parse_StrictMode_ThrowsOnEmptyKeyOrValue` | Strict mode encounters empty key (`: value`) | FormatException |
| `KeyValueFileParser_Parse_ThrowsOnNullLogger` | logger parameter is null | ArgumentNullException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `KeyValueFileParser_Parse_EmptyContent_ReturnsEmpty` | Empty string content | Returns empty dict |
| `KeyValueFileParser_Parse_NullContent_ReturnsEmpty` | null content | Returns empty dict |
| `KeyValueFileParser_Parse_LenientMode_LogsWarningOnInvalidLine` | Lenient mode encounters line without colon | Does not throw, returns empty and logs warning |
| `KeyValueFileParser_Parse_LenientMode_LogsWarningOnEmptyKey` | Lenient mode encounters empty key | Does not throw, returns empty and logs warning |
| `KeyValueFileParser_Parse_DuplicateKey_LogsWarning` | Duplicate key | Later value overwrites earlier, logs warning |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| None | — | The three test files for this capability do not define any `XxxStrategy` helper classes. `DataSourceTests` constructs codecs and converter registries via shared `TestFactory` (`CreateJsonCodec`/`CreateMapCodec`/`CreateRegistry`); `KeyValueFileParserTests` captures warnings via shared `TestLogger`. Both are test-project-level shared infrastructure, not private helper strategies for this capability. |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| `ComputeSha256Hash` behavior for Lazy nodes | Not verified whether hashing an unexpanded Lazy node triggers expansion, and result stability | DataSource |
| Nested structures in Map codec | Only covers flat objects; behavior of nested objects/arrays in Map format not covered | DataSource |
| `KeyValueFileParser` strict mode duplicate key behavior | Only verifies duplicate key overwrite + warning in lenient mode; strict mode branch not covered | DataSource |
| Thread safety of concurrent converter/node read/write | Multi-threaded scenarios not covered | DataSource |
| Performance characteristics of DataSourceNode at extreme nesting (far beyond 2000 levels) | Extreme nesting depth performance not quantified | DataSource |

---

[↑ Back to Origo.Core.Tests](README.en.md)
