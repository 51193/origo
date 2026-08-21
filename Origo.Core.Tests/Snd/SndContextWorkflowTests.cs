using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class SndContextWorkflowTests
{
    /// <summary>Helper: seed a complete save snapshot under root/save_{saveId}/.</summary>
    private static void SeedSaveSnapshot(
        TestMemoryFileSystem fs,
        string root,
        string saveId,
        string activeLevelId,
        string progressJson = """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false"}}""")
    {
        var saveDir = $"{root}/save_{saveId}";
        var levelDir = $"{saveDir}/level_{activeLevelId}";
        fs.SeedFile($"{saveDir}/progress.json", progressJson);
        fs.SeedFile($"{saveDir}/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");
        fs.SeedFile($"{levelDir}/session.json", "{}");
        fs.SeedFile($"{levelDir}/session_state_machines.json", """{"machines":[]}""");
    }

    private static void SeedInitialSave(TestMemoryFileSystem fs, string initialRoot, string levelId = "default")
    {
        var saveDir = $"{initialRoot}/save_000";
        var levelDir = $"{saveDir}/level_{levelId}";
        fs.SeedFile($"{saveDir}/progress.json",
            $$$"""{"origo.session_topology":{"type":"String","data":"__foreground__={{{levelId}}}=false"}}""");
        fs.SeedFile($"{saveDir}/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");
        fs.SeedFile($"{levelDir}/session.json", "{}");
        fs.SeedFile($"{levelDir}/session_state_machines.json", """{"machines":[]}""");
    }

    // ── ListSaves ──

    [Fact]
    public void ListSaves_ReturnsEmptyWhenNoSaves()
    {
        var ctx = CreateContext(out _, out _);
        var saves = ctx.Save.ListSaves();
        Assert.Empty(saves);
    }

    [Fact]
    public void ListSaves_ReturnsSaveIds()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "abc", "default");
        var saves = ctx.Save.ListSaves();
        Assert.Contains("abc", saves);
    }

    // ── RequestSaveGame ──

    [Fact]
    public void RequestSaveGame_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.Save.RequestSaveGame(""));
    }

    [Fact]
    public void RequestSaveGame_ThrowsOnNullId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.Save.RequestSaveGame(null!));
    }

    [Fact]
    public void RequestSaveGame_PersistsAndSetsActiveSaveSlot()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        ctx.Save.RequestSaveGame("slot_01");
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/save_slot_01/progress.json"));
        var (found, saveId) = ctx.Blackboard.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("slot_01", saveId);
    }

    [Fact]
    public void RequestSaveGame_IncrementsThenDecrementsPendingCount()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        ctx.Save.RequestSaveGame("slot_02");
        // Before flush, count should be > 0
        Assert.True(ctx.Deferred.GetPendingPersistenceRequestCount() > 0);
        ctx.FlushFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    // ── RequestSaveGameAuto ──

    [Fact]
    public void RequestSaveGameAuto_WithExplicitId_UsesIt()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        var effectiveId = ctx.Save.RequestSaveGameAuto("my_auto");
        Assert.Equal("my_auto", effectiveId);
        ctx.FlushFrame();
        Assert.True(fs.Exists("root/save_my_auto/progress.json"));
    }

    [Fact]
    public void RequestSaveGameAuto_WithNullId_GeneratesTimestamp()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        var effectiveId = ctx.Save.RequestSaveGameAuto();
        Assert.False(string.IsNullOrWhiteSpace(effectiveId));
        // Should be parseable as a long (unix timestamp ms)
        Assert.True(long.TryParse(effectiveId, out _));
        ctx.FlushFrame();
    }

    // ── RequestLoadGame ──

    [Fact]
    public void RequestLoadGame_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.Save.RequestLoadGame(""));
    }

    [Fact]
    public void RequestLoadGame_ThrowsOnNullId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.Save.RequestLoadGame(null!));
    }

    [Fact]
    public void RequestLoadGame_LoadsSaveAndRestoresProgress()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "save1", "default");

        ctx.Save.RequestLoadGame("save1");
        ctx.FlushFrame();

        Assert.NotNull(ctx.Blackboard.ProgressBlackboard);
        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
        var (found, saveId) = ctx.Blackboard.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("save1", saveId);
    }

    [Fact]
    public void RequestLoadGame_IncrementsThenDecrementsPendingCount()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "save2", "default");

        ctx.Save.RequestLoadGame("save2");
        Assert.True(ctx.Deferred.GetPendingPersistenceRequestCount() > 0);
        ctx.FlushFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    // ── HasContinueData / SetContinueTarget / RequestContinueGame ──

    [Fact]
    public void HasContinueData_FalseWhenNoTargetSet()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.Lifecycle.HasContinueData());
    }

    [Fact]
    public void SetContinueTarget_MakesHasContinueDataTrue()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "slot_x", "default");
        ctx.Save.SetContinueTarget("slot_x");
        Assert.True(ctx.Lifecycle.HasContinueData());
    }

    [Fact]
    public void HasContinueData_FalseWhenTargetSaveDoesNotExist()
    {
        var ctx = CreateContext(out _, out _);
        ctx.Save.SetContinueTarget("ghost_slot");

        Assert.False(ctx.Lifecycle.HasContinueData());
    }

    [Fact]
    public void RequestContinueGame_ReturnsFalseWhenTargetSaveDoesNotExist()
    {
        var ctx = CreateContext(out _, out _);
        ctx.Save.SetContinueTarget("ghost_slot");

        Assert.False(ctx.Lifecycle.RequestContinueGame());
    }

    [Fact]
    public void RequestContinueGame_ReturnsFalseWhenNoContinue()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.Lifecycle.RequestContinueGame());
    }

    [Fact]
    public void RequestContinueGame_ReturnsTrueAndLoadsWhenContinueSet()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "cont", "default");
        ctx.Save.SetContinueTarget("cont");

        Assert.True(ctx.Lifecycle.RequestContinueGame());
        ctx.FlushFrame();

        Assert.NotNull(ctx.Blackboard.ProgressBlackboard);
        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
    }

    // ── RequestLoadInitialSave ──

    [Fact]
    public void RequestLoadInitialSave_LoadsFromInitialRoot()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedInitialSave(fs, "res://initial");

        ctx.Lifecycle.RequestLoadInitialSave();
        ctx.FlushFrame();

        Assert.NotNull(ctx.Blackboard.ProgressBlackboard);
        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
        // After initial load, active save id should be cleared
        var (found, saveId) = ctx.Blackboard.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal(string.Empty, saveId);
    }

    [Fact]
    public void RequestLoadInitialSave_WithCustomInitialLevelId_UsesConfiguredLevel()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver,
            "root", "res://initial", "entry.json")
        { InitialLevelId = "my_level" });

        SeedInitialSave(fs, "res://initial", "my_level");

        ctx.Lifecycle.RequestLoadInitialSave();
        ctx.FlushFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("my_level", fg.LevelId);
    }

    [Fact]
    public void RequestLoadInitialSave_RestoresExtraFilesFromInitialRoot()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedInitialSave(fs, "res://initial");
        fs.SeedFile("res://initial/save_000/extra/seed.json", """{"value":1}""");

        ctx.Lifecycle.RequestLoadInitialSave();
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/current/extra/seed.json"),
            "Initial-save extra/ files must be restored into current/extra/ by the initial-load workflow.");
    }

    // ── RequestSwitchForegroundLevel ──

    [Fact]
    public void RequestSwitchForegroundLevel_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.Save.RequestSwitchForegroundLevel(""));
    }

    [Fact]
    public void RequestSwitchForegroundLevel_SwitchesLevel()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        // Seed target level
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", """{"machines":[]}""");

        ctx.Save.RequestSwitchForegroundLevel("b");
        ctx.FlushFrame();

        Assert.Equal("b", ctx.Runtime.SessionManager.ForegroundSession?.LevelId);
    }

    // ── CloneTemplate ──

    [Fact]
    public void CloneTemplate_ClonesAndOverridesName()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        SeedTemplate(fs, "tmpl_a", "OriginalName");
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), fs);
        runtime.SndWorld.LoadTemplates("maps/templates.map", logger);

        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
        var cloned = ctx.Template.CloneTemplate("tmpl_a", "NewName");

        Assert.Equal("NewName", cloned.Name);
    }

    [Fact]
    public void CloneTemplate_WithoutOverrideName_KeepsOriginal()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        SeedTemplate(fs, "tmpl_b", "KeepMe");
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), fs);
        runtime.SndWorld.LoadTemplates("maps/templates.map", logger);

        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
        var cloned = ctx.Template.CloneTemplate("tmpl_b");
        Assert.Equal("KeepMe", cloned.Name);
    }

    private static void SeedTemplate(TestMemoryFileSystem fs, string alias, string name)
    {
        fs.SeedFile("maps/templates.map", $"{alias}: templates/{alias}.json");
        fs.SeedFile($"templates/{alias}.json",
            $$"""
              {
                "name": "{{name}}",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": [] },
                "data": { "pairs": {} }
              }
              """);
    }

    // ── Console API ──

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsFalseForEmptyCommand()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.ConsoleAccess.TrySubmitConsoleCommand(""));
        Assert.False(ctx.ConsoleAccess.TrySubmitConsoleCommand("   "));
    }

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsTrueWhenConsoleInputExists()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);
        Assert.True(ctx.ConsoleAccess.TrySubmitConsoleCommand("snd_count"));
    }

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsFalseWhenNoConsoleInput()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.ConsoleAccess.TrySubmitConsoleCommand("snd_count"));
    }

    [Fact]
    public void DriveFrame_ProcessesQueuedConsoleCommands()
    {
        var ctx = CreateContextWithConsole(out _, out _, out var output);
        var received = new List<string>();
        output.Subscribe(line => received.Add(line));

        ctx.ConsoleAccess.TrySubmitConsoleCommand("snd_count");
        ((IOrigoFrameDriver)ctx.Runtime).DriveFrame(0);

        Assert.Contains(received, s => s.Contains("Snd count:"));
    }

    [Fact]
    public void SubscribeConsoleOutput_ReturnsPositiveId()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);
        var id = ctx.ConsoleAccess.SubscribeConsoleOutput(_ => { });
        Assert.True(id > 0);
    }

    [Fact]
    public void SubscribeConsoleOutput_ThrowsWhenNoChannel()
    {
        var ctx = CreateContext(out _, out _);
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.ConsoleAccess.SubscribeConsoleOutput(_ => { }));
        Assert.Contains("Console", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsubscribeConsoleOutput_RemovesSubscription()
    {
        var ctx = CreateContextWithConsole(out _, out _, out var output);
        var received = new List<string>();
        var subId = ctx.ConsoleAccess.SubscribeConsoleOutput(line => received.Add(line));
        ctx.ConsoleAccess.UnsubscribeConsoleOutput(subId);

        output.Publish("test");
        Assert.Empty(received);
    }

    [Fact]
    public void UnsubscribeConsoleOutput_ZeroId_DoesNotThrow()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);

        var ex = Record.Exception(() => ctx.ConsoleAccess.UnsubscribeConsoleOutput(0));
        Assert.Null(ex);
    }

    [Fact]
    public void UnsubscribeConsoleOutput_NegativeId_DoesNotThrow()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);

        var ex = Record.Exception(() => ctx.ConsoleAccess.UnsubscribeConsoleOutput(-1));
        Assert.Null(ex);
    }

    // ── GetPendingPersistenceRequestCount / EnqueueBusinessDeferred ──

    [Fact]
    public void GetPendingPersistenceRequestCount_InitiallyZero()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void EnqueueBusinessDeferred_ExecutesOnFlush()
    {
        var ctx = CreateContext(out _, out _);
        var executed = false;
        ctx.Deferred.EnqueueBusinessDeferred(() => executed = true);
        Assert.False(executed);
        ctx.FlushFrame();
        Assert.True(executed);
    }

    // ── GetProgressStateMachines ──

    [Fact]
    public void GetProgressStateMachines_NullWhenNoProgress()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Null(ctx.StateMachines.GetProgressStateMachines());
    }

    [Fact]
    public void GetProgressStateMachines_NotNullAfterProgressRunCreated()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);
        Assert.NotNull(ctx.StateMachines.GetProgressStateMachines());
    }

    // ── SndContext constructor validation ──

    [Fact]
    public void Constructor_ThrowsOnNullRuntime()
    {
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        Assert.Throws<ArgumentNullException>(() =>
            new SndContext(new SndContextParameters(null!, io, metaAccess, pathResolver, "root", "init", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnNullFileSystem()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestMemoryFileSystem();
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        Assert.Throws<ArgumentNullException>(() =>
            new SndContext(new SndContextParameters(runtime, null!, metaAccess, pathResolver, "root", "init", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptySaveRootPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        Assert.Throws<ArgumentException>(() =>
            new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "", "init", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyInitialSaveRootPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        Assert.Throws<ArgumentException>(() =>
            new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyEntryConfigPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        Assert.Throws<ArgumentException>(() =>
            new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "init", "")));
    }

    [Fact]
    public void Constructor_ThrowsOnBlankInitialLevelId()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var parameters = new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "init", "e.json")
        { InitialLevelId = "" };

        Assert.Throws<ArgumentException>(() => new SndContext(parameters));
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidInitialLevelId()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var parameters = new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "init", "e.json")
        { InitialLevelId = "bad/level" };

        Assert.Throws<ArgumentException>(() => new SndContext(parameters));
    }

    // ── SndContext initial state ──

    [Fact]
    public void InitialState_NoProgressBlackboard_NoForegroundSession()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Null(ctx.Blackboard.ProgressBlackboard);
        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.NotNull(ctx.Blackboard.SystemBlackboard);
        Assert.NotNull(ctx.Runtime.SessionManager);
        Assert.NotNull(ctx.Runtime.SessionManager);
    }

    // ── BeginWorkflow concurrent guard ──

    [Fact]
    public void RequestSaveGame_ConcurrentWorkflow_AllowsSequentialSavesInSingleFlush()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        // Request save twice - second enqueued action should throw
        ctx.Save.RequestSaveGame("slot_a");
        ctx.Save.RequestSaveGame("slot_b");
        // The second call will try BeginWorkflow while first is in progress within same flush
        // Both are enqueued as system deferred; first completes, second runs after
        // Actually both run in same flush, sequentially, so this should succeed
        var ex = Record.Exception(() => ctx.FlushFrame());
        // The first save succeeds and EndWorkflow is called, so the second should also succeed
        Assert.Null(ex);
    }

    // ── Helpers ──

    private static SndContext CreateContext(out TestMemoryFileSystem fs, out TestLogger logger)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        return new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
    }

    private static SndContext CreateContextWithConsole(
        out TestMemoryFileSystem fs,
        out TestLogger logger,
        out ConsoleOutputChannel output)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputBuffer();
        output = new ConsoleOutputChannel();
        var bb = new Blackboard.Blackboard();
        var tm = new TypeStringMapping();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output);
        fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        return new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
    }

    private static void SetupProgressRun(SndContext ctx, TestMemoryFileSystem fs)
    {
        // Load main menu entry to establish a ProgressRun
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. NullSndContext — cover all no-op methods
// ─────────────────────────────────────────────────────────────────────────────
