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

    [Fact]
    public void NextUInt64_MatchesCanonicalXorShift128PlusReferenceVectors()
    {
        var (stateS0, stateS1) = RandomNumberGenerator.CreateStateFromSeed("same-seed");

        var (first, nextS0, nextS1) = RandomNumberGenerator.NextUInt64(stateS0, stateS1);

        Assert.Equal(0xB4C39361BA81DF34UL, first);
        Assert.Equal(0x6C653E2DF0F8D2FBUL, nextS0);
        Assert.Equal(0x485E5533C9890C39UL, nextS1);

        var (second, secondNextS0, secondNextS1) =
            RandomNumberGenerator.NextUInt64(nextS0, nextS1);

        Assert.Equal(0x7B217F5FBBCFE0C5UL, second);
        Assert.Equal(0x485E5533C9890C39UL, secondNextS0);
        Assert.Equal(0x32C32A2BF246D48CUL, secondNextS1);
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
