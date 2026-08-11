using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Xunit;
using System.Text.Json;

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

        Assert.Throws<InvalidOperationException>(() => progressRun.LoadAndMountForeground("target"));
    }

    [Fact]
    public void LoadFromPayload_WhenBackgroundLevelPayloadMissing_ThrowsInvalidOperation()
    {
        // A topology that references a background level with no payload in
        // the save must fail fast like the foreground path does, instead of
        // silently mounting an empty background session.
        var ctx = CreateContext();
        var progressRun = TestFactory.CreateProgressRun("001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);

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
                }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
        Assert.Contains("bg", ex.Message, StringComparison.Ordinal);
        Assert.False(ctx.Runtime.SessionManager.Contains("bg"));
    }

    [Fact]
    public void LoadFromPayload_WhenForegroundLevelPayloadMissing_ThrowsInvalidOperation()
    {
        // A topology that references a foreground level with no payload must
        // fail fast like the background path does, instead of silently
        // mounting an empty foreground.
        var ctx = CreateContext();
        var progressRun = TestFactory.CreateProgressRun("001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = []
        };

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
        Assert.Contains("default", ex.Message, StringComparison.Ordinal);
        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);
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
        Assert.Throws<InvalidOperationException>(() => ctx.Deferred.FlushDeferredActionsForCurrentFrame());
        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.False(ctx.Runtime.SessionManager.Contains("bg"));
    }

    [Fact]
    public void RequestLoadGame_Failure_DisposesProgressRunAndClearsContextReference()
    {
        var ctx = CreateContext();
        var payload = new SaveGamePayload
        {
            SaveId = "002",
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

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(payload, "002", ctx.Runtime.Logger);
        ctx.Save.RequestLoadGame("002");
        Assert.Throws<InvalidOperationException>(() => ctx.Deferred.FlushDeferredActionsForCurrentFrame());

        // The failed progress run must not remain reachable: reads of the
        // progress blackboard and the progress-run accessor fail fast instead
        // of exposing half-deserialized state.
        Assert.Null(ctx.Blackboard.ProgressBlackboard);
        Assert.Throws<InvalidOperationException>(() => ctx.EnsureProgressRun());
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
