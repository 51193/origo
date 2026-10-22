using System;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression coverage for save hook ordering: background progress writes
///     are captured, foreground/background topology writes are overridden or
///     restored, and topology/level mismatches are rejected.
/// </summary>
public class BackgroundSaveHookTests
{
    [Fact]
    public void BackgroundBeforeSave_ProgressWrite_IsIncludedInSnapshot()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BackgroundProgressBeforeSaveStrategy())
            .Build();

        var background = harness.CreateBackgroundSession("bg_progress", "bg_progress_level");
        background.Spawn(new SndMetaData
        {
            Name = "bg_progress_entity",
            NodeMetaData = new NodeMetaData(),
            DataMetaData = new DataMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [_backgroundProgressStrategy]
            }
        });

        harness.Context.Save.RequestSaveGame("bg_progress_save");
        harness.Context.FlushFrame();

        using var progress = harness.Context.StorageService.ReadProgressNodeFromSnapshot("bg_progress_save");
        Assert.NotNull(progress);
        Assert.Contains("bg_before_save", TestFactory.JsonFromNode(progress));
    }

    [Fact]
    public void BackgroundBeforeSave_TopologyWrite_IsOverriddenByFramework()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BackgroundAppendingTopologyBeforeSaveStrategy())
            .Build();
        SpawnBackgroundEntity(harness, "bg_topology_entity", _backgroundAppendTopologyStrategy);

        harness.Context.Save.RequestSaveGame("bg_topology_save");
        harness.Context.FlushFrame();

        using var progress = harness.Context.StorageService.ReadProgressNodeFromSnapshot("bg_topology_save");
        Assert.NotNull(progress);
        var json = TestFactory.JsonFromNode(progress);
        Assert.Contains("bg_topology_append_hook", json, StringComparison.Ordinal);
        Assert.Contains(ISessionManager.ForegroundKey, json, StringComparison.Ordinal);
        Assert.Contains("game=game_level=true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("bogus", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BackgroundBeforeSave_MalformedTopology_DoesNotAbortSave()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BackgroundMalformedTopologyBeforeSaveStrategy())
            .Build();
        SpawnBackgroundEntity(harness, "bg_bad_topology_entity", _backgroundMalformedTopologyStrategy);

        harness.Context.Save.RequestSaveGame("bg_bad_topology_save");
        harness.Context.FlushFrame();

        using var progress = harness.Context.StorageService.ReadProgressNodeFromSnapshot("bg_bad_topology_save");
        Assert.NotNull(progress);
        var json = TestFactory.JsonFromNode(progress);
        Assert.Contains("bg_topology_malformed_hook", json, StringComparison.Ordinal);
        Assert.Contains(ISessionManager.ForegroundKey, json, StringComparison.Ordinal);
        Assert.Contains("game=game_level=true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BackgroundBeforeSave_ThrowingAfterTopologyWrite_RestoresFrameworkTopology()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BackgroundThrowingTopologyBeforeSaveStrategy())
            .Build();
        SpawnBackgroundEntity(harness, "bg_throwing_topology_entity", _backgroundThrowingTopologyStrategy);

        harness.Context.Save.RequestSaveGame("bg_throwing_topology_save");
        Assert.Throws<InvalidOperationException>(() => harness.Context.FlushFrame());

        var (found, topology) = harness.Context.Blackboard.ProgressBlackboard!
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains(ISessionManager.ForegroundKey, topology!, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", topology, StringComparison.Ordinal);
    }

    [Fact]
    public void ForegroundBeforeSave_ThrowingAfterTopologyWrite_RestoresFrameworkTopology()
    {
        var host = new FullMemorySndSceneHost(new TestLogger());
        var (context, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(
            world => world.RegisterStrategy(() => new BackgroundThrowingTopologyBeforeSaveStrategy()),
            host);
        host.BindWorld(context.Runtime.SndWorld);
        var foreground = context.Runtime.SessionManager.ForegroundSession!;
        foreground.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex(
            "fg_throwing_topology_entity", _backgroundThrowingTopologyStrategy));

        context.Save.RequestSaveGame("fg_throwing_topology_save");
        Assert.Throws<InvalidOperationException>(() => context.FlushFrame());

        var (found, topology) = context.Blackboard.ProgressBlackboard!
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains(ISessionManager.ForegroundKey, topology!, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", topology, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SnapshotWithDuplicateLevelIds_Throws()
    {
        var harness = GameplaySimulationHarness.Create().Build();
        harness.Context.Save.RequestSaveGame("topology_duplicate_save");
        harness.Context.FlushFrame();

        var progressPath = "root/save_topology_duplicate_save/progress.json";
        var json = harness.FileSystem.ReadAllText(progressPath);
        const string original = "__foreground__=main_menu=false,game=game_level=true";
        Assert.Contains(original, json, StringComparison.Ordinal);
        var updated = json.Replace(
            original,
            "__foreground__=main_menu=false,duplicate=main_menu=true",
            StringComparison.Ordinal);
        Assert.NotEqual(json, updated);
        harness.FileSystem.WriteAllText(progressPath, updated, overwrite: true);

        harness.Context.Save.RequestLoadGame("topology_duplicate_save");
        var exception = Assert.Throws<InvalidOperationException>(() => harness.Context.FlushFrame());
        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SnapshotWithUnreferencedBackgroundLevel_Throws()
    {
        var harness = GameplaySimulationHarness.Create().Build();
        harness.Context.Save.RequestSaveGame("topology_mismatch_save");
        harness.Context.FlushFrame();

        var progressPath = "root/save_topology_mismatch_save/progress.json";
        var json = harness.FileSystem.ReadAllText(progressPath);
        Assert.Contains(",game=game_level=true", json, StringComparison.Ordinal);
        harness.FileSystem.WriteAllText(
            progressPath,
            json.Replace(",game=game_level=true", string.Empty, StringComparison.Ordinal),
            overwrite: true);

        harness.Context.Save.RequestLoadGame("topology_mismatch_save");
        var exception = Assert.Throws<InvalidOperationException>(() => harness.Context.FlushFrame());
        Assert.Contains("does not reference", exception.Message, StringComparison.Ordinal);
    }

    private static void SpawnBackgroundEntity(
        GameplaySimulationHarness harness,
        string name,
        string strategyIndex) =>
        SpawnEntity(harness.GameSession, name, strategyIndex);

    private static void SpawnEntity(ISessionRun session, string name, string strategyIndex)
    {
        session.Spawn(new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            DataMetaData = new DataMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [strategyIndex]
            }
        });
    }

    private const string _backgroundProgressStrategy = "test.bg.save_progress";
    private const string _backgroundAppendTopologyStrategy = "test.bg.save_topology_append";
    private const string _backgroundMalformedTopologyStrategy = "test.bg.save_topology_malformed";
    private const string _backgroundThrowingTopologyStrategy = "test.bg.save_topology_throwing";

    [StrategyIndex(_backgroundProgressStrategy)]
    private sealed class BackgroundProgressBeforeSaveStrategy : LifecycleStrategyBase
    {
        public override void BeforeSave(Abstractions.Entity.ISndEntity entity, ISndContext ctx) =>
            ctx.Blackboard.ProgressBlackboard!.SetValue("bg_before_save", 1);
    }

    [StrategyIndex(_backgroundAppendTopologyStrategy)]
    private sealed class BackgroundAppendingTopologyBeforeSaveStrategy : LifecycleStrategyBase
    {
        public override void BeforeSave(Abstractions.Entity.ISndEntity entity, ISndContext ctx)
        {
            ctx.Blackboard.ProgressBlackboard!.SetValue("bg_topology_append_hook", 1);
            var (found, topology) = ctx.Blackboard.ProgressBlackboard!
                .TryGet<string>(WellKnownKeys.SessionTopology);
            if (found && !string.IsNullOrEmpty(topology))
                ctx.Blackboard.ProgressBlackboard.SetValue(
                    WellKnownKeys.SessionTopology, $"{topology},bogus=bogus_level=true");
        }
    }

    [StrategyIndex(_backgroundMalformedTopologyStrategy)]
    private sealed class BackgroundMalformedTopologyBeforeSaveStrategy : LifecycleStrategyBase
    {
        public override void BeforeSave(Abstractions.Entity.ISndEntity entity, ISndContext ctx)
        {
            ctx.Blackboard.ProgressBlackboard!.SetValue("bg_topology_malformed_hook", 1);
            ctx.Blackboard.ProgressBlackboard.SetValue(
                WellKnownKeys.SessionTopology, "evil=evil=true");
        }
    }

    [StrategyIndex(_backgroundThrowingTopologyStrategy)]
    private sealed class BackgroundThrowingTopologyBeforeSaveStrategy : LifecycleStrategyBase
    {
        public override void BeforeSave(Abstractions.Entity.ISndEntity entity, ISndContext ctx)
        {
            ctx.Blackboard.ProgressBlackboard!.SetValue(
                WellKnownKeys.SessionTopology, "evil=evil=true");
            throw new InvalidOperationException("background topology hook failed");
        }
    }
}
