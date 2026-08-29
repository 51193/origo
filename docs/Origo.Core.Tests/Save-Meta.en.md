<!-- docsync-pair: Origo.Core.Tests/Save-Meta -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Persistence: Metadata Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Save/Meta](../Origo.Core/Save/Meta/README.en.md)
> [↔ Behavior under test: usage/persistence-flow](../usage/persistence-flow.en.md)

## Behavior Overview

Validates the construction, merging, and persistence of `meta.map` display metadata.
Covers the `ISaveMetaContributor` contributor interface, `DelegateSaveMetaContributor` delegate wrapper,
`SaveMetaBuildContext` context data passing, `SaveMetaMerger` multi-source merging,
contributor registration, and the full SaveGame chain.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `DelegateSaveMetaContributorTests.cs` | DelegateSaveMetaContributor delegate invocation and null constructor guard |
| `SaveMetaBuildContextTests.cs` | SaveMetaBuildContext property storage and null parameter guards |
| `SaveMetaIntegrationTests.cs` | Full chain: register→RequestSaveGame→CustomMeta written to meta.map; also includes SaveMetaNullAndSessionContextTests |
| `SaveMetaMergerTests.cs` | SaveMetaMerger multi-contributor merging, override priority, null handling |

## DelegateSaveMetaContributorTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `DelegateSaveMetaContributor_Contribute_InvokesDelegate` | Wrapped delegate correctly called and returns dictionary, key/value pass-through | ISaveMetaContributor |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `DelegateSaveMetaContributor_Constructor_ThrowsOnNull` | null delegate argument | ArgumentNullException |

## SaveMetaBuildContextTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SaveMetaBuildContext_StoresAllProperties` | SaveId/CurrentLevelId/Progress/Session/SceneAccess all correctly stored | ISaveMetaContributor |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SaveMetaBuildContext_ThrowsOnNullArgs` | Any constructor parameter is null (SaveId/CurrentLevelId/Progress/Session/SceneAccess) | ArgumentNullException |

## SaveMetaIntegrationTests Details

### SaveMetaContributorRegistrationTests Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `RegisterSaveMetaContributor_WithISaveMetaContributor_ContributesToSavePayload` | After registering ISaveMetaContributor, RequestSaveGame Payload.CustomMeta contains contributed key-value | persistence-flow: meta.map |
| `RegisterSaveMetaContributor_WithDelegate_ContributesToSavePayload` | Behavior after delegate registration is identical to interface registration | persistence-flow: meta.map |
| `MultipleContributors_LaterOverwritesEarlier` | When multiple contributors provide the same key, later overwrites earlier | persistence-flow |
| `MultipleContributors_EachAddsDifferentKey` | Multiple contributors each provide different keys, final CustomMeta contains all | persistence-flow |
| `SaveWithoutContributors_CustomMetaIsNull` | CustomMeta is null when no contributors registered | persistence-flow |
| `ContributorReceivesCorrectSaveMetaBuildContext` | Contributor callback receives correct SaveMetaBuildContext (SaveId/LevelId/Progress/Session) | ISaveMetaContributor |
| `SaveMultipleTimes_EachSaveHasCorrectMeta` | Multiple saves each carry their own cycle's CustomMeta | persistence-flow |

### SaveMetaContributorRegistrationTests Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `RegisterSaveMetaContributor_ThrowsOnNullContributor` | null ISaveMetaContributor | ArgumentNullException |
| `RegisterSaveMetaContributor_ThrowsOnNullDelegate` | null delegate | ArgumentNullException |

## SaveMetaNullAndSessionContextTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `NullSndContext_RegisterSaveMetaContributor_Throws` | Registering a contributor (interface or delegate) on NullSndContext | InvalidOperationException |

## SaveMetaMergerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Merge_ContributorsThenOverrides_OverridesWin` | Contributor key-values overridden by overrides, non-conflicting keys each retained | SaveMetaMerger |
| `Merge_LaterContributorOverwritesEarlierSameKey` | For same key across multiple contributors, later overwrites earlier | SaveMetaMerger |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Merge_NoContributorsNoOverrides_ReturnsNull` | No contributors and no overrides | Returns null |
| `Merge_SkipsNullOverrideValues` | Override key with null value | Retains contributor original value, does not override with null |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `KeyValueContributor` | SaveMetaIntegrationTests.cs | ISaveMetaContributor stub with fixed key/value |
| `SndContextTestHelper` | SaveMetaIntegrationTests.cs | Helper for quick SndContext construction and ProgressRun init |
| `FuncContributor` | SaveMetaMergerTests.cs | Delegate-driven ISaveMetaContributor stub |
| `NullSceneHost` | SaveMetaMergerTests.cs | ISndSceneHost empty implementation for SaveMetaBuildContext construction |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| ISaveMetaContributor accessing a disposed Session during contribution | Timely release of contributor references after Dispose | session-model: Dispose Semantics |
| SaveMetaMerger rollback behavior when a contributor throws | Consistency of merge result on single contributor exception | — |

---

[↑ Back to Origo.Core.Tests](README.en.md)
