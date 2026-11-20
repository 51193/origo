using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Analysis.Perf;
using Xunit;

namespace Origo.EvolutionDemo.Analysis.Tests;

/// <summary>Regression tests for segment splitting and the segment/window selection composition.</summary>
public sealed class ProfileSegmentsTests
{
    [Fact]
    public void Split_StartsNewSegmentOnSimTimeDecrease()
    {
        TickProfile[] ticks =
        [
            PerfFixture.Tick(10, 1d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(20, 2d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(30, 3d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(40, 1d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(50, 2d, phases: PerfFixture.StandardPhases()),
        ];
        var segments = ProfileSegments.Split(ticks);

        Assert.Equal(2, segments.Count);
        Assert.Equal(0, segments[0].Index);
        Assert.Equal(3, segments[0].Ticks.Count);
        Assert.Equal(1, segments[1].Index);
        Assert.Equal(2, segments[1].Ticks.Count);
        Assert.Equal(1d, segments[1].Ticks[0].SimTime, 10);
    }

    [Fact]
    public void Split_KeepsSingleSegmentWhenTimeIsMonotonic()
    {
        TickProfile[] ticks =
        [
            PerfFixture.Tick(10, 1d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(20, 2d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(30, 2d, phases: PerfFixture.StandardPhases()),
            PerfFixture.Tick(40, 3d, phases: PerfFixture.StandardPhases()),
        ];

        Assert.Single(ProfileSegments.Split(ticks));
    }

    [Fact]
    public void Split_ThrowsOnEmptyProfile()
    {
        Assert.Throws<InvalidOperationException>(() => ProfileSegments.Split([]));
    }

    [Fact]
    public void Select_DefaultsToLastSegmentAndFlagsImplicitSelection()
    {
        var selection = ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl");

        Assert.Equal(100, selection.Ticks.Count);
        Assert.Equal(2, selection.SegmentCount);
        Assert.Equal(1, selection.SelectedSegment);
        Assert.True(selection.ImplicitLast);
        Assert.False(selection.HasWindow);
        Assert.Equal("last", selection.SegmentLabel);
        Assert.Equal("none", selection.WindowLabel);
    }

    [Fact]
    public void Select_SingleSegmentHasNoSegmentSelection()
    {
        var selection = ProfileSegments.Select(PerfFixture.SampledSeries(sampleCount: 5), "profile.jsonl");

        Assert.Equal(5, selection.Ticks.Count);
        Assert.Equal(1, selection.SegmentCount);
        Assert.Null(selection.SelectedSegment);
        Assert.False(selection.ImplicitLast);
        Assert.Equal("all", selection.SegmentLabel);
    }

    [Fact]
    public void Select_AllPoolsEverySegment()
    {
        var selection = ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", "all");

        Assert.Equal(111, selection.Ticks.Count);
        Assert.Null(selection.SelectedSegment);
        Assert.False(selection.ImplicitLast);
        Assert.Equal("all", selection.SegmentLabel);
    }

    [Fact]
    public void Select_ExplicitLastIsNotImplicit()
    {
        var selection = ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", "last");

        Assert.Equal(100, selection.Ticks.Count);
        Assert.Equal(1, selection.SelectedSegment);
        Assert.False(selection.ImplicitLast);
    }

    [Fact]
    public void Select_IndexChoosesSegment()
    {
        var profile = PerfFixture.TwoSegmentProfile();
        var smoke = ProfileSegments.Select(profile, "profile.jsonl", "0");
        var benchmark = ProfileSegments.Select(profile, "profile.jsonl", "1");

        Assert.Equal(11, smoke.Ticks.Count);
        Assert.Equal(20d, smoke.Ticks[0].WallMs, 10);
        Assert.Equal(100, benchmark.Ticks.Count);
        Assert.Equal(10d, benchmark.Ticks[0].WallMs, 10);
    }

    [Fact]
    public void Select_OutOfRangeIndex_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", "2"));

        Assert.Contains("out of range", error.Message, StringComparison.Ordinal);
        Assert.Contains("2 segment(s)", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("benchmark")]
    [InlineData("1.5")]
    public void Select_UnknownSegmentChoice_Throws(string choice)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", choice));

        Assert.Contains("--segment expects all, last", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WindowComposesInsideSelectedSegment()
    {
        var profile = PerfFixture.TwoSegmentProfile();
        var lastWindow = ProfileSegments.Select(profile, "profile.jsonl", from: 0d, to: 12d);
        var allWindow = ProfileSegments.Select(profile, "profile.jsonl", "all", 0d, 12d);

        Assert.Equal(12, lastWindow.Ticks.Count);
        Assert.All(lastWindow.Ticks, static tick => Assert.True(tick.WallMs <= 10d + 1e-9));
        Assert.Equal(23, allWindow.Ticks.Count);
        Assert.Equal("0..12", lastWindow.WindowLabel);
        Assert.True(lastWindow.ImplicitLast);
    }

    [Fact]
    public void Select_WindowAppliesToExplicitFirstSegment()
    {
        var selection = ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", "0", 5d, 10d);

        Assert.Equal(6, selection.Ticks.Count);
        Assert.Equal(5d, selection.Ticks[0].SimTime, 10);
        Assert.Equal(10d, selection.Ticks[^1].SimTime, 10);
        Assert.Equal(0, selection.SelectedSegment);
    }

    [Fact]
    public void Select_EmptyWindow_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", from: 500d, to: 600d));

        Assert.Contains("window contains no samples", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_OpenEndedWindowKeepsBound()
    {
        var fromOnly = ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", from: 95d);
        var toOnly = ProfileSegments.Select(PerfFixture.TwoSegmentProfile(), "profile.jsonl", to: 5d);

        Assert.Equal(6, fromOnly.Ticks.Count);
        Assert.Equal(">=95", fromOnly.WindowLabel);
        Assert.Equal(5, toOnly.Ticks.Count);
        Assert.Equal("<=5", toOnly.WindowLabel);
    }

    [Fact]
    public void Split_TwoSegmentFixtureKeepsEverySample()
    {
        var profile = PerfFixture.TwoSegmentProfile();
        var segments = ProfileSegments.Split(profile);
        var ticks = segments.SelectMany(static segment => segment.Ticks).ToArray();

        Assert.Equal(profile.Length, ticks.Length);
        Assert.Equal(new[] { 11, 100 }, segments.Select(static segment => segment.Ticks.Count).ToArray());
    }
}
