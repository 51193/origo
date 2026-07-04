using Godot;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

public class OrigoAutoHostBootstrapIntegrationTests : IDeferredTestFixture
{
    private OrigoAutoHost? _autoHost;
    private int _frame;

    public bool IsComplete => _frame >= 1;
    public void Setup() => _frame = 0;
    public void AdvanceFrame() => _frame++;

    [DeferredTest(Description = "OrigoAutoHost._Ready creates Runtime and SndManager")]
    public void Ready_CreatesRuntimeAndSndManager()
    {
        _autoHost = new OrigoAutoHost { Name = "TestAutoHost" };
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(_autoHost);

        IntegrationTestRunner.AssertNotNull(_autoHost.Runtime, "Runtime");
        IntegrationTestRunner.AssertNotNull(_autoHost.SndManager, "SndManager");
        IntegrationTestRunner.Assert(_autoHost.SndManager.IsInsideTree(), "SndManager should be in scene tree.");
    }

    [DeferredTest(Description = "OrigoAutoHost._Ready creates ConsoleInput and ConsoleOutputChannel")]
    public void Ready_CreatesConsoleChannels()
    {
        _autoHost = new OrigoAutoHost { Name = "TestAutoHost2" };
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(_autoHost);

        IntegrationTestRunner.AssertNotNull(_autoHost.ConsoleInput, "ConsoleInput");
        IntegrationTestRunner.AssertNotNull(_autoHost.ConsoleOutputChannel, "ConsoleOutputChannel");
    }
}
