using Origo.Core.Grid;
using Xunit;

namespace Origo.Core.Tests;

public class GridCoordinateSystem2DTests
{
    [Fact]
    public void GridToWorld2D_Origin_CellCenter()
    {
        var (wx, wz) = GridCoordinateSystem.GridToWorld(new GridPos(0, 0), 1f, 10);
        Assert.Equal(-4.5f, wx, 0.001f);
        Assert.Equal(-4.5f, wz, 0.001f);
    }

    [Fact]
    public void GridToWorld2D_MaxCoord_CellCenter()
    {
        var (wx, wz) = GridCoordinateSystem.GridToWorld(new GridPos(9, 9), 1f, 10);
        Assert.Equal(4.5f, wx, 0.001f);
        Assert.Equal(4.5f, wz, 0.001f);
    }

    [Fact]
    public void GridToWorld2D_DifferentAxes_Independent()
    {
        var (wx, wz) = GridCoordinateSystem.GridToWorld(new GridPos(2, 7), 1f, 10);
        Assert.Equal(-2.5f, wx, 0.001f);
        Assert.Equal(2.5f, wz, 0.001f);
    }

    [Fact]
    public void WorldToGrid2D_RoundTrip()
    {
        var cellSize = 2f;
        var gridSize = 16;
        for (var gx = 0; gx < gridSize; gx++)
        for (var gz = 0; gz < gridSize; gz++)
        {
            var (wx, wz) = GridCoordinateSystem.GridToWorld(new GridPos(gx, gz), cellSize, gridSize);
            var result = GridCoordinateSystem.WorldToGrid(wx, wz, cellSize, gridSize, out var oob);
            Assert.False(oob);
            Assert.Equal(new GridPos(gx, gz), result);
        }
    }

    [Fact]
    public void WorldToGrid2D_OutOfBounds_ReportsTrue()
    {
        GridCoordinateSystem.WorldToGrid(100f, 0f, 1f, 10, out var oob);
        Assert.True(oob);
    }

    [Fact]
    public void WorldToGrid2D_PartialOutOfBounds_ReportsTrue()
    {
        var (wx, wz) = GridCoordinateSystem.GridToWorld(new GridPos(5, 5), 1f, 10);
        GridCoordinateSystem.WorldToGrid(wx, 1000f, 1f, 10, out var oob);
        Assert.True(oob);
    }
}
