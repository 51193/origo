using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GameplayProfileCodecTests
{
    [Fact]
    public void Profile_RoundTripsAndAlwaysUnlocksCore()
    {
        var progression = new GameplayProgression
        {
            Points = 123,
            LifetimeSeconds = 4.5,
        };
        progression.UnlockedIds.Add("unlock.spike");
        progression.CompletedMilestones.Add("milestone.first");
        var json = GameplayProfileCodec.Serialize(progression);
        Assert.True(GameplayProfileCodec.TryDeserialize(json, out var profile));
        var restored = new GameplayProgression();
        GameplayProfileCodec.Apply(profile!, restored);
        Assert.Equal(123, restored.Points);
        Assert.Contains("unlock.core", restored.UnlockedIds);
        Assert.Contains("unlock.spike", restored.UnlockedIds);
        Assert.Contains("milestone.first", restored.CompletedMilestones);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ broken")]
    [InlineData("{\"SchemaVersion\":1,\"Points\":0,\"LifetimeSeconds\":0}")]
    public void Profile_RejectsCorruptOrIncompleteJson(string json)
    {
        Assert.False(GameplayProfileCodec.TryDeserialize(json, out _));
    }
}
