<!-- docsync-pair: Origo.Core/Save/Meta/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Meta

> [↑ Back to Save](../README.en.md)

## Overview
Save display metadata (meta.map) building and merging system. Display metadata is separated from business data, used only for save selection UI. Collected via a pluggable contributor pattern.

## Included Files

| File | Responsibility |
|------|------|
| `ISaveMetaContributor.cs` | Metadata contributor interface |
| `DelegateSaveMetaContributor.cs` | Delegate-adapted contributor |
| `SaveMetaBuildContext.cs` | Read-only build context |
| `ReadOnlyBlackboard.cs` | Read-only blackboard adapter: reads pass through, every mutation throws `InvalidOperationException` |
| `SaveMetaDataEntry.cs` | Save slot entry model |
| `SaveMetaMerger.cs` | Merge logic: merges contributor outputs in registration order |

## Module Details

### Metadata Contribution Flow
1. Register contributors via `ISndSaveOperations.RegisterSaveMetaContributor()`
2. Create `SaveMetaBuildContext` at save time (saveId, levelId, blackboards, read-only `ISndSceneReadAccess`); the Progress/Session blackboards are wrapped in read-only adapters, so `SetValue`/`Clear`/`DeserializeAll` throw `InvalidOperationException` immediately
3. `SaveMetaMerger.Merge()` calls each contributor in order; later overwrites earlier for same keys
4. Persist: the merged dictionary is converted to a JSON DataSourceNode tree via `BuildStringMapNode()` and written to `meta.map` by `SavePayloadWriter`

### ISaveMetaContributor
```csharp
IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context);
```

Contributors return independent dictionaries. `SaveMetaMerger` merges in registration order. Returning a null dictionary, a blank key, or a null value throws `InvalidOperationException` with contributor-type context instead of silently dropping entries.

### SaveMetaMerger

Static utility class. Merge logic: iterate contributors → contribute in registration order, later same-name keys overwrite earlier ones → return null when no keys are present. A contributor output that violates the interface contract (null dictionary, blank key, or null value) throws `InvalidOperationException` immediately, so invalid metadata fails the save instead of degrading silently.

## Design Decisions

### Why display metadata is separated from business data
Business data (progress.json) contains complete state, potentially MB-scale. Display metadata is KB-scale, enabling fast save listing without full parse.

### Why contributors overwrite by registration order
Different contributors may have different perspectives on the same key. Ordered overwrite provides predictable priority.

### Why invalid contributor output must fail the save

The contributor interface promises mergeable key-value pairs; silently skipping a null dictionary, blank key, or null value would disguise an implementation bug as "no metadata" and make the offending contributor impossible to identify. Saves are a strict-validation path, so `SaveMetaMerger` throws with contributor-type context and preserves fail-fast semantics.

### Why contributors return independent dictionaries
Prevents contributors from calling `Clear()` or `Remove()` on a shared mutable target. Isolation via `IReadOnlyDictionary`.

### Why SaveMetaBuildContext is a readonly struct
Avoids heap allocation on the save call tree. `readonly` prevents side-effect leakage. `in` parameter for ref passing.

---
[↑ Back to Save](../README.en.md)
