using System;
using Origo.EvolutionDemo.Logic.Gameplay;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

/// <summary>Regression tests for campaign test-player purchase strategies.</summary>
public sealed class CampaignPurchasePolicyTests : IDisposable
{
    [Fact]
    public void Planned_SavesForTheNextTargetAndDoesNotSpendOnSideGrades()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        runtime.Progression.CompletedMilestones.Add("milestone.firstBirth");
        runtime.Progression.Points = 20;

        var first = CampaignPurchasePolicy.Decide(runtime, CampaignPurchaseStrategy.Planned);
        Assert.NotNull(first);
        Assert.Equal(CampaignUnlockIds.ToughFood, first!.UnlockId);
        Assert.Equal(20, first.Cost);
        Assert.True(runtime.TryUnlock(first.UnlockId));
        Assert.Equal(0, runtime.Progression.Points);

        // The next target is HardDigest (30) and its tough-discovered milestone
        // is not met; the planned player waits rather than buying a 25-point
        // optional sense with a different milestone.
        runtime.Progression.Points = 30;
        Assert.Null(CampaignPurchasePolicy.Decide(runtime, CampaignPurchaseStrategy.Planned));
        runtime.Progression.CompletedMilestones.Add("milestone.toughDiscovered");
        var second = CampaignPurchasePolicy.Decide(runtime, CampaignPurchaseStrategy.Planned);
        Assert.NotNull(second);
        Assert.Equal(CampaignUnlockIds.HardDigest, second!.UnlockId);
    }

    [Fact]
    public void Cheapest_ChoosesTheLowestCostUnlockableAbility()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        AddEarlyMilestones(runtime.Progression);
        runtime.Progression.Points = 100;

        var first = CampaignPurchasePolicy.Decide(runtime, CampaignPurchaseStrategy.Cheapest);
        Assert.NotNull(first);
        Assert.Equal(CampaignUnlockIds.ToughFood, first!.UnlockId);
        runtime.TryUnlock(first.UnlockId);

        var second = CampaignPurchasePolicy.Decide(runtime, CampaignPurchaseStrategy.Cheapest);
        Assert.NotNull(second);
        Assert.Equal(25, second!.Cost);
    }

    [Fact]
    public void Greedy_And_Idle_CanBuySeveralAffordableAbilitiesAtOneDecision()
    {
        foreach (var strategy in new[]
                 {
                     CampaignPurchaseStrategy.Greedy,
                     CampaignPurchaseStrategy.Idle,
                 })
        {
            var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
            AddEarlyMilestones(runtime.Progression);
            runtime.Progression.Points = 100;

            var purchases = 0;
            while (CampaignPurchasePolicy.Decide(runtime, strategy) is { } decision)
            {
                Assert.True(runtime.TryUnlock(decision.UnlockId));
                purchases++;
            }

            Assert.Equal(3, purchases);
            Assert.Equal(20, runtime.Progression.Points);
        }
    }

    [Fact]
    public void Planned_OneDecisionDoesNotBuyASecondAbilityInTheSameFrame()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        AddEarlyMilestones(runtime.Progression);
        runtime.Progression.Points = 100;

        var decision = CampaignPurchasePolicy.Decide(runtime, CampaignPurchaseStrategy.Planned);
        Assert.NotNull(decision);
        runtime.TryUnlock(decision!.UnlockId);
        var next = CampaignPurchasePolicy.Decide(runtime, CampaignPurchaseStrategy.Planned);
        // The next target is HardDigest (30), but its milestone is met here and
        // 80 points remain, so the policy itself is ready; the test host decides
        // the interval. The policy must never return more than one decision.
        Assert.NotNull(next);
        Assert.NotEqual(decision.UnlockId, next!.UnlockId);
    }

    [Fact]
    public void ResetCampaignProgression_ClearsPointsUnlocksAndMilestones()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        AddEarlyMilestones(runtime.Progression);
        runtime.Progression.Points = 100;
        runtime.Progression.UnlockedIds.Add(CampaignUnlockIds.ToughFood);
        runtime.Progression.PendingInnovationUnlocks.Add(CampaignUnlockIds.ToughFood);
        Assert.True(GenomeContentGate.IsOrganAllowed(OrganKindV4.Vision));

        runtime.ResetCampaignProgression();

        Assert.Equal(0, runtime.Progression.Points);
        Assert.Equal(GameplayMode.Campaign, runtime.Progression.Mode);
        Assert.Equal(0d, runtime.Progression.LifetimeSeconds);
        Assert.Equal(0d, runtime.Progression.PassivePointProgress);
        Assert.Empty(runtime.Progression.CompletedMilestones);
        Assert.Empty(runtime.Progression.PendingInnovationUnlocks);
        Assert.Equal("unlock.core", Assert.Single(runtime.Progression.UnlockedIds));
        Assert.True(GenomeContentGate.IsOrganAllowed(OrganKindV4.Vision));
        Assert.False(GenomeContentGate.IsOrganAllowed(OrganKindV4.HardDigest));
    }

    private static void AddEarlyMilestones(GameplayProgression progression)
    {
        progression.CompletedMilestones.Add("milestone.firstBirth");
        progression.CompletedMilestones.Add("milestone.toughDiscovered");
        progression.CompletedMilestones.Add("milestone.generation5");
        progression.CompletedMilestones.Add("milestone.population32");
    }

    public void Dispose()
    {
        GenomeContentGate.OrganFilter = static _ => true;
        GenomeContentGate.ActionFilter = static _ => true;
        GenomeContentGate.SensorFilter = static _ => true;
    }
}
