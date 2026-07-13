using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Runtime.Console;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Console;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class PressButtonCommandHandlerTests
{
    private static CommandInvocation MakeInvocation(string entityName, string buttonPath)
    {
        return new CommandInvocation
        {
            Command = "press_button",
            PositionalArgs = [entityName, buttonPath],
            NamedArgs = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void Properties_HaveExpectedValues()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new PressButtonCommandHandler(runtime);

        Assert.Equal("press_button", handler.Name);
        Assert.Contains("<entity>", handler.HelpText);
        Assert.Contains("<path>", handler.HelpText);
        Assert.Equal(2, handler.MinPositionalArgs);
        Assert.Equal(2, handler.MaxPositionalArgs);
    }

    [Fact]
    public void TryExecute_TooFewArgs_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new PressButtonCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var invocation = new CommandInvocation
        {
            Command = "press_button",
            PositionalArgs = ["Entity"],
            NamedArgs = new Dictionary<string, string>()
        };

        var ok = handler.TryExecute(invocation, output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains(ConsoleMessages.InvalidArgumentCount, error);
    }

    [Fact]
    public void TryExecute_EntityNotFound_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new PressButtonCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(
            MakeInvocation("NonExistent", "PlayButton"), output, out var error);

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
        var handler = new PressButtonCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(
            MakeInvocation("DummyEntity", "PlayButton"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("is not a Godot entity", error);
    }
}
