using Origo.Core.DataSource;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

public class PersistentBlackboardTests
{
    [Fact]
    public void PersistentBlackboard_SetAndLoadFromDisk_Works()
    {
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var registry = TestFactory.CreateRegistry();
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(fs, path, io, registry, new Blackboard.Blackboard());

        board.Set("n", 7);
        var loaded = new PersistentBlackboard(fs, path, io, registry, new Blackboard.Blackboard());
        loaded.LoadFromDisk();
        var (found, n) = loaded.TryGet<int>("n");

        Assert.True(found);
        Assert.Equal(7, n);
    }

    [Fact]
    public void PersistentBlackboard_Clear_PersistsEmptyData()
    {
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var registry = TestFactory.CreateRegistry();
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(fs, path, io, registry, new Blackboard.Blackboard());
        board.Set("x", 1);
        board.Clear();

        using var node = io.ReadTree(path);
        Assert.Equal(DataSourceNodeKind.Object, node.Kind);
        Assert.Empty(node.Keys);
    }
}
