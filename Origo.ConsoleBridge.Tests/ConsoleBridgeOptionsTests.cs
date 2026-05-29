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
    }

    [Fact]
    public void Options_CustomPort_Assigned()
    {
        var options = new ConsoleBridgeOptions { Port = 5555 };
        Assert.Equal(5555, options.Port);
    }
}
