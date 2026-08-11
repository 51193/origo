<!-- docsync-pair: Origo.Core.Tests/Grid -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Grid Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Grid](../Origo.Core/Grid/README.en.md)

## Behavior Overview

Validates single-axis/dual-axis coordinate conversion (GridToWorld / WorldToGrid) in the grid coordinate system, GridPos value semantics, A* pathfinding algorithm, and GridParser coordinate string parsing.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `GridCoordinateSystemTests.cs` | Single-axis GridToWorld / WorldToGrid correctness and round-trip consistency |
| `GridPosTests.cs` | record struct equality, deconstruction |
| `GridCoordinateSystem2DTests.cs` | 2D convenience overloads: GridToWorld(GridPos) / WorldToGrid 2D round-trip |
| `AstarTests.cs` | A* pathfinding: direct path, around obstacles, unreachable, out-of-bounds, empty path |
| `GridParserTests.cs` | Coordinate parsing: valid input, invalid format, null, JsonElement |

## GridCoordinateSystemTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GridToWorld_Origin_CellCenter` | Origin grid coordinate converts to world cell center | Grid |
| `GridToWorld_MaxCoord_CellCenter` | Maximum grid coordinate conversion | Grid |
| `GridToWorld_Center_CellCenter` | Center grid coordinate conversion | Grid |
| `WorldToGrid_OriginMapsToZero` | World origin bounds map to grid 0 | Grid |
| `GridToWorld_WorldToGrid_RoundTrip` | Full grid round-trip consistency | Grid |
| `NonUnitCellSize_CorrectOffset` | Non-unit CellSize offset correct | Grid |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `WorldToGrid_OutOfBounds_ReportsTrue` | World coordinate far beyond grid bounds | `outOfBounds = true` |
| `GridToWorld_NonPositiveCellSize_Throws` | `cellSize <= 0` | `ArgumentOutOfRangeException` (fail-fast) |
| `GridToWorld_NonPositiveGridSize_Throws` | `gridSize <= 0` | `ArgumentOutOfRangeException` (fail-fast) |
| `WorldToGrid_NonPositiveCellSize_Throws` | `cellSize <= 0` | `ArgumentOutOfRangeException` (fail-fast) |
| `WorldToGrid_NonPositiveGridSize_Throws` | `gridSize <= 0` | `ArgumentOutOfRangeException` (fail-fast) |

## GridPosTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Equals_SameValues_True` | Same coordinate values equal | Grid |
| `Deconstruct_ReturnsComponents` | Deconstruction returns X / Z | Grid |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Equals_DifferentValues_False` | Different coordinate values | `== false` |

## GridCoordinateSystem2DTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GridToWorld2D_Origin_CellCenter` | 2D origin conversion | Grid |
| `GridToWorld2D_MaxCoord_CellCenter` | 2D max coordinate conversion (9,9) | Grid |
| `GridToWorld2D_DifferentAxes_Independent` | Different axes independent conversion | Grid |
| `WorldToGrid2D_RoundTrip` | 2D full grid round-trip | Grid |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `WorldToGrid2D_OutOfBounds_ReportsTrue` | Single axis out of bounds | `outOfBounds = true` |
| `WorldToGrid2D_PartialOutOfBounds_ReportsTrue` | One axis normal, one out of bounds | `outOfBounds = true` |

## AstarTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `FindPath_StartEqualsEnd_ReturnsEmptyPath` | Start equals end returns empty path | Grid/Astar |
| `FindPath_StraightLine_ReturnsDirectPath` | Straight line with no blocking returns direct path | Grid/Astar |
| `FindPath_Diagonal_ReturnsCorrectLength` | Diagonal path length correct | Grid/Astar |
| `FindPath_AroundObstacle_FindsPath` | Finds path around obstacles | Grid/Astar |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `FindPath_NonPositiveGridSize_Throws` | gridSize is 0 or negative | ArgumentOutOfRangeException |
| `FindPath_BlockedEndpoint_ReturnsNull` | Endpoint blocked | `null` |
| `FindPath_CompletelyBlocked_ReturnsNull` | Entire grid blocked | `null` |
| `FindPath_OutOfBounds_ReturnsNull` | Endpoint out of bounds | `null` |
| `FindPath_NoPathExists_ReturnsNull` | Completely enclosed by walls | `null` |

## GridParserTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ParseCoords_ValidString_ReturnsCoords` | `"3,5"` parsed to (3, 5) | Grid/GridParser |
| `ParseCoords_WithSpaces_Trims` | Whitespace tolerant | Grid/GridParser |
| `ParseCoords_NegativeValues_Works` | Negative coordinates | Grid/GridParser |
| `ParseCoords_JsonElement_Works` | JsonElement input | Grid/GridParser |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ParseCoords_InvalidFormat_ReturnsNull` | `"abc"`, `"3"`, empty string | `null` |
| `ParseCoords_NullInput_ReturnsNull` | null input | `null` |
| `ParseCoords_JsonElement_NumberKind_ReturnsNull` | JsonElement of number kind (42) | `null` |
| `ParseCoords_JsonElement_TrueKind_ReturnsNull` | JsonElement of boolean kind (true) | `null` |
| `ParseCoords_JsonElement_NullKind_ReturnsNull` | JsonElement of null kind | `null` |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| A* performance boundaries on large grids (10000×10000) | Extreme size performance | Grid/Astar |
| WorldToGrid 2D per-axis independent non-square grid | Non-square grid | Grid |
| GridCoordinateSystem integer overflow protection | Very large gridSize × cellSize | Grid |

---

[↑ Back to Origo.Core.Tests](README.en.md)
