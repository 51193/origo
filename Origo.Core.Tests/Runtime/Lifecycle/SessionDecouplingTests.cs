using Origo.Core.Runtime.Lifecycle;
using System;
using System.Threading;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Tests;

/// <summary>
///     Tests verifying the decoupling of foreground/background SessionRun
///     after the refactoring to session-bound IStateMachineContext, ISndSceneHost-based SceneHost,
///     injectable ISavePathPolicy, and ISaveStorageService-based LevelBuilder.
/// </summary>
public class SessionDecouplingTests
{
    // ── 1. SessionStateMachineContext binds SessionBlackboard per session ──

    [Fact]
    public void SessionStateMachineContext_Binds_SessionBlackboard()
    {
        BlackboardProbeStrategy.Reset();
        try
        {
            var (ctx, _) = CreateContext(w =>
            {
                w.RegisterStrategy(() => new BlackboardProbeStrategy());
                w.RegisterStrategy(() => new NoOpPopStrategy());
            });

            SetupForegroundSession(ctx);
            var fg = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
            using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

            // Seed each session's blackboard with a unique marker.
            fg.SessionBlackboard.SetValue("marker", "foreground");
            bg.SessionBlackboard.SetValue("marker", "background");

            // Push into each session's state machine – the strategy hook reads ctx.SessionBlackboard.
            var fgMachine = fg.GetSessionStateMachines().CreateOrGet(
                "probe_sm", "test.bb_probe", "test.noop_pop");
            fgMachine.Push("state_a");

            var bgMachine = bg.GetSessionStateMachines().CreateOrGet(
                "probe_sm", "test.bb_probe", "test.noop_pop");
            bgMachine.Push("state_a");

            // Each hook should have observed its own session's blackboard.
            Assert.Equal(2, BlackboardProbeStrategy.ObservedMarkers!.Count);
            Assert.Equal("foreground", BlackboardProbeStrategy.ObservedMarkers[0]);
            Assert.Equal("background", BlackboardProbeStrategy.ObservedMarkers[1]);
        }
        finally
        {
            BlackboardProbeStrategy.Reset();
        }
    }

    // ── 2. SessionStateMachineContext binds SceneAccess per session ──

    [Fact]
    public void SessionStateMachineContext_Binds_SceneAccess()
    {
        SceneAccessProbeStrategy.Reset();
        try
        {
            var (ctx, _) = CreateContext(w =>
            {
                w.RegisterStrategy(() => new SceneAccessProbeStrategy());
                w.RegisterStrategy(() => new NoOpPopStrategy());
            });

            SetupForegroundSession(ctx);
            var fg = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
            using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

            // Spawn a unique entity in each session's scene so we can distinguish them.
            // Foreground uses TestSndSceneHost (simple meta OK).
            fg.Spawn(new SndMetaData { Name = "fg_entity" });
            // Background uses FullMemorySndSceneHost (needs full meta).
            bg.Spawn(CreateFullMeta("bg_entity"));

            // Push triggers the hook which reads ctx.SceneAccess.
            var fgMachine = fg.GetSessionStateMachines().CreateOrGet(
                "scene_sm", "test.scene_probe", "test.noop_pop");
            fgMachine.Push("s1");

            var bgMachine = bg.GetSessionStateMachines().CreateOrGet(
                "scene_sm", "test.scene_probe", "test.noop_pop");
            bgMachine.Push("s1");

            // Each hook should have seen a different scene.
            Assert.Equal(2, SceneAccessProbeStrategy.ObservedEntityNames!.Count);
            Assert.Contains("fg_entity", SceneAccessProbeStrategy.ObservedEntityNames[0]);
            Assert.DoesNotContain("bg_entity", SceneAccessProbeStrategy.ObservedEntityNames[0]);
            Assert.Contains("bg_entity", SceneAccessProbeStrategy.ObservedEntityNames[1]);
            Assert.DoesNotContain("fg_entity", SceneAccessProbeStrategy.ObservedEntityNames[1]);
        }
        finally
        {
            SceneAccessProbeStrategy.Reset();
        }
    }

    // ── 3. SceneHost returns ISndSceneHost for both foreground and background ──

    [Fact]
    public void SceneHost_ReturnsISndSceneHost_ForBothForegroundAndBackground()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

        // Both should expose ISndSceneHost (which includes FindByName/Spawn/GetEntities).
        Assert.IsType<ISndSceneHost>(((SessionRun)fg).SceneHost, exactMatch: false);
        Assert.IsType<ISndSceneHost>(((SessionRun)bg).SceneHost, exactMatch: false);

        // Verify methods are directly callable without casting.
        var fgHost = ((SessionRun)fg).SceneHost;
        var bgHost = ((SessionRun)bg).SceneHost;

        // Foreground uses TestSndSceneHost (simple meta).
        var fgEntity = fgHost.CreateEntity(new SndMetaData { Name = "fg_test" });
        // Background uses FullMemorySndSceneHost (needs full meta).
        var bgEntity = bgHost.CreateEntity(CreateFullMeta("bg_test"));

        Assert.NotNull(fgHost.FindByName("fg_test"));
        Assert.NotNull(bgHost.FindByName("bg_test"));
        Assert.Single(fgHost.GetEntities());
        Assert.Single(bgHost.GetEntities());
    }

    // ── 4. Background SceneHost supports Spawn/FindByName without casting ──

    [Fact]
    public void BackgroundSession_SceneHost_Spawn_FindByName_WithoutCasting()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

        // Directly use SceneHost (typed as ISndSceneHost) – no cast needed.
        var spawned = ((SessionRun)bg).SceneHost.CreateEntity(CreateFullMeta("soldier"));
        Assert.NotNull(spawned);
        Assert.Equal("soldier", spawned.Name);

        var found = bg.FindByName("soldier");
        Assert.NotNull(found);
        Assert.Same(spawned, found);

        var all = bg.GetEntities();
        Assert.Single(all);
    }

    // ── 5. DefaultSaveStorageService uses injected ISavePathPolicy ──

    [Fact]
    public void DefaultSaveStorageService_Uses_Injected_PathPolicy()
    {
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var customPolicy = new PrefixedSavePathPolicy("custom_");
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root", customPolicy);

        var payload = new LevelPayload
        {
            LevelId = "dungeon",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
        };

        // WriteLevelPayloadOnlyToCurrent should use the custom policy's GetCurrentDirectory / GetLevelDirectory.
        storage.WriteLevelPayloadOnlyToCurrent(payload);

        // The custom policy prefixes "custom_" to directory names,
        // so level directory becomes "custom_current/custom_level_dungeon/".
        var expectedSndScene =
            $"root/{customPolicy.GetLevelSndSceneFile(customPolicy.GetLevelDirectory(customPolicy.GetCurrentDirectory(), "dungeon"))}";
        Assert.True(fs.Exists(expectedSndScene),
            $"Expected file at '{expectedSndScene}' to exist (custom path policy should change layout).");

        // Also verify that the default (non-custom) path does NOT contain the file.
        Assert.False(fs.Exists("root/current/level_dungeon/snd_scene.json"),
            "File should NOT be at default path when custom path policy is injected.");
    }

    // ── 6. LevelBuilder.Commit goes through ISaveStorageService ──

    [Fact]
    public void LevelBuilder_Commit_UsesStorageService()
    {
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var sndWorld = TestFactory.CreateSndWorld();
        var trackingStorage = new TrackingSaveStorageService(
            new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root"));

        var builder = new LevelBuilder("my_level", sndWorld, trackingStorage);
        builder.AddEntity(new SndMetaData { Name = "npc" });

        builder.Commit();

        // Verify that Commit() delegated to ISaveStorageService.WriteLevelPayloadOnly.
        Assert.Equal(1, trackingStorage.WriteLevelPayloadOnlyCalls);
        Assert.Equal("my_level", trackingStorage.LastWrittenPayload!.LevelId);

        // Also verify the file actually landed on disk via the inner real service.
        Assert.True(fs.Exists("root/current/level_my_level/snd_scene.json"));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateContext(
        Action<SndWorld>? configureWorld = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        configureWorld?.Invoke(runtime.SndWorld);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        return (ctx, fs);
    }

    private static void SetupForegroundSession(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");
    }

    /// <summary>
    ///     Creates SndMetaData with full sub-metadata required by FullMemorySndSceneHost.
    /// </summary>
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

    // ── Test strategies ───────────────────────────────────────────────

    [StrategyIndex("test.bb_probe")]
    private sealed class BlackboardProbeStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string?>?> _observedMarkers = new();
        internal static List<string?>? ObservedMarkers { get => _observedMarkers.Value; set => _observedMarkers.Value = value; }

        public static void Reset() => ObservedMarkers = [];

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
        {
            string? marker = null;
            if (ctx.SessionBlackboard is { } bb)
            {
                var (found, value) = bb.TryGet<string>("marker");
                if (found) marker = value;
            }

            ObservedMarkers?.Add(marker);
        }
    }

    [StrategyIndex("test.scene_probe")]
    private sealed class SceneAccessProbeStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<List<string>>?> _observedEntityNames = new();
        internal static List<List<string>>? ObservedEntityNames { get => _observedEntityNames.Value; set => _observedEntityNames.Value = value; }

        public static void Reset() => ObservedEntityNames = [];

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
        {
            var names = new List<string>();
            if (ctx.SceneAccess is ISndSceneHost sceneHost)
                foreach (var entity in sceneHost.GetEntities())
                    names.Add(entity.Name);
            ObservedEntityNames?.Add(names);
        }
    }

    [StrategyIndex("test.noop_pop")]
    private sealed class NoOpPopStrategy : StateMachineStrategyBase
    {
    }

    // ── Custom ISavePathPolicy that prefixes all directory segments ──

    private sealed class PrefixedSavePathPolicy(string prefix) : ISavePathPolicy
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

    // ── Tracking wrapper for ISaveStorageService ──

    private sealed class TrackingSaveStorageService(DefaultSaveStorageService inner) : ISaveStorageService
    {
        private readonly DefaultSaveStorageService _inner = inner;

        public int WriteLevelPayloadOnlyCalls { get; private set; }
        public LevelPayload? LastWrittenPayload { get; private set; }

        public IReadOnlyList<string> EnumerateSaveIds() => _inner.EnumerateSaveIds();

        public IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData() => _inner.EnumerateSavesWithMetaData();

        public void WriteSavePayloadToCurrent(SaveGamePayload payload) => _inner.WriteSavePayloadToCurrent(payload);

        public void WriteSavePayloadToCurrentThenSnapshot(
            SaveGamePayload payload, string newSaveId,
            ILogger logger) =>
            _inner.WriteSavePayloadToCurrentThenSnapshot(payload, newSaveId, logger);

        public void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload)
        {
            WriteLevelPayloadOnlyCalls++;
            LastWrittenPayload = levelPayload;
            _inner.WriteLevelPayloadOnlyToCurrent(levelPayload);
        }

        public void WriteProgressOnlyToCurrent(DataSourceNode progressNode, DataSourceNode progressStateMachinesNode) =>
            _inner.WriteProgressOnlyToCurrent(progressNode, progressStateMachinesNode);

        public SaveGamePayload ReadSavePayloadFromSnapshot(string saveId, string activeLevelId) =>
            _inner.ReadSavePayloadFromSnapshot(saveId, activeLevelId);

        public DataSourceNode? ReadProgressNodeFromSnapshot(string saveId) =>
            _inner.ReadProgressNodeFromSnapshot(saveId);

        public LevelPayload? TryReadLevelPayloadFromCurrent(string levelId) =>
            _inner.TryReadLevelPayloadFromCurrent(levelId);

        public LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId) =>
            _inner.TryReadLevelPayloadFromSnapshot(saveId, levelId);

        public LevelPayload? ResolveLevelPayload(string saveId, string levelId) =>
            _inner.ResolveLevelPayload(saveId, levelId);

        public void SnapshotCurrentToSave(string newSaveId) => _inner.SnapshotCurrentToSave(newSaveId);

        public void DeleteCurrentDirectory() => _inner.DeleteCurrentDirectory();

        public void RestoreExtraFilesFromSnapshot(string saveId) =>
            _inner.RestoreExtraFilesFromSnapshot(saveId);

        public void RestoreExtraFilesFromSnapshot(
            ISaveStorageService sourceStorage, string saveId) =>
            _inner.RestoreExtraFilesFromSnapshot(sourceStorage, saveId);
    }
}
