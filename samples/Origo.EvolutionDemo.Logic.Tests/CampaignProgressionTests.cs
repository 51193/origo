using System;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class CampaignProgressionTests : IDisposable
{
    [Fact]
    public void CampaignStart_IsPrimordialAndContentGated()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        Assert.True(GameplayRuntime.CreateDefault(null, GameplayMode.Campaign).Unlocks.IsUnlocked("unlock.core"));
        Assert.True(GenomeContentGate.IsOrganAllowed(OrganKindV4.Vision));
        Assert.False(GenomeContentGate.IsOrganAllowed(OrganKindV4.HardDigest));
        Assert.False(GenomeContentGate.IsOrganAllowed(OrganKindV4.SpikeOrgan));
        Assert.False(GenomeContentGate.IsOrganAllowed(OrganKindV4.FoodTypeVision));
        Assert.True(GenomeContentGate.IsSensorAllowed(SensorKindV4.FoodAhead));
        Assert.False(GenomeContentGate.IsSensorAllowed(SensorKindV4.FoodTough));
        Assert.True(GenomeContentGate.IsActionAllowed(ActionKindV4.Move));
        Assert.False(GenomeContentGate.IsActionAllowed(ActionKindV4.Spike));

        var settings = DemoSettings.Defaults();
        CampaignProgressionRules.ApplyStartSettings(settings, runtime.Progression);
        Assert.Equal(700f, settings.PondRadius);
        Assert.Equal(24, settings.InitialPopulation);
        Assert.Equal(64, settings.PopulationCap);
        Assert.Equal(180, settings.InitialFood);
        Assert.Equal(180, settings.FoodCap);
        Assert.Equal(0f, settings.ToughFoodShare);
        Assert.Equal(0f, settings.ToxicFoodShare);
    }

    [Fact]
    public void Sandbox_ExposesAllContent()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Sandbox);
        Assert.True(runtime.Unlocks.IsContentAvailable("gene.organ.spike"));
        Assert.True(runtime.Unlocks.IsContentAvailable("gene.organ.food_type_vision"));
        Assert.True(GenomeContentGate.IsOrganAllowed(OrganKindV4.SpikeOrgan));
        Assert.True(GenomeContentGate.IsSensorAllowed(SensorKindV4.FoodTough));
        Assert.False(runtime.Unlocks.IsContentAvailable("mechanic.spawn.enrichment"));
        Assert.Equal("spawn.base", runtime.Resolver.Resolve("spawn.policy")?.Id);
        Assert.False(runtime.TryUnlock("unlock.forage.hard_digest"));
    }

    [Fact]
    public void CampaignPassivePoints_AccumulateOnlyInCampaign()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        runtime.Progression.CompletedMilestones.Add("milestone.firstBreath");
        runtime.TickProgression(30.1d, new MilestoneMetrics());
        Assert.Equal(1, runtime.Progression.Points);
        Assert.InRange(runtime.Progression.PassivePointProgress, 0d, 0.2d);

        var sandbox = GameplayRuntime.CreateDefault(null, GameplayMode.Sandbox);
        sandbox.TickProgression(120d, new MilestoneMetrics(Population: 2_000d));
        Assert.Equal(0, sandbox.Progression.Points);
        Assert.Empty(sandbox.Progression.CompletedMilestones);
    }

    [Fact]
    public void CampaignMilestones_AwardPointsAndCompleteOnce()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        runtime.TickProgression(0d, new MilestoneMetrics(MealsTotal: 1d, Births: 1d, Population: 32d));
        Assert.Contains("milestone.firstMeal", runtime.Progression.CompletedMilestones);
        Assert.Contains("milestone.firstBirth", runtime.Progression.CompletedMilestones);
        Assert.Contains("milestone.population32", runtime.Progression.CompletedMilestones);
        var points = runtime.Progression.Points;
        runtime.TickProgression(0d, new MilestoneMetrics(MealsTotal: 10d, Births: 10d, Population: 100d));
        Assert.True(runtime.Progression.Points > points);
        var after = runtime.Progression.Points;
        runtime.TickProgression(0d, new MilestoneMetrics(MealsTotal: 10d, Births: 10d, Population: 100d));
        Assert.Equal(after, runtime.Progression.Points);
    }

    [Fact]
    public void FoodComposition_UnlocksInStages()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        var settings = DemoSettings.Defaults();
        Assert.Equal((0f, 0f), CampaignProgressionRules.FoodShares(runtime.Progression, settings));
        runtime.Progression.UnlockedIds.Add(CampaignUnlockIds.ToughFood);
        Assert.Equal((0.15f, 0f), CampaignProgressionRules.FoodShares(runtime.Progression, settings));
        runtime.Progression.UnlockedIds.Add(CampaignUnlockIds.ToxicFood);
        Assert.Equal((0.25f, 0.15f), CampaignProgressionRules.FoodShares(runtime.Progression, settings));
        runtime.Progression.UnlockedIds.Add(CampaignUnlockIds.FoodTypeVision);
        Assert.Equal((0.40f, 0.40f), CampaignProgressionRules.FoodShares(runtime.Progression, settings));
    }

    [Fact]
    public void PurchasedOrganUnlock_SchedulesInnovators()
    {
        var runtime = GameplayRuntime.CreateDefault(null, GameplayMode.Campaign);
        runtime.Progression.Points = 10_000;
        runtime.Progression.CompletedMilestones.Add("milestone.generation5");
        Assert.True(runtime.TryUnlock(CampaignUnlockIds.Sprint));
        Assert.Equal((int)DemoOrganMaskV4.Sprint,
            CampaignProgressionRules.InnovationOrganMask(CampaignUnlockIds.Sprint));
        Assert.Contains(CampaignUnlockIds.Sprint, runtime.TakePendingInnovations());
        Assert.Empty(runtime.TakePendingInnovations());
    }

    public void Dispose()
    {
        GenomeContentGate.OrganFilter = static _ => true;
        GenomeContentGate.ActionFilter = static _ => true;
        GenomeContentGate.SensorFilter = static _ => true;
    }
}
