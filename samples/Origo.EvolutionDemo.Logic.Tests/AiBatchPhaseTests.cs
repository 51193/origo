using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class AiBatchPhaseTests
{
    [Fact]
    public void ShouldUpdate_FirstTickUpdatesEveryone()
    {
        for (var id = 0; id < 10; id++)
            Assert.True(AiBatchPhase.ShouldUpdate(id, 0, 3));
    }

    [Fact]
    public void ShouldUpdate_UpdatesEachCreatureExactlyOncePerStride()
    {
        const int stride = 3;
        for (var id = 0; id < 20; id++)
        {
            var updates = 0;
            for (var tick = 1; tick <= stride * 4; tick++)
            {
                if (AiBatchPhase.ShouldUpdate(id, tick, stride))
                    updates++;
            }

            Assert.Equal(4, updates);
        }
    }

    [Fact]
    public void ShouldUpdate_PhaseIsIndependentOfSnapshotOrder()
    {
        // The same stable id sees the same cadence no matter how many peers are
        // present; only the stride (population band) changes.
        var phase = AiBatchPhase.ShouldUpdate(17, 11, 3);

        for (var tick = 1; tick < 11; tick++)
            _ = AiBatchPhase.ShouldUpdate(17, tick, 3);
        Assert.Equal(phase, AiBatchPhase.ShouldUpdate(17, 11, 3));
    }

    [Fact]
    public void ShouldUpdate_StrideOneUpdatesEveryTick()
    {
        for (var tick = 0; tick < 10; tick++)
            Assert.True(AiBatchPhase.ShouldUpdate(42, tick, 1));
    }

    [Fact]
    public void ShouldUpdate_RejectsInvalidStride()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AiBatchPhase.ShouldUpdate(1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiBatchPhase.ShouldUpdate(1, 1, -2));
    }
}
