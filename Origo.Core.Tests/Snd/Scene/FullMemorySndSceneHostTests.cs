using System;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

public class FullMemorySndSceneHostTests
{
    private static FullMemorySndSceneHost CreateBoundHost()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        return host;
    }

    private static SndMetaData CreateMeta(string name) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData(),
        DataMetaData = new DataMetaData()
    };

    [Fact]
    public void CreateEntity_NullMeta_ThrowsArgumentNull()
    {
        var host = CreateBoundHost();

        var ex = Assert.Throws<ArgumentNullException>(() => host.CreateEntity(null!));
        Assert.Equal("metaData", ex.ParamName);
    }

    [Fact]
    public void CreateEntity_BeforeBindWorld_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            host.CreateEntity(CreateMeta("E")));
        Assert.Contains("SndWorld", ex.Message);
    }

    [Fact]
    public void CreateEntity_BeforeBindContext_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            host.CreateEntity(CreateMeta("E")));
        Assert.Contains("ISndContext", ex.Message);
    }

    [Fact]
    public void CreateEntity_ReturnsEntityAndAddsToCollection()
    {
        var host = CreateBoundHost();

        var entity = host.CreateEntity(CreateMeta("E"));

        Assert.NotNull(entity);
        Assert.Equal("E", entity.Name);
        var found = host.FindByName("E");
        Assert.NotNull(found);
        Assert.Same(entity, found);
        Assert.Single(host.GetEntities());
    }

    [Fact]
    public void RemoveEntity_NonexistentName_ThrowsInvalidOperation()
    {
        var host = CreateBoundHost();

        var ex = Assert.Throws<InvalidOperationException>(() => host.RemoveEntity("nonexistent"));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void RemoveEntity_ExistingName_RemovesAndNotFoundAfter()
    {
        var host = CreateBoundHost();
        host.CreateEntity(CreateMeta("E"));

        host.RemoveEntity("E");

        Assert.Null(host.FindByName("E"));
        Assert.Empty(host.GetEntities());
    }

    [Fact]
    public void RequestKillEntity_SetsPendingKillTrue()
    {
        var host = CreateBoundHost();
        var entity = host.CreateEntity(CreateMeta("E"));

        Assert.False(entity.IsPendingKill);
        host.RequestKillEntity("E");

        Assert.True(entity.IsPendingKill);
    }

    [Fact]
    public void RequestKillEntity_DoubleRequest_ThrowsInvalidOperation()
    {
        var host = CreateBoundHost();
        host.CreateEntity(CreateMeta("E"));
        host.RequestKillEntity("E");

        var ex = Assert.Throws<InvalidOperationException>(() => host.RequestKillEntity("E"));
        Assert.Contains("already pending kill", ex.Message);
    }
}
