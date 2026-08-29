<!-- docsync-pair: Origo.Core/Save/Serialization/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Serialization (Save)

> [↑ Back to Save](../README.en.md)

## Overview
Save serialization layer implementation. `BlackboardSerializer` and `SndSceneSerializer` for data transformation; `SaveContext` aggregates these capabilities. All operations produce `DataSourceNode`, never touching file I/O directly. Strategy hooks batch-triggered by `SessionRun`.

## Included Files

| File | Responsibility |
|------|------|
| `BlackboardSerializer.cs` | Blackboard ↔ DataSourceNode (via TypedData + ConverterRegistry) |
| `SndSceneSerializer.cs` | SND scene ↔ DataSourceNode |
| `SaveContext.cs` | Save orchestration: holds serializers + builds SaveGamePayload |

## Module Details

### BlackboardSerializer
- **Serialize**: `blackboard.SerializeAll()` → `registry.Write<Dict<string,TypedData>>()` → DataSourceNode
- **DeserializeInto**: DataSourceNode → `registry.Read<Dict<string,TypedData>>()` → `blackboard.DeserializeAll(dict)`

`TypedData` is a struct (value type); its inline values are stored directly in the dictionary, avoiding extra heap allocations.

Depends on `BlackboardDataConverter` (registered in DataSource.Converters).

### SndSceneSerializer
- **Build**: `sceneAccess.BuildMetaList()` → `_world.WriteMetaListNode(metaList)` → DataSourceNode.  
  Does not fire BeforeSave — `SessionRun.BuildLevelPayload` fires it in batch before calling.
- **RecoverInto**: input must be Array format → `SndMappings.ResolveMetaListFromJsonArray` → `sceneAccess.RecoverFromMetaList`.  
  Fires no AfterLoad hooks and performs no ClearAll — `SessionRun.LoadFromPayload` fires hooks in batch afterwards; callers handle clearing before recovery.

### SaveContext
Core orchestration object, **transient** — created fresh per save/load operation, held by no runtime component:

```
SaveContext = IBlackboard(Progress) + IBlackboard(Session) + SndWorld
```

| Method | Description |
|--------|-------------|
| `BuildSndScene(ISndSceneAccess)` | Builds scene metadata (no BeforeSave hooks) |
| `RecoverSndScene(ISndSceneAccess, DataSourceNode)` | Recovers the scene (no AfterLoad hooks, no ClearAll) |
| `SaveGame(...)` | Collects all data into a `SaveGamePayload` |

## Design Decisions

### Why DataSourceNode output rather than direct JSON
Separates serialization from I/O. Neutral tree format supports any future codec.

### Why SaveContext holds both Progress and Session blackboard references
Process saves contain both levels. References (not copies) ensure latest state after BeforeSave hooks.

### Why SndSceneSerializer requires Array input

A SND scene is an entity array (`[entity1, entity2, ...]`), not an object. Validating the shape at the entry point catches errors early (e.g. accidentally passing `session.json` instead of `snd_scene.json`) instead of producing ambiguous behavior later in parsing.

### Why serializer does not trigger strategy hooks
BeforeSave and AfterLoad hooks uniformly orchestrated by `SessionRun` before/after serializer calls. Serializer focuses on data transformation.

---
[↑ Back to Save](../README.en.md)
