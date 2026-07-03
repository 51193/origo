using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Xunit;

namespace Origo.Core.Tests;

public class LoggerGenericTests
{
    private sealed class TestCategory
    {
    }

    [Fact]
    public void LoggerT_TagDerived_FromTypeName()
    {
        var inner = new TestLogger();
        var logger = new Logger<TestCategory>(inner);

        logger.Log(LogLevel.Info, "test message");

        Assert.Single(inner.Infos);
        Assert.Contains(nameof(TestCategory), inner.Infos[0]);
        Assert.Contains("test message", inner.Infos[0]);
    }

    [Fact]
    public void LoggerT_ExplicitInterface_UsesProvidedTag()
    {
        var inner = new TestLogger();
        ILogger logger = new Logger<TestCategory>(inner);

        logger.Log(LogLevel.Warning, "custom_tag", "tagged message");

        Assert.Single(inner.Warnings);
        Assert.Contains("custom_tag", inner.Warnings[0]);
        Assert.Contains("tagged message", inner.Warnings[0]);
    }

    [Fact]
    public void LoggerT_DifferentTypes_HaveDifferentTags()
    {
        var inner = new TestLogger();
        var loggerA = new Logger<string>(inner);
        var loggerB = new Logger<int>(inner);

        loggerA.Log(LogLevel.Info, "msg_a");
        loggerB.Log(LogLevel.Info, "msg_b");

        Assert.Equal(2, inner.Infos.Count);
        Assert.Contains("String", inner.Infos[0]);
        Assert.Contains("Int32", inner.Infos[1]);
    }

    [Fact]
    public void LoggerT_WrapsNullLogger()
    {
        var logger = new Logger<TestCategory>(NullLogger.Instance);

        var ex = Record.Exception(() => logger.Log(LogLevel.Info, "noop"));
        Assert.Null(ex);
    }

    [Fact]
    public void LoggerT_Constructor_NullInner_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new Logger<TestCategory>(null!));
    }

    [Fact]
    public void LoggerT_GenericType_TagUsesFriendlyName()
    {
        var inner = new TestLogger();
        var logger = new Logger<GenericTest<int, string>>(inner);

        logger.Log(LogLevel.Debug, "value");

        var tag = typeof(GenericTest<int, string>).Name;
        Assert.Single(inner.Debugs);
        Assert.Contains(tag, inner.Debugs[0]);
    }

    private sealed class GenericTest<T1, T2>
    {
    }
}
