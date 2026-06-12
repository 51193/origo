using Origo.Core.Abstractions.Logging;
using Origo.GodotAdapter.Logging;
using Xunit;

namespace Origo.GodotAdapter.Tests.LoggingTests;

public class GodotLoggerTests
{
    [Fact]
    public void Log_WithHandler_InvokesHandlerWithCorrectLevelTagAndMessage()
    {
        LogLevel capturedLevel = default;
        string capturedTag = null!;
        string capturedMessage = null!;

        var logger = new GodotLogger((level, tag, message) =>
        {
            capturedLevel = level;
            capturedTag = tag;
            capturedMessage = message;
        });

        logger.Log(LogLevel.Warning, "TestTag", "Test message");

        Assert.Equal(LogLevel.Warning, capturedLevel);
        Assert.Equal("TestTag", capturedTag);
        Assert.Equal("Test message", capturedMessage);
    }

    [Fact]
    public void Log_WithNullHandler_DoesNotThrow()
    {
        var logger = new GodotLogger();

        var ex = Record.Exception(() => logger.Log(LogLevel.Error, "tag", "msg"));

        Assert.Null(ex);
    }

    [Fact]
    public void Log_EachLogLevel_PassesCorrectLevel()
    {
        var levels = new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error };
        foreach (var expectedLevel in levels)
        {
            LogLevel captured = default;
            var logger = new GodotLogger((level, _, _) => captured = level);

            logger.Log(expectedLevel, "tag", "msg");

            Assert.Equal(expectedLevel, captured);
        }
    }

    [Fact]
    public void Log_NullTagAndMessage_DoesNotThrow()
    {
        var logger = new GodotLogger((_, _, _) => { });

        var ex = Record.Exception(() => logger.Log(LogLevel.Info, null!, null!));

        Assert.Null(ex);
    }

    // ── Minimum level filtering ──

    [Fact]
    public void MinimumLevel_DefaultInfo_SuppressesDebug()
    {
        var invoked = false;
        var logger = new GodotLogger((_, _, _) => invoked = true);

        logger.Log(LogLevel.Debug, "tag", "msg");

        Assert.False(invoked);
    }

    [Fact]
    public void MinimumLevel_DefaultInfo_AllowsInfo()
    {
        var invoked = false;
        var logger = new GodotLogger((_, _, _) => invoked = true);

        logger.Log(LogLevel.Info, "tag", "msg");

        Assert.True(invoked);
    }

    [Fact]
    public void MinimumLevel_ExplicitDebug_AllowsDebug()
    {
        var invoked = false;
        var logger = new GodotLogger((_, _, _) => invoked = true, LogLevel.Debug);

        logger.Log(LogLevel.Debug, "tag", "msg");

        Assert.True(invoked);
    }

    [Fact]
    public void MinimumLevel_Error_SuppressesWarning()
    {
        var invoked = false;
        var logger = new GodotLogger((_, _, _) => invoked = true, LogLevel.Error);

        logger.Log(LogLevel.Warning, "tag", "msg");

        Assert.False(invoked);
    }

    [Fact]
    public void MinimumLevel_Error_AllowsError()
    {
        var invoked = false;
        var logger = new GodotLogger((_, _, _) => invoked = true, LogLevel.Error);

        logger.Log(LogLevel.Error, "tag", "msg");

        Assert.True(invoked);
    }
}
