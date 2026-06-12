using Origo.Core.Grid;
using Xunit;

namespace Origo.Core.Tests;

public class GridPosTests
{
    [Fact]
    public void Equals_SameValues_True()
    {
        var a = new GridPos(1, 2);
        var b = new GridPos(1, 2);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_DifferentValues_False()
    {
        var a = new GridPos(1, 2);
        var b = new GridPos(1, 3);
        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    [Fact]
    public void Deconstruct_ReturnsComponents()
    {
        var pos = new GridPos(3, 7);
        var (x, z) = pos;
        Assert.Equal(3, x);
        Assert.Equal(7, z);
    }
}
