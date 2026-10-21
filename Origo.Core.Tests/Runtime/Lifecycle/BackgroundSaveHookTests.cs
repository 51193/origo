using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression coverage for background-session BeforeSave hooks: their
///     progress-state writes must be captured by the same save payload.
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

    private const string _backgroundProgressStrategy = "test.bg.save_progress";

    [StrategyIndex(_backgroundProgressStrategy)]
    private sealed class BackgroundProgressBeforeSaveStrategy : LifecycleStrategyBase
    {
        public override void BeforeSave(Abstractions.Entity.ISndEntity entity, ISndContext ctx) =>
            ctx.Blackboard.ProgressBlackboard!.SetValue("bg_before_save", 1);
    }
}
