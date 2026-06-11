using Origo.Core.Abstractions.FileSystem;
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
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var dataSourceIo = TestFactory.CreateIoGateway(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var registry = TestFactory.CreateRegistry();
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());

        board.SetValue("n", 7);
        var loaded = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());
        loaded.LoadFromDisk();
        var (found, n) = loaded.TryGet<int>("n");

        Assert.True(found);
        Assert.Equal(7, n);
    }

    [Fact]
    public void PersistentBlackboard_Clear_PersistsEmptyData()
    {
        var fs = new TestFileSystem();
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var dataSourceIo = TestFactory.CreateIoGateway(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var registry = TestFactory.CreateRegistry();
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());
        board.SetValue("x", 1);
        board.Clear();

        using var node = dataSourceIo.ReadTree(path);
        Assert.Equal(DataSourceNodeKind.Map, node.Kind);
        Assert.Empty(node.Keys);
    }
}
