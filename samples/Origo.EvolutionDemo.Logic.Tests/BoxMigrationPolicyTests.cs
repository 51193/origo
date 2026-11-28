using System;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

/// <summary>Destination-profile checks for creature migration.</summary>
public sealed class BoxMigrationPolicyTests
{
    [Fact]
    public void OpenDestination_AcceptsFounder()
    {
        var genome = GenomeV4Factory.CreateDemoFounder(new Random(17));
        var result = BoxMigrationPolicy.Validate(genome, GenomeContentGate.Open);
        Assert.True(result.Allowed);
        Assert.Empty(result.Rejections);
    }

    [Fact]
    public void RestrictedDestination_RejectsExpressedOrgan()
    {
        var genome = GenomeV4Factory.CreateDemoFounder(new Random(17));
        var gate = new GenomeContentGate(static _ => false, static _ => true, static _ => true);
        var result = BoxMigrationPolicy.Validate(genome, gate);
        Assert.False(result.Allowed);
        Assert.Contains(result.Rejections, static reason => reason.StartsWith("organ:", StringComparison.Ordinal));
    }
}
