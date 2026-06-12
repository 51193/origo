using System;

namespace Origo.Core.Grid;

/// <summary>
///     Grid-to-world and world-to-grid coordinate conversion for axis-aligned square grids.
///     Grid coordinates are integer indices <c>[0, gridSize)</c>. The grid is centered at
///     world origin, with grid cell <c>(0, 0)</c> at the world-space minimum corner.
/// </summary>
public static class GridCoordinateSystem
{
    public static float GridToWorld(int gridCoord, float cellSize, int gridSize)
    {
        var halfWorld = gridSize * cellSize * 0.5f;
        return gridCoord * cellSize - halfWorld + cellSize * 0.5f;
    }

    public static float WorldToGrid(float worldCoord, float cellSize, int gridSize, out bool outOfBounds)
    {
        var halfWorld = gridSize * cellSize * 0.5f;
        var raw = (worldCoord + halfWorld) / cellSize;
        var gridCoord = (int)MathF.Floor(raw);
        outOfBounds = gridCoord < 0 || gridCoord >= gridSize;
        return gridCoord;
    }

    public static (float X, float Z) GridToWorld(GridPos pos, float cellSize, int gridSize)
    {
        return (
            GridToWorld(pos.X, cellSize, gridSize),
            GridToWorld(pos.Z, cellSize, gridSize)
        );
    }

    public static GridPos WorldToGrid(float worldX, float worldZ, float cellSize, int gridSize,
        out bool outOfBounds)
    {
        var gx = WorldToGrid(worldX, cellSize, gridSize, out var oobX);
        var gz = WorldToGrid(worldZ, cellSize, gridSize, out var oobZ);
        outOfBounds = oobX || oobZ;
        return new GridPos((int)gx, (int)gz);
    }
}
