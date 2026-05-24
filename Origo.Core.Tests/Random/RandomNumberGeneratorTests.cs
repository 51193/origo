using System.Linq;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

public class RandomNumberGeneratorTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var left = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("same-seed"), 8);
        var right = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("same-seed"), 8);

        Assert.Equal(left, right);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var left = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("seed-a"), 8);
        var right = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("seed-b"), 8);

        Assert.NotEqual(left, right);
    }

    private static ulong[] ProduceSequence((ulong s0, ulong s1) state, int count)
    {
        var values = new ulong[count];
        var s0 = state.s0;
        var s1 = state.s1;

        foreach (var i in Enumerable.Range(0, count))
        {
            var (value, nextS0, nextS1) = RandomNumberGenerator.NextUInt64(s0, s1);
            values[i] = value;
            s0 = nextS0;
            s1 = nextS1;
        }

        return values;
    }
}
