using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Console;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class CommandHandlerBaseTests
{
    private static CommandInvocation MakeInvocation(params string[] args)
    {
        return new CommandInvocation
        {
            Command = "test",
            PositionalArgs = args,
            NamedArgs = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void Constructor_NullRuntime_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new TestHandler(null!, 0, 0));

    [Fact]
    public void TryExecute_NullInvocation_Throws()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TestHandler(runtime, 0, 0);
        var output = new ConsoleOutputChannel();

        Assert.Throws<ArgumentNullException>(() =>
            handler.TryExecute(null!, output, out _));
    }

    [Fact]
    public void TryExecute_NullOutputChannel_Throws()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TestHandler(runtime, 0, 0);

        Assert.Throws<ArgumentNullException>(() =>
            handler.TryExecute(MakeInvocation(), null!, out _));
    }

    [Fact]
    public void TryExecute_TooFewArgs_ReturnsErrorWithHelpText()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TestHandler(runtime, 2, 3);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(MakeInvocation("a"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("参数数量不合法", error);
        Assert.Contains(handler.HelpText, error);
    }

    [Fact]
    public void TryExecute_TooManyArgs_ReturnsErrorWithHelpText()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TestHandler(runtime, 0, 1);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(
            MakeInvocation("a", "b"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("参数数量不合法", error);
    }

    [Fact]
    public void TryExecute_ExactArgs_Succeeds()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TestHandler(runtime, 2, 2);
        var output = new ConsoleOutputChannel();
        var messages = new List<string>();
        output.Subscribe(messages.Add);

        var ok = handler.TryExecute(
            MakeInvocation("a", "b"), output, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Contains("ok", messages);
    }

    [Fact]
    public void TryExecute_UnlimitedMax_AcceptsManyArgs()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new TestHandler(runtime, 0, -1);
        var output = new ConsoleOutputChannel();
        var messages = new List<string>();
        output.Subscribe(messages.Add);

        var ok = handler.TryExecute(
            MakeInvocation("a", "b", "c", "d", "e"), output, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Contains("ok", messages);
    }

    private sealed class TestHandler(OrigoRuntime runtime, int min, int max) : CommandHandlerBase(runtime)
    {
        public override string Name => "test";
        public override string HelpText => "test — test handler";
        public override int MinPositionalArgs { get; } = min;

        public override int MaxPositionalArgs { get; } = max;

        protected override bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
            out string? errorMessage)
        {
            outputChannel.Publish("ok");
            errorMessage = null;
            return true;
        }
    }
}
