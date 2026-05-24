using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleCommandExtendedTests
{
    private static (OrigoRuntime runtime, ConsoleInputQueue input, ConsoleOutputChannel output, List<string> messages)
        CreateConsoleRuntime()
    {
        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();
        var consoleInput = new ConsoleInputQueue();
        var consoleOutput = new ConsoleOutputChannel();

        var runtime = TestFactory.CreateRuntime(logger, sceneHost, tm, bb, consoleInput, consoleOutput);

        var messages = new List<string>();
        consoleOutput.Subscribe(messages.Add);

        return (runtime, consoleInput, consoleOutput, messages);
    }

    // ── help ──

    [Fact]
    public void HelpCommand_ListsAllRegisteredCommands()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("Available commands"));
        var allOutput = string.Join("\n", messages);
        Assert.Contains("help", allOutput);
        Assert.Contains("spawn", allOutput);
        Assert.Contains("snd_count", allOutput);
    }

    // ── find_entity ──

    [Fact]
    public void FindEntityCommand_NotFound_ReportsNotFound()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("find_entity ghost");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("not found", messages[0]);
    }

    [Fact]
    public void FindEntityCommand_MissingArg_ReportsUsage()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("find_entity");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("参数数量不合法", messages[0]);
    }

    // ── clear_entities ──

    [Fact]
    public void ClearEntitiesCommand_ClearsAll()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("clear_entities");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("Cleared 0 entities", messages[0]);
    }

    // ── bb_set / bb_get / bb_keys ──

    [Fact]
    public void BlackboardSetGet_RoundTrip()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_set system score 42");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("score = 42", messages[0]);

        messages.Clear();
        input.Enqueue("bb_get system score");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("score", messages[0]);
        Assert.Contains("42", messages[0]);
    }

    [Fact]
    public void BlackboardSetGet_StringValue()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_set system name Alice");
        runtime.Console!.ProcessPending();
        messages.Clear();

        input.Enqueue("bb_get system name");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("Alice", messages[0]);
    }

    [Fact]
    public void BlackboardSetGet_BoolValue()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_set system flag true");
        runtime.Console!.ProcessPending();
        messages.Clear();

        input.Enqueue("bb_get system flag");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("True", messages[0]);
        Assert.Contains("Boolean", messages[0]);
    }

    [Fact]
    public void BlackboardKeys_ListsKeys()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_set system k1 a");
        runtime.Console!.ProcessPending();
        input.Enqueue("bb_set system k2 b");
        runtime.Console!.ProcessPending();
        messages.Clear();

        input.Enqueue("bb_keys system");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("k1", messages[0]);
        Assert.Contains("k2", messages[0]);
    }

    [Fact]
    public void BlackboardKeys_EmptyBlackboard()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_keys system");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("empty", messages[0]);
    }

    [Fact]
    public void BlackboardGet_MissingKey_ReportsNotFound()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_get system missing");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("not found", messages[0]);
    }

    [Fact]
    public void BlackboardSet_InvalidLayer_ReportsError()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_set invalid k v");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("Unknown", messages[0]);
    }

    [Fact]
    public void BlackboardGet_MissingArgs_ReportsUsage()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_get");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("参数数量不合法", messages[0]);
    }

    [Fact]
    public void BlackboardSet_MissingArgs_ReportsUsage()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_set system");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("参数数量不合法", messages[0]);
    }

    [Fact]
    public void BlackboardKeys_MissingArgs_ReportsUsage()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("bb_keys");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Contains("参数数量不合法", messages[0]);
    }

    // ── RegisterHandler ──

    [Fact]
    public void RegisterHandler_LateRegistration_CommandAvailable()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("help");
        runtime.Console!.ProcessPending();
        var initialHelp = messages[0];

        // register a custom handler
        runtime.Console.RegisterHandler(new TestPingHandler());
        messages.Clear();

        input.Enqueue("ping");
        runtime.Console!.ProcessPending();
        Assert.Single(messages);
        Assert.Equal("pong", messages[0]);

        // help should now include ping
        messages.Clear();
        input.Enqueue("help");
        runtime.Console.ProcessPending();
        var allOutput = string.Join("\n", messages);
        Assert.Contains("ping", allOutput);
    }

    // ── GetRegisteredNames ──

    [Fact]
    public void GetRegisteredNames_ReturnsSortedNames()
    {
        var router = new ConsoleCommandRouter();
        var outputChannel = new ConsoleOutputChannel();
        router.Register(new TestPingHandler());

        var names = router.GetRegisteredNames();
        Assert.Contains("ping", names);
    }

    // ── ConsoleCommandHandlerBase argument validation ──

    [Fact]
    public void ConsoleCommandHandlerBase_TooFewArgs_ReturnsErrorWithHelpText()
    {
        var handler = new TestMinMaxHandler(2, 3);
        var invocation = new CommandInvocation
        {
            Command = "test_minmax",
            PositionalArgs = new[] { "a" },
            NamedArgs = new Dictionary<string, string>()
        };
        var output = new ConsoleOutputChannel();
        var result = handler.TryExecute(invocation, output, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("参数数量不合法", error);
        Assert.Contains(handler.HelpText, error);
    }

    [Fact]
    public void ConsoleCommandHandlerBase_TooManyArgs_ReturnsErrorWithHelpText()
    {
        var handler = new TestMinMaxHandler(0, 1);
        var invocation = new CommandInvocation
        {
            Command = "test_minmax",
            PositionalArgs = new[] { "a", "b" },
            NamedArgs = new Dictionary<string, string>()
        };
        var output = new ConsoleOutputChannel();
        var result = handler.TryExecute(invocation, output, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("参数数量不合法", error);
    }

    [Fact]
    public void ConsoleCommandHandlerBase_ExactArgs_Succeeds()
    {
        var handler = new TestMinMaxHandler(2, 2);
        var invocation = new CommandInvocation
        {
            Command = "test_minmax",
            PositionalArgs = new[] { "a", "b" },
            NamedArgs = new Dictionary<string, string>()
        };
        var output = new ConsoleOutputChannel();
        var messages = new List<string>();
        output.Subscribe(messages.Add);

        var result = handler.TryExecute(invocation, output, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Contains("ok", messages);
    }

    [Fact]
    public void ConsoleCommandHandlerBase_UnlimitedMax_AcceptsAnyCount()
    {
        var handler = new TestMinMaxHandler(0, -1);
        var invocation = new CommandInvocation
        {
            Command = "test_minmax",
            PositionalArgs = new[] { "a", "b", "c", "d", "e" },
            NamedArgs = new Dictionary<string, string>()
        };
        var output = new ConsoleOutputChannel();
        var messages = new List<string>();
        output.Subscribe(messages.Add);

        var result = handler.TryExecute(invocation, output, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Contains("ok", messages);
    }

    [Fact]
    public void HelpCommand_ShowsHelpTextForEachCommand()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();

        runtime.Console!.RegisterHandler(new TestPingHandler());

        input.Enqueue("help");
        runtime.Console.ProcessPending();

        var allOutput = string.Join("\n", messages);

        // Built-in commands should have their help text displayed
        Assert.Contains("help", allOutput);
        Assert.Contains("spawn", allOutput);

        // The custom handler's HelpText should appear
        Assert.Contains("ping — test command.", allOutput);
    }

    [Fact]
    public void SpawnCommand_NamedArgs_SpawnsEntity()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();
        var fs = new TestFileSystem();
        fs.SeedFile("maps/t.map", "tpl: templates/x.json");
        fs.SeedFile("templates/x.json",
            """
            {
              "name": "X",
              "node": { "pairs": {} },
              "strategy": { "indices": [] },
              "data": { "pairs": {} }
            }
            """);
        runtime.SndWorld.LoadTemplates(fs, "maps/t.map", new TestLogger());

        input.Enqueue("spawn name=Ent1 template=tpl");
        runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("Spawned 'Ent1'", StringComparison.Ordinal));
    }

    [Fact]
    public void SpawnCommand_NamedMissingTemplate_ReportsError()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();
        input.Enqueue("spawn name=only");
        runtime.Console!.ProcessPending();
        Assert.Contains(messages, m => m.Contains("template", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SpawnCommand_PositionalWrongCount_ReportsUsage()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();
        input.Enqueue("spawn onearg");
        runtime.Console!.ProcessPending();
        Assert.Contains(messages, m => m.Contains("Usage", StringComparison.Ordinal));
    }

    [Fact]
    public void SpawnCommand_PositionalSingleArg_ReportsUsage()
    {
        var (runtime, input, _, messages) = CreateConsoleRuntime();
        input.Enqueue("spawn   tplkey");
        runtime.Console!.ProcessPending();
        Assert.Contains(messages, m => m.Contains("Usage", StringComparison.Ordinal));
    }

    // ── Test doubles ──

    private sealed class TestPingHandler : IConsoleCommandHandler
    {
        public string Name => "ping";
        public string HelpText => "ping — test command.";
        public int MinPositionalArgs => 0;
        public int MaxPositionalArgs => 0;

        public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
            out string? errorMessage)
        {
            outputChannel.Publish("pong");
            errorMessage = null;
            return true;
        }
    }

    private sealed class TestMinMaxHandler : ConsoleCommandHandlerBase
    {
        public TestMinMaxHandler(int min, int max)
        {
            MinPositionalArgs = min;
            MaxPositionalArgs = max;
        }

        public override string Name => "test_minmax";
        public override string HelpText => "test_minmax <arg1> [arg2] — test handler.";
        public override int MinPositionalArgs { get; }
        public override int MaxPositionalArgs { get; }

        protected override bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
            out string? errorMessage)
        {
            outputChannel.Publish("ok");
            errorMessage = null;
            return true;
        }
    }
}

// ── ConsoleCommandParser ───────────────────────────────────────────────
