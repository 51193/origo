using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Xunit;

namespace Origo.Core.Tests;

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
