using Origo.Core.Logging;
using Xunit;

namespace Origo.Core.Tests;

// ── KeyValueFileParser ─────────────────────────────────────────────────

public class LogMessageBuilderTests
{
    [Fact]
    public void LogMessageBuilder_Build_PlainMessage()
    {
        var msg = new LogMessageBuilder().Build("hello");
        Assert.Equal("hello", msg);
    }

    [Fact]
    public void LogMessageBuilder_SetElapsedMs_IncludesTimestamp()
    {
        var msg = new LogMessageBuilder().SetElapsedMs(12.345).Build("test");
        Assert.StartsWith("[+12.34ms]", msg);
        Assert.Contains("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddPrefix_IncludesPrefix()
    {
        var msg = new LogMessageBuilder().AddPrefix("ctx", "val").Build("test");
        Assert.Contains("ctx=val", msg);
        Assert.Contains(" | test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddSuffix_IncludesSuffix()
    {
        var msg = new LogMessageBuilder().AddSuffix("key", "val").Build("test");
        Assert.Contains("test | key=val", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddPrefix_NullKey_Skipped()
    {
        var msg = new LogMessageBuilder().AddPrefix(null!, "val").Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddPrefix_NullValue_Skipped()
    {
        var msg = new LogMessageBuilder().AddPrefix("key", null).Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddSuffix_WhitespaceKey_Skipped()
    {
        var msg = new LogMessageBuilder().AddSuffix("  ", "val").Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_CombinedPrefixSuffix()
    {
        var msg = new LogMessageBuilder()
            .SetElapsedMs(1.0)
            .AddPrefix("p", "1")
            .AddSuffix("s", "2")
            .Build("msg");
        Assert.Contains("[+1ms]", msg);
        Assert.Contains("p=1 | msg", msg);
        Assert.Contains("msg | s=2", msg);
    }
}

// ── NullLogger ─────────────────────────────────────────────────────────
