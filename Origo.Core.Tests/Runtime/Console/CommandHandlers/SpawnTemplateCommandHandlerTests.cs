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
            PositionalArgs = new[] { "extraPositional" },
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
            PositionalArgs = Array.Empty<string>(),
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
}
