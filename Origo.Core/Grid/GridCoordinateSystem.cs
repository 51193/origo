using System;

namespace Origo.Core.Grid;

/// <summary>
///     Grid-to-world and world-to-grid coordinate conversion for axis-aligned square grids.
///     Grid coordinates are integer indices <c>[0, gridSize)</c>. The grid is centered at
///     world origin, with grid cell <c>(0, 0)</c> at the world-space minimum corner.
/// </summary>
public static class GridCoordinateSystem
{
    /// <summary>Converts a single grid coordinate to its world-space position (grid centered at world origin).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellSize" /> or <paramref name="gridSize" /> is not positive.</exception>
    public static float GridToWorld(int gridCoord, float cellSize, int gridSize)
    {
        ValidateDimensions(cellSize, gridSize);
        var halfWorld = gridSize * cellSize * 0.5f;
        return gridCoord * cellSize - halfWorld + cellSize * 0.5f;
    }

    /// <summary>
    ///     Converts a world-space position to its grid coordinate.
    ///     Returns the index of the containing cell (floor-based); <paramref name="outOfBounds" /> is
    ///     set when the result falls outside <c>[0, gridSize)</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellSize" /> or <paramref name="gridSize" /> is not positive.</exception>
    public static float WorldToGrid(float worldCoord, float cellSize, int gridSize, out bool outOfBounds)
    {
        ValidateDimensions(cellSize, gridSize);
        var halfWorld = gridSize * cellSize * 0.5f;
        var raw = (worldCoord + halfWorld) / cellSize;
        var gridCoord = (int)MathF.Floor(raw);
        outOfBounds = gridCoord < 0 || gridCoord >= gridSize;
        return gridCoord;
    }

    /// <summary>Converts a <see cref="GridPos" /> to world-space X/Z coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellSize" /> or <paramref name="gridSize" /> is not positive.</exception>
    public static (float X, float Z) GridToWorld(GridPos pos, float cellSize, int gridSize)
    {
        ValidateDimensions(cellSize, gridSize);
        return (
            GridToWorld(pos.X, cellSize, gridSize),
            GridToWorld(pos.Z, cellSize, gridSize)
        );
    }

    /// <summary>
    ///     Converts world-space X/Z to a <see cref="GridPos" />; <paramref name="outOfBounds" /> is set
    ///     when either axis falls outside the grid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellSize" /> or <paramref name="gridSize" /> is not positive.</exception>
    public static GridPos WorldToGrid(float worldX, float worldZ, float cellSize, int gridSize,
        out bool outOfBounds)
    {
        ValidateDimensions(cellSize, gridSize);
        var gx = WorldToGrid(worldX, cellSize, gridSize, out var oobX);
        var gz = WorldToGrid(worldZ, cellSize, gridSize, out var oobZ);
        outOfBounds = oobX || oobZ;
        return new GridPos((int)gx, (int)gz);
    }

    private static void ValidateDimensions(float cellSize, int gridSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize,
                "Cell size must be a finite positive number.");
        if (gridSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(gridSize), gridSize,
                "Grid size must be positive.");
    }
}
