using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class LogRelayTests
{
    [Fact]
    public void Log_ForwardsToInnerAndOutputChannel()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Info, "TestTag", "test message");

        Assert.True(inner.LoggedCount >= 1);
        Assert.Single(received);
        Assert.Equal("[INFO][TestTag] test message", received[0]);
    }

    [Fact]
    public void Log_FiltersOutDebugByDefault()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Debug, "Tag", "debug msg");

        Assert.True(inner.LoggedCount >= 1);
        Assert.Empty(received);
    }

    [Fact]
    public void Log_AllowsDebugWhenMinLevelSet()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output, LogLevel.Debug);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Debug, "Tag", "debug msg");

        Assert.Single(received);
        Assert.Equal("[DEBUG][Tag] debug msg", received[0]);
    }

    [Fact]
    public void Log_AllLevels_FormattedCorrectly()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output, LogLevel.Debug);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Info, "A", "m");
        relay.Log(LogLevel.Warning, "B", "m");
        relay.Log(LogLevel.Error, "C", "m");

        Assert.Equal(3, received.Count);
        Assert.Equal("[INFO][A] m", received[0]);
        Assert.Equal("[WARNING][B] m", received[1]);
        Assert.Equal("[ERROR][C] m", received[2]);
    }

    [Fact]
    public void Log_ErrorLevel_AlwaysPasses()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output, LogLevel.Error);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Info, "T", "info");
        relay.Log(LogLevel.Error, "T", "error");

        Assert.Single(received);
        Assert.Equal("[ERROR][T] error", received[0]);
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [Fact]
    public void Log_NullTag_StillDelivers()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output, LogLevel.Debug);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Info, null!, "msg");

        Assert.Single(received);
        Assert.Contains("[INFO]", received[0]);
        Assert.Contains("msg", received[0]);
    }

    [Fact]
    public void Log_NullMessage_StillDelivers()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Info, "Tag", null!);

        Assert.Single(received);
        Assert.Equal("[INFO][Tag] ", received[0]);
    }

    [Fact]
    public void Log_EmptyTag_StillDelivers()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Warning, "", "warn");

        Assert.Single(received);
        Assert.Equal("[WARNING][] warn", received[0]);
    }

    [Fact]
    public void Log_EmptyMessage_StillDelivers()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output);
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Error, "E", "");

        Assert.Single(received);
        Assert.Equal("[ERROR][E] ", received[0]);
    }

    [Fact]
    public void Log_LongMessage_Delivers()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output);
        var received = new List<string>();
        output.Subscribe(received.Add);

        var longMsg = new string('x', 4096);
        relay.Log(LogLevel.Info, "T", longMsg);

        Assert.Single(received);
        Assert.StartsWith("[INFO][T] ", received[0]);
        Assert.EndsWith(longMsg, received[0]);
    }

    [Fact]
    public void Log_WarningLevel_PassesAtDefault()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output); // default minLevel = Info
        var received = new List<string>();
        output.Subscribe(received.Add);

        relay.Log(LogLevel.Warning, "W", "warning msg");

        Assert.Single(received);
        Assert.Equal("[WARNING][W] warning msg", received[0]);
    }

    [Fact]
    public void Log_InnerAlwaysCalled_RegardlessOfFilter()
    {
        var inner = new TestLogger();
        var output = new ConsoleOutputChannel();
        var relay = new LogRelay(inner, output, LogLevel.Error); // Only Error passes

        relay.Log(LogLevel.Debug, "D", "debug");
        relay.Log(LogLevel.Info, "I", "info");
        relay.Log(LogLevel.Warning, "W", "warn");

        // Inner should always be called
        Assert.True(inner.LoggedCount >= 3);
    }

    private sealed class TestLogger : ILogger
    {
        public int LoggedCount { get; private set; }

        public void Log(LogLevel level, string tag, string message) => LoggedCount++;
    }
}
