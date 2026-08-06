using System;
using System.Linq;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

public class PersistentRandomTests
{
    [Fact]
    public void InitSeed_StoresStateInBlackboard()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        Assert.True(pr.InitSeed("test_seed"));
    }

    [Fact]
    public void TryNextInt32_BeforeInit_ReturnsFalse()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        Assert.False(pr.TryNextInt32(out _));
    }

    [Fact]
    public void InitSeed_ThenNextInt32_ReturnsTrue()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        pr.InitSeed("my_seed");
        Assert.True(pr.TryNextInt32(out _));
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var bb1 = new Origo.Core.Blackboard.Blackboard();
        var pr1 = new PersistentRandom(bb1);
        pr1.InitSeed("stable");

        var bb2 = new Origo.Core.Blackboard.Blackboard();
        var pr2 = new PersistentRandom(bb2);
        pr2.InitSeed("stable");

        for (var i = 0; i < 10; i++)
        {
            pr1.TryNextInt32(out var v1);
            pr2.TryNextInt32(out var v2);
            Assert.Equal(v1, v2);
        }
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var bb1 = new Origo.Core.Blackboard.Blackboard();
        var pr1 = new PersistentRandom(bb1);
        pr1.InitSeed("seed_a");

        var bb2 = new Origo.Core.Blackboard.Blackboard();
        var pr2 = new PersistentRandom(bb2);
        pr2.InitSeed("seed_b");

        var anyDiffer = false;
        for (var i = 0; i < 5; i++)
        {
            pr1.TryNextInt32(out var v1);
            pr2.TryNextInt32(out var v2);
            if (v1 != v2) anyDiffer = true;
        }

        Assert.True(anyDiffer);
    }

    [Fact]
    public void NextInt32_Ranged_WithinBounds()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        pr.InitSeed("range_test");

        for (var i = 0; i < 100; i++)
        {
            var val = pr.NextInt32(10, 20);
            Assert.InRange(val, 10, 19);
        }
    }

    [Fact]
    public void NextInt32_MaxEqualsMin_Throws()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        pr.InitSeed("range_guard");
        Assert.Throws<ArgumentOutOfRangeException>(() => pr.NextInt32(5, 5));
    }

    [Fact]
    public void NextInt32_MaxLessThanMin_Throws()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        pr.InitSeed("range_guard");
        Assert.Throws<ArgumentOutOfRangeException>(() => pr.NextInt32(10, 3));
    }

    [Fact]
    public void NextInt32_LargeSpan_StaysWithinBounds()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        pr.InitSeed("large_span");

        // Spans wider than int.MaxValue overflow the old (uint) range math,
        // producing roughly half the results outside the requested range.
        for (var i = 0; i < 2000; i++)
            Assert.InRange(pr.NextInt32(-5, int.MaxValue), -5, int.MaxValue - 1);
        for (var i = 0; i < 2000; i++)
            Assert.InRange(pr.NextInt32(int.MinValue, int.MaxValue), int.MinValue, int.MaxValue - 1);
    }

    [Fact]
    public void NextFloat_InRange()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        pr.InitSeed("float_test");

        for (var i = 0; i < 100; i++)
        {
            var val = pr.NextFloat();
            Assert.InRange(val, 0f, 1f);
        }
    }

    [Fact]
    public void NextFloat_IsStrictlyLessThanOne()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        pr.InitSeed("float_upper_bound");

        for (var i = 0; i < 10000; i++)
        {
            var val = pr.NextFloat();
            Assert.True(val < 1.0f, $"NextFloat produced {val} which violates the [0, 1) contract.");
            Assert.True(val >= 0.0f);
        }
    }

    [Fact]
    public void NextFloat_EdgeRawValuesThatRoundToOne_AreClamped()
    {
        // Raw values in [2^32 - 2^7, 2^32) convert to a double just below 1.0 but
        // round to exactly 1.0f. Search the XorShift state space for a state whose
        // next value falls in that range (probability 2^-25 per step), then verify
        // NextFloat clamps the result below 1.0.
        var s0 = 0x9E3779B97F4A7C15ul;
        var s1 = 0x243F6A8885A308D3ul;
        (ulong S0, ulong S1)? hit = null;
        const uint edgeStart = 4294967168u; // 2^32 - 2^7

        for (var i = 0; i < 50_000_000; i++)
        {
            var (value, nextS0, nextS1) = RandomNumberGenerator.NextUInt64(s0, s1);
            if ((uint)value >= edgeStart)
            {
                hit = (s0, s1);
                break;
            }
            s0 = nextS0;
            s1 = nextS1;
        }

        Assert.True(hit is not null, "Failed to find an XorShift128+ state producing an edge raw value.");
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.SetValue("rand.state1", hit.Value.S0);
        bb.SetValue("rand.state2", hit.Value.S1);
        var pr = new PersistentRandom(bb, "rand.state1", "rand.state2");

        var raw = (uint)RandomNumberGenerator.NextUInt64(hit.Value.S0, hit.Value.S1).value;
        Assert.True(raw >= edgeStart, "Search hit should produce an edge raw value.");
        Assert.True(pr.NextFloat() < 1.0f, "NextFloat must clamp edge raw values below 1.0f.");
    }

    [Fact]
    public void NextInt32_BeforeInit_Throws()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        Assert.Throws<InvalidOperationException>(() => pr.NextInt32(0, 10));
    }

    [Fact]
    public void NextFloat_BeforeInit_Throws()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb);
        Assert.Throws<InvalidOperationException>(() => pr.NextFloat());
    }

    [Fact]
    public void NullBlackboard_Throws() => Assert.Throws<ArgumentNullException>(() => new PersistentRandom(null!));

    [Fact]
    public void CustomStateKeys_UseProvidedKeys()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var pr = new PersistentRandom(bb, "my.state1", "my.state2");
        pr.InitSeed("custom");
        Assert.True(pr.TryNextInt32(out _));
    }
}
