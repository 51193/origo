using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests for foreground-level switching: the old foreground
///     session must be destroyed with full disposal semantics (BeforeQuit
///     hooks, observer teardown, strategy pool release) before the new
///     foreground is mounted, so that switching away and back to a level
///     re-mounts observer bindings cleanly.
/// </summary>
public class SwitchForegroundCleanupTests
{
    private const string BeforeQuitIdx = "switch_cleanup.before_quit";
    private const string ObserverIdx = "switch_cleanup.observer";

    [Fact]
    public void SwitchForeground_RunsFullDisposalSemantics_ForOldForegroundEntities()
    {
        var (ctx, fs, logger) = CreateContext();
        var quitEvents = new List<string>();
        var observerEvents = new List<string>();
        BeforeQuitSpyStrategy.Bind(quitEvents);
        ObserverSpyStrategy.Bind(observerEvents);

        var fg = ctx.EnsureProgressRun().SessionManager.ForegroundSession
                 ?? throw new InvalidOperationException("Foreground session not created.");
        var target = fg.Spawn(CreateMeta("target", [BeforeQuitIdx]));
        var observer = fg.Spawn(CreateMeta("observer"));
        observer.MountObserverStrategy(target, ObserverIdx);

        SeedEmptyLevel(fs, "level_b");

        ctx.Save.RequestSwitchForegroundLevel("level_b");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // BeforeQuit must have fired for every entity of the old foreground.
        Assert.Contains("BeforeQuit:target", quitEvents);

        // Observer bindings must have been torn down bidirectionally.
        Assert.Contains("OnUnmounted:observer->target", observerEvents);

        // Every strategy reference must have been returned to the pool.
        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    [Fact]
    public void SwitchForeground_BackToPreviousLevel_RemountsObserverBindings()
    {
        var (ctx, fs, _) = CreateContext();
        var observerEvents = new List<string>();
        ObserverSpyStrategy.Bind(observerEvents);

        var fg = ctx.EnsureProgressRun().SessionManager.ForegroundSession
                 ?? throw new InvalidOperationException("Foreground session not created.");
        var target = fg.Spawn(CreateMeta("target", [BeforeQuitIdx]));
        var observer = fg.Spawn(CreateMeta("observer"));
        observer.MountObserverStrategy(target, ObserverIdx);

        SeedEmptyLevel(fs, "level_b");

        // Level A -> B
        ctx.Save.RequestSwitchForegroundLevel("level_b");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        observerEvents.Clear();

        // Level B -> A (back to the original level): the persisted observer
        // bindings must re-mount on the recovered entities.
        ctx.Save.RequestSwitchForegroundLevel("test_level");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var fgAfter = ctx.EnsureProgressRun().SessionManager.ForegroundSession;
        Assert.NotNull(fgAfter);
        Assert.Equal("test_level", fgAfter.LevelId);
        Assert.Contains(observerEvents, e => e == "OnMounted:observer->target");
    }

    [Fact]
    public void SwitchForeground_LoadFailure_LeavesNoHalfMountedForeground()
    {
        var (ctx, fs, _) = CreateContext();

        var fgBefore = ctx.EnsureProgressRun().SessionManager.ForegroundSession;
        Assert.NotNull(fgBefore);
        fgBefore.Spawn(CreateMeta("survivor", [BeforeQuitIdx]));

        // A corrupt target-level payload whose snd_scene references an
        // unregistered strategy: entity recovery fails mid-load.
        SeedBadLevel(fs, "bad_level");

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            ctx.Save.RequestSwitchForegroundLevel("bad_level");
            ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        });
        Assert.Contains("not found", ex.Message, StringComparison.Ordinal);

        // The failed switch must not leave a half-mounted foreground behind:
        // the old foreground was already destroyed, and the partially loaded
        // new session must have been disposed as well.
        var sessionManager = ctx.EnsureProgressRun().SessionManager;
        Assert.Null(sessionManager.ForegroundSession);
        Assert.False(sessionManager.Contains(ISessionManager.ForegroundKey));

        // A subsequent switch to a healthy level must still succeed.
        SeedEmptyLevel(fs, "level_b");
        ctx.Save.RequestSwitchForegroundLevel("level_b");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var fgAfter = ctx.EnsureProgressRun().SessionManager.ForegroundSession;
        Assert.NotNull(fgAfter);
        Assert.Equal("level_b", fgAfter.LevelId);
    }

    private static (SndContext ctx, TestMemoryFileSystem fs, TestLogger logger) CreateContext()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var tm = new TypeStringMapping();
        var systemBb = new Blackboard.Blackboard();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, systemBb, dataSourceIo);
        runtime.SndWorld.RegisterStrategy(() => new BeforeQuitSpyStrategy());
        runtime.SndWorld.RegisterStrategy(() => new ObserverSpyStrategy());

        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "res://initial", "res://entry/entry.json"));
        host.BindWorld(runtime.SndWorld);
        host.BindContext(ctx);

        var progressRun = TestFactory.CreateProgressRun(
            "test_save", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("test_level");

        return (ctx, fs, logger);
    }

    private static void SeedEmptyLevel(TestMemoryFileSystem fs, string levelId)
    {
        fs.SeedFile($"root/current/level_{levelId}/snd_scene.json", "[]");
        fs.SeedFile($"root/current/level_{levelId}/session.json", "{}");
        fs.SeedFile($"root/current/level_{levelId}/session_state_machines.json", "{\"machines\":[]}");
    }

    private static void SeedBadLevel(TestMemoryFileSystem fs, string levelId)
    {
        fs.SeedFile($"root/current/level_{levelId}/snd_scene.json",
            """
            [
              {
                "name": "half_made",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": ["switch_cleanup.nonexistent"], "active_indices": [] },
                "data": { "pairs": {} }
              }
            ]
            """);
        fs.SeedFile($"root/current/level_{levelId}/session.json", "{}");
        fs.SeedFile($"root/current/level_{levelId}/session_state_machines.json", "{\"machines\":[]}");
    }

    private static SndMetaData CreateMeta(string name, string[]? lifecycleIndices = null) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [.. lifecycleIndices ?? []]
            },
            DataMetaData = new DataMetaData()
        };

    [StrategyIndex(BeforeQuitIdx)]
    private sealed class BeforeQuitSpyStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static void Bind(List<string> events) => _events.Value = events;

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"BeforeQuit:{entity.Name}");
    }

    [StrategyIndex(ObserverIdx)]
    [ObserveData("target.hp")]
    private sealed class ObserverSpyStrategy : ObserverStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static void Bind(List<string> events) => _events.Value = events;

        public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            _events.Value?.Add($"OnMounted:{entity.Name}->{target.Name}");

        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            _events.Value?.Add($"OnUnmounted:{entity.Name}->{target.Name}");
    }
}
