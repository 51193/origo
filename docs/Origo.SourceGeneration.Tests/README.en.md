# Origo.SourceGeneration.Tests

> [↑ Back to Origo Manual](../README.en.md) · [↔ Module Under Test: Origo.SourceGeneration](../Origo.SourceGeneration/README.en.md)

## Overview

`Origo.SourceGeneration.Tests` contains two categories of tests:

- **Generator behavior tests**: Directly drive `TypedDataGenerator`, run the generator on in-memory compilations and assert the generated sources, generator diagnostics, and the result of merged compilation of "original source + generated source". It references the generator as a regular library (not as an analyzer attachment), enabling instantiation and execution within tests.
- **Generated artifact performance benchmarks**: Obtain the generated `TypedData` by referencing `Origo.Core` (public API plus internal members accessed via `InternalsVisibleTo`), comparing throughput of inline storage/Kind dispatch against an unoptimized boxing implementation across multiple value types and the reference type `string`. Marked `[Trait("Category","Benchmark")]`, run once in a separate CI step (`scripts/benchmark.sh`) and printing comparison tables, separated from test runs subject to coverage gates.

## Files

| File | Responsibility |
|------|------|
| `GeneratorTestHarness.cs` | Constructs in-memory `CSharpCompilation`, runs `TypedDataGenerator`, exposes generated sources, generator diagnostics, merged compilation errors |
| `TypedDataGeneratorTests.cs` | Generator behavior tests: Home/Adapter mode output, two storage models, `ORIGOSG001`–`ORIGOSG008` diagnostics, generation determinism and incremental pipeline |
| `PrivateFieldNamingTests.cs` | Architecture guardrail: production assembly private fields follow `_camelCase` naming |
| `ApiDocumentationGuardrailTests.cs` | Architecture guardrail: public/protected API declarations in production source must carry `<summary>` or `<inheritdoc />` (vendored FastNoiseLite exempt) |
| `Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | Generated artifact performance benchmarks: write/read/mixed dispatch for multiple value types + `string`, generated inline `TypedData` vs unoptimized boxing; fixed pool + large iterations + multi-round min noise reduction, relaxed thresholds + comparison tables, and per-side measured allocation (`GC.GetAllocatedBytesForCurrentThread`, placed in separate `NoInlining` methods to avoid polluting timing) |

## TypedDataGeneratorTests Test Details

### Happy Paths

| Test Method | Verified Behavior | Doc Source |
|---------|-----------|---------|
| `Home_Primitives_GeneratesExpectedMembers_AndCompiles` | Home mode registers system primitive types, generates `KindMap`/`TryGetInt32`/`AsInt32`/`explicit operator`/`TypedDataFactory<T>`/`TypedDataHomeKindRegistration` + `[ModuleInitializer]`, merged compilation zero errors | Origo.SourceGeneration |
| `Home_PublicGeneratedMembers_EmitDocCommentsBeforeAttributes` | Generated public members emit XML doc comments before `[MethodImpl]` so CS1591 recognizes them | Origo.SourceGeneration |
| `Home_GeneratedPublicMembers_CarryXmlSummaries` | Generated `TryGet*` accessors, conversion operators and string reader carry the expected XML `<summary>` text | Origo.SourceGeneration |
| `Home_StringStoredViaRefSlot` | `string` accessed via `_ref` slot (`AsString() => (string?)_ref`, `case 13: return td._ref`) | Origo.SourceGeneration |
| `Adapter_ValueAndRefTypes_UseRefSlot_AndCompiles` | Adapter layer non-system value types and reference types uniformly go through `_ref`, generating `TypedDataLayeredExtensions`, `RegisterKind`, Converter/TypeMap branches, merged compilation zero errors | Origo.SourceGeneration |
| `Generation_IsDeterministic` | Same input run twice produces completely identical source text; Home and Adapter outputs use LF line endings on every host | Origo.SourceGeneration |
| `StartKind_OffsetIsHonored_AndNumberingIsSequential` | `StartKind` offset honored (128/129), numbering is sequential per declaration order | Origo.SourceGeneration |
| `OverlappingStartKinds_SameType_Deduplicated` | Same type declared redundantly in overlapping `StartKind` groups is deduplicated, no diagnostics, no compilation errors | Origo.SourceGeneration |
| `Incremental_SameInputTwice_ProducesIdenticalOutput` | Same input run twice consecutively, generated sources are item-by-item identical | Origo.SourceGeneration |
| `Incremental_SameInputTwice_NoAdditionalOutputs` | Same input run three times, first and third run generated source count and content match (no extra output) | Origo.SourceGeneration |
| `Incremental_UnrelatedCodeChange_GeneratedOutputUnchanged` | Appending an unrelated comment does not change generated output | Origo.SourceGeneration |
| `Incremental_NoAttribute_ThenAddAttribute_ProducesNewOutput` | From no attribute to adding attribute, output changes from empty to non-empty | Origo.SourceGeneration |
| `Incremental_HasAttribute_ThenRemoveAttribute_OutputDisappears` | From having attribute to removing attribute, output changes from non-empty to empty | Origo.SourceGeneration |
| `Incremental_AddTypeToExistingAttribute_OutputChanges` | Adding a type to an existing attribute, output gains corresponding type member (`Single`) | Origo.SourceGeneration |
| `Adapter_NamedStartKindArgument_IsExtracted` | Named-argument form `startKind: 128` extracts equivalently to positional (StubVec3=128, StubRef=129 registered), no diagnostics, zero compile errors | Origo.SourceGeneration |
| `Adapter_ReferenceOnlyTypes_HaveNoInlineHelpers` | Adapter mode with only reference types (StubRefA/B) does not emit `ReadBitsAs`/`BitsFrom`/`_inlineBits` inline machinery, only `AsStubRefA/B` and `RegisterKind(128/129)` | Origo.SourceGeneration |
| `Adapter_BCLTypeItself_WithReservedKindName_IsAllowed` | Registering BCL `string` itself at kind 128 emits `AsString` with no diagnostics or compile errors | Origo.SourceGeneration |
| `Attribute_DefaultStartKind_StartsAtOne` | Without `StartKind`, numbering starts at 1 (Int32=1, String=2, matching `RegisterKind`) | Origo.SourceGeneration |
| `Attribute_NoTypes_ProducesNoOutput` | Only `StartKind` (128) specified with no type arguments | Produces no sources, no diagnostics, no compile errors |
| `Home_WithoutString_GeneratesCompilableCode` | Home mode without `string` registered (int/float only) | Zero compile errors, does not generate `KindMap.String`/`IsString`/`AsString`/`TryGetString` |
| `Incremental_SameInputTwice_GenerationStepIsCached` | Two new Compilation instances with identical sources (modeling every editor keystroke) | Second run's SourceOutput steps all skip regeneration with Cached reason |
| `Incremental_SameInputTwice_TrackedStepsPresent` | Same input run twice | Incremental pipeline retains TrackedSteps (non-empty), observable step execution/caching reasons |

### Error Paths

| Test Method | Triggered Error | Expected Behavior |
|---------|-----------|---------|
| `Home_UnsupportedValueType_ReportsORIGOSG002_ButStillGeneratesValidTypes` | Home group registers uninlinable value type (`decimal`) | Reports `ORIGOSG002` (Error), excludes only the unsupported type, `int` still generated normally with merged compilation zero errors |
| `Adapter_SystemPrimitive_ReportsORIGOSG001` | Adapter group registers system primitive type (`int`) | Reports `ORIGOSG001` (Error), excludes the primitive, does not produce inline accessors for it |
| `Adapter_WithoutFriendAccess_ReportsORIGOSG007_AndEmitsNoSources` | Adapter assembly not listed in `Origo.Core.Contracts` `InternalsVisibleTo` | Reports `ORIGOSG007` (Error) and emits no generated sources, zero compile errors |
| `Attribute_NullArrayElement_ReportsORIGOSG008_AndEmitsNoSources` | Type arguments contain a null element | Reports `ORIGOSG008` (Error) and emits no sources; a null placeholder would renumber the following types |
| `KindPastByteRange_ReportsORIGOSG003_IncludingWrapToNonZero` | `StartKind` 254 with `byte`/`sbyte`/`short` assigns kinds 254/255/256: 255 is the reserved `UnregisteredKind` sentinel and 256 exceeds the byte range (wrapping to 0) | Reports `ORIGOSG003` (Error) for both out-of-range types, drops `AsSByte`/`AsInt16`, and still generates the in-range `Byte = 254` |
| `OverlappingStartKindRanges_ReportORIGOSG004_AndDropCollidingTypes` | Two groups assign same Kind 1 to different types (`int`/`long`) | Reports `ORIGOSG004` (Error), excludes the two colliding types |
| `HomeAndAdapterCoexistence_HomeWins_NonSystemTypesRejected` | Same compilation has both Home attribute (system primitive types) and Adapter attribute (non-system value types) | Home mode takes effect, system types generated normally, non-system value types report `ORIGOSG002` (Error) |
| `GenericInstantiations_WithSameName_ReportORIGOSG005` | `List<int>` and `List<string>` share the name 'List' | Reports `ORIGOSG005` (Error), drops the colliding types, does not produce List identifiers |
| `SameTypeNameDifferentNamespaces_ReportORIGOSG005_AndDropCollidingTypes` | `A.Dup`/`B.Dup` from different namespaces share the name (kind-name collision after sanitizing) | Reports `ORIGOSG005` (Error), drops colliding types, does not produce uncompilable duplicate identifiers |
| `SameTypeRegisteredTwice_ReportORIGOSG005` | The same type registered twice (`List<int>` twice) | Reports `ORIGOSG005` (Error), drops the duplicate (prevents CS0111 duplicate members in generated code) |
| `SameTypeRegisteredToDifferentKinds_ReportORIGOSG005` | The same type registered under two different Kinds (1 and 5) | Reports `ORIGOSG005` (Error), drops the type |
| `TypeNamedNull_ReservedKindMapSentinel_ReportORIGOSG005_AndDropType` | Registered type named 'Null' collides with the KindMap reserved sentinel | Reports `ORIGOSG005` (Error), drops the type (prevents CS0102 duplicate members), zero compile errors |
| `ValueTypeNamedNull_InHomeMode_ReportORIGOSG002_AndDropType` | Home mode registers non-system value type `A.Null` (struct) | Reports `ORIGOSG002` (Error), drops the type (does not generate members colliding with the handwritten `IsNull`), zero compile errors |
| `PointerType_ReportsDiagnostic_InsteadOfEmittingInvalidIdentifier` | Adapter group registers pointer type `int*` (sanitized name is an invalid identifier) | Reports `ORIGOSG006` (Error), drops the registration instead of emitting uncompilable accessor identifiers, zero compile errors |
| `AdapterType_KindNameCollidesWithHomeReservedKind_ReportORIGOSG005` | Adapter type named like a Home inline kind (`A.Int32`) | Reports `ORIGOSG005` (Error), drops the registration, emits no `AsInt32`, zero compile errors |

### Edge Paths

| Test Method | Edge Condition | Expected Behavior |
|---------|---------|---------|
| `Home_NoAttribute_ProducesNoOutput` | No `SndInlineTypes` attribute | Produces no sources, no diagnostics |
| `MalformedAttribute_NoTypes_ProducesNoOutput` | Declares `SndInlineTypes` but passes no type arguments | Produces no sources, no diagnostics |
| `Home_OnlyReferenceTypes_NoInlineMethods` | Only registers reference types (`string`) | Generates `KindMap`/`AsString`, but does not produce `explicit operator`/`_inlineBits` inline mechanisms |
| `Home_DoesNotEmitSilentStubHelpers` | Home mode generation (regression guard) | Never generates `BitsFrom`/`ReadBitsAs`/`Pack`/`return default;` silent stub helpers |
| `Adapter_DoesNotEmitInlineHelpers` | Adapter mode generation (regression guard) | Does not produce `ReadBitsAs`/`BitsFrom`/`_inlineBits` |

## ApiDocumentationGuardrailTests Test Details

### Happy Paths

| Test Method | Verified Behavior | Doc Source |
|---------|-----------|---------|
| `PublicApi_EveryDeclaration_HasSummaryOrInheritDoc` | Scans public/protected declarations across production roots; every one carries `<summary>` or `<inheritdoc />` (vendored FastNoiseLite exempt) | AGENTS.md: source comment rule |

## Benchmarks/TypedDataGeneratedBenchmarkTests Test Details

> See [Benchmarks.en.md](Benchmarks.en.md). Marked `[Trait("Category","Benchmark")]` (class-level), run only by `scripts/benchmark.sh`.

## Test Support Infrastructure

| Facility | Type | Purpose |
|------|------|------|
| `GeneratorTestHarness` | Internal static class | Constructs in-memory `CSharpCompilation` (with trusted platform assemblies as references, excluding `Origo.*`), runs `TypedDataGenerator`, exposes generated sources, generator diagnostics, merged compilation errors; also provides `CreateTrackedDriver`/`RunIncremental` for incremental pipeline assertions |
| `GeneratorOutput` | Internal record | Encapsulates generated sources array, generator diagnostics, merged compilation errors, provides `AllGeneratedText` and `HasGeneratorDiagnostic(id)` |

## Line Coverage Gate

Coverlet enforces `Origo.SourceGeneration` line coverage ≥ 90% (effective in CI and local `dotnet test` runs).

## Design Decisions

### Why use a generator driver instead of snapshot/Verifier frameworks

Directly using `CSharpGeneratorDriver` to drive the generator minimizes dependencies and allows asserting generated source text, generator diagnostics, and merged compilation results all within the same test. This seamlessly integrates with the repository's unified xUnit v3 usage without introducing additional verification framework dependencies.

### Why use runtime trusted platform assemblies as references while excluding Origo.* assemblies

The test compilation uses the current runtime's `TRUSTED_PLATFORM_ASSEMBLIES` as metadata references, enabling the in-memory compilation to resolve arbitrary BCL usages (`BitConverter`, `Unsafe`, `ModuleInitializer`, etc.) without fixed reference assembly packages. `Origo.*` assemblies are explicitly excluded: the performance benchmarks reference the real `Origo.Core`, which brings it into the test process's trusted platform assembly list, while the generator driver tests simulate `Origo.Core.Snd.Metadata` types with embedded source scaffolds — if the in-memory compilation also referenced the real `Origo.Core`, same-named types would conflict (CS0433).

### Why Adapter cases reference a separate host assembly and declare InternalsVisibleTo

The generator determines Home/Adapter mode by the assembly that defines `TypedData`. Adapter cases place the `TypedData` definition in a referenced host assembly, making the current compilation recognized as an adapter layer; the host assembly declares `InternalsVisibleTo` so that generated adapter layer code can access `TypedData`'s internal fields, matching the real relationship between Origo.Core/Origo.GodotAdapter.

---

> [↑ Back to Origo Manual](../README.en.md)
