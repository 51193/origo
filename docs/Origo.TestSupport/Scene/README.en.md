<!-- docsync-pair: Origo.TestSupport/Scene/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->

# Scene

> [↑ Back to TestSupport](../README.en.md)

## Overview

Test doubles for `ISndSceneHost`. They manage entities in in-memory lists and provide two lightweight implementations: `TestSndSceneHost` (with the built-in minimal `DummySndEntity`, aligned with production host contracts) and `StubSndSceneHost` (zero-dependency, no strategies/nodes, used by `LevelBuilder` and data-flow tests).

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestSndSceneHost.cs` | Implements `ISndSceneHost`, managing entities in `List<ISndEntity>`. Exports the entity metadata list via `BuildMetaList()` and exposes the `ClearAllCount` counter. The built-in `DummySndEntity` provides minimal Name and metadata support. **Aligned with the production host contract**: `GetEntities()` returns a snapshot (not downcastable to the mutable backing list) and `RemoveEntity` throws `InvalidOperationException` for unknown entities (consistent with `SndEntityCollection`), preventing test blind spots. |
| `StubSndSceneHost.cs` | Lightweight stub scene host using the embedded `StubSndEntity` (no strategies/nodes): node access throws, strategy/observer operations are silent no-ops, and only basic key-value data access is supported. Used by `LevelBuilder` offline construction and data-flow tests that do not need a full `SndWorld`/`ISndContext`. |

## Usage Pattern

```csharp
var host = new TestSndSceneHost();
var entity = host.CreateEntity(new SndMetaData { Name = "test" });
Assert.Single(host.GetEntities());
```

---

[↑ Back to TestSupport](../README.en.md)
