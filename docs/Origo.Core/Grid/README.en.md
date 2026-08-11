<!-- docsync-pair: Origo.Core/Grid/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Grid

> [↑ Back to Origo.Core](../README.en.md)

## Overview

Generic grid utility set. Provides grid coordinate types, bidirectional world coordinate conversion, A* pathfinding, coordinate parsing, and more. Usable by any square-grid-based game (e.g., tactical, roguelike, sandbox).

## Included Files

| File | Responsibility |
|------|---------------|
| `GridPos.cs` | `readonly record struct`, represents 2D integer grid coordinates |
| `GridCoordinateSystem.cs` | Single-axis / dual-axis GridToWorld / WorldToGrid coordinate conversion |
| `Astar.cs` | Generic A* pathfinding, accepts a `Func<GridPos, bool>` blocking detection delegate |
| `GridParser.cs` | Coordinate string parsing (`"x,z"` format + `JsonElement`) |

## Implementation Details

### GridPos

```csharp
public readonly record struct GridPos(int X, int Z);
```

Value type, automatically gains equality comparison, deconstruction, `ToString()`. Serves as the unified carrier for all grid coordinates within the framework.

### GridCoordinateSystem

All static methods, stateless:

- **GridToWorld(int gridCoord, float cellSize, int gridSize)**: Single-axis conversion, converts grid integer coordinates to world center coordinates
- **WorldToGrid(float worldCoord, float cellSize, int gridSize, out bool outOfBounds)**: Single-axis inverse conversion, floors the value
- **GridToWorld(GridPos pos, float cellSize, int gridSize)**: 2D convenience overload, returns `(float X, float Z)` tuple
- **WorldToGrid(float worldX, float worldZ, float cellSize, int gridSize, out bool outOfBounds)**: 2D inverse conversion, returns `GridPos`

`cellSize` and `gridSize` must be positive, otherwise `ArgumentOutOfRangeException` is thrown (fail-fast, preventing division-by-zero NaN coordinates).

Formula (single-axis): `worldCoord = gridCoord * cellSize - (gridSize * cellSize) / 2 + cellSize * 0.5f`.

### Astar

```csharp
public static List<GridPos>? FindPath(GridPos start, GridPos end, int gridSize, Func<GridPos, bool> isBlocked)
```

Standard A* search (**4-direction neighbors**: up/down/left/right; diagonals are not traversable), using the Euclidean distance heuristic. Returns a path list excluding the starting point; returns `null` when no feasible path exists.

- `isBlocked` is a `Func<GridPos, bool>` delegate; callers assemble blocking detection logic (e.g., combining terrain blocking + dynamic blocking)
- `gridSize` must be positive, otherwise `ArgumentOutOfRangeException` (consistent with `GridCoordinateSystem.ValidateDimensions` — a non-positive grid must not be treated as a valid input)
- Automatically validates whether the start/endpoint is out-of-bounds; a blocked endpoint returns `null` (the start cell is the entity's current position and is not checked for blocking — standard A* semantics)

### GridParser

```csharp
public static (int X, int Z)? ParseCoords(object? input)
```

Parses `"x,z"` format coordinate strings. Supports `string` and `JsonElement` input, tolerates whitespace; returns `null` for invalid input.

## Design Decisions

### Why single-axis rather than dual-axis interface

Single-axis conversion (`GridToWorld(int, float, int)`) is more flexible than dual-axis (`GridToWorld(int, int, int, float, out float, out float)`): different axes can have different cellSizes (e.g., non-square grids), no out parameter is needed, and the return value is a single float that directly participates in Godot Vector3 construction.

### Why 2D convenience overloads are also provided

In practice, the vast majority of call sites use the same cellSize and gridSize and simultaneously convert X and Z. Providing `GridToWorld(GridPos, float, int)` eliminates the repetitive boilerplate of `GridToWorld(gx, ...)` + `GridToWorld(gz, ...)`. The single-axis methods remain as the primary API; the 2D overloads are pure convenience.

### Why isBlocked uses a delegate rather than a collection

`Func<GridPos, bool>` is more general than `HashSet<GridPos>`: callers can compose multiple blocking sources (terrain data + dynamic blocking) without pre-merging into a single collection. Migration cost is low — existing `HashSet<GridPos>` users only need to pass `p => set.Contains(p)`.

### Why not in the Snd namespace

Coordinate conversion and pathfinding are general geometric utilities that do not depend on the SND entity model. An independent namespace allows use in any game logic (including non-Snd systems).

> **Consumption note**: the Grid module has no production consumer inside the Core repository (test-only usage) — it is provided as a framework capability for game-side consumption (e.g. origo.demo).

---

[↑ Back to Origo.Core](../README.en.md)
