using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

/// <summary>Box-scoped profile and gate isolation tests.</summary>
public sealed class BoxUnlockProfileTests
{
    [Fact]
    public void Profiles_AreValidatedAndSorted()
    {
        var profile = BoxUnlockProfile.Create("test", ["unlock.core", "unlock.forage.sprint", "unlock.core"]);

        Assert.Equal("test", profile.Id);
        Assert.Equal([CampaignUnlockIds.Core, CampaignUnlockIds.Sprint], profile.UnlockIds);
        Assert.Throws<ArgumentException>(() => BoxUnlockProfile.Create("", ["unlock.core"]));
        Assert.Throws<ArgumentException>(() => BoxUnlockProfile.Create("empty", []));
    }

    [Fact]
    public void RuntimeProfiles_DoNotShareGenomeGates()
    {
        var all = GameplayRuntime.CreateDefault(null, GameplayMode.Sandbox);
        var full = BoxUnlockProfile.All(all.Content, "full");
        var restricted = BoxUnlockProfile.Create("restricted",
            [CampaignUnlockIds.Core, CampaignUnlockIds.Predation]);

        var fullRuntime = GameplayRuntime.CreateForBox(full);
        var restrictedRuntime = GameplayRuntime.CreateForBox(restricted);

        Assert.True(fullRuntime.GenomeGate.IsOrganAllowed(OrganKindV4.SpikeOrgan));
        Assert.True(restrictedRuntime.GenomeGate.IsOrganAllowed(OrganKindV4.SpikeOrgan));
        Assert.True(fullRuntime.GenomeGate.IsOrganAllowed(OrganKindV4.HardDigest));
        Assert.False(restrictedRuntime.GenomeGate.IsOrganAllowed(OrganKindV4.HardDigest));
        Assert.True(fullRuntime.GenomeGate.IsSensorAllowed(SensorKindV4.FoodTough));
        Assert.False(restrictedRuntime.GenomeGate.IsSensorAllowed(SensorKindV4.FoodTough));

        for (var seed = 0; seed < 20; seed++)
        {
            var founder = CreatureGenome.RandomFounder(new Random(seed), restrictedRuntime.GenomeGate);
            var phenotype = GenomeV4Compiler.Compile(founder.Genome, ExpressionContextV4.Neutral);
            Assert.DoesNotContain(phenotype.Body.Organs,
                static organ => organ.Kind == OrganKindV4.HardDigest);
        }
    }

    [Fact]
    public void ProfileRuntime_HasNoPointsOrMilestones()
    {
        var all = GameplayRuntime.CreateDefault(null, GameplayMode.Sandbox);
        var runtime = GameplayRuntime.CreateForBox(BoxUnlockProfile.All(all.Content));

        runtime.TickProgression(120d, new MilestoneMetrics(Population: 2_000d));

        Assert.Equal(0, runtime.Progression.Points);
        Assert.Empty(runtime.Progression.CompletedMilestones);
        Assert.Equal(GameplayMode.Profile, runtime.Progression.Mode);
        Assert.False(runtime.TryUnlock(CampaignUnlockIds.Pond1));
    }
}
