<!-- docsync-pair: Origo.GodotAdapter.Tests/Snd -->
<!-- docsync-revision: 1 -->
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
| `SndEntityNodeExtensionsTests.cs` | `GetNodeFromSnd<T>()` / `GetNativeNode()` contracts: wrong-type throws, node handle extraction |

## SndEntityCollectionTests Details

### Correct Paths

| Test method | Verified behavior | Doc source |
|---------|-----------|---------|
| `CreateEntity` group | Creating an entity adds it to the collection, visible via `FindByName`/`GetEntities`, `OwningSession` auto-bound on session binding | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList` group | Batch recovery adds all entities; `BuildMetaList` maps one-to-one to the recovered metadata | Origo.GodotAdapter/Snd |
| `RemoveEntity` / `RemoveAllEntities` group | Removal releases the engine node via the detach callback, list clears, `GetEntities` view stays in sync | Origo.GodotAdapter/Snd |
| `RequestKillEntity` group | Kill marking takes effect immediately, duplicate kill throws, `ProcessAll` frame processing ticks | Origo.GodotAdapter/Snd |

### Error Paths

| Test method | Verified behavior | Doc source |
|---------|-----------|---------|
| `RecoverFromMetaList` partial failure | When the N-th entity fails, all staged entities are rolled back (collection empty, detach callback invoked per entity) | Origo.GodotAdapter/Snd |
| `FindByName` missing | Returns null; `RemoveEntity` on a missing entity throws `InvalidOperationException` | Origo.GodotAdapter/Snd |

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
