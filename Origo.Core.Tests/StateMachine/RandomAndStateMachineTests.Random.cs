using System.Linq;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    [Fact]
    public void CreateStateFromSeed_SameSeed_ProducesSameState()
    {
        var left = RandomNumberGenerator.CreateStateFromSeed("same-seed");
        var right = RandomNumberGenerator.CreateStateFromSeed("same-seed");

        Assert.Equal(left.s0, right.s0);
        Assert.Equal(left.s1, right.s1);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var left = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("seed-a"), 8);
        var right = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("seed-b"), 8);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void SameState_ProducesSameSequence()
    {
        var initial = RandomNumberGenerator.CreateStateFromSeed("same-seed");
        var left = ProduceSequence(initial, 8);
        var right = ProduceSequence(initial, 8);

        Assert.Equal(left, right);
    }

    [Fact]
    public void ReturnedState_CanContinueSequence()
    {
        var initial = RandomNumberGenerator.CreateStateFromSeed("continuous-seed");

        var (v1, s0, s1) = RandomNumberGenerator.NextUInt64(initial.s0, initial.s1);
        var (v2, s2, s3) = RandomNumberGenerator.NextUInt64(s0, s1);
        var (v3, _, _) = RandomNumberGenerator.NextUInt64(s2, s3);

        var expected = ProduceSequence(initial, 3);
        Assert.Equal(expected, new[] { v1, v2, v3 });
    }

    [Fact]
    public void NextInt32AndNextInt64_StayConsistentWithNextUInt64Step()
    {
        var initial = RandomNumberGenerator.CreateStateFromSeed("numeric-seed");

        var (uValue, uS0, uS1) = RandomNumberGenerator.NextUInt64(initial.s0, initial.s1);
        var (i64Value, i64S0, i64S1) = RandomNumberGenerator.NextInt64(initial.s0, initial.s1);
        var (i32Value, i32S0, i32S1) = RandomNumberGenerator.NextInt32(initial.s0, initial.s1);

        Assert.Equal(uS0, i64S0);
        Assert.Equal(uS1, i64S1);
        Assert.Equal(unchecked((long)uValue), i64Value);

        Assert.Equal(uS0, i32S0);
        Assert.Equal(uS1, i32S1);
        Assert.Equal(unchecked((int)(uValue & uint.MaxValue)), i32Value);
    }

    private static ulong[] ProduceSequence((ulong s0, ulong s1) initial, int count)
    {
        var values = new ulong[count];
        var s0 = initial.s0;
        var s1 = initial.s1;

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
