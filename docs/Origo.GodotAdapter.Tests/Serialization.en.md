<!-- docsync-pair: Origo.GodotAdapter.Tests/Serialization -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Serialization Tests (Adapter Layer)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Serialization](../Origo.GodotAdapter/Serialization/README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.en.md)

## Behavior Under Test Overview

Verifies serialization round-trips for 14 Godot engine types: Vector2 / 3 / 4, Vector2I / 3I, Quaternion,
Color, Basis, Transform2D / 3D, Rect2 / 2I, Aabb, Plane. All types undergo full round-trip via
`DataSourceConverterRegistry.Write→Read`, and bidirectional type name mapping (`TypeStringMapping`) is verified.

Also verifies the TypedData multi-layer inlining system: 14 Godot types resolved at runtime via
`TypedDataTypeMap` Kind (Kind ∈ [128, 141]), `TypedData.FromObject` / `TryGetXxx` extension methods /
`AsXxx` / `TypedDataObjectConverter` bridge full round-trips, and no conflict with Core Kind range (< 128).

`GodotTypedDataPerformanceTests` is marked `[Trait("Category","Benchmark")]` (class-level),
run only by `scripts/benchmark.sh`; `test.sh` full test run excludes them via
`--filter "Category!=Benchmark"`, so its 6 cases are not counted in the regular coverage gate run.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `GodotDataSourceConvertersTests.cs` | 14 Godot type converter round-trips: write→read values match |
| `GodotJsonConverterRegistryTests.cs` | Type name mapping registration (all 14 types) + round-trip after converter registration |
| `GodotTypedDataLayeredTests.cs` | Multi-layer TypedData: Kind resolution, FromObject round-trip, TryGet / AsXxx extensions, DataType / Data, ObjectConverter fallback, cross-layer Kind isolation |
| `GodotTypedDataGeneratedCoverageTests.cs` | Generated accessor per-type coverage: AsXxx / TryGetXxx / KindMap / Converter round-trips, unregistered-Kind fallback and failure paths |
| `GodotTypedDataPerformanceTests.cs` | (Benchmark) Multi-layer dispatch performance: registered vs unregistered write / read throughput, ObjectConverter switch vs fallback, Factory path, entity frame simulation |

## GodotDataSourceConvertersTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Vector2Converter_RoundTrip` | Vector2(1.5, -2.5) round-trip matches | GodotAdapter Serialization |
| `Vector2IConverter_RoundTrip` | Vector2I(3, -4) round-trip matches | GodotAdapter Serialization |
| `Vector3IConverter_RoundTrip` | Vector3I(5, -6, 7) round-trip matches | GodotAdapter Serialization |
| `Vector4Converter_RoundTrip` | Vector4(1.1, 2.2, 3.3, 4.4) round-trip matches | GodotAdapter Serialization |
| `QuaternionConverter_RoundTrip` | Quaternion components match within ε | GodotAdapter Serialization |
| `BasisConverter_RoundTrip` | Basis (diagonal scale) round-trip matches | GodotAdapter Serialization |
| `BasisConverter_IdentityRoundTrip` | Basis.Identity round-trip matches | GodotAdapter Serialization |
| `Transform2DConverter_RoundTrip` | Transform2D (basis vectors + translation) round-trip matches | GodotAdapter Serialization |
| `ColorConverter_RoundTrip` | Color(0.1, 0.2, 0.3, 0.4) round-trip matches | GodotAdapter Serialization |
| `ColorConverter_OpaqueWhiteRoundTrip` | Color(1, 1, 1) round-trip matches | GodotAdapter Serialization |
| `Rect2Converter_RoundTrip` | Rect2 (pos + size) round-trip matches | GodotAdapter Serialization |
| `Rect2IConverter_RoundTrip` | Rect2I (pos + size) round-trip matches | GodotAdapter Serialization |
| `AabbConverter_RoundTrip` | Aabb (pos + size) round-trip matches | GodotAdapter Serialization |
| `AabbConverter_ZeroSizeRoundTrip` | Aabb(0, 0, 0, 0, 0, 0) round-trip matches | GodotAdapter Serialization |

## GodotJsonConverterRegistryTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `RegisterTypeMappings_RegistersAll14TypeNames` | After registration, all 14 types resolve to names (type→name) | GodotAdapter Serialization |
| `RegisterTypeMappings_AllTypesCanBeResolvedByName` | After registration, all 14 types resolve from names (name→type) | GodotAdapter Serialization |
| `RegisterDataSourceConverters_AllowsVectorRoundTrip` | After converter registration, Vector3 round-trips correctly | GodotAdapter Serialization |
| `RegisterDataSourceConverters_AllowsTransformAndPlaneConverters` | Transform3D and Plane round-trips correctly | GodotAdapter Serialization |
| `RegisterDataSourceConverters_Vector2IAnd3IRoundTrip` | Vector2I / Vector3I round-trip correctly | GodotAdapter Serialization |
| `RegisterDataSourceConverters_Vector4AndQuaternionRoundTrip` | Vector4 matches, Quaternion components match within ε | GodotAdapter Serialization |
| `RegisterDataSourceConverters_Rect2AndRect2IRoundTrip` | Rect2 / Rect2I round-trip correctly | GodotAdapter Serialization |
| `RegisterDataSourceConverters_AabbRoundTrip` | Aabb round-trips correctly | GodotAdapter Serialization |

## GodotTypedDataLayeredTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Godot_Vector2_Kind_Is_Resolved` | Vector2 resolves to Kind 128 | GodotAdapter Snd |
| `Godot_Vector3_Kind_Is_Resolved` | Vector3 resolves to Kind 130 | GodotAdapter Snd |
| `Godot_Color_Kind_Is_Resolved` | Color resolves to Kind 137 | GodotAdapter Snd |
| `Godot_Transform3D_Kind_Is_Resolved` | Transform3D resolves to Kind 136 | GodotAdapter Snd |
| `Godot_Plane_Kind_Is_Resolved` | Plane resolves to Kind 141 | GodotAdapter Snd |
| `Godot_Vector2_FromObject_RoundTrip` | Vector2 via TypedData(128) construction, DataType and ToObject restore match | GodotAdapter Snd |
| `Godot_Vector3_FromObject_RoundTrip` | Vector3 via TypedData(130) construction round-trip matches | GodotAdapter Snd |
| `Godot_Color_FromObject_RoundTrip` | Color via TypedData(137) construction round-trip matches | GodotAdapter Snd |
| `Godot_Vector2_Extension_TryGet` | `TryGetVector2` returns true and value matches | GodotAdapter Snd |
| `Godot_Vector3_Extension_TryGet` | `TryGetVector3` returns true and value matches | GodotAdapter Snd |
| `Godot_Color_Extension_TryGet` | `TryGetColor` returns true and value matches | GodotAdapter Snd |
| `All_GodotTypes_Registered` | All 14 Godot type Kinds fall within [128, 141] range | GodotAdapter Snd |
| `DataType_ForGodotType_ReturnsCorrectType` | DataType of Godot-type TypedData is correct | GodotAdapter Snd |
| `Data_ForGodotType_ReturnsUnboxedValue` | ToObject returns the unboxed original value | GodotAdapter Snd |
| `AsXxx_ForGodotType_Works` | `AsVector2()` returns correct value | GodotAdapter Snd |
| `TryGetAllGodotTypes_RoundTrip` | Vector2 / 2I / 3 / 3I, Color, Rect2 / 2I round-trip correctly via respective TryGet methods | GodotAdapter Snd |
| `GodotType_ObjectConverter_ToObject_UsesFallback` | `TypedDataObjectConverter.ToObject` returns Vector3 via fallback | GodotAdapter Snd |
| `GodotType_ObjectConverter_FromObject_UsesFallback` | `FromObject(130, v)` returns (0, refValue) via fallback | GodotAdapter Snd |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `Godot_Type_WrongKind_ReturnsFalse` | Reading with wrong TryGet (Vector2 using TryGetVector3 / TryGetColor) | Returns false |
| `Core_Int_DoesNotConflict_With_GodotKind` | int TypedData read with TryGetVector2 | TryGetVector2 false, TryGetInt32 true returns 42 |
| `GodotType_Null_PreservesDataType` | TypedData(130, 0, null) (null value) | DataType still Vector3, ToObject returns null |
| `GodotKind_NotRecognized_ByCoreOnlyUnregistered` | Godot type Kind ≥ 128 vs Core int Kind < 128 | Godot Kind non-zero and ≥ 128, int Kind=5 and < 128 |

## GodotTypedDataGeneratedCoverageTests Test Details

Parametrically exercises every generated accessor (`As*`/`TryGet*`), the kind map, and the object converter bridge for all 14 registered Godot types, executing every generated branch.

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `KindMap_ResolvesEveryRegisteredType` | All 14 Godot types resolve to their respective Kinds via `TypedDataTypeMap.GetKindForType` (128–141 one-to-one) | GodotAdapter Snd |
| `Converter_FromObject_RoundTrips` | `FromObject`→`TypedData`→`ToObject` full round-trip for all 14 types, DataType and value match | GodotAdapter Snd |
| `Converter_UnregisteredKind_FromObjectFallsBackToRef` | Unregistered Kind(250) via `FromObject` falls back to the ref slot: inlineBits=0, same instance referenced | GodotAdapter Snd |
| `Converter_ToObject_UnregisteredKind_FallsBackToRef` | Unregistered Kind(250) via `ToObject` falls back returning the original value; null value returns null | GodotAdapter Snd |
| `Accessors_AsAndTryGet_ReturnValue` | `As*` and `TryGet*` accessors for all 14 types return matching values | GodotAdapter Snd |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|------------|-----------------|-------------------|
| `Accessors_TryGet_RefTypeMismatch_Fails` | Ref slot holds a non-Godot value (string) when calling `TryGet*` | Returns false |
| `Accessors_As_UnregisteredKind_Throws` | `As*` called on TypedData with unregistered Kind(250) | InvalidCastException |

## GodotTypedDataPerformanceTests Test Details (Benchmark)

### Happy Path (Performance Baseline)

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `WriteThroughput_Registered_Outperforms_Unregistered` | Registered Kind(130) vs unregistered Kind(255) write throughput comparison; asserts both sides extract equivalent values | GodotAdapter Snd |
| `ReadThroughput_TryGetVector3_Outperforms_IsT` | `TryGetVector3` (Kind) vs `ToObject is Vector3` read, results match and print comparison | GodotAdapter Snd |
| `ObjectConverter_ToObject_GodotSwitch_Outperforms_Data` | ToObject switch dispatch vs Data property path comparison; asserts return value is correct Vector3 | GodotAdapter Snd |
| `ObjectConverter_FromObject_GodotSwitch_Outperforms_Fallback` | FromObject Kind-switch(137) vs unregistered fallback(255) comparison; asserts both sides extract equivalent Color values | GodotAdapter Snd |
| `Factory_CreateExtract_Vector3_RegisteredVsUnregistered` | `TypedDataFactory<Vector3>` Create+Extract Kind-based path throughput; asserts round-trip correct | GodotAdapter Snd |
| `MixedEntitySimulation_GodotTypes` | 500 entities × 60 frames mixed simulation throughput and allocation; asserts entity 0 position / alive data intact | GodotAdapter Snd |

## Test Support Strategy

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| None | — | Serialization tests define no support strategy classes; performance tests print unified summary tables via `PerfReporter.ReportTable` / `PerfReporter.CompareTable` |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Error paths for Godot type converters on malformed / missing-field JSON nodes not covered | Deserialization fault tolerance behavior not verified | Origo.GodotAdapter/Serialization |
| `TryGetAllGodotTypes_RoundTrip` does not cover TryGet round-trips for Vector4 / Quaternion / Basis / Transform2D / 3D / Aabb / Plane | Extension method round-trips for some Godot types not directly verified | Origo.GodotAdapter/Snd |
| Performance benchmarks include correctness smoke assertions (value equality / round-trip verification) but no hard performance thresholds | Data correctness regressions will fail tests; performance degradation is observational only, does not auto-fail | Origo.GodotAdapter/Snd |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
