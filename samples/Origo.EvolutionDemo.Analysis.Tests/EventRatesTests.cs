using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Genome;
using Origo.EvolutionDemo.Analysis.IO;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Cumulative-counter interval rate tests.</summary>
public sealed class EventRatesTests
{
    [Fact]
    public void Analyze_DerivesDeltaAndRateAndFlagsReset()
    {
        using var directory = new TemporaryDirectory();
        var first = SaveFixture.CreateV7Save(
            directory, "rates_1",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            time: 10d,
            extraControllerPairs: new Dictionary<string, object> { ["evo.births"] = 10d });
        var second = SaveFixture.CreateV7Save(
            directory, "rates_2",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            time: 20d,
            extraControllerPairs: new Dictionary<string, object> { ["evo.births"] = 40d });
        var third = SaveFixture.CreateV7Save(
            directory, "rates_3",
            SaveFixture.Creatures(SaveFixture.DefaultGenome(), 1),
            time: 30d,
            extraControllerPairs: new Dictionary<string, object> { ["evo.births"] = 5d });

        var rates = EventRateAnalyzer.Analyze(
            SaveSnapshotReader.Read([first, second, third]), ["evo.births"]);

        Assert.Equal(2, rates.Count);
        Assert.Equal(30d, rates[0].Delta, 9);
        Assert.Equal(3d, rates[0].RatePerSecond, 9);
        Assert.False(rates[0].Reset);
        Assert.Equal(5d, rates[1].Delta, 9);
        Assert.True(rates[1].Reset);
    }
}
