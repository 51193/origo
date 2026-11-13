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
            { "id": "spawn.rich", "groupId": "spawn.policy", "handlerId": "rich_spawn",
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
    public void EnrichmentSpawn_IsOnlySelectedAfterUnlock()
    {
        var runtime = GameplayRuntime.CreateDefault();
        Assert.Equal("spawn.base", runtime.Resolver.Resolve("spawn.policy")?.Id);
        Assert.True(runtime.Unlocks.TryUnlock("unlock.spawn.enrichment"));
        Assert.Equal("spawn.enrichment", runtime.Resolver.Resolve("spawn.policy")?.Id);
        Assert.Equal("food_enrichment_spawn", runtime.Resolver.Resolve("spawn.policy")?.HandlerId);
    }
}
