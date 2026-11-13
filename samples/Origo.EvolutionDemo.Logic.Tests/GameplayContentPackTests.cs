using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GameplayContentPackTests
{
    [Fact]
    public void Pack_ParsesAndBuildsSnapshot()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "content": [
            { "id": "gene.organ.spike", "kind": "gene", "unlockId": "unlock.spike" }
          ],
          "unlocks": [
            { "id": "unlock.spike", "title": "Spike", "category": "organs",
              "cost": 10, "prerequisites": [], "grants": ["gene.organ.spike"],
              "requiredMilestones": [] }
          ],
          "variants": [],
          "milestones": []
        }
        """;
        var snapshot = GameplayContentPackLoader.LoadSnapshot([json]);
        Assert.True(snapshot.Content.ContainsKey("gene.organ.spike"));
        var progression = new GameplayProgression();
        var unlocks = new UnlockService(snapshot, progression);
        Assert.True(unlocks.TryUnlock("unlock.spike"));
    }

    [Fact]
    public void Pack_MissingOptionalCollections_DefaultsToEmpty()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "content": [],
          "unlocks": [],
          "variants": [
            { "id": "spawn.pack", "groupId": "spawn.policy", "handlerId": "base_spawn",
              "unlockId": "unlock.core", "priority": 1 }
          ],
          "milestones": []
        }
        """;
        var registry = new GameplayContentRegistry();
        registry.AddHandler(new GameplayHandlerDefinition("base_spawn", [], []));
        registry.AddUnlock(new UnlockDefinition("unlock.core", "core", "core", 0, [], [], []));
        GameplayContentPackLoader.AddTo(registry, json);
        var snapshot = registry.BuildSnapshot();
        var variant = snapshot.Variants["spawn.pack"];
        Assert.NotNull(variant.Data);
        Assert.NotNull(variant.BiomeTags);
        Assert.Empty(variant.Data);
    }

    [Fact]
    public void Pack_RejectsUnknownSchema()
    {
        const string json = """{ "schemaVersion": 9, "content": [], "unlocks": [], "variants": [], "milestones": [] }""";
        Assert.Throws<System.IO.InvalidDataException>(() =>
            GameplayContentPackLoader.Parse(json));
    }
}
