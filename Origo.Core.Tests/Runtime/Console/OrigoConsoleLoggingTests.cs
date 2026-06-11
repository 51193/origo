using System;
using System.Linq;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class OrigoConsoleLoggingTests
{
    [Fact]
    public void ProcessPending_SimpleCommand_LogsReceivedExecutingAndSuccess()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("Received command") && m.Contains("help"));
        Assert.Contains(logger.Debugs, m => m.Contains("Executing command") && m.Contains("help")
                                                                            && m.Contains("(positional: [") &&
                                                                            m.Contains("named: {"));
        Assert.Contains(logger.Debugs, m => m.Contains("\"help\" executed successfully"));
        Assert.Equal(3, logger.Debugs.Count);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void ProcessPending_MultipleCommands_LogsEachCommandIndividually()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        input.Enqueue("snd_count");
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        var debugs = logger.Debugs;
        Assert.Equal(9, debugs.Count);
        Assert.Equal(6, debugs.Count(m => m.Contains("\"help\"") && !m.Contains("snd_count")));
        Assert.Contains(debugs, m => m.Contains("\"snd_count\""));
        Assert.Equal(3, debugs.Count(m => m.Contains("executed successfully")));
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
    public void ProcessPending_PositionalArgs_LoggedInExecutingMessage()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("find_entity entity_a");
        runtime.Console!.ProcessPending();

        var execMsg = logger.Debugs.FirstOrDefault(m => m.Contains("Executing command")
                                                        && m.Contains("find_entity"));
        Assert.NotNull(execMsg);
        Assert.Contains("positional: [entity_a]", execMsg);
    }

    [Fact]
    public void ProcessPending_NamedArgs_LoggedInExecutingMessage()
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

        var execMsg = logger.Debugs.FirstOrDefault(m => m.Contains("Executing command")
                                                        && m.Contains("spawn"));
        Assert.NotNull(execMsg);
        Assert.Contains("name=test_entity", execMsg);
        Assert.Contains("template=enemy", execMsg);
        Assert.Contains("named: {", execMsg);
    }

    [Fact]
    public void ProcessPending_UnknownCommand_LogsFailure()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("no_such_cmd_xyz");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("Received command")
                                            && m.Contains("no_such_cmd_xyz"));
        Assert.Contains(logger.Debugs, m => m.Contains("Executing command")
                                            && m.Contains("no_such_cmd_xyz"));
        Assert.Contains(logger.Debugs, m => m.Contains("failed")
                                            && m.Contains("no_such_cmd_xyz"));
        Assert.DoesNotContain(logger.Debugs,
            m => m.Contains("executed successfully") && m.Contains("no_such_cmd_xyz"));
        Assert.Equal(3, logger.Debugs.Count);
    }

    [Fact]
    public void ProcessPending_HandlerReturnsError_LogsFailure()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new FailingHandler());
        input.Enqueue("fail");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("\"fail\" failed")
                                            && m.Contains("Simulated failure"));
        Assert.DoesNotContain(logger.Debugs,
            m => m.Contains("executed successfully") && m.Contains("fail"));
    }

    [Fact]
    public void ProcessPending_HandlerThrowsException_LogsWarning()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new ThrowingHandler());
        input.Enqueue("throw");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Warnings, m => m.Contains("\"throw\" threw exception")
                                              && m.Contains("Test exception from handler"));
        Assert.DoesNotContain(logger.Debugs,
            m => m.Contains("executed") && m.Contains("throw"));
    }

    [Fact]
    public void ProcessPending_MixedSuccessAndFailure_AllLoggedCorrectly()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new FailingHandler());
        input.Enqueue("help");
        input.Enqueue("fail");
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        Assert.Equal(9, logger.Debugs.Count);
        Assert.Empty(logger.Warnings);
        Assert.Equal(2, logger.Debugs.Count(m => m.Contains("executed successfully")));
        Assert.Contains(logger.Debugs, m => m.Contains("failed") && m.Contains("Simulated failure"));
    }

    [Fact]
    public void ProcessPending_LongCommandLine_LogsFullContent()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        var longCmd = new string('x', 500);
        input.Enqueue(longCmd);
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs,
            m => m.Contains($"Received command: \"{longCmd}\""));
        Assert.Contains(logger.Debugs,
            m => m.Contains($"Executing command: \"{longCmd}\""));
    }

    [Fact]
    public void ProcessPending_UnicodeCommand_CharactersPreserved()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("héllo 世界");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs,
            m => m.Contains("Received command") && m.Contains("héllo 世界"));
        Assert.Contains(logger.Debugs,
            m => m.Contains("Executing command") && m.Contains("\"héllo\"") && m.Contains("世界"));
    }

    [Fact]
    public void ProcessPending_ParseError_InvalidNamedArg_LogsParseError()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help key=");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs,
            m => m.Contains("Received command") && m.Contains("help key="));
        Assert.Contains(logger.Debugs,
            m => m.Contains("Parse error") && m.Contains("Invalid named argument"));
        Assert.DoesNotContain(logger.Debugs,
            m => m.Contains("Executing command"));
    }

    [Fact]
    public void ProcessPending_TabsInCommand_TrimmedBeforeLogging()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("\t  help  \t");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs,
            m => m.Contains("Received command: \"help\""));
        Assert.Contains(logger.Debugs,
            m => m.Contains("Executing command: \"help\""));
    }

    [Fact]
    public void ProcessPending_ReceiveBeforeParse_OrderIsCorrect()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("help");
        runtime.Console!.ProcessPending();

        var receivedIdx = logger.Debugs.FindIndex(m => m.Contains("Received command"));
        var executingIdx = logger.Debugs.FindIndex(m => m.Contains("Executing command"));
        var resultIdx = logger.Debugs.FindIndex(m => m.Contains("executed"));

        Assert.True(receivedIdx >= 0);
        Assert.True(executingIdx >= 0);
        Assert.True(resultIdx >= 0);
        Assert.True(receivedIdx < executingIdx);
        Assert.True(executingIdx < resultIdx);
    }

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
    public void ProcessPending_EmptyStringAfterTrim_NotLogged()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("   ");
        runtime.Console!.ProcessPending();

        Assert.Empty(logger.Debugs);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void ProcessPending_HandlerReturnsErrorWithNullMessage_LogsFailure()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        runtime.Console!.RegisterHandler(new NullErrorHandler());
        input.Enqueue("nullerr");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs, m => m.Contains("Received command") && m.Contains("nullerr"));
        Assert.Contains(logger.Debugs, m => m.Contains("Executing command") && m.Contains("nullerr"));
        Assert.Contains(logger.Debugs, m => m.Contains("failed") && m.Contains("nullerr"));
        Assert.DoesNotContain(logger.Debugs,
            m => m.Contains("executed successfully") && m.Contains("nullerr"));
        Assert.Equal(3, logger.Debugs.Count);
    }

    [Fact]
    public void ProcessPending_CommandWithEmbeddedQuotes_LoggedCorrectly()
    {
        var (runtime, logger, input, _) = CreateTestHarness();
        input.Enqueue("echo \"hello world\"");
        runtime.Console!.ProcessPending();

        Assert.Contains(logger.Debugs,
            m => m.Contains("Received command") && m.Contains("echo \"hello world\""));
        Assert.Contains(logger.Debugs,
            m => m.Contains("Executing command") && m.Contains("echo"));
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
