<!-- docsync-pair: Origo.Core.Tests/Save-Meta -->
<!-- docsync-revision: 12 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Persistence: Metadata Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core.Contracts/Save/Meta](../Origo.Core.Contracts/Save/Meta/README.en.md)
> [↔ Behavior under test: usage/persistence-flow](../usage/persistence-flow.en.md)

## Behavior Overview

Validates the construction, merging, and persistence of `meta.map` display metadata.
Covers the `ISaveMetaContributor` contributor interface, `DelegateSaveMetaContributor` delegate wrapper,
`SaveMetaBuildContext` context data passing, `SaveMetaMerger` multi-source merging,
contributor registration, fail-fast on invalid contributor output, and the full SaveGame chain.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `DelegateSaveMetaContributorTests.cs` | DelegateSaveMetaContributor delegate invocation and null constructor guard |
| `SaveMetaBuildContextTests.cs` | SaveMetaBuildContext property storage and null parameter guards |
| `SaveMetaIntegrationTests.cs` | Full chain: register→RequestSaveGame→CustomMeta written to meta.map; also includes SaveMetaNullAndSessionContextTests |
| `SaveMetaMergerTests.cs` | SaveMetaMerger multi-contributor merging, override priority, fail-fast on invalid output |

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
| `RegisterSaveMetaContributor_NullContribution_FailsSave` | Contributor returns a null dictionary | `FlushFrame` for RequestSaveGame throws InvalidOperationException; no save is produced |
| `RegisterSaveMetaContributor_BlankContributionKey_FailsSave` | Contributor returns a blank key | `FlushFrame` for RequestSaveGame throws InvalidOperationException; no save is produced |
| `RegisterSaveMetaContributor_NullContributionValue_FailsSave` | Contributor returns a null value | `FlushFrame` for RequestSaveGame throws InvalidOperationException; no save is produced |

## SaveMetaNullAndSessionContextTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `NullSndContext_RegisterSaveMetaContributor_Throws` | Registering a contributor (interface or delegate) on NullSndContext | InvalidOperationException |

## SaveMetaMergerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Merge_LaterContributorOverwritesEarlierSameKey` | For the same key across multiple contributors, later overwrites earlier | SaveMetaMerger |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Merge_NoContributors_ReturnsNull` | No contributors | Returns null |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Merge_NullContribution_Throws` | Contributor returns a null dictionary | InvalidOperationException (includes contributor type) |
| `Merge_BlankKey_Throws` | Contributor returns an empty key | InvalidOperationException (includes contributor type) |
| `Merge_WhitespaceKey_Throws` | Contributor returns a whitespace-only key | InvalidOperationException (includes contributor type) |
| `Merge_NullValue_Throws` | Contributor returns a null value for a key | InvalidOperationException (includes contributor type) |

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
