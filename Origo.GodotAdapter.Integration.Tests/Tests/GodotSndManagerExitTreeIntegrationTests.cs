using System;
using System.Linq;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;
using Origo.Core.Snd.Scene;

namespace Origo.GodotAdapter.Integration.Tests;

/// <summary>
///     Regression tests for the manager's <c>_ExitTree</c> fallback: when
///     business code removes the manager node directly (a scene switch or
///     freeing the node outside the framework's session teardown), the
///     Core-side entity state must still be released. Deferred tests run
///     inside the frame loop, where scene-tree mutations are allowed
///     (instant tests run inside the runner's <c>_Ready</c>, where
///     <c>add_child</c> is rejected by the engine).
/// </summary>
public class GodotSndManagerExitTreeIntegrationTests : IDeferredTestFixture, System.IDisposable
{
    private const string _exitTreeTestIndex = "test.exit_tree";
    private const string _exitTreeThrowObserverIndex = "test.exit_tree_throw_unmount";

    private IntegrationTestHarness? _harness;
    private int _frame;

    public bool IsComplete => _frame >= 1;

    public void Setup()
    {
        _frame = 0;
        _harness = new IntegrationTestHarness(new StubLogger());
    }

    public void AdvanceFrame() => _frame++;

    public void Dispose()
    {
        _harness?.Dispose();
        _harness = null;
        GC.SuppressFinalize(this);
    }

    [DeferredTest(Description = "Manager removed from tree directly (bypassing session teardown) releases entity strategies")]
    public void ManagerRemovedFromTreeDirectly_ReleasesEntityStrategies()
    {
        var harness = _harness!;
        harness.BindRuntimeDependencies();
        harness.BindContext();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(harness.SndManager);

        harness.SndWorld.RegisterStrategy(() => new ExitTreeTestStrategy());
        var meta = new SndMetaData
        {
            Name = "exit_tree_entity",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [ExitTreeTestStrategy.Index] },
            DataMetaData = new DataMetaData()
        };
        ((ISndSceneHost)harness.SndManager).CreateEntity(meta);

        // Business code bypasses the framework and removes the manager node
        // directly (e.g. a scene switch without session teardown): the
        // manager must still release the Core-side strategy references so
        // they do not leak past the node's lifetime.
        root.RemoveChild(harness.SndManager);

        IntegrationTestRunner.Assert(
            harness.SndManager.GetEntities().Count == 0,
            "entities should be cleared when the manager leaves the tree");

        harness.SndWorld.StrategyPool.LogPoolLeaks();
        var stubLogger = (StubLogger)harness.Logger;
        IntegrationTestRunner.Assert(
            !stubLogger.Messages.Any(m => m.Contains("refCount")),
            "strategy pool references must be released on direct manager removal");
    }

    [DeferredTest(Description = "Manager removed from tree directly still releases strategies when OnUnmounted throws")]
    public void ManagerRemovedFromTreeDirectly_WhenOnUnmountedThrows_StillReleasesStrategies()
    {
        var harness = _harness!;
        harness.BindRuntimeDependencies();
        harness.BindContext();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(harness.SndManager);

        harness.SndWorld.RegisterStrategy(() => new ExitTreeTestStrategy());
        harness.SndWorld.RegisterStrategy(() => new ExitTreeThrowOnUnmountObserver());

        var observerMeta = new SndMetaData
        {
            Name = "exit_tree_observer",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [ExitTreeTestStrategy.Index] },
            DataMetaData = new DataMetaData()
        };
        var targetMeta = new SndMetaData
        {
            Name = "exit_tree_target",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };

        var observer = ((ISndSceneHost)harness.SndManager).CreateEntity(observerMeta);
        var target = ((ISndSceneHost)harness.SndManager).CreateEntity(targetMeta);
        observer.MountObserverStrategy(target, _exitTreeThrowObserverIndex);

        // Observer teardown throws inside _ExitTree. The strategy release is
        // a separate cleanup step and must still run; otherwise both the
        // lifecycle strategy and the observer strategy leak their pool refs.
        root.RemoveChild(harness.SndManager);

        IntegrationTestRunner.Assert(
            harness.SndManager.GetEntities().Count == 0,
            "entities should be cleared when the manager leaves the tree");

        harness.SndWorld.StrategyPool.LogPoolLeaks();
        var stubLogger = (StubLogger)harness.Logger;
        IntegrationTestRunner.Assert(
            !stubLogger.Messages.Any(m => m.Contains("refCount")),
            "strategy pool references must be released when observer teardown throws");
    }

    [StrategyIndex(_exitTreeTestIndex)]
    private sealed class ExitTreeTestStrategy : LifecycleStrategyBase
    {
        public const string Index = _exitTreeTestIndex;
    }

    [StrategyIndex(_exitTreeThrowObserverIndex)]
    [ObserveData("exit_tree.hp")]
    private sealed class ExitTreeThrowOnUnmountObserver : ObserverStrategyBase
    {
        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            throw new InvalidOperationException("ExitTree OnUnmounted boom");
    }
}
