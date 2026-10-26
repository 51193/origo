using System;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

/// <summary>
///     Regression for the normal adapter exit path. Godot calls a child's
///     _ExitTree before its parent's, so the manager must shut the context down
///     while the entity collection is still populated; otherwise the entry's
///     later shutdown cannot run entity BeforeQuit hooks.
/// </summary>
public partial class OrigoDefaultEntryExitTreeIntegrationTests : IDeferredTestFixture, IDisposable
{
    private const string _probeIndex = "test.entry_exit.before_quit_probe";

    private ProbeEntry? _entry;
    private int _frame;

    public bool IsComplete => _frame >= 1;

    public void Setup()
    {
        _frame = 0;
        BeforeQuitProbeStrategy.BeforeQuitCalls = 0;
        _entry = new ProbeEntry { Name = "EntryExitProbe" };
    }

    public void AdvanceFrame() => _frame++;

    [DeferredTest(Description = "Freeing a live OrigoDefaultEntry runs foreground entity BeforeQuit")]
    public void NormalEntryExit_RunsBeforeQuit()
    {
        var entry = _entry!;
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(entry);

        // Bootstrap enqueues the main-menu entry load; drive one frame so the
        // foreground ProgressRun exists before spawning the probe entity.
        entry.Runtime.DriveFrame(0);
        var session = entry.Runtime.SessionManager.ForegroundSession;
        IntegrationTestRunner.AssertNotNull(session, "foreground session after entry load");

        session!.Spawn(new SndMetaData
        {
            Name = "exit_probe_entity",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [_probeIndex] },
            DataMetaData = new DataMetaData()
        });

        IntegrationTestRunner.Assert(
            BeforeQuitProbeStrategy.BeforeQuitCalls == 0,
            "BeforeQuit must not run before the entry leaves the tree");

        IntegrationTestRunner.FreeNode(entry);

        IntegrationTestRunner.Assert(
            BeforeQuitProbeStrategy.BeforeQuitCalls == 1,
            "normal entry exit must run the foreground entity BeforeQuit hook exactly once");
    }

    public void Dispose()
    {
        if (_entry is not null && GodotObject.IsInstanceValid(_entry))
            IntegrationTestRunner.FreeNode(_entry);

        _entry = null;
        BeforeQuitProbeStrategy.BeforeQuitCalls = 0;
        GC.SuppressFinalize(this);
    }

    private sealed partial class ProbeEntry : OrigoDefaultEntry
    {
        public override void _Ready()
        {
            AutoDiscoverStrategies = false;
            ConfigPath = "res://TestScenes/test_entry_levels.json";
            SceneAliasMapPath = "res://TestScenes/empty_scene_aliases.map";
            SndTemplateMapPath = "res://TestScenes/empty_templates.map";
            InitialSaveRootPath = "res://TestScenes/empty_initial";
            SaveRootPath = "user://origo_entry_exit_test_saves";
            base._Ready();
        }

        protected override void ConfigureStrategies(ISndWorldAccess world) =>
            world.RegisterStrategy(() => new BeforeQuitProbeStrategy());
    }

    [StrategyIndex(_probeIndex)]
    private sealed class BeforeQuitProbeStrategy : LifecycleStrategyBase
    {
        public static int BeforeQuitCalls;

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => BeforeQuitCalls++;
    }
}
