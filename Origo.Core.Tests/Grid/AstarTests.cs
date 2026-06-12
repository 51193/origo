using System.Collections.Generic;
using Origo.Core.Grid;
using Xunit;

namespace Origo.Core.Tests;

public class AstarTests
{
    [Fact]
    public void FindPath_StartEqualsEnd_ReturnsEmptyPath()
    {
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(0, 0), 10,
            _ => false);
        Assert.NotNull(path);
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_StraightLine_ReturnsDirectPath()
    {
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(3, 0), 10,
            _ => false);
        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
    }

    [Fact]
    public void FindPath_Diagonal_ReturnsCorrectLength()
    {
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(2, 2), 10,
            _ => false);
        Assert.NotNull(path);
        Assert.Equal(4, path!.Count);
    }

    [Fact]
    public void FindPath_AroundObstacle_FindsPath()
    {
        var blocked = new HashSet<GridPos> { new(1, 1), new(1, 2), new(2, 1) };
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(2, 2), 5,
            p => blocked.Contains(p));
        Assert.NotNull(path);
        Assert.True(path!.Count > 2);
    }

    [Fact]
    public void FindPath_BlockedEndpoint_ReturnsNull()
    {
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(1, 1), 5,
            p => p == new GridPos(1, 1));
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_CompletelyBlocked_ReturnsNull()
    {
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(3, 3), 5,
            _ => true);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_OutOfBounds_ReturnsNull()
    {
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(100, 0), 10,
            _ => false);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_NoPathExists_ReturnsNull()
    {
        var blocked = new HashSet<GridPos>
        {
            new(0, 1), new(1, 0), new(1, 1)
        };
        var path = Astar.FindPath(new GridPos(0, 0), new GridPos(1, 2), 3,
            p => blocked.Contains(p));
        Assert.Null(path);
    }
}
