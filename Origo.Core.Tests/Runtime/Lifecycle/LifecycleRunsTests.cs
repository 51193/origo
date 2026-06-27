using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Tests;

public class LifecycleRunsTests
{
    [Fact]
    public void SessionRun_Dispose_ClearsSessionAndScene_ThenThrowsOnAccess()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        var run = progressRun.LoadAndMountForeground("default");
        run.SessionBlackboard.SetValue("foo", 1);

        run.Dispose();

        // Scene should have been cleared during Dispose.
        Assert.Empty(host.BuildMetaList());

        // After Dispose, all property access should throw ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() => run.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => run.GetSessionStateMachines());
        Assert.Throws<ObjectDisposedException>(() => run.FindByName("x"));
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_RestoresProgressAndSession()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}"),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("""{"x":{"type":"Int32","data":3}}"""),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
                }
            }
        };

        progressRun.LoadFromPayload(payload);
        var (found, value) = progressRun.ForegroundSession!.SessionBlackboard.TryGet<int>("x");

        Assert.True(found);
        Assert.Equal(3, value);
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_WithEmptyProgressNode_ThrowsMissingSessionTopology()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson("{}"),
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
        Assert.Contains(WellKnownKeys.SessionTopology, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressRun_SwitchForegroundLevel_PersistsOldSession_AndLoadsNewSessionFromCurrent()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        progressRun.LoadAndMountForeground("a");

        // Seed target level payload into current/, as SwitchForeground is strict.
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", "{\"machines\":[]}");

        progressRun.SwitchForeground("b");

        Assert.Equal("b", progressRun.ForegroundSession!.LevelId);
        Assert.True(progressRun.SessionManager.Contains(ISessionManager.ForegroundKey));

        var (foundTopology, topology) =
            progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(foundTopology);
        Assert.Equal("__foreground__=b=false", topology);

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
        Assert.True(fs.Exists("root/current/level_a/session_state_machines.json"));
    }

    [Fact]
    public void ProgressRun_SwitchForegroundLevel_WhenTargetMissing_EntersEmptySessionAndClearsScene()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        progressRun.LoadAndMountForeground("a");

        // Missing target level payload in current/ → enter empty session and clear scene (README contract).
        runtime.ForegroundSceneHost.CreateEntity(new SndMetaData
            { Name = "Temp", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        Assert.NotEmpty(runtime.ForegroundSceneHost.BuildMetaList());

        progressRun.SwitchForeground("b");

        Assert.Empty(runtime.ForegroundSceneHost.BuildMetaList());
        Assert.Equal("b", progressRun.ForegroundSession?.LevelId);
        Assert.NotNull(progressRun.ForegroundSession);
        Assert.Equal("b", progressRun.ForegroundSession!.LevelId);

        var (foundTopology, topology) =
            progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(foundTopology);
        Assert.Equal("__foreground__=b=false", topology);

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
    }

    [Fact]
    public void ProgressRun_LoadAndMountForeground_SyncsSessionTopologyToProgressBlackboard()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        Assert.False(progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology).found);

        progressRun.LoadAndMountForeground("dungeon");

        var (found, topology) = progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Equal("__foreground__=dungeon=false", topology);
        Assert.Equal("dungeon", progressRun.ForegroundSession!.LevelId);
    }

    [Fact]
    public void ProgressRun_BuildSavePayload_ThrowsWhenProgressTopologyForegroundDoesNotMatchForeground()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        progressRun.LoadAndMountForeground("alpha");
        progressRun.ProgressBlackboard.SetValue(WellKnownKeys.SessionTopology, "__foreground__=wrong=false");

        Assert.Throws<InvalidOperationException>(() => progressRun.BuildSavePayload("new-save-01"));
    }

    // ── SessionRun serialization round-trip ──

    [Fact]
    public void SessionRun_SerializeToPayload_RoundTrip_PreservesBlackboardData()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("level1");

        var fg = progressRun.ForegroundSession!;
        fg.SessionBlackboard.SetValue("score", 42);

        sndContext.RequestSaveGame("roundtrip_001");
        sndContext.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_roundtrip_001/level_level1/session.json"));

        var (found, value) = fg.SessionBlackboard.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void SessionRun_LoadFromPayload_WhenSceneLoadFails_ResetsSessionState()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));

        fs.SeedFile("root/current/level_bad/snd_scene.json", "{invalid}");
        fs.SeedFile("root/current/level_bad/session.json", """{"after":{"type":"Int32","data":3}}""");
        fs.SeedFile("root/current/level_bad/session_state_machines.json", """{"machines":[]}""");

        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);

        Assert.ThrowsAny<Exception>(() => progressRun.LoadAndMountForeground("bad"));
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_MissingProgressStateMachinesNode_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false"}}""")
            // ProgressStateMachinesNode omitted → CreateNull() — should throw
        };

        Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
    }

    // ── Mount key tracking tests ──────────────────────────────────

    [Fact]
    public void SessionRun_MountKey_IsNull_WhenNotMounted()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);

        var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");
        Assert.True(sndContext.SessionManager.Contains("bg1"));

        sndContext.SessionManager.DestroySession("bg1");
        Assert.False(sndContext.SessionManager.Contains("bg1"));
    }

    [Fact]
    public void SessionRun_MountKey_SetOnMount_ClearedOnUnmount()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);

        var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.True(sndContext.SessionManager.Contains("bg1"));

        sndContext.SessionManager.DestroySession("bg1");
        Assert.False(sndContext.SessionManager.Contains("bg1"));
    }

    [Fact]
    public void SessionRun_Dispose_AutoUnmountsFromManager()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);

        var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");
        Assert.True(sndContext.SessionManager.Contains("bg1"));

        bg.Dispose();

        // After Dispose, session should have auto-unmounted.
        Assert.False(sndContext.SessionManager.Contains("bg1"));
    }

    // ── LoadAndMountForeground tests ──────────────────────────────

    [Fact]
    public void LoadAndMountForeground_WhenNoPayloadFound_MountsEmptySession()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);

        // No data seeded — should mount empty session.
        var session = progressRun.LoadAndMountForeground("missing_level");

        Assert.NotNull(session);
        Assert.Equal("missing_level", session.LevelId);
        Assert.NotNull(progressRun.SessionManager.ForegroundSession);
    }

    // ── ResolveLevelPayload tests ──────────────────────────────────

    [Fact]
    public void ResolveLevelPayload_ReturnsNull_WhenNoData()
    {
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");

        var result = service.ResolveLevelPayload("001", "nonexistent");

        Assert.Null(result);
    }

    // ── Lifecycle logging tests ───────────────────────────────────

    [Fact]
    public void SessionRun_Create_LogsCreation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);

        var run = progressRun.LoadAndMountForeground("test_level");

        Assert.Contains(logger.Infos, msg => msg.Contains("SessionRun") && msg.Contains("test_level"));

        run.Dispose();
    }

    [Fact]
    public void ProgressRun_Create_LogsCreation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));

        var progressRun = TestFactory.CreateProgressRun("test_save", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);

        Assert.Contains(logger.Infos, msg => msg.Contains("ProgressRun") && msg.Contains("test_save"));

        progressRun.Dispose();
    }

    [Fact]
    public void SessionManager_Mount_LogsMounting()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);

        using var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.Contains(logger.Infos, msg => msg.Contains("SessionManager") && msg.Contains("bg1"));
    }

    // ── Edge cases for new Dispose semantics ──────────────────────────

    [Fact]
    public void SessionManager_Clear_EmptiesAllSessions()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");

        sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");
        sndContext.SessionManager.CreateBackgroundSession("bg2", "bg2", true);

        Assert.True(sndContext.SessionManager.Contains("bg1"));
        Assert.True(sndContext.SessionManager.Contains("bg2"));
        Assert.NotNull(sndContext.SessionManager.ForegroundSession);

        sndContext.SessionManager.DestroySession("bg1");
        sndContext.SessionManager.DestroySession("bg2");
        sndContext.SessionManager.DestroySession(ISessionManager.ForegroundKey);

        Assert.Empty(sndContext.SessionManager.Keys);
        Assert.Null(sndContext.SessionManager.ForegroundSession);
    }

    [Fact]
    public void LoadAndMountForeground_WithEmptyLevelId_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);

        Assert.Throws<ArgumentException>(() => progressRun.LoadAndMountForeground(""));
        Assert.Throws<ArgumentException>(() => progressRun.LoadAndMountForeground("   "));
    }

    [Fact]
    public void SwitchForeground_WithEmptyLevelId_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");

        Assert.Throws<ArgumentException>(() => progressRun.SwitchForeground(""));
        Assert.Throws<ArgumentException>(() => progressRun.SwitchForeground("   "));
    }

    [Fact]
    public void BuildSavePayload_WithoutTopologySet_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var sndContext = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, metaAccess, pathResolver, "root", runtime, sndContext, sharedDataSourceIo: dataSourceIo);
        sndContext.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");

        // Manually clear the topology from progress blackboard to simulate a missing entry
        progressRun.ProgressBlackboard.SetValue(WellKnownKeys.SessionTopology, string.Empty);

        Assert.Throws<InvalidOperationException>(() => progressRun.BuildSavePayload("no_topology"));
    }
}
