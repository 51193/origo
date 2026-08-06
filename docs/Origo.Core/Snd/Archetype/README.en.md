<!-- docsync-pair: Origo.Core/Snd/Archetype/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Archetype

> [↑ Back to Snd](../README.en.md)

## Design Positioning
Archetype is Origo's **numeric externalization tool** — extracting entity attribute values into independent flat key-value files for strategies to load on demand.

- Archetype contains no behavior definitions (no strategy indices, no node references)
- Archetype has no mandatory mapping with Templates: entities may use zero, one, or multiple recipes
- Loading timing and quantity are entirely determined by strategy logic

## Overview
Numeric recipe file loading and application utilities. Parses flat key-value Archetype definition files, infers string values to appropriate types (int/long/float/bool/string), and writes to entity data.

## Included Files

| File | Responsibility |
|------|------|
| `SndArchetypeLoader.cs` | TryLoad (read from .map) + ApplyAttributes (type inference → entity data) |

## Implementation Details

### SndArchetypeLoader
- **TryLoad(ISndFileAccess fileAccess, string path)**: Reads .map via framework file abstraction, returns `Dictionary<string, string>`. Returns false if the file is missing, the format is incorrect, or the map is empty (no attribute keys).
- **ApplyAttributes(ISndEntity entity, Dictionary<string, string> attributes)**: Parses each value in order: int → long → float → bool → string.

Type inference rules:
1. `int.TryParse` (InvariantCulture) → `int`
2. `long.TryParse` (InvariantCulture) → `long` (avoid precision loss)
3. `float.TryParse` (InvariantCulture) → `float`
4. `bool.TryParse` → `bool`
5. Fall through → `string`

## Design Decisions

### Why strict type inference order
`.map` format has no type metadata. The fallback order matches most common game data scenarios. For precise type control, use JSON templates.

### Why standalone Snd submodule
Output targets `ISndEntity` data writing, directly serving SND workflow. DataSource is general-purpose and should not contain SND bindings.

---
[↑ Back to Snd](../README.en.md)
