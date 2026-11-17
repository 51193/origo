using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class IterateCheckpointScheduleTests
{
    [Fact]
    public void Marks_ReturnsAbsoluteMultiplesInsideIterationRange()
    {
        var marks = IterateCheckpointSchedule.Marks(0f, 120f, 40f);

        Assert.Equal([40f, 80f], marks);
    }

    [Fact]
    public void Marks_StartingMidIntervalAlignsToAbsoluteMultiples()
    {
        var marks = IterateCheckpointSchedule.Marks(15f, 100f, 40f);

        Assert.Equal([40f, 80f], marks);
    }

    [Fact]
    public void Marks_ExcludesTheFinalTarget()
    {
        var marks = IterateCheckpointSchedule.Marks(0f, 90f, 30f);

        Assert.Equal([30f, 60f], marks);
    }

    [Fact]
    public void Marks_ZeroIntervalDisablesCheckpoints()
    {
        Assert.Empty(IterateCheckpointSchedule.Marks(0f, 3_600f, 0f));
    }

    [Theory]
    [InlineData(3600f, 3600f)]
    [InlineData(3600f, 3_500f)]
    public void Marks_EmptyWhenIterationHasNoSessionRange(float startTime, float targetTime)
    {
        Assert.Empty(IterateCheckpointSchedule.Marks(startTime, targetTime, 60f));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(29.9f)]
    public void Marks_RejectsIntervalBelowMinimum(float intervalSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IterateCheckpointSchedule.Marks(0f, 3_600f, intervalSeconds));
    }

    [Fact]
    public void Marks_RejectsNegativeInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IterateCheckpointSchedule.Marks(0f, 3_600f, -30f));
    }

    [Fact]
    public void Marks_RejectsNonFiniteValues()
    {
        Assert.Throws<ArgumentException>(() => IterateCheckpointSchedule.Marks(float.NaN, 3_600f, 60f));
        Assert.Throws<ArgumentException>(() => IterateCheckpointSchedule.Marks(0f, float.PositiveInfinity, 60f));
        Assert.Throws<ArgumentException>(() => IterateCheckpointSchedule.Marks(0f, 3_600f, float.NaN));
    }

    [Fact]
    public void Marks_KeepsLongIterationMarksOrdered()
    {
        var marks = IterateCheckpointSchedule.Marks(0f, 3_600f, 300f);

        Assert.Equal(11, marks.Count);
        Assert.Equal(300f, marks[0]);
        Assert.Equal(3_300f, marks[^1]);
        for (var index = 1; index < marks.Count; index++)
            Assert.Equal(300f, marks[index] - marks[index - 1], 3);
    }
}
