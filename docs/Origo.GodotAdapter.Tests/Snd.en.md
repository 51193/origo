<!-- docsync-pair: Origo.GodotAdapter.Tests/Snd -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# SND Entity Tests (Adapter)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Tested module: Origo.GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.en.md)

## Tested Behavior Overview

Verifies the parts of the adapter-layer SND entity system that **do not require the Godot runtime**: the pure-C# entity collection `SndEntityCollection<T>` (add/remove/find, batched recovery rollback, kill marking, frame processing orchestration), the forced-loading entry of the adapter TypedData registration, and the contracts of `GetNodeFromSnd<T>` / `GetNativeNode`.

## Test File List

| File | Verification focus |
|------|-----------|
| `Snd/SndEntityCollectionTests.cs` | Full entity collection capability: create/find/remove/kill marking, `RecoverFromMetaList` batch recovery with partial-failure rollback, `RemoveAllEntities`, frame processing `ProcessAll`, meta list building, `OwningSession` binding |
| `Snd/TypedDataInitializerTests.cs` | `TypedDataInitializer.EnsureLoaded()` triggering adapter-layer kind registration: idempotency and availability |
| `SndEntityNodeExtensionsTests.cs` | `GetNodeFromSnd<T>()` / `GetNativeNode()` contracts: non-Godot entity/handle returns null, node handle extraction |

## SndEntityCollectionTests Details

### Correct Paths

| Test method | Verified behavior | Doc source |
|---------|-----------|---------|
| `CreateEntity` group | Creating an entity adds it to the collection, visible via `FindByName`/`GetEntities`, `OwningSession` auto-bound on session binding | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList` group | Batch recovery adds all entities; `BuildMetaList` maps one-to-one to the recovered metadata | Origo.GodotAdapter/Snd |
| `RemoveEntity` / `RemoveAllEntities` group | Removal releases the engine node via the detach callback, list clears, `GetEntities` view stays in sync | Origo.GodotAdapter/Snd |
| `RequestKillEntity` group | Kill marking takes effect immediately, duplicate kill throws, `ProcessAll` frame processing ticks | Origo.GodotAdapter/Snd |
| `CreateEntity_AddsAndRecovers` | Creating an entity adds it to the collection and invokes `RecoverForLifecycle` (StableName set, counts correct) | Origo.GodotAdapter/Snd |
| `CreateEntity_OwningSession_BindsEntity` | When `OwningSession` is already set, created entities are auto-bound to the same session | Origo.GodotAdapter/Snd |
| `FindByName_ReturnsEntity` | Find by name returns the entity; missing name returns null | Origo.GodotAdapter/Snd |
| `GetEntities_ReturnsAllAndIsEnumerable` | `GetEntities` returns all entities; collection is directly enumerable | Origo.GodotAdapter/Snd |
| `GetEntities_ReturnsSnapshot_NotTheMutableBackingList` | `GetEntities` returns a snapshot copy (not the mutable backing list, not downcastable to bypass collection management; later mutations do not affect an already-obtained view) | Origo.GodotAdapter/Snd |
| `ProcessAll_ProcessesEveryEntity` | Frame processing invokes `ProcessSnd` once per entity | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_RecoversAll` | Batch recovery adds all entities, findable by name | Origo.GodotAdapter/Snd |
| `RemoveEntity_DetachesAndRemoves` | Removing an entity invokes the detach callback and deletes it from the collection | Origo.GodotAdapter/Snd |
| `RemoveAllEntities_ClearsCollection` | Clears the collection with the detach callback invoked per entity (b, a in reverse order) | Origo.GodotAdapter/Snd |
| `RequestKillEntity_MarksPending` | Kill marking takes effect immediately (`IsPendingKill`=true) | Origo.GodotAdapter/Snd |
| `BuildMetaList_ReturnsAllMetadata` | Builds all entity metadata in order (a, b) | Origo.GodotAdapter/Snd |

### Error Paths

| Test method | Verified behavior | Doc source |
|---------|-----------|---------|
| `RecoverFromMetaList` partial failure | When the N-th entity fails, all staged entities are rolled back (collection empty, detach callback invoked per entity) | Origo.GodotAdapter/Snd |
| `FindByName` missing | Returns null; `RemoveEntity` on a missing entity throws `InvalidOperationException` | Origo.GodotAdapter/Snd |
| `CreateEntity_NullMeta_Throws` | meta is null — throws `ArgumentNullException` | Origo.GodotAdapter/Snd |
| `CreateEntity_RecoverFailure_RollsBackAndPropagates` | Recovery fails during creation: exception propagates, collection rolls back to empty, detach callback invoked | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_Failure_RollsBackStaged` | When the N-th entity fails, all staged entities are rolled back (collection empty, detach callback invoked per entity) | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_Failure_ReportsFailingMeta` | Recovery failure reports the failing meta and exception via the failure callback | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_Null_Throws` | metaList is null — throws `ArgumentNullException` | Origo.GodotAdapter/Snd |
| `RemoveEntity_Unknown_Throws` | Removing a missing entity throws `InvalidOperationException` | Origo.GodotAdapter/Snd |
| `RequestKillEntity_AlreadyPending_Throws` | Duplicate kill on an already-marked entity throws `InvalidOperationException` | Origo.GodotAdapter/Snd |
| `ProcessAll_ContainerModifiedDuringProcess_Throws` | Collection mutated during frame processing (entity spawned inside ProcessSnd) | Throws `InvalidOperationException` (contains "modified during ProcessAll"; consistent with FullMemorySndSceneHost) |
| `RequestKillEntity_Unknown_Throws` | Killing a missing entity throws `InvalidOperationException` | Origo.GodotAdapter/Snd |

## TypedDataInitializerTests Details

### Happy Path

| Test method | Verified behavior | Doc source |
|---------|-----------|---------|
| `EnsureLoaded_TriggersAdapterKindRegistration` | `TypedDataInitializer.EnsureLoaded()` triggers adapter-layer kind registration (Vector2 resolves to Kind 128) | Origo.GodotAdapter/Snd |

## SndEntityNodeExtensionsTests Details

### Error Paths

| Test method | Verified behavior | Doc source |
|---------|-----------|---------|
| `GetNativeNode_NonGodotHandle_ReturnsNull` | `GetNativeNode()` returns null when the node handle is not a Godot node handle (graceful contract deviation, no crash) | Origo.GodotAdapter/Snd |
| `GetNodeFromSnd_NonGodotEntity_ReturnsNull` | `GetNodeFromSnd<T>()` returns null when the entity is not a Godot entity | Origo.GodotAdapter/Snd |

## Test Support Strategy

| Helper | Definition location | Purpose |
|--------|---------|------|
| `SndEntityCollectionTests` embedded fake entity | `SndEntityCollectionTests.cs` | Pure-C# fake entity implementing `ISndEntityFacade` (no Godot dependency), recording `RecoverForLifecycle`/`DetachFromManager` calls |
| `InMemoryLogger` | `SndEntityCollectionTests.cs` | Minimal `ILogger` stand-in |

## Known Coverage Gaps

| Gap description | Impact | Doc basis |
|---------|------|---------|
| Engine-side behavior of real `GodotSndEntity` / `GodotSndManager` (scene tree operations, `DetachAndFree` callback) is not covered here | Covered by `Origo.GodotAdapter.Integration.Tests` running in the Godot `--headless` runtime | Origo.GodotAdapter.Integration.Tests/README |

---
[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
