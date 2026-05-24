using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleCommandRouterTests
{
    [Fact]
    public void ConsoleCommandRouter_Register_And_TryExecute_Success()
    {
        var router = new ConsoleCommandRouter();
        var handler = new StubHandler("test");
        router.Register(handler);

        var invocation = new CommandInvocation
        {
            Command = "test",
            PositionalArgs = Array.Empty<string>(),
            NamedArgs = new Dictionary<string, string>()
        };
        var channel = new ConsoleOutputChannel();

        var ok = router.TryExecute(invocation, channel, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.True(handler.WasExecuted);
    }

    [Fact]
    public void ConsoleCommandRouter_TryExecute_UnknownCommand_ReturnsFalse()
    {
        var router = new ConsoleCommandRouter();
        var invocation = new CommandInvocation
        {
            Command = "unknown",
            PositionalArgs = Array.Empty<string>(),
            NamedArgs = new Dictionary<string, string>()
        };
        var channel = new ConsoleOutputChannel();

        var ok = router.TryExecute(invocation, channel, out var err);
        Assert.False(ok);
        Assert.Contains("Unknown command", err);
    }

    [Fact]
    public void ConsoleCommandRouter_Register_NullHandler_Throws()
    {
        var router = new ConsoleCommandRouter();
        Assert.Throws<ArgumentNullException>(() => router.Register(null!));
    }

    [Fact]
    public void ConsoleCommandRouter_Register_CaseInsensitive()
    {
        var router = new ConsoleCommandRouter();
        var handler = new StubHandler("Test");
        router.Register(handler);

        var invocation = new CommandInvocation
        {
            Command = "TEST",
            PositionalArgs = Array.Empty<string>(),
            NamedArgs = new Dictionary<string, string>()
        };
        var channel = new ConsoleOutputChannel();

        var ok = router.TryExecute(invocation, channel, out _);
        Assert.True(ok);
    }

    [Fact]
    public void ConsoleCommandRouter_Register_DuplicateName_OverridesPreviousHandler()
    {
        var router = new ConsoleCommandRouter();
        var oldHandler = new StubHandler("test");
        var newHandler = new StubHandler("test");
        router.Register(oldHandler);
        router.Register(newHandler);

        var invocation = new CommandInvocation
        {
            Command = "test",
            PositionalArgs = Array.Empty<string>(),
            NamedArgs = new Dictionary<string, string>()
        };
        var channel = new ConsoleOutputChannel();

        var ok = router.TryExecute(invocation, channel, out _);
        Assert.True(ok);
        Assert.False(oldHandler.WasExecuted);
        Assert.True(newHandler.WasExecuted);
    }

    private sealed class StubHandler : IConsoleCommandHandler
    {
        public StubHandler(string name)
        {
            Name = name;
        }

        public bool WasExecuted { get; private set; }
        public string Name { get; }
        public string HelpText => $"{Name} — stub command.";
        public int MinPositionalArgs => 0;
        public int MaxPositionalArgs => -1;

        public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
            out string? errorMessage)
        {
            WasExecuted = true;
            errorMessage = null;
            return true;
        }
    }
}

// ── ConsoleInputQueue ──────────────────────────────────────────────────
