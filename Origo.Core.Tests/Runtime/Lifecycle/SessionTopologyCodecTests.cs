using System;
using Origo.Core.Runtime.Lifecycle;
using Xunit;

namespace Origo.Core.Tests;

public class SessionTopologyCodecTests
{
    public static TheoryData<string> Parse_MalformedOrEmptyKeyOrLevel_Data { get; } = new()
    {
        "only_key",
        "key=level_only",
        "=level=true",
        "key==true"
    };

    public static TheoryData<string, bool> Parse_SyncFieldParsing_Data { get; } = new()
    {
        { "bg=alpha=TRUE", true },
        { "bg=alpha=true", true },
        { "bg=alpha=False", false },
        { "bg=alpha=not_bool", false }
    };

    [Fact]
    public void Parse_AndSerialize_RoundTripPreservesDescriptors()
    {
        var raw = SessionTopologyCodec.Join(new[]
        {
            SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, "main_menu", false),
            SessionTopologyCodec.Serialize("bg_city", "city_level", true)
        });

        var descriptors = SessionTopologyCodec.Parse(raw);

        Assert.Equal(2, descriptors.Count);
        Assert.Equal(ISessionManager.ForegroundKey, descriptors[0].Key);
        Assert.Equal("main_menu", descriptors[0].LevelId);
        Assert.False(descriptors[0].SyncProcess);
        Assert.Equal("bg_city", descriptors[1].Key);
        Assert.Equal("city_level", descriptors[1].LevelId);
        Assert.True(descriptors[1].SyncProcess);
    }

    [Theory]
    [MemberData(nameof(Parse_MalformedOrEmptyKeyOrLevel_Data))]
    public void Parse_MalformedOrEmptyKeyOrLevel_ThrowsInvalidOperation(string raw) =>
        Assert.Throws<InvalidOperationException>(() => SessionTopologyCodec.Parse(raw));

    [Fact]
    public void Parse_LevelIdContainsExtraSeparator_UsesSecondFieldAsLevelId()
    {
        var descriptors = SessionTopologyCodec.Parse("bg=world=1=true");

        var descriptor = Assert.Single(descriptors);
        Assert.Equal("bg", descriptor.Key);
        Assert.Equal("world", descriptor.LevelId);
        Assert.False(descriptor.SyncProcess);
    }

    [Theory]
    [MemberData(nameof(Parse_SyncFieldParsing_Data))]
    public void Parse_SyncFieldParsing_FollowsBoolTryParseRules(string raw, bool expectedSync)
    {
        var descriptor = Assert.Single(SessionTopologyCodec.Parse(raw));
        Assert.Equal(expectedSync, descriptor.SyncProcess);
    }

    [Fact]
    public void Join_EmptyEntries_ReturnsEmptyString()
    {
        var result = SessionTopologyCodec.Join(Array.Empty<string>());
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Parse_IgnoreEmptyEntries()
    {
        var descriptors = SessionTopologyCodec.Parse(
            $"{SessionTopologyCodec.Serialize("bg", "l1", true)},,");

        var descriptor = Assert.Single(descriptors);
        Assert.Equal("bg", descriptor.Key);
        Assert.Equal("l1", descriptor.LevelId);
        Assert.True(descriptor.SyncProcess);
    }
}
