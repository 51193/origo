using System;
using System.Collections.Generic;
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
        TestFileSystem fs,
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

    private static void SeedInitialSave(TestFileSystem fs, string initialRoot, string levelId = "default")
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
        var saves = ctx.ListSaves();
        Assert.Empty(saves);
    }

    [Fact]
    public void ListSaves_ReturnsSaveIds()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "abc", "default");
        var saves = ctx.ListSaves();
        Assert.Contains("abc", saves);
    }

    // ── RequestSaveGame ──

    [Fact]
    public void RequestSaveGame_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.RequestSaveGame(""));
    }

    [Fact]
    public void RequestSaveGame_PersistsAndSetsActiveSaveSlot()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/progress.json"));
        var (found, saveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("slot_01", saveId);
    }

    [Fact]
    public void RequestSaveGame_IncrementsThenDecrementsPendingCount()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        ctx.RequestSaveGame("slot_02");
        // Before flush, count should be > 0
        Assert.True(ctx.GetPendingPersistenceRequestCount() > 0);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
    }

    // ── RequestSaveGameAuto ──

    [Fact]
    public void RequestSaveGameAuto_WithExplicitId_UsesIt()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        var effectiveId = ctx.RequestSaveGameAuto("my_auto");
        Assert.Equal("my_auto", effectiveId);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.True(fs.Exists("root/save_my_auto/progress.json"));
    }

    [Fact]
    public void RequestSaveGameAuto_WithNullId_GeneratesTimestamp()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        var effectiveId = ctx.RequestSaveGameAuto();
        Assert.False(string.IsNullOrWhiteSpace(effectiveId));
        // Should be parseable as a long (unix timestamp ms)
        Assert.True(long.TryParse(effectiveId, out _));
        ctx.FlushDeferredActionsForCurrentFrame();
    }

    // ── RequestLoadGame ──

    [Fact]
    public void RequestLoadGame_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.RequestLoadGame(""));
    }

    [Fact]
    public void RequestLoadGame_LoadsSaveAndRestoresProgress()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "save1", "default");

        ctx.RequestLoadGame("save1");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
        var (found, saveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("save1", saveId);
    }

    [Fact]
    public void RequestLoadGame_IncrementsThenDecrementsPendingCount()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "save2", "default");

        ctx.RequestLoadGame("save2");
        Assert.True(ctx.GetPendingPersistenceRequestCount() > 0);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
    }

    // ── HasContinueData / SetContinueTarget / RequestContinueGame ──

    [Fact]
    public void HasContinueData_FalseWhenNoTargetSet()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.HasContinueData());
    }

    [Fact]
    public void SetContinueTarget_MakesHasContinueDataTrue()
    {
        var ctx = CreateContext(out _, out _);
        ctx.SetContinueTarget("slot_x");
        Assert.True(ctx.HasContinueData());
    }

    [Fact]
    public void RequestContinueGame_ReturnsFalseWhenNoContinue()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.RequestContinueGame());
    }

    [Fact]
    public void RequestContinueGame_ReturnsTrueAndLoadsWhenContinueSet()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "cont", "default");
        ctx.SetContinueTarget("cont");

        Assert.True(ctx.RequestContinueGame());
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
    }

    // ── RequestLoadInitialSave ──

    [Fact]
    public void RequestLoadInitialSave_LoadsFromInitialRoot()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedInitialSave(fs, "res://initial");

        ctx.RequestLoadInitialSave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
        // After initial load, active save id should be cleared
        var (found, saveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal(string.Empty, saveId);
    }

    // ── RequestSwitchForegroundLevel ──

    [Fact]
    public void RequestSwitchForegroundLevel_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.RequestSwitchForegroundLevel(""));
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

        ctx.RequestSwitchForegroundLevel("b");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal("b", ctx.SessionManager.ForegroundSession?.LevelId);
    }

    // ── CloneTemplate ──

    [Fact]
    public void CloneTemplate_ClonesAndOverridesName()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        SeedTemplate(fs, "tmpl_a", "OriginalName");
        runtime.SndWorld.LoadTemplates(fs, "maps/templates.map", logger);

        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));
        var cloned = ctx.CloneTemplate("tmpl_a", "NewName");

        Assert.Equal("NewName", cloned.Name);
    }

    [Fact]
    public void CloneTemplate_WithoutOverrideName_KeepsOriginal()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        SeedTemplate(fs, "tmpl_b", "KeepMe");
        runtime.SndWorld.LoadTemplates(fs, "maps/templates.map", logger);

        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));
        var cloned = ctx.CloneTemplate("tmpl_b");
        Assert.Equal("KeepMe", cloned.Name);
    }

    private static void SeedTemplate(TestFileSystem fs, string alias, string name)
    {
        fs.SeedFile("maps/templates.map", $"{alias}: templates/{alias}.json");
        fs.SeedFile($"templates/{alias}.json",
            $$"""
              {
                "name": "{{name}}",
                "node": { "pairs": {} },
                "strategy": { "indices": [] },
                "data": { "pairs": {} }
              }
              """);
    }

    // ── Console API ──

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsFalseForEmptyCommand()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.TrySubmitConsoleCommand(""));
        Assert.False(ctx.TrySubmitConsoleCommand("   "));
    }

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsTrueWhenConsoleInputExists()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);
        Assert.True(ctx.TrySubmitConsoleCommand("snd_count"));
    }

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsFalseWhenNoConsoleInput()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.TrySubmitConsoleCommand("snd_count"));
    }

    [Fact]
    public void ProcessConsolePending_ProcessesQueuedCommands()
    {
        var ctx = CreateContextWithConsole(out _, out _, out var output);
        var received = new List<string>();
        output.Subscribe(line => received.Add(line));

        ctx.TrySubmitConsoleCommand("snd_count");
        ctx.ProcessConsolePending();

        Assert.Contains(received, s => s.Contains("Snd count:"));
    }

    [Fact]
    public void SubscribeConsoleOutput_ReturnsPositiveId()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);
        var id = ctx.SubscribeConsoleOutput(_ => { });
        Assert.True(id > 0);
    }

    [Fact]
    public void SubscribeConsoleOutput_ThrowsWhenNoChannel()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<InvalidOperationException>(() => ctx.SubscribeConsoleOutput(_ => { }));
    }

    [Fact]
    public void UnsubscribeConsoleOutput_RemovesSubscription()
    {
        var ctx = CreateContextWithConsole(out _, out _, out var output);
        var received = new List<string>();
        var subId = ctx.SubscribeConsoleOutput(line => received.Add(line));
        ctx.UnsubscribeConsoleOutput(subId);

        output.Publish("test");
        Assert.Empty(received);
    }

    // ── GetPendingPersistenceRequestCount / EnqueueBusinessDeferred ──

    [Fact]
    public void GetPendingPersistenceRequestCount_InitiallyZero()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void EnqueueBusinessDeferred_ExecutesOnFlush()
    {
        var ctx = CreateContext(out _, out _);
        var executed = false;
        ctx.EnqueueBusinessDeferred(() => executed = true);
        Assert.False(executed);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.True(executed);
    }

    // ── GetProgressStateMachines ──

    [Fact]
    public void GetProgressStateMachines_NullWhenNoProgress()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Null(ctx.GetProgressStateMachines());
    }

    [Fact]
    public void GetProgressStateMachines_NotNullAfterProgressRunCreated()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);
        Assert.NotNull(ctx.GetProgressStateMachines());
    }

    // ── SndContext constructor validation ──

    [Fact]
    public void Constructor_ThrowsOnNullRuntime()
    {
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentNullException>(() =>
            new SndContext(new SndContextParameters(null!, fs, "root", "init", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnNullFileSystem()
    {
        var runtime = TestFactory.CreateRuntime();
        Assert.Throws<ArgumentNullException>(() =>
            new SndContext(new SndContextParameters(runtime, null!, "root", "init", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptySaveRootPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentException>(() =>
            new SndContext(new SndContextParameters(runtime, fs, "", "init", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyInitialSaveRootPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentException>(() =>
            new SndContext(new SndContextParameters(runtime, fs, "root", "", "e.json")));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyEntryConfigPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentException>(() =>
            new SndContext(new SndContextParameters(runtime, fs, "root", "init", "")));
    }

    // ── SndContext initial state ──

    [Fact]
    public void InitialState_NoProgressBlackboard_NoCurrentSession()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Null(ctx.ProgressBlackboard);
        Assert.Null(ctx.CurrentSession);
        Assert.NotNull(ctx.SystemBlackboard);
        Assert.NotNull(ctx.SessionManager);
        Assert.NotNull(ctx.SndRuntime);
    }

    // ── BeginWorkflow concurrent guard ──

    [Fact]
    public void RequestSaveGame_ConcurrentWorkflow_AllowsSequentialSavesInSingleFlush()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        // Request save twice - second enqueued action should throw
        ctx.RequestSaveGame("slot_a");
        ctx.RequestSaveGame("slot_b");
        // The second call will try BeginWorkflow while first is in progress within same flush
        // Both are enqueued as system deferred; first completes, second runs after
        // Actually both run in same flush, sequentially, so this should succeed
        var ex = Record.Exception(() => ctx.FlushDeferredActionsForCurrentFrame());
        // The first save succeeds and EndWorkflow is called, so the second should also succeed
        Assert.Null(ex);
    }

    // ── Helpers ──

    private static SndContext CreateContext(out TestFileSystem fs, out TestLogger logger)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        fs = new TestFileSystem();
        return new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));
    }

    private static SndContext CreateContextWithConsole(
        out TestFileSystem fs,
        out TestLogger logger,
        out ConsoleOutputChannel output)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputQueue();
        output = new ConsoleOutputChannel();
        var bb = new Blackboard.Blackboard();
        var tm = new TypeStringMapping();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output);
        fs = new TestFileSystem();
        return new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));
    }

    private static void SetupProgressRun(SndContext ctx, TestFileSystem fs)
    {
        // Load main menu entry to establish a ProgressRun
        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. NullSndContext — cover all no-op methods
// ─────────────────────────────────────────────────────────────────────────────
