using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class CarcassMathTests
{
    [Fact]
    public void Advance_ReleasesOnePulseAfterInterval()
    {
        var (pulses, timer, remaining) = CarcassMath.Advance(0f, 2, 1.25f, 1.30f, 10);
        Assert.Equal(1, pulses);
        Assert.Equal(1, remaining);
        Assert.InRange(timer, 0.04f, 0.06f);
    }

    [Fact]
    public void Advance_ReleasesMultiplePulsesButNeverMoreThanRemaining()
    {
        var (pulses, timer, remaining) = CarcassMath.Advance(0f, 2, 1f, 3.5f, 10);
        Assert.Equal(2, pulses);
        Assert.Equal(0, remaining);
        Assert.Equal(0f, timer);
    }

    [Fact]
    public void Advance_RespectsFoodCapacityAndPreservesTimer()
    {
        var (pulses, timer, remaining) = CarcassMath.Advance(0f, 2, 1f, 3.5f, 0);
        Assert.Equal(0, pulses);
        Assert.Equal(2, remaining);
        Assert.InRange(timer, 3.49f, 3.51f);
    }

    [Fact]
    public void Advance_ReleasesWhenCapacityFreesUp()
    {
        var (pulses, timer, remaining) = CarcassMath.Advance(2.5f, 2, 1f, 0.6f, 1);
        Assert.Equal(1, pulses);
        Assert.Equal(1, remaining);
        Assert.InRange(timer, 2.09f, 2.11f);
    }

    [Fact]
    public void Advance_ZeroIntervalDoesNotRelease()
    {
        var (pulses, timer, remaining) = CarcassMath.Advance(0f, 2, 0f, 10f, 10);
        Assert.Equal(0, pulses);
        Assert.Equal(2, remaining);
        Assert.Equal(0f, timer);
    }
}
