using System;
using System.Collections.Generic;
using Origo.Core.Logging;
using Origo.Core.Save.Meta;
using Xunit;

namespace Origo.Core.Tests;

public class SaveMetaMapCodecTests
{
    [Fact]
    public void SaveMetaMapCodec_Parse_InvalidLines_LogWarnings()
    {
        var logger = new TestLogger();
        var map = SaveMetaMapCodec.Parse("a: 1\ninvalid\nb:\n# cmt", logger, "test.meta");

        Assert.NotNull(map);
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public void SaveMetaMapCodec_Parse_IgnoresCommentsBlankLinesAndInvalidLines()
    {
        var map = SaveMetaMapCodec.Parse(
            """
            # comment

            title: Chapter 2
            invalid_line
            : missing_key
            missing_value:
            play_time: 03:12:55
            """,
            NullLogger.Instance);
        Assert.Equal("Chapter 2", map["title"]);
        Assert.Equal("03:12:55", map["play_time"]);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_SortsKeysOrdinal()
    {
        var text = SaveMetaMapCodec.Serialize(new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        Assert.Equal("a: 1\nb: 2", text);
    }

    [Fact]
    public void SaveMetaMapCodec_Parse_DuplicateKeys_LogsWarningAndLastWins()
    {
        var logger = new TestLogger();
        var map = SaveMetaMapCodec.Parse("title: First\ntitle: Second", logger, "dup.meta");

        Assert.Equal("Second", map["title"]);
        Assert.Contains(logger.Warnings, w => w.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
