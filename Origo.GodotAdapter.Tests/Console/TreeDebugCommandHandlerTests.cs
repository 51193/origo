using System.Collections.Generic;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Console;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class TreeDebugCommandHandlerTests
{
    private static CommandInvocation MakeInvocation(params string[] args)
    {
        return new CommandInvocation
        {
            Command = "tree_debug",
            PositionalArgs = args,
            NamedArgs = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void Properties_HaveExpectedValues()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TreeDebugCommandHandler(runtime);

        Assert.Equal("tree_debug", handler.Name);
        Assert.Contains("<entity>", handler.HelpText);
        Assert.Equal(1, handler.MinPositionalArgs);
        Assert.Equal(1, handler.MaxPositionalArgs);
    }

    [Fact]
    public void TryExecute_TooFewArgs_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TreeDebugCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(MakeInvocation(), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains(ConsoleMessages.InvalidArgumentCount, error);
    }

    [Fact]
    public void TryExecute_TooManyArgs_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TreeDebugCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(MakeInvocation("a", "b"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains(ConsoleMessages.InvalidArgumentCount, error);
    }

    [Fact]
    public void TryExecute_NoForegroundSession_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TreeDebugCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(MakeInvocation("Entity"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("not found", error);
    }

    [Fact]
    public void TryExecute_EntityNotFound_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        TestRuntimeHelper.BootstrapForegroundSession(runtime);
        var handler = new TreeDebugCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(MakeInvocation("NonExistent"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("Entity 'NonExistent' not found", error);
    }

    [Fact]
    public void TryExecute_EntityNotGodot_ReturnsError()
    {
        var (runtime, sceneHost) = TestRuntimeHelper.CreateRuntime();
        TestRuntimeHelper.BootstrapForegroundSession(runtime);
        sceneHost.AddEntity(new InMemorySndEntity("DummyEntity"));
        var handler = new TreeDebugCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(MakeInvocation("DummyEntity"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("is not a Godot entity", error);
    }
}
