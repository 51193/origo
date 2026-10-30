# Meta

> [↑ Back to Save](../README.en.md)

## Overview
Display-metadata contracts for `meta.map` — the save-selection metadata separated from business data: the contributor contract, the read-only build context handed to contributors, the read-only blackboard adapter, and the save-slot entry model. The merge implementation and the delegate adapter live in [Origo.Core.Kernel/Save/Meta](../../../Origo.Core.Kernel/Save/Meta/README.en.md).

## Included Files

| File | Responsibility |
|------|------|
| `ISaveMetaContributor.cs` | Contributor contract: `Contribute(in SaveMetaBuildContext)` returns this contributor's own key-value dictionary for display metadata |
| `SaveMetaBuildContext.cs` | Read-only `readonly struct` context of one save operation: `SaveId`, `CurrentLevelId`, read-only `Progress`/`Session` blackboards, and `SceneAccess` |
| `ReadOnlyBlackboard.cs` | `internal` read-only `IBlackboard` adapter: reads pass through, every mutation throws `InvalidOperationException` |
| `SaveMetaDataEntry.cs` | Save-slot entry model: `SaveId` plus the display metadata dictionary read from that slot's `meta.map` for save listing |

## Module Details

### Metadata Contribution Flow
1. Register contributors via `ISndSaveOperations.RegisterSaveMetaContributor()`
2. Create `SaveMetaBuildContext` at save time (SaveId, CurrentLevelId, blackboards, read-only `ISndSceneReadAccess`); the Progress/Session blackboards are wrapped in `ReadOnlyBlackboard`, so `SetValue`/`Clear`/`DeserializeAll` throw `InvalidOperationException` immediately
3. The framework merger combines the contributor dictionaries in registration order; later contributors overwrite earlier ones for same-name keys
4. Persist: the merged dictionary is converted to a JSON DataSourceNode tree and written to `meta.map`

### ISaveMetaContributor
```csharp
IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context);
```

Contributors return independent dictionaries and must not mutate the context beyond reading it. Returning a null dictionary, a blank key, or a null value throws `InvalidOperationException` with contributor-type context instead of silently dropping entries. Keys and values must also round-trip through the line-based `meta.map` format: keys must be non-empty, must not start with `#`, must not contain `:` or line breaks, must not have leading/trailing whitespace, and must be well-formed UTF-16; values must not contain line breaks, must not have leading/trailing whitespace, and must be well-formed UTF-16. Violations throw `InvalidOperationException` naming the contributor while display metadata is merged, before any save file is written.

### SaveMetaBuildContext

`readonly struct` carrying `SaveId`, `CurrentLevelId`, the read-only `Progress` and `Session` blackboards, and the read-only `ISndSceneReadAccess` for scene queries. The `in` parameter avoids copying on the save call tree.

### SaveMetaDataEntry

Carries the slot ID and the display metadata read from that slot's `meta.map`, as produced for save listing through `ISndSaveOperations.ListSavesWithMetaData()`.

## Design Decisions

### Why display metadata is separated from business data
Business data (progress.json) contains complete state, potentially MB-scale. Display metadata is KB-scale, enabling fast save listing without full parse.

### Why contributors return independent dictionaries
Prevents contributors from calling `Clear()` or `Remove()` on a shared mutable target. Isolation via `IReadOnlyDictionary`.

### Why invalid contributor output must fail the save

The contributor interface promises mergeable key-value pairs; silently skipping a null dictionary, blank key, or null value would disguise an implementation bug as "no metadata" and make the offending contributor impossible to identify. The same applies to entries the line-based `meta.map` format cannot round-trip (a `:` in a key, a padded value, a line break): leaving them to the codec would surface a format error after `current/` was partially written and without contributor context. Saves are a strict-validation path, so the merger throws with contributor-type context before any file is written and preserves fail-fast semantics.

### Why SaveMetaBuildContext is a readonly struct
Avoids heap allocation on the save call tree. `readonly` prevents side-effect leakage. `in` parameter for ref passing.

### Why the context blackboards are read-only adapters
Contributors only produce display metadata, so the context wraps the progress/session blackboards in `ReadOnlyBlackboard`; a mutation attempt fails immediately instead of silently changing saved game state.

---
[↑ Back to Save](../README.en.md)
