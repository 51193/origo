using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GameplayRuntimeTests
{
    [Fact]
    public void ExternalContent_MergesWithoutLosingCoreProgression()
    {
        var runtime = GameplayRuntime.CreateDefault();
        Assert.True(runtime.Unlocks.IsUnlocked("unlock.core"));
        const string packJson = """
        {
          "schemaVersion": 1,
          "content": [
            { "id": "mechanic.spawn.rich", "kind": "mechanic", "unlockId": "unlock.core" }
          ],
          "unlocks": [],
          "variants": [
            { "id": "spawn.rich", "groupId": "spawn.policy", "handlerId": "base_spawn",
              "strategyIndex": null, "unlockId": "unlock.core", "biomeTags": [],
              "priority": 10, "data": {} }
          ],
          "milestones": []
        }
        """;
        runtime.ApplyExternalPack(packJson);
        Assert.True(runtime.Content.Content.ContainsKey("mechanic.spawn.rich"));
        Assert.Equal("spawn.rich", runtime.Resolver.Resolve("spawn.policy")?.Id);
        Assert.True(runtime.Unlocks.IsUnlocked("unlock.core"));
    }

    [Fact]
    public void Milestone_AwardsPlaceholderPointsOnce()
    {
        var runtime = GameplayRuntime.CreateDefault();
        runtime.ApplyExternalPack("""
        {
          "schemaVersion": 1,
          "content": [],
          "unlocks": [],
          "variants": [],
          "milestones": [
            { "id": "milestone.kills3", "eventKey": "kills", "threshold": 3,
              "rewardPoints": 25, "grants": [] }
          ]
        }
        """);
        var before = runtime.Progression.Points;
        runtime.EvaluateMilestones(0, 0, 3);
        Assert.Equal(before + 25, runtime.Progression.Points);
        Assert.Contains("milestone.kills3", runtime.Progression.CompletedMilestones);
        runtime.EvaluateMilestones(0, 0, 10);
        Assert.Equal(before + 25, runtime.Progression.Points);
    }

    [Fact]
    public void Milestone_CanGrantUnlock()
    {
        var runtime = GameplayRuntime.CreateDefault();
        Assert.False(runtime.Unlocks.IsUnlocked("unlock.spawn.enrichment"));
        runtime.ApplyExternalPack("""
        {
          "schemaVersion": 1,
          "content": [],
          "unlocks": [],
          "variants": [],
          "milestones": [
            { "id": "milestone.reward", "eventKey": "lifetimeSeconds", "threshold": 1,
              "rewardPoints": 0, "grants": [], "unlockGrants": ["unlock.spawn.enrichment"] }
          ]
        }
        """);
        runtime.EvaluateMilestones(1, 0, 0);
        Assert.True(runtime.Unlocks.IsUnlocked("unlock.spawn.enrichment"));
    }

    [Fact]
    public void EnrichmentSpawn_IsOnlySelectedAfterUnlock()
    {
        var runtime = GameplayRuntime.CreateDefault();
        Assert.Equal("spawn.base", runtime.Resolver.Resolve("spawn.policy")?.Id);
        Assert.True(runtime.Unlocks.TryUnlock("unlock.spawn.enrichment"));
        Assert.Equal("spawn.enrichment", runtime.Resolver.Resolve("spawn.policy")?.Id);
        Assert.Equal("food_enrichment_spawn", runtime.Resolver.Resolve("spawn.policy")?.HandlerId);
    }
}
