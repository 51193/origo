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
}
