using System;
using Origo.Core.Logging;
using Xunit;

namespace Origo.Core.Tests;

public class LogMessageBuilderTests
{
    [Fact]
    public void Build_PlainMessage()
    {
        var msg = new LogMessageBuilder().Build("hello");
        Assert.Equal("hello", msg);
    }

    [Fact]
    public void SetElapsedMs_IncludesTimestamp()
    {
        var msg = new LogMessageBuilder().SetElapsedMs(12.345).Build("test");
        Assert.Contains("[+12.3", msg);
        Assert.Contains("ms]", msg);
        Assert.Contains("test", msg);
    }

    [Fact]
    public void AddContext_AppendsContext()
    {
        var msg = new LogMessageBuilder().AddContext("key", "val").Build("test");
        Assert.Contains("test | key=val", msg);
    }

    [Fact]
    public void AddContext_MultipleEntries_AllIncluded()
    {
        var msg = new LogMessageBuilder()
            .AddContext("a", "1")
            .AddContext("b", "2")
            .Build("test");
        Assert.Contains("test | a=1, b=2", msg);
    }

    [Fact]
    public void AddContext_NullKey_Skipped()
    {
        var msg = new LogMessageBuilder().AddContext(null!, "val").Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void AddContext_NullValue_Preserved()
    {
        var msg = new LogMessageBuilder().AddContext("key", null).Build("test");
        Assert.Equal("test | key=", msg);
    }

    [Fact]
    public void AddContext_WhitespaceKey_Skipped()
    {
        var msg = new LogMessageBuilder().AddContext("  ", "val").Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void Combined_ElapsedAndContext()
    {
        var msg = new LogMessageBuilder()
            .SetElapsedMs(1.0)
            .AddContext("p", "1")
            .Build("msg");
        Assert.Contains("[+1.00ms]", msg);
        Assert.Contains("msg | p=1", msg);
    }

    [Fact]
    public void SetElapsedMs_Zero_NotTruncated()
    {
        var msg = new LogMessageBuilder().SetElapsedMs(0.0).Build("test");
        Assert.StartsWith("[+0.00ms]", msg);
    }

    [Fact]
    public void SetElapsedMs_NaN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LogMessageBuilder().SetElapsedMs(double.NaN));
    }

    [Fact]
    public void SetElapsedMs_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LogMessageBuilder().SetElapsedMs(-1.0));
    }
}
