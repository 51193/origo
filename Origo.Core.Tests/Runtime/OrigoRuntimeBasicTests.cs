using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class OrigoRuntimeBasicTests
{
    [Fact]
    public void OrigoRuntime_Constructor_CreatesSndWorld()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);

        Assert.NotNull(runtime.SndWorld);
        Assert.Same(logger, runtime.Logger);
    }

    [Fact]
    public void OrigoRuntime_ConsoleInputQueue_NullWithoutInjection()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);

        Assert.Null(runtime.ConsoleInput);
        Assert.Null(runtime.ConsoleOutputChannel);
        Assert.Null(runtime.Console);
    }

    [Fact]
    public void OrigoRuntime_WithConsole_CreatesConsole()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var inputQueue = new ConsoleInputQueue();
        var outputChannel = new ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), inputQueue, outputChannel);

        Assert.NotNull(runtime.Console);
        Assert.Same(inputQueue, runtime.ConsoleInput);
        Assert.Same(outputChannel, runtime.ConsoleOutputChannel);
    }

    [Fact]
    public void OrigoRuntime_ResetConsoleState_ClearsInputQueue()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var inputQueue = new ConsoleInputQueue();
        inputQueue.Enqueue("test");
        var outputChannel = new ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), inputQueue, outputChannel);

        runtime.ResetConsoleState();
        Assert.False(inputQueue.TryDequeueCommand(out _));
    }

    [Fact]
    public void OrigoRuntime_FlushEndOfFrameDeferred_ExecutesDeferredActions()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);

        var businessRan = false;
        var systemRan = false;
        runtime.EnqueueBusinessDeferred(() => businessRan = true);
        runtime.EnqueueSystemDeferred(() => systemRan = true);
        runtime.FlushEndOfFrameDeferred();

        Assert.True(businessRan);
        Assert.True(systemRan);
    }
}
