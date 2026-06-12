using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Xunit;

namespace Origo.Core.Tests;

// ── KeyValueFileParser ─────────────────────────────────────────────────

public class NullLoggerTests
{
    [Fact]
    public void NullLogger_Instance_IsSingleton() => Assert.Same(NullLogger.Instance, NullLogger.Instance);

    [Fact]
    public void NullLogger_ImplementsILogger()
    {
        ILogger logger = NullLogger.Instance;
        Assert.NotNull(logger);
    }
}

// ── TestLogger filtering ──────────────────────────────────────────────

public class TestLoggerFilterTests
{
    [Fact]
    public void MinimumLevel_DefaultDebug_RecordsAllLevels()
    {
        var logger = new TestLogger();

        logger.Log(LogLevel.Debug, "tag", "debug msg");
        logger.Log(LogLevel.Info, "tag", "info msg");
        logger.Log(LogLevel.Warning, "tag", "warn msg");
        logger.Log(LogLevel.Error, "tag", "err msg");

        Assert.Single(logger.Debugs);
        Assert.Single(logger.Infos);
        Assert.Single(logger.Warnings);
        Assert.Single(logger.Errors);
    }

    [Fact]
    public void MinimumLevel_SetToInfo_SuppressesDebug()
    {
        var logger = new TestLogger { MinimumLevel = LogLevel.Info };

        logger.Log(LogLevel.Debug, "tag", "debug msg");
        logger.Log(LogLevel.Info, "tag", "info msg");

        Assert.Empty(logger.Debugs);
        Assert.Single(logger.Infos);
    }

    [Fact]
    public void MinimumLevel_SetToError_OnlyRecordsError()
    {
        var logger = new TestLogger { MinimumLevel = LogLevel.Error };

        logger.Log(LogLevel.Debug, "tag", "d");
        logger.Log(LogLevel.Info, "tag", "i");
        logger.Log(LogLevel.Warning, "tag", "w");
        logger.Log(LogLevel.Error, "tag", "e");

        Assert.Empty(logger.Debugs);
        Assert.Empty(logger.Infos);
        Assert.Empty(logger.Warnings);
        Assert.Single(logger.Errors);
    }
}

// ── WellKnownKeys ──────────────────────────────────────────────────────
