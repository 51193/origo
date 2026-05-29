using Origo.Core.Abstractions.Logging;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeOptionsTests
{
    [Fact]
    public void DefaultPort_IsExpectedValue() => Assert.Equal(9876, ConsoleBridgeOptions.DefaultPort);

    [Fact]
    public void DefaultOptions_HasCorrectDefaults()
    {
        var options = new ConsoleBridgeOptions();
        Assert.Equal(ConsoleBridgeOptions.DefaultPort, options.Port);
        Assert.Equal(LogLevel.Info, options.MinLogLevel);
    }

    [Fact]
    public void Options_CustomPort_Assigned()
    {
        var options = new ConsoleBridgeOptions { Port = 5555 };
        Assert.Equal(5555, options.Port);
    }

    [Fact]
    public void Options_CustomMinLogLevel_Assigned()
    {
        var options = new ConsoleBridgeOptions { MinLogLevel = LogLevel.Error };
        Assert.Equal(LogLevel.Error, options.MinLogLevel);
    }
}
