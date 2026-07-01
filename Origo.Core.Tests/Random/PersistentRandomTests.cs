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
