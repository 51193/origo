using System.Linq;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class RandomNumberGeneratorExtendedTests
{
    [Fact]
    public void RandomNumberGenerator_SameSeed_ProducesSameSequence()
    {
        var seq1 = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("test-seed"), 10).ToList();
        var seq2 = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("test-seed"), 10).ToList();

        Assert.Equal(seq1, seq2);
    }

    [Fact]
    public void RandomNumberGenerator_DifferentSeed_ProducesDifferentSequence()
    {
        var (s10, s11) = RandomNumberGenerator.CreateStateFromSeed("seed-a");
        var (val1, _, _) = RandomNumberGenerator.NextUInt64(s10, s11);
        var (s20, s21) = RandomNumberGenerator.CreateStateFromSeed("seed-b");
        var (val2, _, _) = RandomNumberGenerator.NextUInt64(s20, s21);

        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void RandomNumberGenerator_NextInt32_ReturnsValue()
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed("seed");
        var (val, _, _) = RandomNumberGenerator.NextInt32(s0, s1);
        // Just verifying it returns without throwing; value is deterministic
        Assert.IsType<int>(val);
    }

    [Fact]
    public void RandomNumberGenerator_NextInt64_ReturnsValue()
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed("seed");
        var (val, _, _) = RandomNumberGenerator.NextInt64(s0, s1);
        Assert.IsType<long>(val);
    }

    [Fact]
    public void RandomNumberGenerator_Sequence_IsNotConstant()
    {
        var values = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("varies"), 100).ToHashSet();
        // With 100 values from a good RNG, we expect at least many unique values
        Assert.True(values.Count > 90);
    }

    [Fact]
    public void RandomNumberGenerator_SameState_SameFirstValue()
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed("default");
        var (left, _, _) = RandomNumberGenerator.NextUInt64(s0, s1);
        var (right, _, _) = RandomNumberGenerator.NextUInt64(s0, s1);
        Assert.Equal(left, right);
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

// ── TypedData ──────────────────────────────────────────────────────────
