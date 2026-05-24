using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class LifecycleRunsTests
{
    [Fact]
    public void SessionRun_Dispose_ClearsSessionAndScene_ThenThrowsOnAccess()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
        var run = progressRun.LoadAndMountForeground("default");
        run.SessionBlackboard.Set("foo", 1);

        run.Dispose();

        // Scene should have been cleared during Dispose.
        Assert.Empty(host.SerializeMetaList());

        // After Dispose, all property access should throw ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() => run.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => run.GetSessionStateMachines());
        Assert.Throws<ObjectDisposedException>(() => run.SceneHost);
        Assert.Throws<ObjectDisposedException>(() => ((SessionRun)run).PersistLevelState());
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_RestoresProgressAndSession()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);

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
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);

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
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
        progressRun.LoadAndMountForeground("a");

        // Seed target level payload into current/, as SwitchForeground is strict.
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", "{\"machines\":[]}");

        progressRun.SwitchForeground("b");

        Assert.Equal("b", progressRun.ForegroundSession!.LevelId);
        Assert.Equal(ISessionManager.ForegroundKey, ((SessionRun)progressRun.ForegroundSession!).MountKey);

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
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
        progressRun.LoadAndMountForeground("a");

        // Missing target level payload in current/ → enter empty session and clear scene (README contract).
        runtime.Snd.SceneHost.Spawn(new SndMetaData
            { Name = "Temp", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        Assert.NotEmpty(runtime.Snd.SerializeMetaList());

        progressRun.SwitchForeground("b");

        Assert.Empty(runtime.Snd.SerializeMetaList());
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
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
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
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
        progressRun.LoadAndMountForeground("alpha");
        progressRun.ProgressBlackboard.Set(WellKnownKeys.SessionTopology, "__foreground__=wrong=false");

        Assert.Throws<InvalidOperationException>(() => progressRun.BuildSavePayload("new-save-01"));
    }

    // ── SessionRun serialization round-trip ──

    [Fact]
    public void SessionRun_SerializeToPayload_RoundTrip_PreservesBlackboardData()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var systemRuntime = TestFactory.CreateSystemRuntime(logger, fs, "root", runtime);
        var progressRuntime = new ProgressRuntime(systemRuntime, sndContext, sndContext);
        var managerRuntime = new SessionManagerRuntime(progressRuntime, new Blackboard.Blackboard());
        var bb1 = new Blackboard.Blackboard();
        bb1.Set("score", 42);
        var run1 = new SessionRun(managerRuntime, new SessionParameters("level1", bb1, host));

        var payload = run1.SerializeToPayload();
        Assert.Equal("level1", payload.LevelId);
        Assert.False(string.IsNullOrWhiteSpace(TestFactory.JsonFromNode(payload.SessionNode)));

        // Create second session and load the payload
        var bb2 = new Blackboard.Blackboard();
        var run2 = new SessionRun(managerRuntime, new SessionParameters("level1", bb2, host));
        run2.LoadFromPayload(payload);

        var (found, value) = run2.SessionBlackboard.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void SessionRun_LoadFromPayload_WhenSceneLoadFails_ResetsSessionState()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var systemRuntime = TestFactory.CreateSystemRuntime(logger, fs, "root", runtime);
        var progressRuntime = new ProgressRuntime(systemRuntime, sndContext, sndContext);
        var managerRuntime = new SessionManagerRuntime(progressRuntime, new Blackboard.Blackboard());
        var run = new SessionRun(managerRuntime, new SessionParameters("level1", new Blackboard.Blackboard(), host));

        run.SessionBlackboard.Set("before", 1);
        host.Spawn(new SndMetaData
        {
            Name = "temp",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData()
        });

        var payload = new LevelPayload
        {
            LevelId = "level1",
            SndSceneNode = TestFactory.NodeFromJson("{}"),
            SessionNode = TestFactory.NodeFromJson("""{"after":{"type":"Int32","data":3}}"""),
            SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
        };

        Assert.ThrowsAny<Exception>(() => run.LoadFromPayload(payload));
        Assert.Empty(host.SerializeMetaList());
        Assert.False(run.SessionBlackboard.TryGet<int>("before").found);
        Assert.False(run.SessionBlackboard.TryGet<int>("after").found);
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_MissingProgressStateMachinesNode_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);

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
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var systemRuntime = TestFactory.CreateSystemRuntime(logger, fs, "root", runtime);
        var progressRuntime = new ProgressRuntime(systemRuntime, sndContext, sndContext);
        var managerRuntime = new SessionManagerRuntime(progressRuntime, new Blackboard.Blackboard());
        var session = new Blackboard.Blackboard();
        var run = new SessionRun(managerRuntime, new SessionParameters("default", session, host));

        Assert.Null(run.MountKey);

        run.Dispose();
    }

    [Fact]
    public void SessionRun_MountKey_SetOnMount_ClearedOnUnmount()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
        sndContext.SetProgressRun(progressRun);

        var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.Equal("bg1", ((SessionRun)bg).MountKey);

        sndContext.SessionManager.DestroySession("bg1");
        Assert.False(sndContext.SessionManager.Contains("bg1"));
    }

    [Fact]
    public void SessionRun_Dispose_AutoUnmountsFromManager()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
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
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
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
        var service = new DefaultSaveStorageService(fs, "root");

        var result = service.ResolveLevelPayload("001", "nonexistent");

        Assert.Null(result);
    }

    // ── Lifecycle logging tests ───────────────────────────────────

    [Fact]
    public void SessionRun_Create_LogsCreation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);

        var run = progressRun.LoadAndMountForeground("test_level");

        Assert.Contains(logger.Infos, msg => msg.Contains("Created SessionRun") && msg.Contains("test_level"));

        run.Dispose();
    }

    [Fact]
    public void ProgressRun_Create_LogsCreation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));

        var progressRun = TestFactory.CreateProgressRun("test_save", logger, fs, "root", runtime, sndContext);

        Assert.Contains(logger.Infos, msg => msg.Contains("Created ProgressRun") && msg.Contains("test_save"));

        progressRun.Dispose();
    }

    [Fact]
    public void SessionManager_Mount_LogsMounting()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
        sndContext.SetProgressRun(progressRun);

        using var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.Contains(logger.Infos, msg => msg.Contains("Mounted session") && msg.Contains("bg1"));
    }
}
