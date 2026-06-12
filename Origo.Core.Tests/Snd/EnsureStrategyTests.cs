using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class EnsureStrategyTests
{
    [Fact]
    public void EnsureStrategy_DataKeyMissing_SetsDataAndReturnsTrue()
    {
        var entity = new DummySndEntity("test");
        var result = entity.EnsureStrategy("character.path_impl", "character.path.astar");

        Assert.True(result);
        var (found, val) = entity.TryGetData<string>("character.path_impl");
        Assert.True(found);
        Assert.Equal("character.path.astar", val);
    }

    [Fact]
    public void EnsureStrategy_DataKeyExistsWithValue_ReturnsFalse()
    {
        var entity = new DummySndEntity("test");
        entity.SetData("character.path_impl", "character.path.direct");

        var result = entity.EnsureStrategy("character.path_impl", "character.path.astar");

        Assert.False(result);
        var (_, val) = entity.TryGetData<string>("character.path_impl");
        Assert.Equal("character.path.direct", val);
    }

    [Fact]
    public void EnsureStrategy_DataKeyExistsButEmpty_StillSetsAndReturnsTrue()
    {
        var entity = new DummySndEntity("test");
        entity.SetData("character.path_impl", "");

        var result = entity.EnsureStrategy("character.path_impl", "character.path.astar");

        Assert.True(result);
        var (_, val) = entity.TryGetData<string>("character.path_impl");
        Assert.Equal("character.path.astar", val);
    }
}
