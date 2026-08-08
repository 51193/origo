using System;
using Origo.Core.Grid;
using Xunit;

namespace Origo.Core.Tests;

public class GridCoordinateSystemTests
{
    [Fact]
    public void GridToWorld_Origin_CellCenter()
    {
        var cellSize = 1f;
        var gridSize = 10;
        var result = GridCoordinateSystem.GridToWorld(0, cellSize, gridSize);
        Assert.Equal(-4.5f, result, 0.001f);
    }

    [Fact]
    public void GridToWorld_MaxCoord_CellCenter()
    {
        var cellSize = 1f;
        var gridSize = 10;
        var result = GridCoordinateSystem.GridToWorld(9, cellSize, gridSize);
        Assert.Equal(4.5f, result, 0.001f);
    }

    [Fact]
    public void GridToWorld_Center_CellCenter()
    {
        var cellSize = 1f;
        var gridSize = 10;
        var result = GridCoordinateSystem.GridToWorld(5, cellSize, gridSize);
        Assert.Equal(0.5f, result, 0.001f);
    }

    [Fact]
    public void WorldToGrid_OriginMapsToZero()
    {
        var cellSize = 1f;
        var gridSize = 10;
        var halfWorld = gridSize * cellSize * 0.5f;
        var result = GridCoordinateSystem.WorldToGrid(-halfWorld, cellSize, gridSize, out var oob);
        Assert.Equal(0, result);
        Assert.False(oob);
    }

    [Fact]
    public void WorldToGrid_OutOfBounds_ReportsTrue()
    {
        var cellSize = 1f;
        var gridSize = 10;
        GridCoordinateSystem.WorldToGrid(100f, cellSize, gridSize, out var oob);
        Assert.True(oob);
    }

    [Fact]
    public void GridToWorld_WorldToGrid_RoundTrip()
    {
        var cellSize = 2f;
        var gridSize = 16;
        for (var gx = 0; gx < gridSize; gx++)
        {
            var wx = GridCoordinateSystem.GridToWorld(gx, cellSize, gridSize);
            var result = GridCoordinateSystem.WorldToGrid(wx, cellSize, gridSize, out var oob);
            Assert.False(oob);
            Assert.Equal(gx, result);
        }
    }

    [Fact]
    public void NonUnitCellSize_CorrectOffset()
    {
        var cellSize = 0.5f;
        var gridSize = 4;
        var result = GridCoordinateSystem.GridToWorld(0, cellSize, gridSize);
        Assert.Equal(-0.75f, result, 0.001f);
    }

    [Fact]
    public void GridToWorld_NonPositiveCellSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.GridToWorld(0, 0f, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.GridToWorld(0, -1f, 10));
    }

    [Fact]
    public void GridToWorld_NonPositiveGridSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.GridToWorld(0, 1f, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.GridToWorld(0, 1f, -5));
    }

    [Fact]
    public void WorldToGrid_NonPositiveCellSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.WorldToGrid(0f, 0f, 10, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.WorldToGrid(0f, -1f, 10, out _));
    }

    [Fact]
    public void WorldToGrid_NonPositiveGridSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.WorldToGrid(0f, 1f, 0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCoordinateSystem.WorldToGrid(0f, 1f, -5, out _));
    }
}
