using System;
using System.Collections.Generic;
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Compatibility contract tests that drive the supported shell entry surface
///     (<see cref="OrigoHost" /> and the stable Contracts companions) and pin the
///     lifecycle, observer, fail-fast, and state-transition behavior the shell
///     promises to kernel-version changes.
/// </summary>
public class ShellCompatibilityContractTests
{
    private static OrigoHost CreateHost(MemoryFileSystem fileSystem)
    {
        var host = OrigoHost.Create(new OrigoHostOptions
        {
            Meta = new OrigoMeta("Compatibility", "1.0.0", "compatibility"),
            FileSystem = fileSystem,
            SaveRootPath = "root",
            InitialSaveRootPath = "res://initial",
            EntryConfigPath = "entry.json",
            AutoDiscoverStrategies = false,
        });
        fileSystem.WriteAllText("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""",
            overwrite: false);
        fileSystem.WriteAllText("res://levels/main_menu.json", "[]", overwrite: false);
        return host;
    }

    [Fact]
    public void ShellEntry_LifecycleOrdering_IsPreservedThroughSaveLoad()
    {
        var events = new List<string>();
        LifecycleProbeStrategy.Events = events;
        try
        {
            var host = CreateHost(new MemoryFileSystem());
            host.Runtime.SndWorld.RegisterStrategy(static () => new LifecycleProbeStrategy());
            host.Bootstrap();
            host.DriveFrame(0.016);

            var session = host.Runtime.SessionManager.ForegroundSession;
            Assert.NotNull(session);
            session.Spawn(new SndMetaFluentBuilder("hero")
                .AddLifecycleStrategy(LifecycleProbeStrategy.Index)
                .SetInt("hp", 100)
                .Build());
            Assert.Contains($"after_spawn:hero", events);

            host.DriveFrame(0.016);
            Assert.Contains("process:hero", events);

            host.Context.Save.RequestSaveGame("compat_lifecycle");
            host.DriveFrame(0.016);
            Assert.Contains("before_save:hero", events);

            host.Context.Save.RequestLoadGame("compat_lifecycle");
            host.DriveFrame(0.016);

            var loadedSession = host.Runtime.SessionManager.ForegroundSession;
            Assert.NotNull(loadedSession);
            var loaded = loadedSession.FindByName("hero");
            Assert.NotNull(loaded);
            Assert.Contains("after_load:hero", events);

            loadedSession.RequestKillEntity("hero");
            host.DriveFrame(0.016);
            Assert.Contains("before_dead:hero", events);
            Assert.Null(loadedSession.FindByName("hero"));

            AssertOrder(
                events,
                "after_spawn:hero",
                "process:hero",
                "before_save:hero",
                "after_load:hero",
                "before_dead:hero");
        }
        finally
        {
            LifecycleProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void ShellEntry_ObserverRecovery_IsPreservedThroughSaveLoad()
    {
        var events = new List<string>();
        ObserverProbeStrategy.Events = events;
        try
        {
            var host = CreateHost(new MemoryFileSystem());
            host.Runtime.SndWorld.RegisterStrategy(static () => new ObserverProbeStrategy());
            host.Bootstrap();
            host.DriveFrame(0.016);

            var session = host.Runtime.SessionManager.ForegroundSession;
            Assert.NotNull(session);
            var target = session.Spawn(new SndMetaFluentBuilder("target").SetInt("hp", 10).Build());
            var observer = session.Spawn(new SndMetaFluentBuilder("observer").Build());
            observer.MountObserverStrategy(target, ObserverProbeStrategy.Index);
            Assert.Contains("on_mounted:target", events);

            target.SetData("hp", 20);
            Assert.Contains("on_data_changed:target:hp", events);

            host.Context.Save.RequestSaveGame("compat_observer");
            host.DriveFrame(0.016);

            // Recovery must re-mount the persisted observer binding after load.
            events.Clear();
            host.Context.Save.RequestLoadGame("compat_observer");
            host.DriveFrame(0.016);

            var reloadedSession = host.Runtime.SessionManager.ForegroundSession;
            Assert.NotNull(reloadedSession);
            var reloadedTarget = reloadedSession.FindByName("target");
            var reloadedObserver = reloadedSession.FindByName("observer");
            Assert.NotNull(reloadedTarget);
            Assert.NotNull(reloadedObserver);
            Assert.Contains("on_mounted:target", events);

            reloadedTarget.SetData("hp", 30);
            Assert.Contains("on_data_changed:target:hp", events);
            Assert.Equal(30, reloadedTarget.GetData<int>("hp"));
        }
        finally
        {
            ObserverProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void ShellEntry_FailFastValidation_IsPreservedAfterBootstrap()
    {
        var host = CreateHost(new MemoryFileSystem());
        host.Runtime.SndWorld.RegisterStrategy(static () => new LifecycleProbeStrategy());
        host.Bootstrap();
        host.DriveFrame(0.016);

        // Strategy registration is sealed by Bootstrap; a late registration
        // must fail fast through the stable world contract.
        Assert.Throws<InvalidOperationException>(() =>
            host.Runtime.SndWorld.RegisterStrategy(static () => new LateProbeStrategy()));

        // Invalid save and entity operations surface their contract violations
        // on the shell facade instead of silently degrading.
        Assert.Throws<ArgumentException>(() => host.Context.Save.RequestLoadGame("bad/id"));
        var session = host.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(session);
        Assert.Throws<ArgumentNullException>(() => session.Spawn(null!));
    }

    [Fact]
    public void ShellEntry_SessionStateTransitions_ArePreserved()
    {
        var events = new List<string>();
        LifecycleProbeStrategy.Events = events;
        try
        {
            var host = CreateHost(new MemoryFileSystem());
            host.Runtime.SndWorld.RegisterStrategy(static () => new LifecycleProbeStrategy());
            host.Bootstrap();
            host.DriveFrame(0.016);

            var manager = host.Runtime.SessionManager;
            var background = manager.CreateBackgroundSession("background", "background_level", syncProcess: true);
            background.Spawn(new SndMetaFluentBuilder("worker")
                .AddLifecycleStrategy(LifecycleProbeStrategy.Index)
                .Build());
            Assert.Contains("after_spawn:worker", events);

            manager.DestroySession("background");
            Assert.Contains("before_quit:worker", events);
            Assert.DoesNotContain("background", manager.Keys);
        }
        finally
        {
            LifecycleProbeStrategy.Events = null;
        }
    }

    private static void AssertOrder(List<string> events, params string[] expectedOrder)
    {
        var previous = -1;
        foreach (var expected in expectedOrder)
        {
            var index = events.IndexOf(expected);
            Assert.True(index >= 0, $"Missing lifecycle event '{expected}'. Events: {string.Join(", ", events)}");
            Assert.True(index > previous, $"Lifecycle event '{expected}' is out of order. Events: {string.Join(", ", events)}");
            previous = index;
        }
    }
}

[StrategyIndex(Index)]
internal sealed class LifecycleProbeStrategy : LifecycleStrategyBase
{
    internal const string Index = "compat.lifecycle.probe";

    private static readonly System.Threading.AsyncLocal<List<string>?> _events = new();

    internal static List<string>? Events
    {
        get => _events.Value;
        set => _events.Value = value;
    }

    public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => Add("after_spawn", entity);
    public override void Process(ISndEntity entity, double delta, ISndContext ctx) => Add("process", entity);
    public override void AfterLoad(ISndEntity entity, ISndContext ctx) => Add("after_load", entity);
    public override void BeforeSave(ISndEntity entity, ISndContext ctx) => Add("before_save", entity);
    public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => Add("before_quit", entity);
    public override void BeforeDead(ISndEntity entity, ISndContext ctx) => Add("before_dead", entity);

    private static void Add(string phase, ISndEntity entity) =>
        Events?.Add($"{phase}:{entity.Name}");
}

[StrategyIndex(Index)]
[ObserveData("hp")]
internal sealed class ObserverProbeStrategy : ObserverStrategyBase
{
    internal const string Index = "compat.observer.probe";

    private static readonly System.Threading.AsyncLocal<List<string>?> _events = new();

    internal static List<string>? Events
    {
        get => _events.Value;
        set => _events.Value = value;
    }

    public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
        Events?.Add($"on_mounted:{target.Name}");

    public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
        string dataKey, TypedData oldValue, TypedData newValue) =>
        Events?.Add($"on_data_changed:{target.Name}:{dataKey}");

    public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
        Events?.Add($"on_unmounted:{target.Name}");
}

[StrategyIndex(Index)]
internal sealed class LateProbeStrategy : LifecycleStrategyBase
{
    internal const string Index = "compat.lifecycle.late";
}
