<!-- docsync-pair: Origo.Core.Tests/Save-Serialization -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Persistence: Serialization Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Save/Serialization](../Origo.Core/Save/Serialization/README.en.md)
> [↔ Behavior under test: usage/persistence-flow](../usage/persistence-flow.en.md)

## Behavior Overview

Validates serialization orchestration for save Payloads: Blackboard serialize/deserialize round-trip, SND scene entity list serialization,
SaveContext coordination behavior within ProgressRun flow, SaveCoordinator constructor guards,
PersistentBlackboard disk read/write.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `BlackboardSerializerTests.cs` | Blackboard serialization/deserialization: TypedData type preservation, empty blackboard, overwrite semantics |
| `SndSceneSerializerTests.cs` | SND scene serialization: entity metadata list ↔ DataSourceNode round-trip, empty scene, invalid JSON |
| `SaveContextTests.cs` | SaveContext: Payload construction/write orchestration, Sequence blackboard deserialization atomic update, null guards |
| `SaveCoordinatorTests.cs` | SaveCoordinator: constructor null parameter guards, PersistProgress rejection without foreground Session |
| `PersistentBlackboardTests.cs` | PersistentBlackboard: Set/Clear/LoadFromDisk disk round-trip |

## BlackboardSerializerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `BlackboardSerializer_RoundTrip_PreservesData` | int/string multi-key-value serialize→deserialize, type and value fully preserved | BlackboardSerializer |
| `BlackboardSerializer_DeserializeInto_OverwritesExisting` | DeserializeInto completely replaces target blackboard keys with source data | BlackboardSerializer |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `BlackboardSerializer_Serialize_EmptyBlackboard_ReturnsValidJson` | Empty blackboard serialization | Returns valid JSON (contains `{`) |

## SndSceneSerializerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SndSceneSerializer_Serialize_EmptyScene` | Empty scene serialization returns JSON array | SndSceneSerializer |
| `SndSceneSerializer_RoundTrip_PreservesMetaList` | Scene entity serialization→deserialization restores entity names | SndSceneSerializer |
| `SndSceneSerializer_DeserializeInto_ClearsBeforeLoad` | SceneHost is cleared before deserialization (ClearAllCount = 0 verified) | SndSceneSerializer |
| `SndSceneSerializer_DeserializeInto_NoClearWhenFalse` | Repeated deserialization calls do not clear multiple times | SndSceneSerializer |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SndSceneSerializer_DeserializeInto_InvalidJson_Throws` | Invalid JSON (object instead of array) | Throws exception |
| `SndSceneSerializer_Constructor_ThrowsOnNullWorld` | null SndWorld | ArgumentNullException |

## SaveContextTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SaveContext_SerializeProgress_And_DeserializeProgress_RoundTrip` | Progress blackboard serialize→deserialize data consistent | SaveContext |
| `SaveContext_SerializeSession_And_DeserializeSession_RoundTrip` | Session blackboard serialize→deserialize data consistent | SaveContext |
| `SaveContext_SerializeSndScene_ReturnsJson` | BuildSndScene returns non-null DataSourceNode | SaveContext |
| `SaveContext_RecoverSndScene_LoadsEntities` | Restores entities to SceneHost from JSON | SaveContext |
| `SaveContext_SaveGame_CreatesSaveGamePayload` | SaveGame constructs full Payload with SaveId/ActiveLevelId/Levels | SaveContext |
| `SaveContext_SaveGame_WithCustomMeta` | SaveGame carries CustomMeta dictionary | SaveContext |
| `SaveContext_Properties_ExposeBlackboards` | Progress/Session/SndWorld property reference consistency | SaveContext |
| `DeserializeProgress_ThenVerify_BlackboardDataUpdated` | After DeserializeProgress, Progress blackboard key-values correctly updated (including newly added keys) | SaveContext |
| `DeserializeSession_ThenVerify_BlackboardDataUpdated` | After DeserializeSession, Session blackboard key-values correctly updated (including newly added keys) | SaveContext |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SaveContext_Constructor_ThrowsOnNullArgs` | Any constructor parameter is null (Progress/Session/SndWorld) | ArgumentNullException |
| `DeserializeProgress_NullNode_ThrowsArgumentNullException` | null DataSourceNode | ArgumentNullException |

## SaveCoordinatorTests Details

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SaveContext_Constructor_ThrowsOnNullArgs` | null progress / null session / null sndWorld argument | ArgumentNullException |
| `PersistProgress_WithoutForegroundSession_Throws` | PersistProgress called without foreground Session | InvalidOperationException (message contains "foreground") |

## PersistentBlackboardTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `PersistentBlackboard_SetAndLoadFromDisk_Works` | SetValue → reload → LoadFromDisk restores key-value | PersistentBlackboard |
| `PersistentBlackboard_Clear_PersistsEmptyData` | After Clear, data on disk is empty Map | PersistentBlackboard |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `TestStateMachineContext` | SaveCoordinatorTests.cs | IStateMachineContext stub, provides empty blackboard and empty SceneAccess |
| `TestSceneAccess` | SaveCoordinatorTests.cs | ISndSceneAccess stub, returns empty entity list |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| Data not correctly refreshed to Payload after BeforeSave hook fires | Hook not executed before serialization | snd-entity-model |
| BlackboardSerializer TypedData round-trip for complex custom types | Currently only tests int/string, not nested objects/arrays | BlackboardSerializer |
| SndSceneSerializer serialization of entities with strategy metadata | Currently only tests basic Name field | SndSceneSerializer |

## Design Decisions

### SaveContext Deserialization Atomic Rollback

`SaveContext.DeserializeProgress()` and `DeserializeSession()` snapshot the target blackboard before deserialization. If `DeserializeInto()` throws, the blackboard is restored to the snapshot state, ensuring a failed deserialization does not leave the blackboard partially modified.

---

[↑ Back to Origo.Core.Tests](README.en.md)
