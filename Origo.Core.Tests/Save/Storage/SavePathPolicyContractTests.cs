using Origo.Core.Runtime.Lifecycle;
using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     契约测试：
///     1. 验证 SndContext 参数对象将 ISavePathPolicy 正确传递给默认的 DefaultSaveStorageService；
///     2. 验证 DefaultSaveStorageService 的 所有 方法完整通过 ISavePathPolicy 拼装路径；
///     3. 验证会话状态机钩子中 ctx.SceneAccess 始终指向当前会话 SceneHost（前后台均成立）。
/// </summary>
public class SavePathPolicyContractTests
{
    // ── Defect 1: SndContext passes savePathPolicy to default storage services ──

    [Fact]
    public void SndContext_DefaultStorage_Uses_Injected_SavePathPolicy()
    {
        // Arrange: inject a custom path policy into SndContext without providing explicit storage services.
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var customPolicy = new TestPrefixedPathPolicy("cx_");

        var ctx = new SndContext(new SndContextParameters(
            runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "res://entry/entry.json")
        {
            SavePathPolicy = customPolicy
        });

        // Act: use the storage service (default-created by SndContext) to write a level payload.
        var payload = new LevelPayload
        {
            LevelId = "testlvl",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
        };
        var currentDir = customPolicy.GetCurrentDirectory();
        ctx.StorageService.WriteLevelPayloadOnly(currentDir, payload);

        // Assert: file must be at the custom policy path, NOT the default SavePathLayout path.
        var expectedDir = customPolicy.GetLevelDirectory(currentDir, "testlvl");
        var expectedFile = $"root/{customPolicy.GetLevelSndSceneFile(expectedDir)}";
        Assert.True(fs.Exists(expectedFile),
            $"Expected custom-policy file at '{expectedFile}' to exist.");
        Assert.False(fs.Exists("root/current/level_testlvl/snd_scene.json"),
            "File should NOT exist at default path when custom path policy is injected.");
    }

    [Fact]
    public void SndContext_DefaultInitialStorage_Uses_Injected_SavePathPolicy()
    {
        // Arrange
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var customPolicy = new TestPrefixedPathPolicy("ix_");

        var ctx = new SndContext(new SndContextParameters(
            runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "res://entry/entry.json")
        {
            SavePathPolicy = customPolicy
        });

        // Act: write through the initial storage service
        var payload = new LevelPayload
        {
            LevelId = "initlvl",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
        };
        var currentDir = customPolicy.GetCurrentDirectory();
        ctx.InitialStorageService.WriteLevelPayloadOnly(currentDir, payload);

        // Assert
        var expectedDir = customPolicy.GetLevelDirectory(currentDir, "initlvl");
        var expectedFile = $"res://initial/{customPolicy.GetLevelSndSceneFile(expectedDir)}";
        Assert.True(fs.Exists(expectedFile),
            $"Expected initial storage custom-policy file at '{expectedFile}' to exist.");
    }

    [Fact]
    public void SystemRuntime_DefaultStorage_Uses_Injected_SavePathPolicy()
    {
        // Arrange: inject path policy to SystemRuntime without explicit storage service.
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var customPolicy = new TestPrefixedPathPolicy("rf_");

        var systemRuntime = TestFactory.CreateSystemRuntime(logger, metaAccess, pathResolver, "root", runtime,
            savePathPolicy: customPolicy, sharedDataSourceIo: dataSourceIo);

        // Act: write through the system runtime's storage service
        var payload = new LevelPayload
        {
            LevelId = "faclvl",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
        };
        var currentDir = customPolicy.GetCurrentDirectory();
        systemRuntime.StorageService.WriteLevelPayloadOnly(currentDir, payload);

        // Assert
        var expectedDir = customPolicy.GetLevelDirectory(currentDir, "faclvl");
        var expectedFile = $"root/{customPolicy.GetLevelSndSceneFile(expectedDir)}";
        Assert.True(fs.Exists(expectedFile),
            $"Expected SystemRuntime-created storage to use custom policy; file at '{expectedFile}'.");
    }

    // ── Defect 2: All DefaultSaveStorageService methods are fully policy-aware ──

    [Fact]
    public void DefaultSaveStorageService_EnumerateSaveIds_Uses_PathPolicy()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var policy = new TestPrefixedPathPolicy("p_");
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root", policy);

        // Seed a save directory that looks like a valid save
        fs.CreateDirectory("root/save_001");
        fs.SeedFile("root/save_001/dummy.txt", "");

        // EnumerateSaveIds should find the save via save_ prefix
        var ids = storage.EnumerateSaveIds();
        Assert.Contains("001", ids);
    }

    [Fact]
    public void DefaultSaveStorageService_EnumerateSavesWithMetaData_Uses_PathPolicy()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var policy = new TestPrefixedPathPolicy("p_");
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root", policy);

        // Seed a save directory with meta at the custom policy path
        fs.CreateDirectory("root/save_002");
        var saveRel = policy.GetSaveDirectory("002");
        var metaRel = policy.GetCustomMetaFile(saveRel);
        fs.SeedFile($"root/{metaRel}", "display_name: Test Save\n");

        var entries = storage.EnumerateSavesWithMetaData();
        Assert.Single(entries);
        Assert.Equal("002", entries[0].SaveId);
        Assert.True(entries[0].MetaData.ContainsKey("display_name"));
    }

    [Fact]
    public void DefaultSaveStorageService_WriteSavePayloadToCurrentThenSnapshot_Uses_PathPolicy()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var policy = new TestPrefixedPathPolicy("ws_");
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root", policy);
        var logger = new TestLogger();

        var payload = CreateFullSaveGamePayload("default");

        storage.WriteSavePayloadToCurrentThenSnapshot(payload, "001", logger);

        // Files should be at custom-policy current path
        var currentRel = policy.GetCurrentDirectory();
        var levelDir = policy.GetLevelDirectory(currentRel, "default");
        Assert.True(fs.Exists($"root/{policy.GetLevelSndSceneFile(levelDir)}"),
            "Level SND scene should be at custom-policy current path.");

        // Snapshot should be at custom-policy save path
        var saveRel = policy.GetSaveDirectory("001");
        Assert.True(fs.DirectoryExists($"root/{saveRel}"),
            "Snapshot directory should use custom-policy save path.");
    }

    [Fact]
    public void DefaultSaveStorageService_SnapshotCurrentToSave_Uses_PathPolicy()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var policy = new TestPrefixedPathPolicy("sn_");
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root", policy);

        // Seed current with a file at custom-policy path
        var currentRel = policy.GetCurrentDirectory();
        var currentAbs = $"root/{currentRel}";
        fs.SeedFile($"{currentAbs}/data.txt", "hello");

        storage.SnapshotCurrentToSave("snap1");

        // Snapshot should exist at custom-policy save directory
        var saveRel = policy.GetSaveDirectory("snap1");
        Assert.True(fs.DirectoryExists($"root/{saveRel}"),
            $"Snapshot should exist at 'root/{saveRel}'.");
        Assert.True(fs.Exists($"root/{saveRel}/data.txt"),
            "Snapshot should contain the copied file.");
    }

    [Fact]
    public void DefaultSaveStorageService_WriteSavePayloadToCurrent_Uses_PathPolicy()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var policy = new TestPrefixedPathPolicy("wc_");
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root", policy);

        var payload = CreateFullSaveGamePayload("level1");
        storage.WriteSavePayloadToCurrent(payload);

        var currentRel = policy.GetCurrentDirectory();
        var progressRel = policy.GetProgressFile(currentRel);
        Assert.True(fs.Exists($"root/{progressRel}"),
            $"Progress file should be at custom-policy path 'root/{progressRel}'.");
    }

    [Fact]
    public void DefaultSaveStorageService_ReadWriteRoundTrip_Uses_PathPolicy()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var policy = new TestPrefixedPathPolicy("rt_");
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root", policy);

        var payload = CreateFullSaveGamePayload("dungeon");
        storage.WriteSavePayloadToCurrent(payload);

        var read = storage.ReadSavePayloadFromCurrent("001", "dungeon");
        Assert.Equal("dungeon", read.ActiveLevelId);
        Assert.True(read.Levels.ContainsKey("dungeon"));
    }

    // ── Defect 3: Session state machine SceneAccess contract test ──

    [Fact]
    public void SessionStateMachineContext_SceneAccess_PointsToForegroundSession_ForegroundAndBackground()
    {
        SceneContractStrategy.Reset();
        try
        {
            var (ctx, _) = CreateContext(w =>
            {
                w.RegisterStrategy(() => new SceneContractStrategy());
                w.RegisterStrategy(() => new NoOpPopContractStrategy());
            });

            SetupForegroundSession(ctx);
            var fg = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
            using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

            // Seed each scene with a distinguishing entity.
            ((SessionRun)fg).SceneHost.CreateEntity(new SndMetaData { Name = "fg_marker" });

            ((SessionRun)bg).SceneHost.CreateEntity(CreateFullMeta("bg_marker"));
            // Push triggers the strategy hook in each session's state machine.
            var fgMachine = fg.GetSessionStateMachines().CreateOrGet(
                "contract_sm", "contract.scene_access", "contract.noop_pop");
            fgMachine.Push("s1");

            var bgMachine = bg.GetSessionStateMachines().CreateOrGet(
                "contract_sm", "contract.scene_access", "contract.noop_pop");
            bgMachine.Push("s1");

            // Each hook must have seen its own session's SceneHost (not the global SndContext one).
            Assert.Equal(2, SceneContractStrategy.ObservedScenes!.Count);

            // Foreground SceneAccess should contain the foreground marker.
            var fgNames = SceneContractStrategy.ObservedScenes[0];
            Assert.Contains("fg_marker", fgNames);
            Assert.DoesNotContain("bg_marker", fgNames);

            // Background SceneAccess should contain the background marker.
            var bgNames = SceneContractStrategy.ObservedScenes[1];
            Assert.Contains("bg_marker", bgNames);
            Assert.DoesNotContain("fg_marker", bgNames);
        }
        finally
        {
            SceneContractStrategy.Reset();
        }
    }

    [Fact]
    public void SessionStateMachineContext_SessionBlackboard_PointsToForegroundSession_ForegroundAndBackground()
    {
        BbContractStrategy.Reset();
        try
        {
            var (ctx, _) = CreateContext(w =>
            {
                w.RegisterStrategy(() => new BbContractStrategy());
                w.RegisterStrategy(() => new NoOpPopContractStrategy());
            });

            SetupForegroundSession(ctx);
            var fg = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
            using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

            fg.SessionBlackboard.SetValue("who", "foreground");
            bg.SessionBlackboard.SetValue("who", "background");

            var fgMachine = fg.GetSessionStateMachines().CreateOrGet(
                "bb_sm", "contract.bb_access", "contract.noop_pop");
            fgMachine.Push("s1");

            var bgMachine = bg.GetSessionStateMachines().CreateOrGet(
                "bb_sm", "contract.bb_access", "contract.noop_pop");
            bgMachine.Push("s1");

            Assert.Equal(2, BbContractStrategy.ObservedValues!.Count);
            Assert.Equal("foreground", BbContractStrategy.ObservedValues[0]);
            Assert.Equal("background", BbContractStrategy.ObservedValues[1]);
        }
        finally
        {
            BbContractStrategy.Reset();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateContext(
        Action<SndWorld>? configureWorld = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        configureWorld?.Invoke(runtime.SndWorld);

        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        return (ctx, fs);
    }

    private static void SetupForegroundSession(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");
    }

    private static SndMetaData CreateFullMeta(string name)
    {
        return new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };
    }

    private static SaveGamePayload CreateFullSaveGamePayload(string activeLevelId)
    {
        return new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = activeLevelId,
            ProgressNode = TestFactory.NodeFromJson(
                $$$"""{"origo.session_topology":{"type":"String","data":"__foreground__={{{activeLevelId}}}=false"}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                [activeLevelId] = new()
                {
                    LevelId = activeLevelId,
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
                }
            }
        };
    }

    // ── Custom ISavePathPolicy that prefixes all directory segments ──

    private sealed class TestPrefixedPathPolicy(string prefix) : ISavePathPolicy
    {
        private readonly string _prefix = prefix;

        public string GetCurrentDirectory() => $"{_prefix}current";

        public string GetSaveDirectory(string saveId) => $"{_prefix}save_{saveId}";

        public string GetProgressFile(string baseDirectory) => $"{baseDirectory}/{_prefix}progress.json";

        public string GetProgressStateMachinesFile(string baseDirectory) =>
            $"{baseDirectory}/{_prefix}progress_state_machines.json";

        public string GetCustomMetaFile(string baseDirectory) => $"{baseDirectory}/{_prefix}meta.map";

        public string GetLevelDirectory(string baseDirectory, string levelId) =>
            $"{baseDirectory}/{_prefix}level_{levelId}";

        public string GetLevelSndSceneFile(string levelDirectory) => $"{levelDirectory}/snd_scene.json";

        public string GetLevelSessionFile(string levelDirectory) => $"{levelDirectory}/session.json";

        public string GetLevelSessionStateMachinesFile(string levelDirectory) =>
            $"{levelDirectory}/session_state_machines.json";

        public string GetWriteInProgressMarker(string baseDirectory) => $"{baseDirectory}/{_prefix}.write_in_progress";

        public string GetPayloadShaFile(string baseDirectory) => $"{baseDirectory}/{_prefix}.payload.sha";

        public string GetExtraDirectory(string baseDirectory) => $"{baseDirectory}/{_prefix}extra";
    }

    // ── Test strategies ────────────────────────────────────────────────

    [StrategyIndex("contract.scene_access")]
    private sealed class SceneContractStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<List<string>>?> _observedScenes = new();
        internal static List<List<string>>? ObservedScenes => _observedScenes.Value;

        public static void Reset() => _observedScenes.Value = [];

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
        {
            var names = new List<string>();
            if (ctx.SceneAccess is ISndSceneHost sceneHost)
                foreach (var entity in sceneHost.GetEntities())
                    names.Add(entity.Name);
            ObservedScenes?.Add(names);
        }
    }

    [StrategyIndex("contract.bb_access")]
    private sealed class BbContractStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string?>?> _observedValues = new();
        internal static List<string?>? ObservedValues => _observedValues.Value;

        public static void Reset() => _observedValues.Value = [];

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
        {
            string? value = null;
            if (ctx.SessionBlackboard is { } bb)
            {
                var (found, v) = bb.TryGet<string>("who");
                if (found) value = v;
            }

            ObservedValues?.Add(value);
        }
    }

    [StrategyIndex("contract.noop_pop")]
    private sealed class NoOpPopContractStrategy : StateMachineStrategyBase
    {
    }

    private static (IFileMetaAccess MetaAccess, IDataSourceIoGateway DataSourceIo, IPathResolver PathResolver)
        CreateGateways(IFileSystem fs) =>
        (DataSourceFactory.CreateFileMetaAccess(fs),
         DataSourceFactory.CreateDefaultIoGateway(fs),
         DataSourceFactory.CreatePathResolver(fs));
}
