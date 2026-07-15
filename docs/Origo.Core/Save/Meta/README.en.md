<!-- docsync-pair: Origo.Core/Save/Meta/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
| `SaveMetaDataEntry.cs` | Save slot entry model |
| `SaveMetaMerger.cs` | Merge logic: contributors + overrides |

## Module Details

### Metadata Contribution Flow
1. Register contributors via `ISndSaveOperations.RegisterSaveMetaContributor()`
2. Create `SaveMetaBuildContext` at save time
3. `SaveMetaMerger.Merge()` calls each contributor in order; later overwrites earlier for same keys
4. Caller-provided `customMeta` overrides again
5. Written to `meta.map` by `SavePayloadWriter`

### ISaveMetaContributor
```csharp
IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context);
```

Contributors return independent dictionaries. `SaveMetaMerger` merges in registration order.

## Design Decisions

### Why display metadata is separated from business data
Business data (progress.json) contains complete state, potentially MB-scale. Display metadata is KB-scale, enabling fast save listing without full parse.

### Why contributors overwrite by registration order
Different contributors may have different perspectives on the same key. Ordered overwrite provides predictable priority.

### Why contributors return independent dictionaries
Prevents contributors from calling `Clear()` or `Remove()` on a shared mutable target. Isolation via `IReadOnlyDictionary`.

### Why SaveMetaBuildContext is a readonly struct
Avoids heap allocation on the save call tree. `readonly` prevents side-effect leakage. `in` parameter for ref passing.

---
[↑ Back to Save](../README.en.md)
