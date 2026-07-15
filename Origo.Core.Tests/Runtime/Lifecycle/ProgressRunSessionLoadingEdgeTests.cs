using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Xunit;

namespace Origo.Core.Tests;

public class ProgressRunSessionLoadingEdgeTests
{
    [Fact]
    public void LoadFromPayload_WhenTopologyMalformed_ThrowsInvalidOperation()
    {
        var ctx = CreateContext();
        var progressRun = TestFactory.CreateProgressRun("001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"bad_entry"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = []
        };

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
        Assert.Contains("Malformed session topology entry", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromPayload_WhenTopologyMissing_ThrowsInvalidOperation()
    {
        var ctx = CreateContext();
        var progressRun = TestFactory.CreateProgressRun("001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson("{}"),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = []
        };

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
        Assert.Contains(WellKnownKeys.SessionTopology, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadAndMountForeground_WhenSndSceneIsEmpty_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, sndContext.MetaAccess, sndContext.PathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);

        fs.SeedFile("root/current/level_target/snd_scene.json", " ");
        fs.SeedFile("root/current/level_target/session.json", "{}");
        fs.SeedFile("root/current/level_target/session_state_machines.json", "{\"machines\":[]}");

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadAndMountForeground("target"));
        Assert.Contains("invalid snd_scene.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadAndMountForeground_WhenSessionStateMachineJsonIsMalformed_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, sndContext.MetaAccess, sndContext.PathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);

        fs.SeedFile("root/current/level_target/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_target/session.json", "{}");
        fs.SeedFile("root/current/level_target/session_state_machines.json", "{");

        Assert.ThrowsAny<Exception>(() => progressRun.LoadAndMountForeground("target"));
    }

    [Fact]
    public void LoadFromPayload_WhenBackgroundSessionLoadFails_ClearsMountedSessions()
    {
        var ctx = CreateContext();
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false,bg=bg=false"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
                },
                ["bg"] = new()
                {
                    LevelId = "bg",
                    SndSceneNode = TestFactory.NodeFromJson("{}"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
                }
            }
        };

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(payload, "001", ctx.Runtime.Logger);
        ctx.Save.RequestLoadGame("001");
        Assert.ThrowsAny<Exception>(() => ctx.Deferred.FlushDeferredActionsForCurrentFrame());
        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.False(ctx.Runtime.SessionManager.Contains("bg"));
    }

    private static SndContext CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        return new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
    }
}
