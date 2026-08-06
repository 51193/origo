<!-- docsync-pair: Origo.Core/Save/Serialization/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
- **Serialize**: `blackboard.SerializeAll()` → registry.Write → DataSourceNode
- **DeserializeInto**: DataSourceNode → registry.Read → `blackboard.DeserializeAll()`

### SndSceneSerializer
- **Build**: `BuildMetaList()` → DataSourceNode (no BeforeSave hooks)
- **RecoverInto**: DataSourceNode (Array format) → `RecoverFromMetaList` (no AfterLoad hooks, no ClearAll)

### SaveContext
Core orchestration object held by `ProgressRun`. Provides `BuildSndScene(ISndSceneAccess)`, `RecoverSndScene(ISndSceneAccess, DataSourceNode)`, `SaveGame`.

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
