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
        Assert.Contains("屏幕坐标", handler.HelpText);
        Assert.Contains("深度", handler.HelpText);
        Assert.Equal(0, handler.MinPositionalArgs);
        Assert.Equal(0, handler.MaxPositionalArgs);
    }
}
