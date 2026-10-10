# Meta (Kernel)

> [↑ Back to Save](../README.en.md)

## Overview
Display-metadata merge implementation for `meta.map`: the delegate adapter and the merger that runs the registered contributors in registration order. The contributor contract, build context, read-only blackboard adapter, and slot entry model live in [Origo.Core.Contracts/Save/Meta](../../../Origo.Core.Contracts/Save/Meta/README.en.md).

## Included Files

| File | Responsibility |
|------|------|
| `DelegateSaveMetaContributor.cs` | `internal` `ISaveMetaContributor` adapter for a `Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>>` delegate |
| `SaveMetaMerger.cs` | `internal` static merge: contributor outputs in registration order, later same-name keys override earlier ones |

## Module Details

### Metadata Contribution Flow
1. Contributors registered through `ISndSaveOperations.RegisterSaveMetaContributor()` are kept in registration order
2. At save time the framework creates the `SaveMetaBuildContext` (SaveId, CurrentLevelId, read-only progress/session blackboards, read-only `ISndSceneReadAccess`)
3. `SaveMetaMerger.Merge()` calls each contributor in order; later overwrites earlier for same keys; returns null when no keys are present
4. Persist: the merged dictionary is converted to a JSON DataSourceNode tree via `BuildStringMapNode()` and written to `meta.map` by `SavePayloadWriter`

### SaveMetaMerger

Static utility class. Merge logic: iterate contributors → contribute in registration order, later same-name keys overwrite earlier ones → return null when no keys are present. A contributor output that violates the interface contract (null dictionary, blank key, or null value) throws `InvalidOperationException` immediately, so invalid metadata fails the save instead of degrading silently.

### DelegateSaveMetaContributor

Adapts a delegate to the contributor contract so a caller can register a metadata contributor without declaring a dedicated type; the constructor rejects a null delegate and `Contribute` forwards the context unchanged.

## Design Decisions

### Why contributors overwrite by registration order
Different contributors may have different perspectives on the same key. Ordered overwrite provides predictable priority.

### Why invalid contributor output must fail the save

The contributor interface promises mergeable key-value pairs; silently skipping a null dictionary, blank key, or null value would disguise an implementation bug as "no metadata" and make the offending contributor impossible to identify. Saves are a strict-validation path, so `SaveMetaMerger` throws with contributor-type context and preserves fail-fast semantics.

### Why a delegate adapter exists

`RegisterSaveMetaContributor` accepts both an `ISaveMetaContributor` and a delegate; the delegate overload lets a caller register contribution logic without declaring a dedicated type, while the merger still works with a uniform `ISaveMetaContributor` list.

---
[↑ Back to Save](../README.en.md)
