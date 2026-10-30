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
3. `SaveMetaMerger.Merge()` calls each contributor in order; later overwrites earlier for same keys; returns null when no keys are present. Each entry is validated against the `.map` round-trip rules before it is merged, so an entry the format cannot store fails the save while merging — before the payload builder or payload writer runs
4. Persist: the merged dictionary is converted to a JSON DataSourceNode tree via `BuildStringMapNode()` and written to `meta.map` by `SavePayloadWriter`

### SaveMetaMerger

Static utility class. Merge logic: iterate contributors → validate each entry (`MapTextFormat.FindKeyError`/`FindValueError`: non-empty keys without leading `#`, `:`, line breaks, edge whitespace, or unpaired UTF-16 surrogates; values without line breaks, edge whitespace, or unpaired surrogates) → contribute in registration order, later same-name keys overwrite earlier ones → return null when no keys are present. `SaveMetaMerger` throws `InvalidOperationException` immediately on invalid contributor output, including the contributing type in the message and before any save file is written; the rationale is owned by the contributor contract in [Why invalid contributor output must fail the save](../../../Origo.Core.Contracts/Save/Meta/README.en.md#why-invalid-contributor-output-must-fail-the-save). The shared rules live in `Origo.Core.Kernel/DataSource/MapTextFormat.cs` and are also applied by the `.map` codec itself.

### DelegateSaveMetaContributor

Adapts a delegate to the contributor contract so a caller can register a metadata contributor without declaring a dedicated type; the constructor rejects a null delegate and `Contribute` forwards the context unchanged.

## Design Decisions

### Why map-unescapable metadata fails during merging

`meta.map` is a line-based `key: value` format whose decoder trims fields, splits on the first colon, and treats a leading `#` as a comment. A contributor key like `player:name`, a padded value, or a line break cannot round-trip through it. Validating such entries only in the codec would fail deep inside the payload writer: `progress.json` and `progress_state_machines.json` would already be written, the `current/` marker would be present, and the error would name `meta.map` rather than the offending contributor. The merger therefore applies the same shared rules before the payload is built, with the contributor type in the message, so the save fails with no disk side effects and the user can fix the contributor.

### Why contributors overwrite by registration order
Different contributors may have different perspectives on the same key. Ordered overwrite provides predictable priority.

### Why a delegate adapter exists

`RegisterSaveMetaContributor` accepts both an `ISaveMetaContributor` and a delegate; the delegate overload lets a caller register contribution logic without declaring a dedicated type, while the merger still works with a uniform `ISaveMetaContributor` list.

---
[↑ Back to Save](../README.en.md)
