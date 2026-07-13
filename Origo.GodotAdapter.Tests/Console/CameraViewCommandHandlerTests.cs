using Origo.GodotAdapter.Console;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class CameraViewCommandHandlerTests
{
    [Fact]
    public void Properties_HaveExpectedValues()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new CameraViewCommandHandler(runtime);

        Assert.Equal("camera_view", handler.Name);
        Assert.Contains("screen coordinates", handler.HelpText);
        Assert.Contains("depth", handler.HelpText);
        Assert.Equal(0, handler.MinPositionalArgs);
        Assert.Equal(0, handler.MaxPositionalArgs);
    }
}
