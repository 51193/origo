using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

/// <summary>Boundary tests for the smoke generation wait.</summary>
public sealed class CampaignSmokeGateTests
{
    [Fact]
    public void Waits_WhenNormalTargetIsNotReached()
    {
        Assert.True(CampaignSmokeGate.ShouldWaitForGeneration(
            time: 1.9f, targetSeconds: 2f, generationDeadlineSeconds: 30f,
            generation: 0, creatures: 4));
    }

    [Fact]
    public void Waits_ForGenerationBeforeDeadline()
    {
        Assert.True(CampaignSmokeGate.ShouldWaitForGeneration(
            time: 10f, targetSeconds: 2f, generationDeadlineSeconds: 30f,
            generation: 0, creatures: 4));
    }

    [Fact]
    public void Stops_WhenGenerationAppearsAfterTarget()
    {
        Assert.False(CampaignSmokeGate.ShouldWaitForGeneration(
            time: 10f, targetSeconds: 2f, generationDeadlineSeconds: 30f,
            generation: 1, creatures: 4));
    }

    [Fact]
    public void Stops_AtGenerationDeadline()
    {
        Assert.False(CampaignSmokeGate.ShouldWaitForGeneration(
            time: 30f, targetSeconds: 2f, generationDeadlineSeconds: 30f,
            generation: 0, creatures: 4));
    }

    [Fact]
    public void Stops_WhenPondIsExtinct()
    {
        Assert.False(CampaignSmokeGate.ShouldWaitForGeneration(
            time: 10f, targetSeconds: 2f, generationDeadlineSeconds: 30f,
            generation: 0, creatures: 0));
    }
}
