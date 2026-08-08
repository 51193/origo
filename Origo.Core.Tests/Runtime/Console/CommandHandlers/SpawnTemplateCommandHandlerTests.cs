using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Console.CommandHandlers;
using Xunit;

namespace Origo.Core.Tests;

public class SpawnTemplateCommandHandlerTests
{
    [Fact]
    public void SpawnTemplateCommandHandler_MixNamedAndPositional_ReturnsError()
    {
        var runtime = TestFactory.CreateRuntime();
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = ["extraPositional"],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "n",
                ["template"] = "t"
            }
        };
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("mix", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpawnTemplateCommandHandler_NamedMissingName_ReturnsError()
    {
        var runtime = TestFactory.CreateRuntime();
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = "t"
            }
        };
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.Contains("name", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpawnTemplateCommandHandler_UnknownTemplate_ReturnsErrorInsteadOfThrowing()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("maps/templates.map", "hero_template: templates/hero.json");
        fs.SeedFile("templates/hero.json", """{"name":"TemplateHero","node":{"pairs":{"root":"hero"}}}""");
        var runtime = TestFactory.CreateRuntime(
            new TestLogger(), new TestSndSceneHost(), new TypeStringMapping(), new Blackboard.Blackboard(), fs);
        runtime.SndWorld.LoadTemplates("maps/templates.map", new TestLogger());
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "hero",
                ["template"] = "missing_template"
            }
        };
        var output = new ConsoleOutputChannel();

        // A mistyped template alias is a user input error, not a bug signal:
        // it must surface as a command error (like bb_get's unknown layer)
        // instead of throwing through the frame loop.
        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("missing_template", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpawnTemplateCommandHandler_TemplatesNotLoaded_ReturnsErrorInsteadOfThrowing()
    {
        var runtime = TestFactory.CreateRuntime();
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "hero",
                ["template"] = "hero_template"
            }
        };
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("template", err, StringComparison.OrdinalIgnoreCase);
    }
}
