<!-- docsync-pair: Origo.Core.Tests/Snd-Scene -->
<!-- docsync-revision: 12 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# SND Scene Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd/Scene](../Origo.Core/Snd/Scene/README.en.md)
> [↔ Behavior under test: usage/snd-entity-model](../usage/snd-entity-model.en.md)

## Behavior Under Test Overview

Validates the SND scene host layer implementations: StubSndSceneHost entity container operations (CreateEntity/FindByName/RecoverFromMetaList/RemoveAllEntities/BuildMetaList), FullMemorySndSceneHost binding prerequisites and error paths, and NullNodeFactory no-render behavior.

SndEntityFactory spawn orchestration and ProcessAll frame processing are covered by SndEntityLifecycleBatchTests; see [Snd-Entity.md](Snd-Entity.en.md).

## Test File List

| File | Verification Focus |
|------|-------------------|
| `MemorySndSceneHostTests.cs` | Basic entity add/remove/lookup/modify and list serialization behavior of StubSndSceneHost |
| `FullMemorySndSceneHostTests.cs` | Binding prerequisites for FullMemorySndSceneHost, error paths for CreateEntity/RemoveEntity/RequestKillEntity |
| `NullNodeFactoryTests.cs` | NullNodeFactory returns NullNodeHandle; Free/SetVisible are no-ops |
| `SndEntityFactoryRollbackTests.cs` | Verifies that Spawn/SpawnMany roll back when an AfterSpawn hook throws (entity removal, observer teardown, strategy/node release, original exception propagation); rollback order is correct on detach-invalidating hosts (Godot wrapper semantics) so the original exception is not masked; when a rollback step itself throws, the original exception still propagates and the remaining rollback steps all execute |

## MemorySndSceneHostTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `Spawn_AddsEntityAndMeta` | CreateEntity adds the entity to the GetEntities list and BuildMetaList | ISndSceneHost |
| `FindByName_ReturnsEntity` | FindByName finds the created entity; returns null when not found | ISndSceneHost |
| `LoadFromMetaList_DoesNotClearExisting` | RecoverFromMetaList does not clear existing entities (caller handles cleanup) | ISndSceneHost |
| `ClearAll_RemovesEntitiesAndMeta` | After RemoveAllEntities, both GetEntities and BuildMetaList are empty | ISndSceneHost |
| `SerializeMetaList_ReturnsCorrectData` | BuildMetaList returns all current entity metadata | ISndSceneHost |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Spawn_ThrowsOnNull` | null metadata parameter | ArgumentNullException |
| `LoadFromMetaList_ThrowsOnNull` | null metaList parameter | ArgumentNullException |
| `RemoveEntity_Missing_Throws` | Removing a non-existent entity name | InvalidOperationException |

## FullMemorySndSceneHostTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `CreateEntity_ReturnsEntityAndAddsToCollection` | CreateEntity returns an entity; FindByName can find it; GetEntities contains it | ISndSceneHost |
| `RemoveEntity_ExistingName_RemovesAndNotFoundAfter` | After RemoveEntity, FindByName returns null | ISndSceneHost |
| `RequestKillEntity_SetsPendingKillTrue` | RequestKillEntity sets IsPendingKill to true | ISndSceneHost |

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `CreateEntity_NullMeta_ThrowsArgumentNull` | null metadata (paramName="metaData") | ArgumentNullException |
| `CreateEntity_BeforeBindWorld_ThrowsInvalidOperation` | BindWorld not yet called | InvalidOperationException, message contains "SndWorld" |
| `CreateEntity_BeforeBindContext_ThrowsInvalidOperation` | BindContext not yet called | InvalidOperationException, message contains "ISndContext" |
| `RemoveEntity_NonexistentName_ThrowsInvalidOperation` | Non-existent entity name | InvalidOperationException, message contains entity name |
| `RequestKillEntity_DoubleRequest_ThrowsInvalidOperation` | Duplicate RequestKillEntity call | InvalidOperationException, message contains "already pending kill" |

## NullNodeFactoryTests Details

### Correct Paths

| Test Method | Behavior Verified | Documentation Source |
|-------------|------------------|---------------------|
| `NullNodeFactory_CreatesNullNodeHandle` | Create returns a non-null handle; Free/SetVisible are no-ops | INodeFactory |

## SndEntityFactoryRollbackTests Details

### Error Paths

| Test Method | Error Triggered | Expected Behavior |
|-------------|----------------|-------------------|
| `Spawn_AfterSpawnHookThrows_RollsBackEntityAndStrategyReferences` | AfterSpawn hook of Spawn throws InvalidOperationException | Half-initialized entity removed from the host (FindByName returns null); acquired strategy references returned (LogPoolLeaks emits no refCount warning); original exception propagates |
| `SpawnMany_AfterSpawnHookThrows_RollsBackUnfiredEntitiesOnly` | AfterSpawn hook of the second entity in SpawnMany throws InvalidOperationException | E1 whose hook already fired stays; E2 and E3 whose hooks never fired are fully rolled back; rolled-back strategy references returned |
| `Spawn_AfterSpawnHookThrows_OnDetachInvalidatingHost_PropagatesOriginalException` | AfterSpawn throws on a detach-invalidating host (Godot wrapper semantics) | Teardown happens before removal; the original exception is not masked by ObjectDisposedException |
| `Spawn_AfterSpawnHookThrows_WhenRollbackStepAlsoThrows_PropagatesOriginalAndCompletesRollback` | AfterSpawn throws and `TeardownObserverBindings` (OnUnmounted hook) also throws during rollback | Original AfterSpawn exception propagates; `ReleaseStrategiesOnly`/`TeardownOnly`/`RemoveEntity` all still execute; host removal is best-effort |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|----------------|-----------|---------|
| `ThrowingAfterSpawnStrategy` | SndEntityFactoryRollbackTests.cs | AfterSpawn hook throws InvalidOperationException ("Intentional AfterSpawn failure"); verifies Spawn/SpawnMany rollback |
| `NormalStrategy` | SndEntityFactoryRollbackTests.cs | Normal lifecycle strategy; the entity whose hook fired (E1) survives in SpawnMany |
| `NormalTwoStrategy` | SndEntityFactoryRollbackTests.cs | Normal lifecycle strategy; rollback candidate whose hook never fired (E3); verifies unfired entities are rolled back |

## Known Coverage Gaps

| Gap Description | Impact | Documentation Basis |
|-----------------|--------|---------------------|
| Thread safety of concurrent CreateEntity/RemoveEntity | Whether the scene host promises thread safety | ISndSceneHost |

---

[↑ Back to Origo.Core.Tests](README.en.md)
