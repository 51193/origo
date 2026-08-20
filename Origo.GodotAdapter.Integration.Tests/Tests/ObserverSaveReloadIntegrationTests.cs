using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;
using Origo.Core.Snd.Scene;

namespace Origo.GodotAdapter.Integration.Tests;

public class ObserverSaveReloadIntegrationTests
{
    [IntegrationTest(Description = "Observer bindings are restored after a full save/load cycle on the Godot adapter host")]
    public void ObserverBindings_RestoredAcrossSaveAndReload()
    {
        TrackingObserverEvents.Events = [];
        try
        {
            using var harness = new IntegrationTestHarness();
            harness.BindRuntimeDependencies();

            var fs = new GodotFileSystem();
            if (fs.DirectoryExists("user://test_saves"))
                fs.DeleteDirectory("user://test_saves");
            fs.WriteAllText("user://entry.json",
                """{ "levels": { "main_menu": { "snd_scene": "user://main_menu.json" } }, "main_menu_level": "main_menu" }""",
                overwrite: true);
            fs.WriteAllText("user://main_menu.json", "[]", overwrite: true);

            var context = new SndContext(new SndContextParameters(
                harness.Runtime,
                DataSourceFactory.CreateDefaultIoGateway(fs),
                DataSourceFactory.CreateFileMetaAccess(fs),
                DataSourceFactory.CreatePathResolver(fs),
                "user://test_saves", "res://initial", "user://entry.json"));
            ((ISndContextAttachableSceneHost)harness.SndManager).BindContext(context);


            context.Bootstrap();
            ((IOrigoFrameDriver)context.Runtime).DriveFrame(0);

            var session = context.Runtime.SessionManager.ForegroundSession;
            IntegrationTestRunner.AssertNotNull(session, "foreground session after bootstrap");

            var target = session!.Spawn(new Origo.Core.Snd.Metadata.SndMetaData
            {
                Name = "godot_target",
                NodeMetaData = new Origo.Core.Snd.Metadata.NodeMetaData(),
                StrategyMetaData = new Origo.Core.Snd.Metadata.StrategyMetaData(),
                DataMetaData = new Origo.Core.Snd.Metadata.DataMetaData()
            });
            var observer = session.Spawn(new Origo.Core.Snd.Metadata.SndMetaData
            {
                Name = "godot_observer",
                NodeMetaData = new Origo.Core.Snd.Metadata.NodeMetaData(),
                StrategyMetaData = new Origo.Core.Snd.Metadata.StrategyMetaData(),
                DataMetaData = new Origo.Core.Snd.Metadata.DataMetaData()
            });

            observer.MountObserverStrategy(target, GodotTrackingObserver.Index);
            target.SetData("character.hp", 10);
            IntegrationTestRunner.Assert(
                TrackingObserverEvents.Events.Exists(e => e == "changed:character.hp"),
                "observer should fire on data change before save");

            context.Save.RequestSaveGame("obs_godot_slot");
            ((IOrigoFrameDriver)context.Runtime).DriveFrame(0);

            foreach (var key in context.Runtime.SessionManager.Keys)
                context.Runtime.SessionManager.DestroySession(key);
            context.SetProgressRun(null);

            TrackingObserverEvents.Events.Clear();

            context.Save.RequestLoadGame("obs_godot_slot");
            ((IOrigoFrameDriver)context.Runtime).DriveFrame(0);

            var reloaded = context.Runtime.SessionManager.ForegroundSession;
            IntegrationTestRunner.AssertNotNull(reloaded, "foreground session after load");
            var reloadedTarget = reloaded!.FindByName("godot_target");
            IntegrationTestRunner.AssertNotNull(reloadedTarget, "target restored");

            reloadedTarget!.SetData("character.hp", 50);
            IntegrationTestRunner.Assert(
                TrackingObserverEvents.Events.Exists(e => e == "changed:character.hp"),
                "observer binding should be restored after save/load (data change must be observed)");
        }
        finally
        {
            TrackingObserverEvents.Events = null;
        }
    }
    [IntegrationTest(Description = "Session teardown fires OnUnmounted for mounted observers on the Godot adapter host")]
    public void ObserverOnUnmounted_FiresOnSessionDestroy()
    {
        TrackingObserverEvents.Events = [];
        try
        {
            using var harness = new IntegrationTestHarness();
            harness.BindRuntimeDependencies();

            var fs = new GodotFileSystem();
            if (fs.DirectoryExists("user://test_saves"))
                fs.DeleteDirectory("user://test_saves");
            fs.WriteAllText("user://entry.json",
                """{ "levels": { "main_menu": { "snd_scene": "user://main_menu.json" } }, "main_menu_level": "main_menu" }""",
                overwrite: true);
            fs.WriteAllText("user://main_menu.json", "[]", overwrite: true);

            var context = new SndContext(new SndContextParameters(
                harness.Runtime,
                DataSourceFactory.CreateDefaultIoGateway(fs),
                DataSourceFactory.CreateFileMetaAccess(fs),
                DataSourceFactory.CreatePathResolver(fs),
                "user://test_saves", "res://initial", "user://entry.json"));
            ((ISndContextAttachableSceneHost)harness.SndManager).BindContext(context);


            context.Bootstrap();
            ((IOrigoFrameDriver)context.Runtime).DriveFrame(0);

            var session = context.Runtime.SessionManager.ForegroundSession;
            IntegrationTestRunner.AssertNotNull(session, "foreground session after bootstrap");

            var target = session!.Spawn(new SndMetaData
            {
                Name = "godot_quit_target",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(),
                DataMetaData = new DataMetaData()
            });
            var observer = session.Spawn(new SndMetaData
            {
                Name = "godot_quit_observer",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(),
                DataMetaData = new DataMetaData()
            });

            observer.MountObserverStrategy(target, GodotTrackingObserver.Index);

            TrackingObserverEvents.Events.Clear();
            context.Runtime.SessionManager.DestroySession("__foreground__");

            IntegrationTestRunner.Assert(
                TrackingObserverEvents.Events.Exists(e => e == "unmounted:godot_quit_target"),
                "OnUnmounted should fire when the session is destroyed");
        }
        finally
        {
            TrackingObserverEvents.Events = null;
        }
    }

    [IntegrationTest(Description = "Killing a wrapper (GodotSndEntity) observer tears down its bindings before release")]
    public void ObserverOnUnmounted_FiresWhenWrapperObserverKilled()
    {
        TrackingObserverEvents.Events = [];
        try
        {
            using var harness = new IntegrationTestHarness();
            harness.BindRuntimeDependencies();

            var fs = new GodotFileSystem();
            if (fs.DirectoryExists("user://test_saves"))
                fs.DeleteDirectory("user://test_saves");
            fs.WriteAllText("user://entry.json",
                """{ "levels": { "main_menu": { "snd_scene": "user://main_menu.json" } }, "main_menu_level": "main_menu" }""",
                overwrite: true);
            fs.WriteAllText("user://main_menu.json", "[]", overwrite: true);

            var context = new SndContext(new SndContextParameters(
                harness.Runtime,
                DataSourceFactory.CreateDefaultIoGateway(fs),
                DataSourceFactory.CreateFileMetaAccess(fs),
                DataSourceFactory.CreatePathResolver(fs),
                "user://test_saves", "res://initial", "user://entry.json"));
            ((ISndContextAttachableSceneHost)harness.SndManager).BindContext(context);

            context.Bootstrap();
            ((IOrigoFrameDriver)context.Runtime).DriveFrame(0);

            var session = context.Runtime.SessionManager.ForegroundSession;
            IntegrationTestRunner.AssertNotNull(session, "foreground session after bootstrap");

            var target = session!.Spawn(new SndMetaData
            {
                Name = "godot_kill_target",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(),
                DataMetaData = new DataMetaData()
            });
            var observer = session.Spawn(new SndMetaData
            {
                Name = "godot_kill_observer",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(),
                DataMetaData = new DataMetaData()
            });

            observer.MountObserverStrategy(target, GodotTrackingObserver.Index);

            TrackingObserverEvents.Events.Clear();
            session.RequestKillEntity("godot_kill_observer");
            context.Runtime.SessionManager.KillPendingAllSessions();

            IntegrationTestRunner.Assert(
                TrackingObserverEvents.Events.Exists(e => e == "unmounted:godot_kill_target"),
                "killing a wrapper observer must fire OnUnmounted and tear down its binding");
        }
        finally
        {
            TrackingObserverEvents.Events = null;
        }
    }
}

internal static class TrackingObserverEvents
{
    public static List<string>? Events;
}

[StrategyIndex(GodotTrackingObserver.Index)]
[ObserveData("character.hp")]
internal sealed class GodotTrackingObserver : ObserverStrategyBase
{
    public const string Index = "test.godot.obs.track";

    public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
        string dataKey, TypedData oldValue, TypedData newValue)
        => TrackingObserverEvents.Events?.Add($"changed:{dataKey}");

    public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
        => TrackingObserverEvents.Events?.Add($"unmounted:{target.Name}");
}
