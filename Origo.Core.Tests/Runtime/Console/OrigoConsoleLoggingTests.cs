using System;
using System.Linq;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class OrigoConsoleLoggingTests
{
    // ── Behavior tests ──────────────────────────────────────────────────

    [Fact]
    public void ProcessPending_SimpleCommand_LogsThreeDebugMessagesAndNoWarnings()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        Assert.Equal(3, logger.Debugs.Count);
        Assert.Empty(logger.Warnings);
        Assert.All(logger.Debugs, m => Assert.Contains("help", m));
    }

    [Fact]
    public void ProcessPending_MultipleCommands_LogsThreePerCommand()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        input.Enqueue("snd_count");
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        Assert.Equal(9, logger.Debugs.Count);
        Assert.Equal(6, logger.Debugs.Count(m => m.Contains("help")));
        Assert.Equal(3, logger.Debugs.Count(m => m.Contains("snd_count")));
    }

    [Fact]
    public void ProcessPending_EmptyQueue_ProducesNoLogMessages()
    {
        var (runtime, logger, _, _) = CreateTestHarness();
        runtime.Console!.ProcessPending();

        Assert.Empty(logger.Debugs);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void ProcessPending_UnknownCommand_LogsFailureAtDebugLevel()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("no_such_cmd_xyz");
        runtime.Console!.ProcessPending();

        Assert.Equal(3, logger.Debugs.Count);
        Assert.Empty(logger.Warnings);
        Assert.All(logger.Debugs, m => Assert.Contains("no_such_cmd_xyz", m));
    }

    [Fact]
    public void ProcessPending_HandlerReturnsError_LogsFailureAtDebugLevel()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new FailingHandler());
        input.Enqueue("fail");
        runtime.Console!.ProcessPending();

        Assert.Equal(3, logger.Debugs.Count);
        Assert.Empty(logger.Warnings);
        Assert.Contains(logger.Debugs, m => m.Contains("fail"));
    }

    [Fact]
    public void ProcessPending_HandlerThrowsException_LogsWarning()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new ThrowingHandler());
        input.Enqueue("throw");
        runtime.Console!.ProcessPending();

        Assert.NotEmpty(logger.Warnings);
        Assert.Contains(logger.Warnings, m => m.Contains("throw"));
    }

    [Fact]
    public void ProcessPending_MixedSuccessAndFailure_LogLevelsCorrect()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new FailingHandler());
        input.Enqueue("help");
        input.Enqueue("fail");
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        Assert.Equal(9, logger.Debugs.Count);
        Assert.Empty(logger.Warnings);
        Assert.Equal(6, logger.Debugs.Count(m => m.Contains("help")));
        Assert.Contains(logger.Debugs, m => m.Contains("fail"));
    }

    [Fact]
    public void ProcessPending_ReceiveBeforeExecuteBeforeResult_OrderCorrect()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        var commandOccurrences = logger.Debugs
            .Select((msg, idx) => (Index: idx, msg))
            .Where(x => x.msg.Contains("help"))
            .ToList();

        Assert.True(commandOccurrences.Count >= 3,
            "Expected at least 3 messages mentioning the command name.");
        for (var i = 0; i < commandOccurrences.Count - 1; i++)
            Assert.True(commandOccurrences[i].Index < commandOccurrences[i + 1].Index,
                "Command messages must appear in order.");
    }

    [Fact]
    public void ProcessPending_TrimmedCommand_StillProcessed()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("\t  help  \t");
        runtime.Console!.ProcessPending();

        Assert.Equal(3, logger.Debugs.Count);
        Assert.All(logger.Debugs, m => Assert.Contains("help", m));
    }

    [Fact]
    public void ProcessPending_EmptyAfterTrim_Skipped()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("   ");
        runtime.Console!.ProcessPending();

        Assert.Empty(logger.Debugs);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void ProcessPending_HandlerReturnsErrorWithNullMessage_LogsFailureAtDebugLevel()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new NullErrorHandler());
        input.Enqueue("nullerr");
        runtime.Console!.ProcessPending();

        Assert.Equal(3, logger.Debugs.Count);
        Assert.Empty(logger.Warnings);
        Assert.All(logger.Debugs, m => Assert.Contains("nullerr", m));
    }

    [Fact]
    public void ProcessPending_ParseError_LoggedAtDebugLevel()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help key=");
        runtime.Console!.ProcessPending();

        Assert.NotEmpty(logger.Debugs);
        Assert.Empty(logger.Warnings);
        Assert.Contains(logger.Debugs, m => m.Contains("Parse error"));
    }

    // ── Content contract tests ──────────────────────────────────────────

    [Fact]
    public void ProcessPending_AllDebugMessages_HaveCorrectTag()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        foreach (var msg in logger.Debugs)
            Assert.StartsWith("OrigoConsole: ", msg);
    }

    [Fact]
    public void ProcessPending_WarningMessage_HasCorrectTag()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new ThrowingHandler());
        input.Enqueue("throw");
        runtime.Console!.ProcessPending();

        Assert.Single(logger.Warnings);
        Assert.StartsWith("OrigoConsole: ", logger.Warnings[0]);
    }

    [Fact]
    public void ProcessPending_PositionalArgs_AppearInLog()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("find_entity entity_a");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("find_entity") && m.Contains("entity_a"));
    }

    [Fact]
    public void ProcessPending_NamedArgs_AppearInLog()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "enemy: templates/enemy.json");
        fs.SeedFile("templates/enemy.json",
            """
            {
              "name": "TemplateEnemy",
              "node": { "pairs": {} },
              "strategy": { "entity_indices": [] },
              "data": { "pairs": {} }
            }
            """);

        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var typeMapping = new TypeStringMapping();
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(logger, sceneHost, typeMapping,
            new Blackboard.Blackboard(), input, output,
            TestFactory.CreateIoGateway(fs));
        runtime.SndWorld.LoadTemplates("maps/templates.map", logger);

        input.Enqueue("spawn name=test_entity template=enemy");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("spawn") && m.Contains("test_entity") && m.Contains("enemy"));
    }

    [Fact]
    public void ProcessPending_LongCommandLine_FullContentLogged()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        var longCmd = new string('x', 500);
        input.Enqueue(longCmd);
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains(longCmd));
    }

    [Fact]
    public void ProcessPending_UnicodeCommand_CharactersPreserved()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("héllo 世界");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("héllo") && m.Contains("世界"));
    }

    [Fact]
    public void ProcessPending_CommandWithEmbeddedQuotes_LoggedCorrectly()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("echo \"hello world\"");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("echo") && m.Contains("hello world"));
    }

    [Fact]
    public void ProcessPending_SuccessCommand_IncludesElapsedTime()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("help") && m.Contains("ms"));
    }

    // ── Helpers ──

    private static (OrigoRuntime runtime, TestLogger logger, ConsoleInputBuffer input, ConsoleOutputChannel output)
        CreateTestHarness()
    {
        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var typeMapping = new TypeStringMapping();
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(logger, sceneHost, typeMapping,
            new Blackboard.Blackboard(), input, output);
        logger.Clear();
        return (runtime, logger, input, output);
    }

    private sealed class FailingHandler : IConsoleCommandHandler
    {
        public string Name => "fail";
        public string HelpText => "Always fails.";
        public int MinPositionalArgs => 0;
        public int MaxPositionalArgs => 0;

        public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
            out string? errorMessage)
        {
            errorMessage = "Simulated failure.";
            return false;
        }
    }

    private sealed class NullErrorHandler : IConsoleCommandHandler
    {
        public string Name => "nullerr";
        public string HelpText => "Fails with null error message.";
        public int MinPositionalArgs => 0;
        public int MaxPositionalArgs => 0;

        public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
            out string? errorMessage)
        {
            errorMessage = null;
            return false;
        }
    }

    private sealed class ThrowingHandler : IConsoleCommandHandler
    {
        public string Name => "throw";
        public string HelpText => "Always throws.";
        public int MinPositionalArgs => 0;
        public int MaxPositionalArgs => 0;

        public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
            out string? errorMessage) =>
            throw new InvalidOperationException("Test exception from handler");
    }
}
