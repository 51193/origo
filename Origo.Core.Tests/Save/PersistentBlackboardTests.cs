using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

public class PersistentBlackboardTests
{
    private static (IFileMetaAccess metaAccess, IPathResolver pathResolver, IDataSourceIoGateway dataSourceIo, DataSourceConverterRegistry registry) CreateDeps(IFileSystem fs)
    {
        return (
            DataSourceFactory.CreateFileMetaAccess(fs),
            DataSourceFactory.CreatePathResolver(fs),
            TestFactory.CreateIoGateway(fs),
            TestFactory.CreateRegistry()
        );
    }

    [Fact]
    public void PersistentBlackboard_SetAndLoadFromDisk_Works()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, pathResolver, dataSourceIo, registry) = CreateDeps(fs);
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
        var fs = new TestMemoryFileSystem();
        var (metaAccess, pathResolver, dataSourceIo, registry) = CreateDeps(fs);
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());
        board.SetValue("x", 1);
        board.Clear();

        using var node = dataSourceIo.ReadTree(path);
        Assert.Equal(DataSourceNodeKind.Map, node.Kind);
        Assert.Empty(node.Keys);
    }

    [Fact]
    public void PersistentBlackboard_WriteUsesTempAndRename()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, pathResolver, dataSourceIo, registry) = CreateDeps(fs);
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());
        board.SetValue("k", 42);

        Assert.False(fs.Exists(path + ".tmp.json"), "Temp file should be cleaned up after successful atomic write.");
        Assert.True(fs.Exists(path));
    }

    [Fact]
    public void PersistentBlackboard_UpdatedValue_OverwritesViaAtomicRename()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, pathResolver, dataSourceIo, registry) = CreateDeps(fs);
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());

        board.SetValue("k", 1);
        board.SetValue("k", 2);

        var loaded = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());
        loaded.LoadFromDisk();
        var (found, v) = loaded.TryGet<int>("k");
        Assert.True(found);
        Assert.Equal(2, v);
    }

    [Fact]
    public void PersistentBlackboard_StaleTempFile_CleanedUpOnLoad()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, pathResolver, dataSourceIo, registry) = CreateDeps(fs);
        var path = "user://origo/system.json";
        var tmpPath = path + ".tmp.json";

        fs.WriteAllText(tmpPath, "{}", true);

        Assert.True(fs.Exists(tmpPath));

        var board = new PersistentBlackboard(metaAccess, pathResolver, path, dataSourceIo, registry, new Blackboard.Blackboard());
        board.LoadFromDisk();

        Assert.False(fs.Exists(tmpPath), "Stale temp file should be deleted on load.");
    }
}
